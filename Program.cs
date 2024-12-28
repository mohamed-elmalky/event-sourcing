using System.Collections.Concurrent;
using System.Reflection;
using MediatR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())
);
builder.Services.AddSingleton<IEventStore, MemoryEventStore>();
builder.Services.AddSingleton<IMediator, Mediator>();

var app = builder.Build();
var mediator = app.Services.GetRequiredService<IMediator>();
var eventStore = new MemoryEventStore();

for (var i = 0; i < 10; i++)
{
    var addParticipantSlice = new AddParticipantSlice(eventStore, mediator);
    await addParticipantSlice.AddParticipant();
}
eventStore.Events.ForEach(Console.WriteLine);

app.MapGet("/", () => "Hello World!");
app.MapPost("/participants", async (ParticipantRequest request) => 
{
    var addParticipantSlice = new AddParticipantSlice(eventStore, mediator);
    return await addParticipantSlice.AddParticipant();
});

app.Run();

public record ParticipantRequest(string Name);

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
            Participant = new Participant(request.AggregateId, Faker.Name.FullName(), true),
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

    public async Task<string> DeactivateParticipant(string ParticipantId)
    {
        var deactivateParticipantCommand = new DeactiveParticipantCommand(ParticipantId);
        var ParticipantDeactivated = await mediator.Send(deactivateParticipantCommand);
        await eventStore.Append("Participant", ParticipantDeactivated);
        return ParticipantId;
    }
}

public class AddParticipantSlice : Slice
{
    public AddParticipantSlice(IEventStore eventStore, IMediator mediator) : base(mediator, eventStore) { }
    public async Task<string> AddParticipant()
    {
        var aggregateId = GenerateIds.NewId();
        var addParticipantCommand = new AddParticipantCommand(aggregateId);
        var ParticipantAdded = await mediator.Send(addParticipantCommand);
        await eventStore.Append("Participant", ParticipantAdded);
        return aggregateId;
    }
}

public record Participant(string Id, string Name, bool IsActive);

public interface IEventStore
{
    public List<Event> Events { get; }
    Task Append(string aggregateType, Event e);
    Task Load();
}

public class MemoryEventStore : IEventStore
{
    readonly ConcurrentDictionary<string, List<Event>> _store = new();
    public List<Event> Events => _store.SelectMany(x => x.Value).ToList();

    public Task Append(string aggregateType, Event e)
    {
        var key = $"{aggregateType.ToLower()}:{e.AggregateId}";
        var list = _store.GetOrAdd(key, []);
        list.Add(e);
        return Task.CompletedTask;
    }

    public Task Load()
    {
        throw new NotImplementedException();
    }
}

public static class GenerateIds
{
    private static int _id = 1000;
    public static string NewId() => (++_id).ToString();
}