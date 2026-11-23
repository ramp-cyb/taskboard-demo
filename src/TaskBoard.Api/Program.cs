using TaskBoard.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<TaskStore>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var tasks = app.MapGroup("/api/tasks");

tasks.MapGet("/", (TaskStore store, string? search) => Results.Ok(store.List(search)));

tasks.MapGet("/{id:int}", (TaskStore store, int id) =>
    store.Get(id) is { } task ? Results.Ok(task) : Results.NotFound());

tasks.MapPost("/", (TaskStore store, NewTask request) =>
{
    var task = store.Add(request);
    return Results.Created($"/api/tasks/{task.Id}", task);
});

tasks.MapPost("/{id:int}/toggle", (TaskStore store, int id) =>
    store.Toggle(id) is { } task ? Results.Ok(task) : Results.NotFound());

tasks.MapDelete("/{id:int}", (TaskStore store, int id) =>
{
    store.Delete(id);
    return Results.NoContent();
});

app.MapGet("/api/stats", (TaskStore store) => Results.Ok(store.GetStats()));

app.Run();

/// <summary>Lets the tests start the app in memory.</summary>
public partial class Program;
