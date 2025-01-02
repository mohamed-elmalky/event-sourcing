using CommonEvents;
using Shared;

namespace PersonEvents;

public class PersonProjection
{
    public Dictionary<string, Person> Participants { get; } = [];

    public void Load(IEnumerable<Event> events)
    {
        foreach (var e in events)
            Apply(e);
    }

    private void Apply(Event e)
    {
        Participants.TryAdd(e.AggregateId, new Person(e.AggregateId));

        switch (e)
        {
            case PersonAcquired participantAcquired:
                Participants[participantAcquired.AggregateId] = participantAcquired.Participant ?? Participants[participantAcquired.AggregateId];
                break;
            case ParticipantDeactivated participantDeactivated:
                Participants[participantDeactivated.AggregateId].IsActive = false;
                break;
        }
    }
}