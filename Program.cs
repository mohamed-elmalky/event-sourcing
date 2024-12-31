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
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ParticipantUniqeByNameAndNumberBehavior<,>));

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
        return Results.Conflict(ex.Message);
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
}

public record Participant(string Id)
{
    public bool IsActive { get; set; }
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
}

public record Address(string Address1, string Address2, string City, string State, string Country, string ZipCode)
{
    public override string ToString() => $"{Address1}, {Address2}, {City}, {State}, {Country}, {ZipCode}";
}

public abstract record Command<TResponse> : IRequest<TResponse> 
{ 
    public abstract string AggregateId { get; init; }
    public Participant? Participant { get; set; }
}
public abstract record Event(string Kind)
{
    public abstract string AggregateId { get; init; }
    public DateTimeOffset OccuredAt { get; set; }
    public Participant? Participant { get; set; }
}

public record ParticipantAcquired(string AggregateId) : Event("participant-acquired");
public record AddParticipantCommand(string AggregateId) : Command<ParticipantAcquired>;
public class AddParticipantCommandHandler : IRequestHandler<AddParticipantCommand, ParticipantAcquired>
{
    public Task<ParticipantAcquired> Handle(AddParticipantCommand request, CancellationToken cancellationToken)
    {
        var participantAcquired = new ParticipantAcquired(request.AggregateId) { 
            Participant = request.Participant,
            OccuredAt = DateTimeOffset.UtcNow
        };
        return Task.FromResult(participantAcquired);
    }
}

public record ParticipantDeactivated(string AggregateId) : Event("participant-deactivated");
public record DeactiveParticipantCommand(string AggregateId) : Command<ParticipantDeactivated>;
public class DeactiveParticipantCommandHandler : IRequestHandler<DeactiveParticipantCommand, ParticipantDeactivated>
{
    public Task<ParticipantDeactivated> Handle(DeactiveParticipantCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ParticipantDeactivated(request.AggregateId));
    }
}

public class Slice(IMediator mediator, IEventStore eventStore)
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
            IsActive = true
        };
        var addParticipantCommand = new AddParticipantCommand(participant.Id) { Participant = participant };

        var participantAdded = await mediator.Send(addParticipantCommand);
        
        await eventStore.Append("participant", participantAdded);

        await uniquenessDataStore.Add(participant);
        
        Console.WriteLine($"Participant added: {participant.Id}");
        
        return participant.Id;
    }
}

public class ParticipantAlreadyExistsException : Exception
{
    public ParticipantAlreadyExistsException(string message) : base(message) { }
}

/// <summary>
/// Ensures that a participant is unique by name and phone number. This is the second highest priority uniqueness constraint.
/// </summary>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class ParticipantUniqeByNameAndNumberBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : AddParticipantCommand
{
    private readonly IUniquenessDataStore _uniquenessDataStore;

    public ParticipantUniqeByNameAndNumberBehavior(IUniquenessDataStore uniquenessDataStore) => _uniquenessDataStore = uniquenessDataStore;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request.Participant is null)
        {
            Console.WriteLine("Participant is null");
            return await next();
        }

        var nameAndHomePhoneNumber = await _uniquenessDataStore.ByNameAndHomePhoneNumber();
        var nameAndMobilePhoneNumber = await _uniquenessDataStore.ByNameAndMobilePhoneNumber();

        var uniqueByNameAndHomePhoneNumber = !nameAndHomePhoneNumber.ContainsKey($"{request.Participant.Name}:{request.Participant.HomePhone}");
        var uniqueByNameAndMobilePhoneNumber = !nameAndMobilePhoneNumber.ContainsKey($"{request.Participant.Name}:{request.Participant.MobilePhone}");

        if (uniqueByNameAndHomePhoneNumber)
            Console.WriteLine("Participant is unique by name and home phone number");
        else if (uniqueByNameAndMobilePhoneNumber)
            Console.WriteLine("Participant is unique by name and mobile phone number");
        else
        {
            Console.WriteLine("Participant already exists with this name and number");
            throw new ParticipantAlreadyExistsException("Participant already exists with this name and number");
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
        if (request.Participant is null || request.Participant.SSN is null)
        {
            Console.WriteLine("Participant is null");
            return await next();
        }

        var ssns = await _uniquenessDataStore.BySSN();
        var uniqueBySSN = !ssns.ContainsKey(request.Participant.SSN);
        if (uniqueBySSN)
        {
            Console.WriteLine("Unique by SSN");
            // Not really happy with creating the event here, but it's the only way I can short-circuit the pipeline for this specific behavior.
            var participantAcquired = new ParticipantAcquired(request.AggregateId) { 
                Participant = request.Participant,
                OccuredAt = DateTimeOffset.UtcNow
            };
            return (TResponse)participantAcquired;
        }
        else
        {
            Console.WriteLine("Participant already exists with this SSN");
            throw new ParticipantAlreadyExistsException("Participant already exists with this SSN");
        }

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

}
public class UniquenessMemoryDataStore : IUniquenessDataStore
{
    public readonly ConcurrentDictionary<string, Participant> ById = new();
    public readonly ConcurrentDictionary<string, string> BySSN = new();
    public readonly ConcurrentDictionary<string, string> ByNameAndHomePhoneNumber = new();
    public readonly ConcurrentDictionary<string, string> ByNameAndMobilePhoneNumber = new();
    public readonly ConcurrentDictionary<string, string> ByNameAndAddress = new();

    public string NameAndPhoneNumber(string? name, string? phoneNumber) => $"{name}:{phoneNumber}";
    public string NameAndAddress(string? name, Address? address) => $"{name}:{address}";

    public Task Add(Participant participant)
    {
        if (participant.SSN is not null)
            BySSN.TryAdd(participant.SSN, participant.Id);

        if (participant.Name is not null && participant.HomePhone is not null)
            ByNameAndHomePhoneNumber.TryAdd(NameAndPhoneNumber(participant.Name, participant.HomePhone), participant.Id);

        if (participant.Name is not null && participant.MobilePhone is not null)
            ByNameAndMobilePhoneNumber.TryAdd(NameAndPhoneNumber(participant.Name, participant.MobilePhone), participant.Id);
        
        if (participant.Name is not null && participant.Address is not null)
            ByNameAndAddress.TryAdd(NameAndAddress(participant.Name, participant.Address), participant.Id);
        
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
