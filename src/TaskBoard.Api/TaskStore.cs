using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace TaskBoard.Api;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Priority { Low, Medium, High }

public sealed record TaskItem(int Id, string Title, Priority Priority, bool IsDone, DateTime CreatedAt);

public sealed record NewTask(string Title, Priority Priority = Priority.Medium);

/// <summary>Keeps tasks in memory; they are lost when the app stops.</summary>
public sealed class TaskStore
{
    private readonly object _lock = new();
    private readonly List<TaskItem> _tasks = new();
    private int _nextId = 1;

    public TaskStore()
    {
        Add(new NewTask("Prepare the client demo", Priority.High));
        Add(new NewTask("Review open pull requests"));
        Add(new NewTask("Update the README", Priority.Low));
    }

    public IReadOnlyList<TaskItem> List(string? search, string? status = null)
    {
        lock (_lock)
        {
            IEnumerable<TaskItem> result = _tasks;
            if (!string.IsNullOrWhiteSpace(search))
            {
                result = result.Where(t => t.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
            }
            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(t => !t.IsDone);
            }
            else if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(t => t.IsDone);
            }
            return result.OrderBy(t => t.Id).ToList();
        }
    }

    public TaskItem? Get(int id)
    {
        lock (_lock)
        {
            return _tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public TaskItem Add(NewTask request)
    {
        lock (_lock)
        {
            var task = new TaskItem(_nextId++, request.Title.Trim(), request.Priority, false, DateTime.UtcNow);
            _tasks.Add(task);
            return task;
        }
    }

    public TaskItem? Toggle(int id)
    {
        lock (_lock)
        {
            var index = _tasks.FindIndex(t => t.Id == id);
            if (index < 0) return null;
            _tasks[index] = _tasks[index] with { IsDone = !_tasks[index].IsDone };
            return _tasks[index];
        }
    }

    public bool Delete(int id)
    {
        lock (_lock)
        {
            return _tasks.RemoveAll(t => t.Id == id) > 0;
        }
    }

    public string Export()
    {
        lock (_lock)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Title,Priority,Done,Created");
            foreach (var t in _tasks.OrderBy(t => t.Id))
            {
                sb.Append(t.Id);
                sb.Append(',');
                sb.Append(Encode(t.Title));
                sb.Append(',');
                sb.Append(t.Priority);
                sb.Append(',');
                sb.Append(t.IsDone);
                sb.Append(',');
                sb.AppendLine(t.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }

    private static string Encode(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        if (field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n'))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        return field;
    }
}
