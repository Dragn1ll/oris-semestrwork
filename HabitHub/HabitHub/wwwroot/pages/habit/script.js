const habitTypeSelect = document.getElementById('habitType');
const physicalActivityTypeSelect = document.getElementById('physicalActivityType');
const physicalActivitySection = document.getElementById('physicalActivitySection');
const toggleFormBtn = document.getElementById('toggleFormBtn');
const habitForm = document.getElementById('habitForm');
const submitHabit = document.getElementById('submitHabit');
const habitList = document.getElementById('habitList');
const goalInput = document.getElementById('habitGoal');

const typeError = document.getElementById('typeError');
const subtypeError = document.getElementById('subtypeError');
const goalError = document.getElementById('goalError');

let habits = [];
let selectedProgressHabit = null;

const HabitTypeLabels = {
    1: "Физическая активность",
    2: "Здоровое питание",
    3: "Умственные привычки",
    4: "Продуктивность и организация",
    5: "Финансовые привычки",
    6: "Социальные привычки",
    7: "Духовные практики",
    8: "Гигиена и уход за собой",
    9: "Творческие привычки",
    10: "Экологические привычки",
    11: "Привычки сна",
    12: "Вредные привычки",
    13: "Другие привычки"
};

const PhysicalActivityLabels = {
    1: "Ходьба",
    2: "Бег",
    3: "Велоспорт",
    4: "Плавание",
    5: "Лыжи",
    6: "Сноуборд",
    7: "Йога",
    8: "Другая активность"
};

async function fetchWithRetry(url, options = {}, retry = true) {
    const token = localStorage.getItem('jwt');
    options.headers = {
        ...(options.headers || {}),
        'Authorization': `Bearer ${token}`
    };
    options.credentials = 'include';

    const response = await fetch(url, options);

    if (response.status === 401 && retry) {
        const refresh = await fetch('/api/auth/refresh', {
            method: 'POST',
            credentials: 'include'
        });

        if (!refresh.ok) {
            localStorage.removeItem('jwt');
            window.location.href = '/login';
            return null;
        }

        const data = await refresh.json();
        if (data.token) {
            localStorage.setItem('jwt', data.token);
        }

        return fetchWithRetry(url, options, false);
    }

    return response;
}

async function parseApiError(response) {
    try {
        const result = await response.json();
        const parsed = JSON.parse(result.message);
        console.error(`API Error ${parsed.status}: ${parsed.title} — ${parsed.detail}`);
    } catch (e) {
        console.error('Не удалось распарсить ошибку от API', e);
    }
}


function initTypeSelects() {
    for (let id in HabitTypeLabels) {
        const opt = document.createElement('option');
        opt.value = id;
        opt.textContent = HabitTypeLabels[id];
        habitTypeSelect.appendChild(opt);
    }
    for (let id in PhysicalActivityLabels) {
        const opt = document.createElement('option');
        opt.value = id;
        opt.textContent = PhysicalActivityLabels[id];
        physicalActivityTypeSelect.appendChild(opt);
    }
}
initTypeSelects();

habitTypeSelect.addEventListener('change', () => {
    const selectedTypeId = parseInt(habitTypeSelect.value);
    physicalActivitySection.classList.toggle('hidden', selectedTypeId !== 1);
});

toggleFormBtn.onclick = () => habitForm.classList.toggle('hidden');

submitHabit.onclick = async () => {
    typeError.textContent = subtypeError.textContent = goalError.textContent = '';
    const typeId = parseInt(habitTypeSelect.value);
    const subTypeId = parseInt(physicalActivityTypeSelect.value);
    const goal = goalInput.value.trim();

    let valid = true;
    if (!typeId) { typeError.textContent = 'Выберите тип'; valid = false; }
    if (typeId === 1 && !subTypeId) { subtypeError.textContent = 'Выберите подтип'; valid = false; }
    if (!goal) { goalError.textContent = 'Введите цель'; valid = false; }
    if (!valid) return;

    const payload = {
        type: typeId,
        physicalActivityType: typeId === 1 ? subTypeId : null,
        goal
    };

    try {
        const res = await fetchWithRetry('/api/habits/add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!res || !res.ok) {
            if (res) await parseApiError(res);
            alert('Ошибка при создании привычки');
            return;
        }

        await res.json();
        loadHabits();
        habitForm.reset();
        habitForm.classList.add('hidden');
    } catch (e) {
        console.error('Ошибка сети или запроса', e);
    }
};

async function loadHabits() {
    try {
        const res = await fetchWithRetry('/api/habits/get/all');
        if (!res || !res.ok) {
            if (res) await parseApiError(res);
            habitList.innerText = 'Ошибка загрузки';
            return;
        }
        habits = await res.json();
        renderHabits();
    } catch (e) {
        console.error('Network error', e);
        habitList.innerText = 'Ошибка загрузки';
    }
}

