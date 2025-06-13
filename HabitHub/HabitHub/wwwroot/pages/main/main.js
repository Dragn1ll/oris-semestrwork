document.getElementById('refresh-btn').onclick = () => renderPosts();
const currentUserId = localStorage.getItem('userId');

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
        //const parsed = JSON.parse(result.message);
        //console.error(`API Error ${parsed.status}: ${parsed.title} — ${parsed.detail}`);
        console.error(result.message);
    } catch (e) {
        console.error('Не удалось распарсить ошибку от API', e);
    }
}

async function renderPosts() {
    const container = document.getElementById('post-feed');
    container.innerHTML = '';

    let posts;
    try {
        const res = await fetchWithRetry('http://localhost:5000/api/posts/get/all');
        if (!res.ok) {
            await parseApiError(res);
            return;
        }
        posts = await res.json();
    } catch (e) {
        console.error('Ошибка загрузки постов', e);
        return;
    }

    for (const post of posts) {
        const postDiv = document.createElement('div');
        postDiv.className = 'post-item';

        let user, habit;
        try {
            const [userRes, habitRes] = await Promise.all([
                fetchWithRetry(`http://localhost:5000/api/users/get/${post.userId}`),
                fetchWithRetry(`http://localhost:5000/api/habits/get/${post.habitId}`)
            ]);

            if (!userRes.ok || !habitRes.ok) {
                if (!userRes.ok) await parseApiError(userRes);
                if (!habitRes.ok) await parseApiError(habitRes);
                continue;
            }

            user = await userRes.json();
            habit = await habitRes.json();
        } catch (e) {
            console.error('Ошибка загрузки пользователя или привычки', e);
            continue;
        }

        const userName = `${user.surname} ${user.name}`;
        const habitName = habit.goal;

        const header = document.createElement('div');
        header.className = 'post-header';
        header.textContent = `${userName} • ${habitName} • ${new Date(post.dateTime).toLocaleString()}`;

        const text = document.createElement('div');
        text.className = 'post-text';
        text.textContent = post.text;

        const likeBtn = document.createElement('button');
        likeBtn.textContent = `${post.didUserLiked ? '❤️' : '🤍'} ${post.likesCount}`;
        likeBtn.onclick = async () => {
            try {
                const method = post.didUserLiked ? 'DELETE' : 'POST';
                const res = await fetchWithRetry(`http://localhost:5000/api/posts/${post.didUserLiked ? 'delete' : 'add'}/${post.id}/like`, {
                    method
                });

                if (!res.ok) {
                    await parseApiError(res);
                    return;
                }

                post.didUserLiked = !post.didUserLiked;
                post.likesCount += post.didUserLiked ? 1 : -1;
                likeBtn.textContent = `${post.didUserLiked ? '❤️' : '🤍'} ${post.likesCount}`;
            } catch (e) {
                console.error('Ошибка при работе с лайком', e);
            }
        };

        const commentToggleBtn = document.createElement('button');
        commentToggleBtn.textContent = 'Комментарии';
        let commentsVisible = false;
        const commentSection = document.createElement('div');

        commentToggleBtn.onclick = async () => {
            commentsVisible = !commentsVisible;
            commentSection.innerHTML = '';

            if (commentsVisible) {
                try {
                    const res = await fetchWithRetry(`http://localhost:5000/api/posts/get/${post.id}/comments`);
                    if (!res.ok) {
                        await parseApiError(res);
                        return;
                    }

                    const comments = await res.json();

                    // Получаем пользователей всех комментариев, чтобы показать ФИ
                    const userIds = [...new Set(comments.map(c => c.userId))];
                    const usersMap = {};
                    await Promise.all(userIds.map(async id => {
                        const userRes = await fetchWithRetry(`http://localhost:5000/api/users/get/${id}`);
                        if (userRes.ok) {
                            usersMap[id] = await userRes.json();
                        }
                    }));

                    const ul = document.createElement('ul');
                    for (const c of comments) {
                        const user = usersMap[c.userId];
                        const userName = user ? `${user.surname} ${user.name}` : 'Неизвестный пользователь';

                        const li = document.createElement('li');
                        li.textContent = `${new Date(c.dateTime).toLocaleString()} — ${userName}: ${c.text}`;

                        if (c.userId === currentUserId) {
                            const delBtn = document.createElement('button');
                            delBtn.textContent = 'Удалить';
                            delBtn.className = 'delete-comment';
                            delBtn.onclick = async () => {
                                try {
                                    const res = await fetchWithRetry(`http://localhost:5000/api/posts/delete/comments/${c.id}`, {
                                        method: 'DELETE'
                                    });
                                    if (!res.ok) {
                                        await parseApiError(res);
                                        return;
                                    }
                                    renderPosts();
                                } catch (e) {
                                    console.error('Ошибка удаления комментария', e);
                                }
                            };
                            li.appendChild(delBtn);
                        }

                        ul.appendChild(li);
                    }
                    commentSection.appendChild(ul);

                    // Создаем контейнер для input и кнопки
                    const commentInputDiv = document.createElement('div');
                    commentInputDiv.className = 'comment-section';

                    const input = document.createElement('input');
                    input.type = 'text';
                    input.placeholder = 'Оставить комментарий...';

                    const addBtn = document.createElement('button');
                    addBtn.textContent = 'Отправить';
                    addBtn.onclick = async () => {
                        try {
                            const res = await fetchWithRetry(`http://localhost:5000/api/posts/add/${post.id}/comment`, {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify({ comment: input.value })
                            });

                            if (!res.ok) {
                                await parseApiError(res);
                                return;
                            }

                            renderPosts();
                        } catch (e) {
                            console.error('Ошибка добавления комментария', e);
                        }
                    };

                    commentInputDiv.append(input, addBtn);
                    commentSection.appendChild(commentInputDiv);

                } catch (e) {
                    console.error('Ошибка загрузки комментариев', e);
                    commentSection.textContent = 'Ошибка загрузки комментариев';
                }
            }
        };
        const mediaDiv = document.createElement('div');
        mediaDiv.className = 'post-media';
        for (const url of post.mediaFilesUrl) {
            const img = document.createElement('img');
            img.src = url;
            mediaDiv.appendChild(img);
        }

        postDiv.append(header, text, mediaDiv, likeBtn, commentToggleBtn, commentSection);
        container.appendChild(postDiv);
    }
}

renderPosts();
