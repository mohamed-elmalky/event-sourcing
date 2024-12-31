using System.Collections.Concurrent;
using System.Reflection;
using MediatR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())
);
var eventStore = new MemoryEventStore();
var uniquenessDataStore = new UniquenessMemoryDataStore();

builder.Services.AddSingleton<IEventStore>(eventStore);
builder.Services.AddSingleton<IUniquenessDataStore>(uniquenessDataStore);
builder.Services.AddSingleton<IMediator, Mediator>();

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ParticipantUniqueBySSNBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ParticipantUniqeByNameAndHomePhoneNumberBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ParticipantUniqueByNameAndMobileNumberBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ParticipantUniqueByNameAndEmailBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ParticipantUniqueByNameAndAddressBehavior<,>));

var app = builder.Build();

var mediator = app.Services.GetRequiredService<IMediator>();

app.MapGet("/", () => {
    return Results.Ok("Hello? Is it me you're looking for?");
});
app.MapPost("/participants", async (ParticipantRequest request) => 
{
    var addParticipantSlice = new AddParticipantSlice(eventStore, uniquenessDataStore, mediator);

    try
    {
        var id = await addParticipantSlice.AddParticipant(request);
        return Results.Created($"/participants/{id}", id);
    }
    catch (ParticipantAlreadyExistsException ex)
    {
        return Results.Conflict(new { message = ex.Message, id = ex.ParticipantId });
    }
});
app.MapGet("/participants/events/{id}", (string id) => {
    return Results.Ok(eventStore.Load("participant", id));
});
app.MapDelete("/participants/{id}", async (string id) => {
    var deactivateParticipantSlice = new DeactivateParticipantSlice(mediator, eventStore);
    await deactivateParticipantSlice.DeactivateParticipant(id);
    return Results.NoContent();
});
app.MapGet("/participants/{id}", async (string id) => {
    var events = await eventStore.Load("participant", id);
    var participantProjection = new ParticipantProjection(events);
    return Results.Ok(participantProjection.Participants[id]);
});

app.Run();

public record ParticipantRequest
{
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }
}

public record Participant(string Id)
{
    public bool IsActive { get; set; }
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }
}

public record Address(string Address1, string Address2, string City, string State, string Country, string ZipCode)
{
    public override string ToString() => $"{Address1}, {Address2}, {City}, {State}, {Country}, {ZipCode}";
}

public abstract record Command<TResponse> : IRequest<TResponse> 
{ 
    public abstract string AggregateId { get; init; }
    public abstract TResponse ToEvent();
    public Participant? Participant { get; set; }
}
public abstract record Event(string Kind)
{
    public abstract string AggregateId { get; init; }
    public DateTimeOffset OccuredAt { get; set; }
    public Participant? Participant { get; set; }
}

public record ParticipantAcquired(string AggregateId) : Event("participant-acquired");
public record AddParticipantCommand(string AggregateId) : Command<ParticipantAcquired>
{
    public override ParticipantAcquired ToEvent() => new(AggregateId) { 
        Participant = Participant,
        OccuredAt = DateTimeOffset.UtcNow
    };
}
public class AddParticipantCommandHandler : IRequestHandler<AddParticipantCommand, ParticipantAcquired>
{
    public Task<ParticipantAcquired> Handle(AddParticipantCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.ToEvent());
    }
}

public record ParticipantDeactivated(string AggregateId) : Event("participant-deactivated");
public record DeactiveParticipantCommand(string AggregateId) : Command<ParticipantDeactivated>
{
    public override ParticipantDeactivated ToEvent() => new(AggregateId) { 
        Participant = Participant,
        OccuredAt = DateTimeOffset.UtcNow
    };
}
public class DeactiveParticipantCommandHandler : IRequestHandler<DeactiveParticipantCommand, ParticipantDeactivated>
{
    public Task<ParticipantDeactivated> Handle(DeactiveParticipantCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.ToEvent());
    }
}

public abstract class Slice(IMediator mediator, IEventStore eventStore)
{
    protected readonly IMediator mediator = mediator;
    protected readonly IEventStore eventStore = eventStore;
}

public class DeactivateParticipantSlice : Slice
{
    public DeactivateParticipantSlice(IMediator mediator, IEventStore eventStore) : base(mediator, eventStore) { }

    public async Task<string> DeactivateParticipant(string participantId)
    {
        var deactivateParticipantCommand = new DeactiveParticipantCommand(participantId);
        var participantDeactivated = await mediator.Send(deactivateParticipantCommand);
        await eventStore.Append("participant", participantDeactivated);
        return participantId;
    }
}

