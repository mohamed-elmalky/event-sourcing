using CommonEvents;
using Shared;

namespace PersonEvents;

public class ParticipantProjection
{
    public Dictionary<string, Participant> Participants { get; } = [];

    public void Load(IEnumerable<Event> events)
    {
        foreach (var e in events)
            Apply(e);
    }

    private void Apply(Event e)
    {
        Participants.TryAdd(e.AggregateId, new Participant(e.AggregateId));

        switch (e)
        {
            case ParticipantAcquired participantAcquired:
                Participants[participantAcquired.AggregateId] = participantAcquired.Participant ?? Participants[participantAcquired.AggregateId];
                break;
            case ParticipantDeactivated participantDeactivated:
                Participants[participantDeactivated.AggregateId].IsActive = false;
                break;
        }
    }
}
