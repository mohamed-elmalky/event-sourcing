using MediatR;
using Persistence;
using Shared;

namespace PersonEvents;

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

public record ParticipantRequest
{
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }
}

public record ParticipantAcquired(string AggregateId) : Event("participant-acquired")
{
    public Participant? Participant { get; init; }
}
public record AddParticipantCommand(string AggregateId) : Command<ParticipantAcquired>
{
    public Participant? Participant { get; init; }
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
