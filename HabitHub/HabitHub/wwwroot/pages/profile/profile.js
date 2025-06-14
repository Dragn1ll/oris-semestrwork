const currentUserId = localStorage.getItem('userId');
const urlParts = window.location.pathname.split('/');
const profileUserId = urlParts[urlParts.length - 1];
const container = document.getElementById('post-feed');
const actionsDiv = document.getElementById('profile-actions');
const title = document.getElementById('profile-title');

const client = google.accounts.oauth2.initCodeClient({
    client_id: '319850806378-8oith89dlmvcvths4mk276q9si92rmcl.apps.googleusercontent.com',
    scope: 'https://www.googleapis.com/auth/fitness.activity.read',
    ux_mode: 'popup',
    callback: async (response) => {
        // Отправка кода на ваш сервер
        const res = await fetchWithRetry('/api/google/token/add', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ code: response.code }),
        });

        if (res.ok) {
            // Успех - обновляем статус кнопки
            btnGoogle.textContent = 'Google подключен ✅';
            btnGoogle.disabled = true;
            btnGoogle.style.backgroundColor = '#4CAF50';
        } else {
            // Обработка ошибки
            console.error('Ошибка подключения Google');
            await parseApiError(res);
        }
    },
});

async function fetchWithRetry(url, options = {}, retry = true) {
    const token = localStorage.getItem('jwt');
    options.headers = { ...(options.headers||{}), 'Authorization': `Bearer ${token}` };
    options.credentials = 'include';

    const res = await fetch(url, options);
    if (res.status === 401 && retry) {
        const r = await fetch('/api/auth/refresh', { method:'POST', credentials:'include' });
        if (!r.ok) { localStorage.removeItem('jwt'); window.location.href = '/login'; return null; }
        const data = await r.json();
        if (data.token) localStorage.setItem('jwt', data.token);
        return fetchWithRetry(url, options, false);
    }
    return res;
}
async function parseApiError(res) {
    try { const r = await res.json(); console.error('API error: ', JSON.parse(r.message)); }
    catch(e){ console.error('Не удалось распарсить API-ошибку', e); }
}

async function loadProfile() {
    let me = profileUserId === currentUserId;
    title.textContent = me ? 'Мой профиль' : 'Профиль пользователя';

    // Формируем кнопки действий
    if (me) {
        actionsDiv.innerHTML = `
      <button id="btn-add-post">Добавить пост</button>
      <button id="btn-edit-profile">Редактировать профиль</button>
      <button id="btn-delete-profile">Удалить профиль</button>
      <button id="btn-connect-google">Подключить Google</button>
      <button id="btn-connect-telegram">Включить уведомления</button>`;
        document.getElementById('btn-add-post').onclick = () => alert('Добавление поста пока не реализовано');
        document.getElementById('btn-edit-profile').onclick = () => alert('Редактирование профиля');
        document.getElementById('btn-delete-profile').onclick = () => alert('Профиль удалён');
        // Проверка подключения Google
        const btnGoogle = document.getElementById('btn-connect-google');
        btnGoogle.textContent = 'Проверка Google...';
        btnGoogle.disabled = true;

        let googleRes = await fetchWithRetry('/api/google/token/contains');
        if (googleRes.ok) {
            const isConnected = await googleRes.json();
            if (isConnected) {
                btnGoogle.textContent = 'Google подключен ✅';
                btnGoogle.disabled = true;
                btnGoogle.style.backgroundColor = '#4CAF50'; // зеленая кнопка
            } else {
                btnGoogle.textContent = 'Подключить Google';
                btnGoogle.disabled = false;
                btnGoogle.onclick = () => client.requestCode();
            }
        } else {
            btnGoogle.textContent = 'Ошибка подключения';
            btnGoogle.disabled = true;
            await parseApiError(googleRes);
        }
        document.getElementById('btn-connect-telegram').onclick = () => alert(`Подключение бота: id=${currentUserId}`);
    } else {
        actionsDiv.innerHTML = `<button id="btn-send-msg">Отправить сообщение</button>`;
        document.getElementById('btn-send-msg').onclick = () => alert('Функция пока не реализована');
    }

    // Загружаем и отображаем его посты
    await renderPosts();
}

