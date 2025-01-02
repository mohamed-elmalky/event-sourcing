using MediatR;
using Persistence;
using Shared;

namespace PersonEvents;

public record PersonSSNChanged(string AggregateId) : Event("person-ssn-changed")
{
    public string? SSN { get; init; }
}

public record ModifyPersonSSNCommand(string AggregateId) : Command(AggregateId), IRequest<PersonSSNChanged>
{
    public string? SSN { get; init; }
    public override PersonSSNChanged ToEvent() => new(AggregateId) { 
        SSN = SSN,
        OccuredAt = DateTimeOffset.UtcNow
    };
}

public class ModifyPersonSSNCommandHandler : IRequestHandler<ModifyPersonSSNCommand, PersonSSNChanged>
{
    public Task<PersonSSNChanged> Handle(ModifyPersonSSNCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.ToEvent());
    }
}

public class ModifyPersonSSNSlice : Slice
{
    public ModifyPersonSSNSlice(IMediator mediator, IEventStore eventStore) : base(mediator, eventStore) { }

    public async Task ModifyPersonSSN(string id, string ssn)
    {
        var modifyPersonSSNCommand = new ModifyPersonSSNCommand(id) { SSN = ssn };
        var personSSNChanged = await mediator.Send(modifyPersonSSNCommand);
        await eventStore.Append("person-ssn-changed", personSSNChanged);
        Console.WriteLine($"Person SSN changed: {id}");
    }
}
