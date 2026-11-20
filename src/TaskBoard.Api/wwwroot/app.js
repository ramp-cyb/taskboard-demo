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
  await fetch('/api/tasks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ title: title.value, priority: document.getElementById('priority').value })
  });
  title.value = '';
  load();
});

document.getElementById('search').addEventListener('input', load);

load();

// --- Dark mode toggle ---
(function() {
  const STORAGE_KEY = 'taskboard-theme';
  const toggle = document.getElementById('theme-toggle');
  if (!toggle) return;

  const mediaQuery = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  function getSavedTheme() {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return value === 'dark' || value === 'light' ? value : null;
    } catch {
      return null;
    }
  }

  function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    toggle.textContent = theme === 'dark' ? '☀️' : '🌙';
    toggle.setAttribute('aria-pressed', String(theme === 'dark'));
  }

  const saved = getSavedTheme();
  const initialTheme = saved || (mediaQuery && mediaQuery.matches ? 'dark' : 'light');
  applyTheme(initialTheme);

  if (mediaQuery && mediaQuery.addEventListener) {
    mediaQuery.addEventListener('change', function(event) {
      if (!getSavedTheme()) {
        applyTheme(event.matches ? 'dark' : 'light');
      }
    });
  }

  toggle.addEventListener('click', function() {
    const current = document.documentElement.getAttribute('data-theme');
    const next = current === 'dark' ? 'light' : 'dark';
    applyTheme(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Ignore storage errors (e.g. private browsing restrictions)
    }
  });
})();
