using System.Globalization;
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

    [Fact]
    public async Task ExportTasks_ReturnsCsvWithHeadersAndEscapedTitles()
    {
        var commaTask = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask("Task with, comma", Priority.High))).Content.ReadFromJsonAsync<TaskItem>();
        var quoteTask = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask("Task with \"quotes\" inside", Priority.Medium))).Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.GetAsync("/api/tasks/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("tasks.csv", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Id,Title,Priority,Done,Created", lines[0]);
        Assert.Contains($"{commaTask!.Id},\"Task with, comma\",High,False,{commaTask.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", lines);
        Assert.Contains($"{quoteTask!.Id},\"Task with \"\"quotes\"\" inside\",Medium,False,{quoteTask.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", lines);
    }
}
