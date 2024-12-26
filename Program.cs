using System.Collections.Concurrent;

Console.WriteLine("Hello World!");

var addCustomer = new AddCustomer(GenerateIds.NewId());
var customerAdded = addCustomer.ToEvent();
Console.WriteLine(customerAdded.Kind);


Console.WriteLine("Press any key to exit...");
Console.ReadLine();

public abstract record Event()
{
    public abstract string Kind { get; }
    public abstract string AggregateId { get; init; }
}
public abstract record Command()
{
    public abstract Event ToEvent();
};
public interface IEventStore
{
    public List<Event> Events { get; }
    Task Append(string aggregateType, Event e);
    Task Load();
}
public class MemoryEventStore : IEventStore
{
    readonly ConcurrentDictionary<string, List<Event>> _store = new();
    public List<Event> Events => _store.SelectMany(x => x.Value).ToList();

    public Task Append(string aggregateType, Event e)
    {
        var key = $"{aggregateType.ToLower()}:{e.AggregateId}";
        var list = _store.GetOrAdd(key, []);
        list.Add(e);
        return Task.CompletedTask;
    }

    public Task Load()
    {
        throw new NotImplementedException();
    }
}
public static class GenerateIds
{
    private static int _id = 1000;
    public static string NewId() => (++_id).ToString();
}

public record AddCustomer(string AggregateId) : Command
{
    public override Event ToEvent() => new CustomerAdded(AggregateId);
}

public record CustomerAdded(string AggregateId) : Event
{
    public override string Kind => "customer-added";
}

public class MemoryQueue
{
    private readonly ConcurrentQueue<(TaskCompletionSource tsc, Command cmd)> _queue = new();

    public Task Enqueue(Command item)
    {
        var tcs = new TaskCompletionSource();
        _queue.Enqueue((tcs, item));
        return tcs.Task;
    }

    public IEnumerable<(TaskCompletionSource, Command)> Dequeue()
    {
        var cmd = _queue.TryDequeue(out var item);
        if (!cmd)
            yield break;
        yield return item;
    }

    public bool IsEmpty => _queue.IsEmpty;
}

public class CommandHandler
{
    readonly MemoryQueue _queue;
    readonly IEventStore _eventStore;
    const int EXCEPTION_DELAY = 5000;
    const int POLL_DELAY = 100;

    public CommandHandler(MemoryQueue queue, IEventStore eventStore)
    {
        _queue = queue;
        _eventStore = eventStore;
    }

    public async Task Run(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await HandleQueue();
                if (_queue.IsEmpty)
                    await Task.Delay(POLL_DELAY, token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Console.WriteLine("CommandHandler stopped.");
        }
    }

    public async Task HandleQueue()
    {
        foreach (var (tcs, cmd) in _queue.Dequeue())
        {
            try
            {
                await Handle(cmd);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occured procesing {cmd} - {ex.Message}");
                tcs.SetException(ex);
                await Task.Delay(EXCEPTION_DELAY);
            }
        }
    }

    private async Task Handle(Command cmd)
    {
        var (aggregateType, e) = cmd switch
        {
            AddCustomer c => ("customer", c.ToEvent()),
            _ => throw new NotImplementedException($"Unknown command {cmd}")
        };
        await _eventStore.Append(aggregateType, e);
    }
}
