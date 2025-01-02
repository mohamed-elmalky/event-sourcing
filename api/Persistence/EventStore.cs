using System.Collections.Concurrent;
using Shared;

namespace Persistence;

public interface IEventStore
{
    Task<List<Event>> Load(string aggregateId);
    Task<List<Event>> Load(string aggregateType, string aggregateId);
    Task Append(string aggregateType, Event e);
}

public class MemoryEventStore : IEventStore
{
    public const string PARTICIPANT_TYPE = "participant";
    private readonly ConcurrentDictionary<string, List<Event>> _store = new();
    private string Key(string aggregateType, string aggregateId) => $"{aggregateType.ToLower()}:{aggregateId}";

    public Task Append(string aggregateType, Event e)
    {
        var list = _store.GetOrAdd(Key(aggregateType, e.AggregateId), []);
        list.Add(e);
        return Task.CompletedTask;
    }

    public async Task<List<Event>> Load(string aggregateId)
    {
        return await Task.FromResult(_store.TryGetValue(Key(PARTICIPANT_TYPE, aggregateId), out var events) ? events : []);
    }

    public async Task<List<Event>> Load(string aggregateType, string aggregateId)
    {
        return await Task.FromResult(_store.TryGetValue(Key(aggregateType, aggregateId), out var events) ? events : []);
    }
}
