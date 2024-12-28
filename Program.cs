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

using var scope = host.Services.CreateScope();
var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

var addCustomerCommand = new AddCustomerCommand(GenerateIds.NewId());
var customerAdded = await mediator.Send(addCustomerCommand);
Console.WriteLine($"Customer added: {customerAdded.AggregateId}");

Console.WriteLine("Press any key to exit...");
Console.ReadLine();

public abstract record Event()
{
    public abstract string Kind { get; }
    public abstract string AggregateId { get; init; }
}

public record CustomerAdded(string AggregateId) : Event
{
    public override string Kind => "customer-added";
}

public class AddCustomerCommand : IRequest<CustomerAdded>
{
    public string AggregateId { get; init; }
    public AddCustomerCommand(string aggregateId) => AggregateId = aggregateId;
}

public class AddCustomerCommandHandler : IRequestHandler<AddCustomerCommand, CustomerAdded>
{
    public Task<CustomerAdded> Handle(AddCustomerCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CustomerAdded(request.AggregateId));
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
