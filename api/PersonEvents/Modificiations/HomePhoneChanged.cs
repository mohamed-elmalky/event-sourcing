using MediatR;
using Persistence;
using Shared;

namespace PersonEvents;

public record PersonHomePhoneChanged(string AggregateId) : Event("person-home-phone-changed")
{
    public string? HomePhone { get; init; }
}

public record ModifyPersonHomePhoneCommand(string AggregateId) : Command(AggregateId), IRequest<PersonHomePhoneChanged>
{
    public string? HomePhone { get; init; }
    public override PersonHomePhoneChanged ToEvent() => new(AggregateId) { 
        HomePhone = HomePhone,
        OccuredAt = DateTimeOffset.UtcNow
    };
}

public class ModifyPersonHomePhoneCommandHandler(IEventStore eventStore) : IRequestHandler<ModifyPersonHomePhoneCommand, PersonHomePhoneChanged>
{
    public async Task<PersonHomePhoneChanged> Handle(ModifyPersonHomePhoneCommand request, CancellationToken cancellationToken)
    {
        var @event = request.ToEvent();
        await eventStore.Append(@event.Kind, @event);
        return @event;
    }
}
