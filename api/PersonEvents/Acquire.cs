using MediatR;
using Models;
using Persistence;
using Shared;

namespace PersonEvents;

public record PersonAcquired(string AggregateId) : Event("person-acquired")
{
    public Person? Participant { get; init; }
}
public record AddPersonCommand(string AggregateId) : Command(AggregateId), IRequest<PersonAcquired>
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
        var participant = request.ToModel(GenerateIds.NewId());
        var addParticipantCommand = new AddPersonCommand(participant.Id) { Participant = participant };

        var participantAdded = await mediator.Send(addParticipantCommand); // This will trigger the pipeline behaviors. If the participant is not unique, an exception will be thrown.
        await eventStore.Append(participantAdded); // We won't reach this point if the participant is not unique.
        await uniquenessDataStore.Add(participant);
        
        Console.WriteLine($"Participant added: {participant.Id}");
        return participant.Id;
    }
}
