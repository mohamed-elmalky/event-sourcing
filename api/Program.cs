using System.Collections.Concurrent;
using System.Reflection;
using Shared;
using MediatR;
using CommonEvents;
using PersonEvents;
using OrganizationEvents;
using Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())
);
var eventStore = new MemoryEventStore();
var uniquenessDataStore = new UniquenessMemoryDataStore();

builder.Services.AddSingleton<IEventStore>(eventStore);
builder.Services.AddSingleton<IUniquenessDataStore>(uniquenessDataStore);
builder.Services.AddSingleton<IMediator, Mediator>();

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PersonUniqueBySSNBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PersonUniqeByNameAndHomePhoneNumberBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PersonUniqueByNameAndMobileNumberBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PersonUniqueByNameAndEmailBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PersonUniqueByNameAndAddressBehavior<,>));

var app = builder.Build();

var mediator = app.Services.GetRequiredService<IMediator>();

app.MapGet("/", () => {
    return Results.Ok("Hello? Is it me you're looking for?");
});
app.MapPost("/participants", async (PersonRequest request) => 
{
    var addParticipantSlice = new AddPersonSlice(eventStore, uniquenessDataStore, mediator);

    try
    {
        var id = await addParticipantSlice.AddParticipant(request);
        return Results.Created($"/participants/{id}", id);
    }
    catch (ParticipantAlreadyExistsException ex)
    {
        return Results.Conflict(new { message = ex.Message, id = ex.ParticipantId });
    }
});
app.MapGet("/participants/events/{id}", (string id) => {
    return Results.Ok(eventStore.Load("participant", id));
});
app.MapDelete("/participants/{id}", async (string id) => {
    var deactivateParticipantSlice = new DeactivateParticipantSlice(mediator, eventStore);
    await deactivateParticipantSlice.DeactivateParticipant(id);
    return Results.NoContent();
});
app.MapGet("/participants/{id}", async (string id) => {
    var events = await eventStore.Load("participant", id);
    Console.WriteLine($"Events: {events.Count}");
    var participantProjection = new PersonProjection();
    participantProjection.Load(events);
    return Results.Ok(participantProjection.Participants[id]);
});

app.Run();








public partial class Program {}