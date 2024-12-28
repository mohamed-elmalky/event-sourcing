using System.Collections.Concurrent;
using System.Reflection;
using MediatR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())
);
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

app.Run();

public abstract class Command<TResponse> : IRequest<TResponse> { }
public abstract record Event()
{
    public abstract string Kind { get; }
    public abstract string AggregateId { get; init; }
    public DateTimeOffset OccuredAt { get; set; }
}

public record ParticipantAcquired(string AggregateId) : Event
{
    public override string Kind => "participant-acquired";
    public Participant? Participant { get; set; }
}

public record ParticipantDeactivated(string AggregateId) : Event
{
    public override string Kind => "participant-deactivated";
    public Participant? Participant { get; set; }
}


public class AddParticipantCommand : Command<ParticipantAcquired>
{
    public string AggregateId { get; init; }
    public AddParticipantCommand(string aggregateId) => AggregateId = aggregateId;
}

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


public class DeactiveParticipantCommand : Command<ParticipantDeactivated>
{
    public string AggregateId { get; init; }
    public DeactiveParticipantCommand(string aggregateId) => AggregateId = aggregateId;
}

public class DeactiveParticipantCommandHandler : IRequestHandler<DeactiveParticipantCommand, ParticipantDeactivated>
{
    public Task<ParticipantDeactivated> Handle(DeactiveParticipantCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new ParticipantDeactivated(request.AggregateId));
    }
}

public class DeactivateParticipantSlice
{
    private readonly IEventStore _eventStore;
    private readonly IMediator _mediator;
    public DeactivateParticipantSlice(IEventStore eventStore, IMediator mediator)
    {
        _eventStore = eventStore;
        _mediator = mediator;
    }
    public async Task<string> DeactivateParticipant(string ParticipantId)
    {
        var deactivateParticipantCommand = new DeactiveParticipantCommand(ParticipantId);
        var ParticipantDeactivated = await _mediator.Send(deactivateParticipantCommand);
        await _eventStore.Append("Participant", ParticipantDeactivated);
        return ParticipantId;
    }
}

public class AddParticipantSlice
{
    private readonly IEventStore _eventStore;
    private readonly IMediator _mediator;
    public AddParticipantSlice(IEventStore eventStore, IMediator mediator)
    {
        _eventStore = eventStore;
        _mediator = mediator;
    }
    public async Task<string> AddParticipant()
    {
        var aggregateId = GenerateIds.NewId();
        var addParticipantCommand = new AddParticipantCommand(aggregateId);
        var ParticipantAdded = await _mediator.Send(addParticipantCommand);
        await _eventStore.Append("Participant", ParticipantAdded);
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