public class AddParticipantSlice(IEventStore eventStore, IUniquenessDataStore uniquenessDataStore, IMediator mediator) : Slice(mediator, eventStore)
{
    public async Task<string> AddParticipant(ParticipantRequest request)
    {
        var participant = new Participant(GenerateIds.NewId())
        {
            Name = request.Name,
            SSN = request.SSN,
            HomePhone = request.HomePhone,
            MobilePhone = request.MobilePhone,
            IsActive = true,
            Address = request.Address,
            Email = request.Email
        };
        var addParticipantCommand = new AddParticipantCommand(participant.Id) { Participant = participant };

        var participantAdded = await mediator.Send(addParticipantCommand); // This will trigger the pipeline behaviors. If the participant is not unique, an exception will be thrown.
        await eventStore.Append("participant", participantAdded); // We won't reach this point if the participant is not unique.
        await uniquenessDataStore.Add(participant);
        
        Console.WriteLine($"Participant added: {participant.Id}");
        return participant.Id;
    }
}

public class ParticipantAlreadyExistsException(string message) : Exception(message)
{
    public string? ParticipantId { get; set; } 
}

public class ParticipantUniqueByNameAndEmailBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueByNameAndEmailBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || string.IsNullOrEmpty(request.Participant.Email))
        {
            Console.WriteLine("\u2705 Name and email");
            return await next();
        }

        var nameAndEmail = await _uniquenessDataStore.ByNameAndEmail();
        nameAndEmail.TryGetValue($"{request.Participant.Name}:{request.Participant.Email}", out var participantId);
        var particpantIsUnique = participantId is null;
        if (particpantIsUnique)
            Console.WriteLine("\u2705 Name and email");
        else
        {
            Console.WriteLine("\u274C Exists with this name and email");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and email") { ParticipantId = participantId };
        }

        return await next();
    }
}

/// <summary>
/// Ensures that a participant is unique by name and phone number. This is the second highest priority uniqueness constraint.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class ParticipantUniqeByNameAndHomePhoneNumberBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqeByNameAndHomePhoneNumberBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || string.IsNullOrEmpty(request.Participant.HomePhone))
        {
            Console.WriteLine("\u2705 Name and home phone number");
            return await next();
        }

        var nameAndHomePhoneNumber = await _uniquenessDataStore.ByNameAndHomePhoneNumber();
        nameAndHomePhoneNumber.TryGetValue($"{request.Participant.Name}:{request.Participant.HomePhone}", out var participantId);
        var particpantIsUnique = participantId is null;
        if (particpantIsUnique)
            Console.WriteLine("\u2705 Name and home phone number");
        else
        {
            Console.WriteLine("\u274C Exists with this name and home phone number");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and number") { ParticipantId = participantId };
        }

        return await next();
    }
}

public class ParticipantUniqueByNameAndMobileNumberBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueByNameAndMobileNumberBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || string.IsNullOrEmpty(request.Participant.MobilePhone))
        {
            Console.WriteLine("\u2705 Name and mobile phone number");
            return await next();
        }

        var nameAndMobilePhoneNumber = await _uniquenessDataStore.ByNameAndMobilePhoneNumber();
        nameAndMobilePhoneNumber.TryGetValue($"{request.Participant.Name}:{request.Participant.MobilePhone}", out var participantId);
        var particpantIsUnique = participantId is null;
        if (particpantIsUnique)
            Console.WriteLine("\u2705 Name and mobile phone number");
        else
        {
            Console.WriteLine("\u274C Exists with this name and mobile phone number");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and number") { ParticipantId = participantId };
        }

        return await next();
    }
}

/// <summary>
/// Ensures that a participant is unique by SSN. This is the highest priority uniqueness constraint. It short-circuits the pipeline if the participant is unique.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class ParticipantUniqueBySSNBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand where TResponse : ParticipantAcquired
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueBySSNBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.SSN))
        {
            Console.WriteLine("\u2705 SSN");
            return await next();
        }

        var ssns = await _uniquenessDataStore.BySSN();
        ssns.TryGetValue(request.Participant.SSN, out var participantId);
        var particpantIsUnique = participantId is null;
        if (particpantIsUnique)
        {
            Console.WriteLine("\u2705 SSN");
            return (TResponse)request.ToEvent(); // Not really happy with creating the event here, but it's the only way I can short-circuit the pipeline for this specific behavior.
        }
        else
        {
            Console.WriteLine("\u274C Exists with this SSN");
            throw new ParticipantAlreadyExistsException("Participant already exists with this SSN") { ParticipantId = participantId };
        }

    }
}

public class ParticipantUniqueByNameAndAddressBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqueByNameAndAddressBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null || string.IsNullOrEmpty(request.Participant.Name) || request.Participant.Address is null)
        {
            Console.WriteLine("\u2705 Name and address");
            return await next();
        }

        var nameAndAddress = await _uniquenessDataStore.ByNameAndAddress();
        nameAndAddress.TryGetValue($"{request.Participant.Name}:{request.Participant.Address}", out var participantId);
        var particpantIsUnique = participantId is null;
        if (particpantIsUnique)
            Console.WriteLine("\u2705 Name and address");
        else
        {
            Console.WriteLine("\u274C Exists with this name and address");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and address") { ParticipantId = participantId };
        }

        return await next();
    }
}
public class ParticipantProjection
{
    public Dictionary<string, Participant> Participants { get; } = [];

