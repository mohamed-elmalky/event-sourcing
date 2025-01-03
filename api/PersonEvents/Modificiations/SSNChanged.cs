using MediatR;
using Persistence;
using Shared;

namespace PersonEvents;

public record PersonSSNChanged(string AggregateId) : Event("person-ssn-changed")
{
    public string? SSN { get; init; }
}

public record ModifyPersonSSNCommand(string AggregateId) : Command(AggregateId), IRequest<PersonSSNChanged>
{
    public string? SSN { get; init; }
    public override PersonSSNChanged ToEvent() => new(AggregateId) { 
        SSN = SSN,
        OccuredAt = DateTimeOffset.UtcNow
    };
}

public class ModifyPersonSSNCommandHandler(IEventStore eventStore) : IRequestHandler<ModifyPersonSSNCommand, PersonSSNChanged>
{
    public async Task<PersonSSNChanged> Handle(ModifyPersonSSNCommand request, CancellationToken cancellationToken)
    {
        var @event = request.ToEvent();
        await eventStore.Append(@event.Kind, @event);
        return @event;
    }
}
