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
        if (string.IsNullOrWhiteSpace(request.Id))
            throw new ArgumentException("Id is required");

        var events = await eventStore.Load(request.Id);
        if (events.Count == 0)
            throw new ArgumentException("Person not found");
        
        var projection = new PersonProjection();
        projection.Load(events);
        var person = projection.GetPersonById(request.Id);
        if (person == null)
            throw new ArgumentException("Person not found");

        var commands = GetCommandsBasedOnChanges(person, request);
        foreach (var command in commands)
        {
            var @event = await mediator.Send(command);
            await eventStore.Append("person", @event);
        }
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
