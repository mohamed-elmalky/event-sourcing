using MediatR;
using Persistence;
using Shared;

namespace PersonEvents;

public record Person(string Id) : Participant(Id)
{
    public bool IsActive { get; set; }
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }
}

public record PersonRequest
{
    public string? Name { get; set; }
    public string? SSN { get; set; }
    public string? HomePhone { get; set; }
    public string? MobilePhone { get; set; }
    public Address? Address { get; set; }
    public string? Email { get; set; }
}

public record PersonAcquired(string AggregateId) : Event("participant-acquired")
{
    public Person? Participant { get; init; }
}
public record AddPersonCommand(string AggregateId) : Command<PersonAcquired>
{
    public Person? Participant { get; init; }
    public override PersonAcquired ToEvent() => new(AggregateId) { 
        Participant = Participant,
        OccuredAt = DateTimeOffset.UtcNow
    };
}
public class AddPersonCommandHandler : IRequestHandler<AddPersonCommand, PersonAcquired>
{
    public Task<PersonAcquired> Handle(AddPersonCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.ToEvent());
    }
}

public class AddPersonSlice(IEventStore eventStore, IUniquenessDataStore uniquenessDataStore, IMediator mediator) : Slice(mediator, eventStore)
{
    public async Task<string> AddParticipant(PersonRequest request)
    {
        var participant = new Person(GenerateIds.NewId())
        {
            Name = request.Name,
            SSN = request.SSN,
            HomePhone = request.HomePhone,
            MobilePhone = request.MobilePhone,
            IsActive = true,
            Address = request.Address,
            Email = request.Email
        };
        var addParticipantCommand = new AddPersonCommand(participant.Id) { Participant = participant };

        var participantAdded = await mediator.Send(addParticipantCommand); // This will trigger the pipeline behaviors. If the participant is not unique, an exception will be thrown.
        await eventStore.Append("participant", participantAdded); // We won't reach this point if the participant is not unique.
        await uniquenessDataStore.Add(participant);
        
        Console.WriteLine($"Participant added: {participant.Id}");
        return participant.Id;
    }
}