    public ParticipantProjection(IEnumerable<Event> events)
    {
        foreach (var e in events)
            Apply(e);
    }

    private void Apply(Event e)
    {
        Participants.TryAdd(e.AggregateId, new Participant(e.AggregateId));

        switch (e)
        {
            case ParticipantAcquired participantAcquired:
                Participants[participantAcquired.AggregateId] = participantAcquired.Participant ?? Participants[participantAcquired.AggregateId];
                break;
            case ParticipantDeactivated participantDeactivated:
                Participants[participantDeactivated.AggregateId].IsActive = false;
                break;
        }
    }
}

public interface IUniquenessDataStore
{
    public Task Add(Participant participant);
    public Task<ConcurrentDictionary<string, Participant>> ById();
    public Task<ConcurrentDictionary<string, string>> BySSN();
    public Task<ConcurrentDictionary<string, string>> ByNameAndHomePhoneNumber();
    public Task<ConcurrentDictionary<string, string>> ByNameAndMobilePhoneNumber();
    public Task<ConcurrentDictionary<string, string>> ByNameAndAddress();
    public Task<ConcurrentDictionary<string, string>> ByNameAndEmail();

}
public class UniquenessMemoryDataStore : IUniquenessDataStore
{
    private readonly ConcurrentDictionary<string, Participant> ById = new();
    private readonly ConcurrentDictionary<string, string> BySSN = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndHomePhoneNumber = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndMobilePhoneNumber = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndAddress = new();
    private readonly ConcurrentDictionary<string, string> ByNameAndEmail = new();

    public Task Add(Participant participant)
    {
        if (!string.IsNullOrEmpty(participant.SSN))
            BySSN.TryAdd(participant.SSN, participant.Id);

        if (participant.Name is not null && participant.HomePhone is not null)
            ByNameAndHomePhoneNumber.TryAdd($"{participant.Name}:{participant.HomePhone}", participant.Id);

        if (participant.Name is not null && participant.MobilePhone is not null)
            ByNameAndMobilePhoneNumber.TryAdd($"{participant.Name}:{participant.MobilePhone}", participant.Id);

        if (participant.Name is not null && participant.Email is not null)
            ByNameAndEmail.TryAdd($"{participant.Name}:{participant.Email}", participant.Id);

        if (participant.Name is not null && participant.Address is not null)
            ByNameAndAddress.TryAdd($"{participant.Name}:{participant.Address}", participant.Id);
        
        ById.TryAdd(participant.Id, participant);

        return Task.CompletedTask;
    }

    Task<ConcurrentDictionary<string, Participant>> IUniquenessDataStore.ById()
    {
        return Task.FromResult(ById);
    }

    Task<ConcurrentDictionary<string, string>> IUniquenessDataStore.BySSN()
    {
        return Task.FromResult(BySSN);
    }

    Task<ConcurrentDictionary<string, string>> IUniquenessDataStore.ByNameAndHomePhoneNumber()
    {
        return Task.FromResult(ByNameAndHomePhoneNumber);
    }

    Task<ConcurrentDictionary<string, string>> IUniquenessDataStore.ByNameAndMobilePhoneNumber()
    {
        return Task.FromResult(ByNameAndMobilePhoneNumber);
    }

    Task<ConcurrentDictionary<string, string>> IUniquenessDataStore.ByNameAndAddress()
    {
        return Task.FromResult(ByNameAndAddress);
    }

    Task<ConcurrentDictionary<string, string>> IUniquenessDataStore.ByNameAndEmail()
    {
        return Task.FromResult(ByNameAndEmail);
    }
}

public interface IEventStore
{
    Task<List<Event>> Load(string aggregateType, string aggregateId);
    Task Append(string aggregateType, Event e);
}

public class MemoryEventStore : IEventStore
{
    private readonly ConcurrentDictionary<string, List<Event>> _store = new();
    private string Key(string aggregateType, string aggregateId) => $"{aggregateType.ToLower()}:{aggregateId}";

    public Task Append(string aggregateType, Event e)
    {
        var list = _store.GetOrAdd(Key(aggregateType, e.AggregateId), []);
        list.Add(e);
        return Task.CompletedTask;
    }

    public Task<List<Event>> Load(string aggregateType, string aggregateId)
    {
        return Task.FromResult(_store.TryGetValue(Key(aggregateType, aggregateId), out var events) ? events : []);
    }
}

public static class GenerateIds
{
    private static int _id = 1000;
    public static string NewId() => (++_id).ToString();
}