async function toggleActive(h) {
    const payload = { ...h, isActive: !h.isActive };
    try {
        const res = await fetchWithRetry('/api/habits/put', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        if (!res || !res.ok) {
            if (res) await parseApiError(res);
            return;
        }
        loadHabits();
    } catch (e) {
        console.error('Network error', e);
    }
}

async function deleteHabit(id) {
    try {
        const res = await fetchWithRetry(`/api/habits/delete/${id}`, { method: 'DELETE' });
        if (!res || !res.ok) {
            if (res) await parseApiError(res);
            return;
        }
        loadHabits();
    } catch (e) {
        console.error('Network error', e);
    }
}

async function addProgress(habitId, date, percent) {
    const payload = { habitId, date, percentageCompletion: parseFloat(percent) };
    try {
        const res = await fetchWithRetry('/api/habits/progress/add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });
        if (!res || !res.ok) {
            if (res) await parseApiError(res);
            return;
        }
        loadProgress(habitId);
    } catch (e) {
        console.error('Network error', e);
    }
}

async function loadProgress(habitId) {
    try {
        const res = await fetchWithRetry(`/api/habits/${habitId}/progress/get/all`);
        if (!res || !res.ok) {
            if (res) await parseApiError(res);
            return;
        }
        const all = await res.json();
        habits = habits.map(h => h.id === habitId ? { ...h, progress: all } : h);
        renderHabits();
    } catch (e) {
        console.error('Network error', e);
    }
}

function renderHabits() {
    habitList.innerHTML = '';
    habits.forEach(h => {
        const div = document.createElement('div');
        div.className = 'habit-item';

        const header = document.createElement('div');
        header.className = 'habit-header';
        header.textContent = `${h.goal} — ${HabitTypeLabels[h.type]}${h.type === 'PhysicalActivity' ? ` (${PhysicalActivityLabels[h.physicalActivityType]})` : ''}`;

        const actions = document.createElement('div');
        ['Прогресс', h.isActive ? 'Деактивировать' : 'Активировать', 'Удалить'].forEach(text => {
            const btn = document.createElement('button');
            btn.textContent = text;
            btn.onclick = async () => {
                if (text === 'Прогресс') {
                    selectedProgressHabit = selectedProgressHabit === h.id ? null : h.id;
                    if (selectedProgressHabit) loadProgress(h.id);
                    else renderHabits();
                }
                if (text === 'Деактивировать' || text === 'Активировать') toggleActive(h);
                if (text === 'Удалить') deleteHabit(h.id);
            };
            actions.appendChild(btn);
        });

        div.appendChild(header);
        div.appendChild(actions);

        if (selectedProgressHabit === h.id) {
            const sec = document.createElement('div');
            sec.className = 'progress-section';

            const dateInput = document.createElement('input');
            dateInput.type = 'date';
            dateInput.style.width = '35%';
            dateInput.style.padding = '12px';
            dateInput.style.fontSize = '16px';
            dateInput.style.marginRight = '15px';
            dateInput.style.marginBottom = '10px';
            dateInput.style.borderRadius = '4px';
            dateInput.style.border = '1px solid #444';
            dateInput.style.backgroundColor = '#2a2a2a';
            dateInput.style.color = '#fff';

            const percentInput = document.createElement('input');
            percentInput.type = 'number';
            percentInput.min = 0;
            percentInput.max = 100;
            percentInput.placeholder = '%';
            percentInput.style.width = '35%';
            percentInput.style.padding = '12px';
            percentInput.style.fontSize = '16px';
            percentInput.style.marginRight = '15px';
            percentInput.style.marginBottom = '10px';
            percentInput.style.borderRadius = '4px';
            percentInput.style.border = '1px solid #444';
            percentInput.style.backgroundColor = '#2a2a2a';
            percentInput.style.color = '#fff';

            const addBtn = document.createElement('button');
            addBtn.textContent = 'Добавить';
            addBtn.style.padding = '12px 18px';
            addBtn.style.fontSize = '15px';
            addBtn.onclick = () => {
                const date = dateInput.value;
                const percent = percentInput.value;

                if (!date || percent === '') {
                    alert('Введите дату и процент');
                    return;
                }

                const existing = (h.progress || []).find(p => p.date === date);

                if (existing) {
                    existing.percentageCompletion = parseFloat(percent);
                } else {
                    h.progress = [...(h.progress || []), {
                        date,
                        percentageCompletion: parseFloat(percent)
                    }];
                }

                addProgress(h.id, date, percent);
            };

            sec.append(dateInput, percentInput, addBtn);

            const ul = document.createElement('ul');
            ul.style.marginTop = '10px';
            ul.style.paddingLeft = '20px';

            (h.progress || []).forEach(p => {
                const li = document.createElement('li');
                li.textContent = `${p.date} — ${p.percentageCompletion}%`;
                ul.appendChild(li);
            });

            sec.appendChild(ul);
            div.appendChild(sec);
        }

        habitList.appendChild(div);
    });
}

loadHabits();