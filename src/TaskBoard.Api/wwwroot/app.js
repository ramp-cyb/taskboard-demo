const list = document.getElementById('tasks');
const remaining = document.getElementById('remaining');

async function load() {
  const search = document.getElementById('search').value;
  const response = await fetch('/api/tasks?search=' + encodeURIComponent(search));
  const tasks = await response.json();
  render(tasks);
  remaining.textContent = tasks.filter(t => !t.isDone).length + ' remaining';
}

function render(tasks) {
  list.innerHTML = '';
  for (const task of tasks) {
    const item = document.createElement('li');
    item.className = task.isDone ? 'done' : '';

    const box = document.createElement('input');
    box.type = 'checkbox';
    box.checked = task.isDone;
    box.addEventListener('change', async () => {
      await fetch(`/api/tasks/${task.id}/toggle`, { method: 'POST' });
      item.classList.toggle('done');
    });

    const title = document.createElement('span');
    title.className = 'title';
    title.textContent = task.title;

    const priority = document.createElement('span');
    priority.className = 'priority ' + task.priority.toLowerCase();
    priority.textContent = task.priority;

    const remove = document.createElement('button');
    remove.className = 'delete';
    remove.textContent = '✕';
    remove.title = 'Delete';
    remove.addEventListener('click', async () => {
      await fetch(`/api/tasks/${task.id}`, { method: 'DELETE' });
      load();
    });

    item.append(box, title, priority, remove);
    list.append(item);
  }
}

document.getElementById('add-form').addEventListener('submit', async event => {
  event.preventDefault();
  const title = document.getElementById('title');
  const response = await fetch('/api/tasks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title: title.value, priority: document.getElementById('priority').value })
  });
  if (response.ok) {
    title.value = '';
    load();
  } else {
    const error = await response.json();
    alert(error);
  }
});

document.getElementById('search').addEventListener('input', load);

load();
