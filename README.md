# TaskBoard

A small task tracker: an ASP.NET Core 8 API with a plain HTML page, and xUnit tests.

## Run

```bash
dotnet run --project src/TaskBoard.Api
```

Then open the address it prints (for example `http://localhost:5000`).

## Build and test

```bash
dotnet build
dotnet test
```

## API

| Method | Path | What it does |
|---|---|---|
| GET | `/api/tasks?search=` | Lists tasks, optionally filtered by title |
| GET | `/api/tasks/{id}` | One task |
| POST | `/api/tasks` | Adds a task: `{ "title": "...", "priority": "Low" \| "Medium" \| "High" }` |
| POST | `/api/tasks/{id}/toggle` | Marks a task done or not done |
| DELETE | `/api/tasks/{id}` | Deletes a task |
| GET | `/health` | Health check: returns `{ "status": "ok" }` |

Tasks are kept in memory and are lost when the app stops.

## Layout

- `src/TaskBoard.Api` – the API (`Program.cs`, `TaskStore.cs`) and the page (`wwwroot/`)
- `tests/TaskBoard.Tests` – API tests that start the app in memory
