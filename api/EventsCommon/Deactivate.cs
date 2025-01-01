using Shared;
using MediatR;
using Persistence;

namespace CommonEvents;

public record ParticipantDeactivated(string AggregateId) : Event("participant-deactivated");
public record DeactiveParticipantCommand(string AggregateId) : Command<ParticipantDeactivated>
{
    public override ParticipantDeactivated ToEvent() => new(AggregateId) { 
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
