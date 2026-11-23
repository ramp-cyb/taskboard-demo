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

    [Theory]
    [InlineData("priority")]
    [InlineData("Priority")]
    public async Task SortingByPriority_ReturnsHighBeforeMediumBeforeLow(string sortParam)
    {
        var prefix = $"sort-priority-{Guid.NewGuid():N}";
        var high = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask($"{prefix}-high", Priority.High))).Content.ReadFromJsonAsync<TaskItem>();
        var low = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask($"{prefix}-low", Priority.Low))).Content.ReadFromJsonAsync<TaskItem>();
        var medium = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask($"{prefix}-medium", Priority.Medium))).Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(high);
        Assert.NotNull(low);
        Assert.NotNull(medium);

        try
        {
            var allTasks = await _client.GetFromJsonAsync<List<TaskItem>>($"/api/tasks?sort={sortParam}");
            Assert.NotNull(allTasks);

            for (var i = 1; i < allTasks.Count; i++)
            {
                Assert.True(allTasks[i - 1].Priority >= allTasks[i].Priority,
                    $"Expected non-increasing priority order, but {allTasks[i - 1].Priority} preceded {allTasks[i].Priority}.");
            }

            var createdInAll = allTasks
                .Where(t => t.Title.StartsWith(prefix, StringComparison.Ordinal))
                .Select(t => t.Priority)
                .ToList();
            Assert.Equal([Priority.High, Priority.Medium, Priority.Low], createdInAll);

            var filteredTasks = await _client.GetFromJsonAsync<List<TaskItem>>($"/api/tasks?search={prefix}&sort={sortParam}");
            Assert.NotNull(filteredTasks);
            Assert.Equal([high.Id, medium.Id, low.Id], filteredTasks.Select(t => t.Id).ToList());
        }
        finally
        {
            await _client.DeleteAsync($"/api/tasks/{high.Id}");
            await _client.DeleteAsync($"/api/tasks/{low.Id}");
            await _client.DeleteAsync($"/api/tasks/{medium.Id}");
        }
    }

    [Fact]
    public async Task DefaultListing_ReturnsNewestFirst()
    {
        var prefix = $"sort-newest-{Guid.NewGuid():N}";
        var first = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask($"{prefix}-first", Priority.High))).Content.ReadFromJsonAsync<TaskItem>();
        var second = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask($"{prefix}-second", Priority.Low))).Content.ReadFromJsonAsync<TaskItem>();
        var third = await (await _client.PostAsJsonAsync("/api/tasks", new NewTask($"{prefix}-third", Priority.Medium))).Content.ReadFromJsonAsync<TaskItem>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);

        try
        {
            var tasks = await _client.GetFromJsonAsync<List<TaskItem>>($"/api/tasks?search={prefix}");
            Assert.NotNull(tasks);
            Assert.Equal([third.Id, second.Id, first.Id], tasks.Select(t => t.Id).ToList());
        }
        finally
        {
            await _client.DeleteAsync($"/api/tasks/{first.Id}");
            await _client.DeleteAsync($"/api/tasks/{second.Id}");
            await _client.DeleteAsync($"/api/tasks/{third.Id}");
        }
    }
}
