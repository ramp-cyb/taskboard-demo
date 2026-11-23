using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TaskBoard.Api;

namespace TaskBoard.Tests;

public class TaskApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListingTasks_ReturnsTheSeededTasks()
    {
        var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/api/tasks");

        Assert.NotNull(tasks);
        Assert.Contains(tasks, t => t.Title == "Prepare the client demo");
    }

    [Fact]
    public async Task AddingATask_ReturnsItWithAnId()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new NewTask("Write tests", Priority.High));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.Equal("Write tests", task!.Title);
        Assert.Equal(Priority.High, task.Priority);
        Assert.False(task.IsDone);
    }

    [Fact]
    public async Task TogglingATask_MarksItDone()
    {
        var created = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask("Toggle me"))).Content.ReadFromJsonAsync<TaskItem>();

        var toggled = await (await _client.PostAsync($"/api/tasks/{created!.Id}/toggle", null)).Content.ReadFromJsonAsync<TaskItem>();

        Assert.True(toggled!.IsDone);
    }

    [Fact]
    public async Task GettingAMissingTask_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/tasks/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletingATask_RemovesIt()
    {
        var created = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask("Delete me"))).Content.ReadFromJsonAsync<TaskItem>();

        await _client.DeleteAsync($"/api/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/tasks/{created.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("prepare")]
    [InlineData("PREPARE")]
    [InlineData("Prepare")]
    public async Task TaskSearching_CaseInsensitive(string query)
    {
        var response = await _client.GetAsync($"/api/tasks?search={query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();

        Assert.NotNull(tasks);
        Assert.Single(tasks);
        Assert.Contains(tasks, t => t.Title == "Prepare the client demo");
    }

    [Fact]
    public async Task TaskSearching_NoMatch_ReturnsEmpty()
    {
        var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/api/tasks?search=no-matching-task");

        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }

    [Fact]
    public async Task TaskSearching_EmptySearch_ReturnsAllSeededTasks()
    {
        var tasks = await _client.GetFromJsonAsync<List<TaskItem>>("/api/tasks?search=");

        Assert.NotNull(tasks);
        Assert.Contains(tasks, t => t.Title == "Prepare the client demo");
        Assert.Contains(tasks, t => t.Title == "Review open pull requests");
        Assert.Contains(tasks, t => t.Title == "Update the README");
    }

    [Fact]
    public async Task AddingATask_WithDueDate_PersistsAndReturnsIt()
    {
        var dueDate = new DateOnly(2026, 12, 15);
        var response = await _client.PostAsJsonAsync("/api/tasks", new NewTask("Submit report", Priority.High, dueDate));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(created);
        Assert.Equal(dueDate, created!.DueDate);

        var fetched = await _client.GetFromJsonAsync<TaskItem>($"/api/tasks/{created.Id}");
        Assert.NotNull(fetched);
        Assert.Equal(dueDate, fetched!.DueDate);
    }

    [Fact]
    public async Task AddingATask_WithoutDueDate_SetsDueDateToNull()
    {
        var response = await _client.PostAsJsonAsync("/api/tasks", new NewTask("No deadline task", Priority.Low));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(created);
        Assert.Null(created!.DueDate);
    }

    [Fact]
    public async Task TogglingATask_WithDueDate_PreservesDueDate()
    {
        var dueDate = new DateOnly(2026, 11, 1);
        var created = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask("Overdue toggle", Priority.Medium, dueDate))).Content.ReadFromJsonAsync<TaskItem>();

        var toggled = await (await _client.PostAsync($"/api/tasks/{created!.Id}/toggle", null)).Content.ReadFromJsonAsync<TaskItem>();

        Assert.NotNull(toggled);
        Assert.True(toggled!.IsDone);
        Assert.Equal(dueDate, toggled.DueDate);
    }

    [Fact]
    public async Task AddingATask_WithJsonIsoDateString_DeserializesDueDate()
    {
        using var content = new StringContent("""{"title":"From page","priority":"Medium","dueDate":"2026-12-31"}""", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/tasks", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(created);
        Assert.Equal(new DateOnly(2026, 12, 31), created!.DueDate);

        var html = await _client.GetStringAsync("/");
        Assert.Contains("id=\"due-date\"", html);
        Assert.Contains("type=\"date\"", html);
    }
}
