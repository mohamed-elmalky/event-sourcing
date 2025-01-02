using System.ComponentModel.Design;
using Models;
using Persistence;
using Shared;

namespace PersonEvents;

public class ModifyPerson(IEventStore eventStore)
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
        
    }
}
