using System.Collections.Concurrent;
using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.WriteLine("Hello World!");

using IHost host = Host.CreateDefaultBuilder()
    .ConfigureServices(static (context, services) =>
    {
        services.AddMediatR(cfg => 
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())
        );
    })
    .Build();

var eventStore = new MemoryEventStore();

using var scope = host.Services.CreateScope();
var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

for (var i = 0; i < 10; i++)
{
    var addCustomerSlice = new AddCustomerSlice(eventStore, mediator);
    await addCustomerSlice.AddCustomer();
}

var deactivateCustomerSlice = new DeactivateCustomerSlice(eventStore, mediator);
var deactivatedCustomerId = await deactivateCustomerSlice.DeactivateCustomer(eventStore.Events[0].AggregateId);

eventStore.Events.ForEach(Console.WriteLine);

Console.WriteLine("Press any key to exit...");
Console.ReadLine();

public abstract class Command<TResponse> : IRequest<TResponse> { }
public abstract record Event()
{
    public abstract string Kind { get; }
    public abstract string AggregateId { get; init; }
}

public record CustomerAcquired(string AggregateId) : Event
{
    public override string Kind => "customer-added";
}

public record CustomerDeactivated(string AggregateId) : Event
{
    public override string Kind => "customer-deactivated";
}


public class AddCustomerCommand : Command<CustomerAcquired>
{
    public string AggregateId { get; init; }
    public AddCustomerCommand(string aggregateId) => AggregateId = aggregateId;
}

public class AddCustomerCommandHandler : IRequestHandler<AddCustomerCommand, CustomerAcquired>
{
    public Task<CustomerAcquired> Handle(AddCustomerCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CustomerAcquired(request.AggregateId));
    }
}


public class DeactiveCustomerCommand : Command<CustomerDeactivated>
{
    public string AggregateId { get; init; }
    public DeactiveCustomerCommand(string aggregateId) => AggregateId = aggregateId;
}

public class DeactiveCustomerCommandHandler : IRequestHandler<DeactiveCustomerCommand, CustomerDeactivated>
{
    public Task<CustomerDeactivated> Handle(DeactiveCustomerCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CustomerDeactivated(request.AggregateId));
    }
}

public class DeactivateCustomerSlice
{
    private readonly IEventStore _eventStore;
    private readonly IMediator _mediator;
    public DeactivateCustomerSlice(IEventStore eventStore, IMediator mediator)
    {
        _eventStore = eventStore;
        _mediator = mediator;
    }
    public async Task<string> DeactivateCustomer(string customerId)
    {
        var deactivateCustomerCommand = new DeactiveCustomerCommand(customerId);
        var customerDeactivated = await _mediator.Send(deactivateCustomerCommand);
        await _eventStore.Append("customer", customerDeactivated);
        return customerId;
    }
}

public class AddCustomerSlice
{
    private readonly IEventStore _eventStore;
    private readonly IMediator _mediator;
    public AddCustomerSlice(IEventStore eventStore, IMediator mediator)
    {
        _eventStore = eventStore;
        _mediator = mediator;
    }
    public async Task<string> AddCustomer()
    {
        var aggregateId = GenerateIds.NewId();
        var addCustomerCommand = new AddCustomerCommand(aggregateId);
        var customerAdded = await _mediator.Send(addCustomerCommand);
        await _eventStore.Append("customer", customerAdded);
        return aggregateId;
    }
}

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
