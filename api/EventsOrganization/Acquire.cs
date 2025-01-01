using Shared;
using MediatR;
using Persistence;

namespace OrganizationEvents;

public record ParticipantOrganization(string Id)
{
    public string? Name { get; set; }
    public string? EIN { get; set; }
    public Address? Address { get; set; }
}

public record ParticipantOrganizationAcquired(string AggregateId) : Event("participant-organization-acquired")
{
    public ParticipantOrganization? ParticipantOrganization { get; init; }
}
public record AddParticipantOrganizationCommand(string AggregateId) : Command<ParticipantOrganizationAcquired>
{
    public ParticipantOrganization? ParticipantOrganization { get; init; }
    public override ParticipantOrganizationAcquired ToEvent() => new(AggregateId) { 
        ParticipantOrganization = ParticipantOrganization,
        OccuredAt = DateTimeOffset.UtcNow
    };
}

public class AddParticipantOrganizationCommandHandler : IRequestHandler<AddParticipantOrganizationCommand, ParticipantOrganizationAcquired>
{
    public Task<ParticipantOrganizationAcquired> Handle(AddParticipantOrganizationCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.ToEvent());
    }
}

public class AddParticipantOrganizationSlice : Slice
{
    public AddParticipantOrganizationSlice(IMediator mediator, IEventStore eventStore) : base(mediator, eventStore) { }

    public async Task<string> AddParticipantOrganization(ParticipantOrganization participantOrganization)
    {
        var addParticipantOrganizationCommand = new AddParticipantOrganizationCommand(participantOrganization.Id) { ParticipantOrganization = participantOrganization };
        var participantOrganizationAdded = await mediator.Send(addParticipantOrganizationCommand);
        await eventStore.Append("participant-organization", participantOrganizationAdded);
        Console.WriteLine($"Participant organization added: {participantOrganization.Id}");
        return participantOrganization.Id;
    }
}
