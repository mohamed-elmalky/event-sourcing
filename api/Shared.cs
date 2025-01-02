using MediatR;
using Persistence;

namespace Shared;

public abstract record Participant(string Id);

public abstract record Command<TResponse> : IRequest<TResponse> 
{ 
    public abstract string AggregateId { get; init; }
    public abstract TResponse ToEvent();
}
public abstract record Event(string Kind)
{
    public abstract string AggregateId { get; init; }
    public DateTimeOffset OccuredAt { get; set; }
}

public abstract class Slice(IMediator mediator, IEventStore eventStore)
{
    protected readonly IMediator mediator = mediator;
    protected readonly IEventStore eventStore = eventStore;
}

public record Address(string Address1, string Address2, string City, string State, string Country, string ZipCode)
{
    public override string ToString() => $"{Address1}, {Address2}, {City}, {State}, {Country}, {ZipCode}";
}

public class ParticipantAlreadyExistsException(string message) : Exception(message)
{
    public string? ParticipantId { get; set; } 
}

public static class GenerateIds
{
    private static int _id = 1000;
    public static string NewId() => (++_id).ToString();
}

