using System.Text;
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

tasks.MapGet("/export", (TaskStore store) =>
    Results.File(Encoding.UTF8.GetBytes(store.Export()), "text/csv; charset=utf-8", "tasks.csv"));

app.Run();

/// <summary>Lets the tests start the app in memory.</summary>
public partial class Program;