async function renderPosts() {
    container.innerHTML = '';
    let res = await fetchWithRetry(`/api/posts/get/user/${profileUserId}`);
    if(!res?.ok){ await parseApiError(res); return }
    const posts = await res.json();

    for (let post of posts) {
        const div = document.createElement('div');
        div.className = 'post-item';

        let [userRes, habitRes] = await Promise.all([
            fetchWithRetry(`/api/users/get/${post.userId}`),
            fetchWithRetry(`/api/habits/get/${post.habitId}`),
        ]);
        if (!userRes.ok || !habitRes.ok) { if(!userRes.ok) await parseApiError(userRes); if(!habitRes.ok) await parseApiError(habitRes); continue; }

        const u = await userRes.json(), hab = await habitRes.json();
        const header = document.createElement('div');
        header.className = 'post-header';
        header.textContent = `${u.surname} ${u.name} • ${hab.goal} • ${new Date(post.dateTime).toLocaleString()}`;

        const text = document.createElement('div'); text.className='post-text'; text.textContent=post.text;

        const mediaDiv = document.createElement('div'); mediaDiv.className='post-media';
        post.mediaFilesUrl.forEach(url=>{
            const el = renderMedia(url); mediaDiv.appendChild(el);
        });

        const likeBtn = document.createElement('button');
        likeBtn.textContent = `${post.didUserLiked ? '❤️' : '🤍'} ${post.likesCount}`;
        likeBtn.onclick = async ()=>{
            const method = post.didUserLiked ? 'DELETE' : 'POST';
            const res = await fetchWithRetry(`/api/posts/${post.didUserLiked?'delete':'add'}/${post.id}/like`, { method });
            if (!res.ok) { await parseApiError(res); return; }
            post.didUserLiked=!post.didUserLiked;
            post.likesCount += post.didUserLiked ? 1 : -1;
            likeBtn.textContent = `${post.didUserLiked ? '❤️' : '🤍'} ${post.likesCount}`;
        };

        const commentsBtn = document.createElement('button');
        commentsBtn.textContent = `💬 ${post.commentsCount}`;
        const commentsSection = document.createElement('div');
        let commentsVisible = false;
        commentsBtn.onclick = async ()=>{
            commentsVisible = !commentsVisible;
            commentsSection.innerHTML = '';
            if (!commentsVisible) return;
            let cRes = await fetchWithRetry(`/api/posts/get/${post.id}/comments`);
            if (!cRes.ok) { await parseApiError(cRes); return; }
            const cs = await cRes.json();
            // load users names
            const uids = [...new Set(cs.map(c=>c.userId))];
            const map = {};
            await Promise.all(uids.map(async id=>{
                const r = await fetchWithRetry(`/api/users/get/${id}`);
                if(r.ok) map[id] = await r.json();
            }));
            const ul = document.createElement('ul');
            cs.forEach(c=>{
                const li = document.createElement('li');
                const un = map[c.userId];
                li.textContent = `${new Date(c.dateTime).toLocaleString()} — ${un ? `${un.surname} ${un.name}` : 'Пользователь'}: ${c.text}`;
                if(c.userId === currentUserId){
                    const db = document.createElement('button');
                    db.className='delete-comment';
                    db.textContent='Удалить';
                    db.onclick=async ()=>{
                        const dr = await fetchWithRetry(`/api/posts/delete/comment/${c.id}`,{method:'DELETE'});
                        if(!dr.ok){ await parseApiError(dr); return; }
                        await renderPosts();
                    }
                    li.appendChild(db);
                }
                ul.appendChild(li);
            });
            commentsSection.appendChild(ul);

            const inputDiv = document.createElement('div');
            inputDiv.className = 'comment-section';
            const input = document.createElement('input');
            input.type='text'; input.placeholder='Оставить комментарий...';
            const send = document.createElement('button');
            send.textContent='Отправить';
            send.onclick=async ()=>{
                const br = await fetchWithRetry(`/api/posts/add/${post.id}/comment`, {
                    method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({comment: input.value})
                });
                if(!br.ok){ await parseApiError(br); return; }
                await renderPosts();
            };
            inputDiv.append(input, send);
            commentsSection.appendChild(inputDiv);
        };

        div.append(header, text, mediaDiv, likeBtn, commentsBtn, commentsSection);
        container.appendChild(div);
    }
}

function renderMedia(url){
    const ext = url.split('.').pop().toLowerCase();
    if(['jpg','jpeg','png','gif','webp'].includes(ext)){
        const img = document.createElement('img'); img.src=url; return img;
    }
    if(['mp4','webm'].includes(ext)){
        const v = document.createElement('video'); v.src=url; v.controls=true; return v;
    }
    if(['mp3','wav','ogg'].includes(ext)){
        const a = document.createElement('audio'); a.src=url; a.controls=true; return a;
    }
    const a = document.createElement('a');
    a.href=url; a.target='_blank'; a.textContent='Файл'; a.style.color='#f0d000';
    return a;
}

loadProfile();