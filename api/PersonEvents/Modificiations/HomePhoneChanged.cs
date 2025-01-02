using MediatR;
using Persistence;
using Shared;

namespace PersonEvents;

public record PersonHomePhoneChanged(string AggregateId) : Event("person-home-phone-changed")
{
    public string? HomePhone { get; init; }
}

public record ModifyPersonHomePhoneCommand(string AggregateId) : Command(AggregateId), IRequest<PersonHomePhoneChanged>
{
    public string? HomePhone { get; init; }
    public override PersonHomePhoneChanged ToEvent() => new(AggregateId) { 
        HomePhone = HomePhone,
        OccuredAt = DateTimeOffset.UtcNow
    };
}

public class ModifyPersonHomePhoneCommandHandler : IRequestHandler<ModifyPersonHomePhoneCommand, PersonHomePhoneChanged>
{
    public Task<PersonHomePhoneChanged> Handle(ModifyPersonHomePhoneCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(request.ToEvent());
    }
}

public class ModifyPersonHomePhoneSlice : Slice
{
    public ModifyPersonHomePhoneSlice(IMediator mediator, IEventStore eventStore) : base(mediator, eventStore) { }

    public async Task ModifyPersonHomePhone(string id, string homePhone)
    {
        var modifyPersonHomePhoneCommand = new ModifyPersonHomePhoneCommand(id) { HomePhone = homePhone };
        var personHomePhoneChanged = await mediator.Send(modifyPersonHomePhoneCommand);
        await eventStore.Append("person-home-phone-changed", personHomePhoneChanged);
        Console.WriteLine($"Person home phone changed: {id}");
    }
}
