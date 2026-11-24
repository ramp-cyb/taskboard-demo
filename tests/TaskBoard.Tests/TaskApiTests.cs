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
    public async Task UpdatingATask_ChangesTitleAndPriority()
    {
        var created = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask("Old title", Priority.Low))).Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.PutAsJsonAsync($"/api/tasks/{created!.Id}", new UpdateTask("New title", Priority.High));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TaskItem>();
        Assert.Equal("New title", updated!.Title);
        Assert.Equal(Priority.High, updated.Priority);
    }

    [Fact]
    public async Task UpdatingATask_WithBlankTitle_ReturnsBadRequest()
    {
        var created = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask("Keep me"))).Content.ReadFromJsonAsync<TaskItem>();

        var response = await _client.PutAsJsonAsync($"/api/tasks/{created!.Id}", new UpdateTask("   ", Priority.Medium));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatingAMissingTask_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync("/api/tasks/99999", new UpdateTask("Does not matter", Priority.Medium));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
    public async Task ExportTask_ReturnsCSVWithHeader()
    {
        var response = await _client.GetAsync("/api/tasks/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("tasks.csv", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        var body = await response.Content.ReadAsStringAsync();
        var firstLine = body.Split(["\r\n", "\n"], StringSplitOptions.None)[0];

        Assert.Equal("Id,Title,Priority,Done,Created", firstLine);
        Assert.Contains("Prepare the client demo", body);
    }

    [Theory]
    [InlineData("Hello, World", "\"Hello, World\"")]
    [InlineData("Say \"Hello\"", "\"Say \"\"Hello\"\"\"")]
    [InlineData("Line1\nLine2", "\"Line1\nLine2\"")]
    public async Task ExportTask_EscapesSpecialCharacters(string title, string expectedCsvField)
    {
        var createResponse = await _client.PostAsJsonAsync("/api/tasks", new NewTask(title, Priority.Medium));
        var created = await createResponse.Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(created);

        try
        {
            var response = await _client.GetAsync("/api/tasks/export");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Contains($"{created.Id},{expectedCsvField},Medium,False,", body);
        }
        finally
        {
            await _client.DeleteAsync($"/api/tasks/{created.Id}");
        }
    }
}
