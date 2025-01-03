using System.ComponentModel.Design;
using MediatR;
using Models;
using Persistence;
using Shared;

namespace PersonEvents;

public class ModifyPerson(IEventStore eventStore, IMediator mediator) : Slice(mediator, eventStore)
{
    public async Task Execute(PersonRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id)) throw new ArgumentException("Id is required");

        var events = await eventStore.Load(request.Id);
        if (events.Count == 0) throw new ArgumentException("No events found");
        
        var projection = new PersonProjection();
        projection.Load(events);
        var person = projection.GetPersonById(request.Id) ?? throw new ArgumentException("Person not found");

        foreach (var command in GetCommandsBasedOnChanges(person, request))
            _ = await mediator.Send(command);
    }

    private List<Command> GetCommandsBasedOnChanges(Person person, PersonRequest request)
    {
        var commands = new List<Command>();
        if (person.SSN != request.SSN)
            commands.Add(new ModifyPersonSSNCommand(person.Id) { SSN = request.SSN });
        if (person.HomePhone != request.HomePhone)
            commands.Add(new ModifyPersonHomePhoneCommand(person.Id) { HomePhone = request.HomePhone });
        return commands;
    }
}
