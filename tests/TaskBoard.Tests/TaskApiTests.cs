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
    public async Task TaskStats_ReturnsCorrectCounts()
    {
        using var localFactory = new WebApplicationFactory<Program>();
        using var client = localFactory.CreateClient();

        var stats = await client.GetFromJsonAsync<TaskStats>("/api/stats");

        Assert.NotNull(stats);
        Assert.Equal(3, stats.Total);
        Assert.Equal(0, stats.Done);
        Assert.Equal(3, stats.Remaining);
        Assert.Equal(1, stats.Low);
        Assert.Equal(1, stats.Medium);
        Assert.Equal(1, stats.High);
    }

    [Fact]
    public async Task TaskStats_AfterMutations()
    {
        using var localFactory = new WebApplicationFactory<Program>();
        using var client = localFactory.CreateClient();

        // Add a new Low-priority task
        var added = await (await client.PostAsJsonAsync("/api/tasks", new NewTask("New task", Priority.Low))).Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(added);

        // Toggle the newly added task to done
        var toggled = await (await client.PostAsync($"/api/tasks/{added.Id}/toggle", null)).Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(toggled);
        Assert.True(toggled.IsDone);

        var stats = await client.GetFromJsonAsync<TaskStats>("/api/stats");

        Assert.NotNull(stats);
        Assert.Equal(4, stats.Total);
        Assert.Equal(1, stats.Done);
        Assert.Equal(3, stats.Remaining);
        Assert.Equal(2, stats.Low);
        Assert.Equal(1, stats.Medium);
        Assert.Equal(1, stats.High);
    }
}
