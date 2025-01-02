using Shared;
using MediatR;
using Persistence;
using Models;

namespace OrganizationEvents;

public record Organization(string Id) : Participant(Id)
{
    public string? Name { get; set; }
    public string? EIN { get; set; }
    public Address? Address { get; set; }
}

public record OrganizationRequest()
{
    public string? Name { get; set; }
    public string? EIN { get; set; }
    public Address? Address { get; set; }

    public Organization ToModel(string id) => new(id)
    {
        Name = Name,
        EIN = EIN,
        Address = Address
    };
}

public record OrganizationAcquired(string AggregateId) : Event("participant-organization-acquired")
{
    public Organization? ParticipantOrganization { get; init; }
}

public record AddOrganizationCommand(string AggregateId) : Command(AggregateId), IRequest<OrganizationAcquired>
{
    public Organization? ParticipantOrganization { get; init; }
    public override OrganizationAcquired ToEvent() => new(AggregateId) { 
        ParticipantOrganization = ParticipantOrganization,
        OccuredAt = DateTimeOffset.UtcNow
    };
}

public class AddOrganizationCommandHandler : IRequestHandler<AddOrganizationCommand, OrganizationAcquired>
{
    public Task<OrganizationAcquired> Handle(AddOrganizationCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.ToEvent());
    }
}

public class AddOrganizationSlice : Slice
{
    public AddOrganizationSlice(IMediator mediator, IEventStore eventStore) : base(mediator, eventStore) { }

    public async Task<string> AddParticipantOrganization(Organization participantOrganization)
    {
        var addParticipantOrganizationCommand = new AddOrganizationCommand(participantOrganization.Id) { ParticipantOrganization = participantOrganization };
        var participantOrganizationAdded = await mediator.Send(addParticipantOrganizationCommand);
        await eventStore.Append("participant-organization", participantOrganizationAdded);
        Console.WriteLine($"Participant organization added: {participantOrganization.Id}");
        return participantOrganization.Id;
    }
}
