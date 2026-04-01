let connection = null;
let currentUser = null;
let currentDuel = null;

$(function() {
    console.log("Initializing SignalR...");
    
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/duelHub")
        .configureLogging(signalR.LogLevel.Information)
        .withAutomaticReconnect()
        .build();
    
    // ============ SIGNALR HANDLERS ============
    connection.on("UserRegistered", (user) => {
        console.log("UserRegistered:", user);
        currentUser = user;
        updateUserStats(user);
        showNotification(`Добро пожаловать, ${user.username}!`, "success");
    });
    
    connection.on("UpdateLeaderboard", (users) => {
        console.log("UpdateLeaderboard:", users);
        let html = '<table class="table table-dark table-striped">';
        html += '<thead class="bg-danger"><tr><th>#</th><th>Игрок</th><th>⭐ Очки</th><th>🏆 Победы</th><th>💀 Поражения</th></tr></thead><tbody>';
        users.forEach((u, idx) => {
            html += `<tr>
                        <td class="fw-bold">${idx + 1}</td>
                        <td>${escapeHtml(u.username)}</td>
                        <td class="text-danger fw-bold">${u.score}</td>
                        <td>${u.wins}</td>
                        <td>${u.losses}</td>
                      </tr>`;
        });
        html += '</tbody></table>';
        $("#leaderboardTable").html(html);
    });
    
    connection.on("ReceiveChatMessage", (msg) => {
        console.log("ReceiveChatMessage:", msg);
        $("#messagesList").append(`<div class="mb-1"><b class="text-danger">${escapeHtml(msg.user)}</b> [${msg.time}]: ${escapeHtml(msg.text)}</div>`);
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("SystemMessage", (msg) => {
        console.log("SystemMessage:", msg);
        $("#messagesList").append(`<div class="mb-1 text-info"><i class="fas fa-info-circle"></i> ${escapeHtml(msg)}</div>`);
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("QuestResult", (res) => {
        console.log("QuestResult:", res);
        showNotification(res.message, res.success ? "success" : "error");
        if(res.success && res.newScore !== undefined && currentUser) {
            currentUser.score = res.newScore;
            updateUserStats(currentUser);
        }
    });
    
    connection.on("QueueJoined", (msg) => {
        console.log("QueueJoined:", msg);
        showNotification(msg, "info");
    });
    
    connection.on("DuelStarted", (data) => {
        console.log("DuelStarted:", data);
        currentDuel = data;
        showDuelModal(data);
    });
    
    connection.on("DuelResult", (res) => {
        console.log("DuelResult:", res);
        if(res.success && res.newScore !== undefined && currentUser) {
            currentUser.score = res.newScore;
            updateUserStats(currentUser);
        }
        showNotification(res.message, res.success ? "success" : "error");
        $("#duelModal").modal("hide");
        currentDuel = null;
    });
    
    connection.on("DuelTimeout", (msg) => {
        console.log("DuelTimeout:", msg);
        showNotification(msg, "error");
        $("#duelModal").modal("hide");
        currentDuel = null;
    });
    
    // ============ START CONNECTION ============
    connection.start()
        .then(() => {
            console.log("SignalR connected successfully!");
            // Проверяем авторизацию
            $.get("/api/Auth/check", function(data) {
                if (data.authenticated) {
                    console.log("User authenticated:", data.username);
                    showLoggedInUI(data.username, data.score);
                    connection.invoke("RegisterUser", data.username).catch(err => console.error("RegisterUser error:", err));
                } else {
                    console.log("User not authenticated");
                    showLoggedOutUI();
                }
            });
        })
        .catch(err => {
            console.error("SignalR connection error:", err);
            showNotification("Ошибка подключения к чату. Обновите страницу.", "error");
        });
    
    // ============ CHAT HANDLERS ============
    $("#sendChatBtn").click(function() {
        let msg = $("#chatInput").val();
        if(msg && connection && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("SendChatMessage", msg).catch(err => console.error("SendChatMessage error:", err));
            $("#chatInput").val("");
        } else if(!connection || connection.state !== signalR.HubConnectionState.Connected) {
            showNotification("Чат не подключен. Обновите страницу.", "error");
        }
    });
    
    $("#chatInput").keypress(function(e) {
        if(e.which == 13) {
            $("#sendChatBtn").click();
        }
    });
    
    // ============ DUEL BUTTON ============
    $("#duelQueueBtn").click(function() {
        if(connection && connection.state === signalR.HubConnectionState.Connected && currentUser) {
            connection.invoke("JoinDuelQueue").catch(err => console.error("JoinDuelQueue error:", err));
        } else {
            showNotification("Сначала войдите в систему", "error");
        }
    });
    
    window.connection = connection;
});

// ============ UI FUNCTIONS ============
function updateUserStats(user) {
    if($("#userStats").length) {
        $("#userStats").html(`
            <div class="text-start">
                <p><i class="fas fa-user text-danger"></i> <strong>Ник:</strong> ${escapeHtml(user.username)}</p>
                <p><i class="fas fa-star text-danger"></i> <strong>Очки:</strong> ${user.score}</p>
                <p><i class="fas fa-trophy text-danger"></i> <strong>Победы:</strong> ${user.wins}</p>
                <p><i class="fas fa-skull text-danger"></i> <strong>Поражения:</strong> ${user.losses}</p>
                <p><i class="fas fa-check-circle text-danger"></i> <strong>Квестов пройдено:</strong> ${user.completedQuests.length}</p>
            </div>
        `);
    }
}

function showDuelModal(data) {
    let modalHtml = `
        <div class="modal fade" id="duelModal" tabindex="-1">
            <div class="modal-dialog modal-lg">
                <div class="modal-content bg-dark text-white border-danger">
                    <div class="modal-header border-danger">
                        <h5 class="modal-title">⚔️ ДУЭЛЬ С ${escapeHtml(data.opponent)}</h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="alert alert-danger text-center">
                            <h3>⏱️ <span id="duelTimer">60</span> секунд</h3>
                        </div>
                        <div class="card bg-black border-danger mb-3">
                            <div class="card-header bg-danger">ЗАДАНИЕ</div>
                            <div class="card-body">
                                <p class="text-info">${escapeHtml(data.task)}</p>
                            </div>
                        </div>
                        <textarea id="duelSolution" class="form-control bg-black text-white border-danger" rows="6" placeholder="Напиши решение..."></textarea>
                        <button id="submitDuelBtn" class="btn btn-danger w-100 mt-3">⚔️ ОТПРАВИТЬ РЕШЕНИЕ</button>
                    </div>
                </div>
            </div>
        </div>
    `;
    
    $("body").append(modalHtml);
    $("#duelModal").modal("show");
    
    let timeLeft = 60;
    let timer = setInterval(() => {
        timeLeft--;
        $("#duelTimer").text(timeLeft);
        if(timeLeft <= 0) {
            clearInterval(timer);
        }
    }, 1000);
    
    $("#submitDuelBtn").click(function() {
        let solution = $("#duelSolution").val();
        if(solution && window.connection && data.duelId) {
            window.connection.invoke("SubmitDuelSolution", solution, data.duelId).catch(err => console.error("SubmitDuelSolution error:", err));
            clearInterval(timer);
        }
    });
    
    $("#duelModal").on("hidden.bs.modal", function() {
        $(this).remove();
        clearInterval(timer);
    });
}

function showNotification(message, type) {
    let notification = $(`<div class="alert alert-${type === 'success' ? 'success' : type === 'error' ? 'danger' : 'info'} alert-dismissible fade show" style="position: fixed; top: 80px; right: 20px; z-index: 99999; min-width: 300px;" role="alert">
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>`);
    
    $("body").append(notification);
    setTimeout(() => {
        notification.alert('close');
    }, 5000);
}

function escapeHtml(str) {
    if(!str) return "";
    return str.replace(/[&<>]/g, function(m) {
        if(m === '&') return '&amp;';
        if(m === '<') return '&lt;';
        if(m === '>') return '&gt;';
        return m;
    });
}

// ============ AUTH UI ============
function showLoggedInUI(username, score) {
    $("#userInfo").removeClass("d-none");
    $("#showAuthBtn").addClass("d-none");
    $("#userNameDisplay").text(username);
    $("#userScoreDisplay").text(`⭐ ${score}`);
}

function showLoggedOutUI() {
    $("#userInfo").addClass("d-none");
    $("#showAuthBtn").removeClass("d-none");
}

window.showLoggedInUI = showLoggedInUI;
window.showLoggedOutUI = showLoggedOutUI;
window.submitQuest = function(questId) {
    let code = $(`#code_${questId}`).val();
    if(code && window.connection && window.connection.state === signalR.HubConnectionState.Connected) {
        window.connection.invoke("SubmitQuestSolution", code, questId).catch(err => console.error("SubmitQuestSolution error:", err));
    } else if(!code) {
        alert("Напиши решение!");
    } else {
        alert("Чат не подключен. Обновите страницу.");
    }
};