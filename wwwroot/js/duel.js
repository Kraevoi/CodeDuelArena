let connection = null;
window.currentUser = null;

$(function() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/duelHub")
        .configureLogging(signalR.LogLevel.Information)
        .withAutomaticReconnect()
        .build();
    
    connection.on("UserRegistered", (user) => {
        window.currentUser = user;
        $("#userInfo").removeClass("d-none");
        $("#showAuthBtn").addClass("d-none");
        $("#userNameDisplay").text(user.username);
        $("#userScoreDisplay").text(`⭐ ${user.score}`);
        updateUserStats(user);
        //showNotification(`Добро пожаловать, ${user.username}!`, "success");
    });
    
    connection.on("UpdateLeaderboard", (users) => {
        let html = '<table class="table table-dark table-striped"><thead class="bg-danger"><tr><th>#</th><th>Игрок</th><th>⭐ Очки</th><th>🏆 Победы</th><th>💀 Поражения</th></tr></thead><tbody>';
        users.forEach((u, idx) => {
            html += `<tr><td class="fw-bold">${idx + 1}${u.username}${u.score}${u.wins}${u.losses}`;
        });
        html += '</tbody></table>';
        $("#leaderboardTable").html(html);
    });
    
    connection.on("ReceiveChatMessage", (msg) => {
        $("#messagesList").append(`<div style="margin-bottom: 5px;"><span style="color: #dc3545; font-weight: bold;">${escapeHtml(msg.user)}</span> <span style="color: #888;">[${msg.time}]</span>: <span style="color: #fff;">${escapeHtml(msg.text)}</span></div>`);
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("SystemMessage", (msg) => {
        $("#messagesList").append(`<div style="color: #17a2b8; margin-bottom: 5px;"><i class="fas fa-info-circle"></i> ${escapeHtml(msg)}</div>`);
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("QuestResult", (res) => {
        showNotification(res.message, res.success ? "success" : "error");
        if(res.success && res.newScore !== undefined && window.currentUser) {
            window.currentUser.score = res.newScore;
            updateUserStats(window.currentUser);
            $("#userScoreDisplay").text(`⭐ ${window.currentUser.score}`);
        }
    });
    
    connection.on("QueueJoined", (msg) => {
        showNotification(msg, "info");
    });
    
    connection.on("QueueError", (msg) => {
        showNotification(msg, "error");
    });
    
    connection.on("DuelStarted", (data) => {
        showDuelModal(data);
    });
    
    connection.on("DuelStatus", (msg) => {
        showNotification(msg, "info");
    });
    
    connection.on("DuelResult", (res) => {
        if(res.success && res.newScore !== undefined && window.currentUser) {
            window.currentUser.score = res.newScore;
            updateUserStats(window.currentUser);
            $("#userScoreDisplay").text(`⭐ ${window.currentUser.score}`);
        }
        showNotification(res.message, res.success ? "success" : "error");
        $("#duelModal").remove();
    });
    
    connection.on("DuelTimeout", (msg) => {
        showNotification(msg, "error");
        $("#duelModal").remove();
    });
    
    connection.start()
        .then(() => {
            console.log("SignalR connected");
            $.get("/Auth/CheckAuth", function(data) {
                if(data.authenticated) {
                    connection.invoke("RegisterUser", data.username);
                }
            });
        })
        .catch(err => console.error(err));
    
    $("#showAuthBtn").click(() => $("#authModal").modal("show"));
    
    $("#loginBtn").click(function() {
        let username = $("#loginUsername").val().trim();
        let password = $("#loginPassword").val();
        let remember = $("#loginRemember").is(":checked");
        
        $.ajax({
            url: "/Auth/Login",
            type: "POST",
            data: { username: username, password: password, rememberMe: remember },
            success: function(data) {
                if(data.success) {
                    $("#authModal").modal("hide");
                    location.reload();
                } else {
                    $("#authError").text(data.error).removeClass("d-none");
                }
            },
            error: function() {
                $("#authError").text("Ошибка соединения").removeClass("d-none");
            }
        });
    });
    
    $("#registerBtnModal").click(function() {
        let username = $("#regUsername").val().trim();
        let email = $("#regEmail").val().trim();
        let password = $("#regPassword").val();
        let remember = $("#regRemember").is(":checked");
        
        $.ajax({
            url: "/Auth/Register",
            type: "POST",
            data: { username: username, password: password, email: email, rememberMe: remember },
            success: function(data) {
                if(data.success) {
                    $("#authModal").modal("hide");
                    location.reload();
                } else {
                    $("#authError").text(data.error).removeClass("d-none");
                }
            },
            error: function() {
                $("#authError").text("Ошибка соединения").removeClass("d-none");
            }
        });
    });
    
    $("#logoutBtn").click(() => {
        $.post("/Auth/Logout", () => location.reload());
    });
    
    $("#sendChatBtn").click(() => {
        let msg = $("#chatInput").val();
        if(msg && connection) {
            connection.invoke("SendChatMessage", msg);
            $("#chatInput").val("");
        }
    });
    
    $("#chatInput").keypress((e) => {
        if(e.which == 13) $("#sendChatBtn").click();
    });
    
    $("#duelQueueBtn").click(() => {
        if(connection && window.currentUser) {
            connection.invoke("JoinDuelQueue");
        } else {
            showNotification("Сначала войдите в систему", "error");
        }
    });
    
    window.connection = connection;
});

function updateUserStats(user) {
    if($("#userStats").length && user) {
        $("#userStats").html(`
            <div class="text-start">
                <p><i class="fas fa-user text-danger"></i> <strong>Ник:</strong> ${escapeHtml(user.username)}</p>
                <p><i class="fas fa-star text-danger"></i> <strong>Очки:</strong> ${user.score}</p>
                <p><i class="fas fa-trophy text-danger"></i> <strong>Победы:</strong> ${user.wins}</p>
                <p><i class="fas fa-skull text-danger"></i> <strong>Поражения:</strong> ${user.losses}</p>
                <p><i class="fas fa-check-circle text-danger"></i> <strong>Квестов пройдено:</strong> ${user.completedQuests?.length || 0}</p>
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
                            <h3>⏱️ <span id="duelTimer">${data.timeLimit || 60}</span> секунд</h3>
                        </div>
                        <div class="card bg-black border-danger mb-3">
                            <div class="card-header bg-danger">ЗАДАНИЕ</div>
                            <div class="card-body">
                                <p class="text-info">${escapeHtml(data.taskDescription || data.task)}</p>
                                ${data.testCode ? `<pre class="bg-black text-warning p-2">${escapeHtml(data.testCode)}</pre>` : ''}
                                ${data.expectedOutput ? `<p><strong>Ожидаемый вывод:</strong> ${escapeHtml(data.expectedOutput)}</p>` : ''}
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
    
    let timeLeft = data.timeLimit || 60;
    let timer = setInterval(() => {
        timeLeft--;
        $("#duelTimer").text(timeLeft);
        if(timeLeft <= 0) clearInterval(timer);
    }, 1000);
    
    $("#submitDuelBtn").click(function() {
        let solution = $("#duelSolution").val();
        if(solution && window.connection && data.duelId) {
            window.connection.invoke("SubmitDuelSolution", solution, data.duelId);
            $("#submitDuelBtn").prop("disabled", true).text("Решение отправлено...");
            clearInterval(timer);
        }
    });
    
    $("#duelModal").on("hidden.bs.modal", () => {
        $("#duelModal").remove();
        clearInterval(timer);
    });
}

function showNotification(message, type) {
    let bgClass = type === "success" ? "success" : type === "error" ? "danger" : "info";
    let notification = $(`<div class="alert alert-${bgClass} alert-dismissible fade show" style="position: fixed; top: 80px; right: 20px; z-index: 99999; min-width: 300px;" role="alert">
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>`);
    $("body").append(notification);
    setTimeout(() => notification.alert('close'), 5000);
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

window.submitQuest = function(questId) {
    let code = $(`#code_${questId}`).val();
    if(code && window.connection) {
        window.connection.invoke("SubmitQuestSolution", code, questId);
    } else if(!code) {
        alert("Напиши решение!");
    }
};


window.applyTheme = function(theme) {
    var themes = {
        dark: {
            bg: "linear-gradient(135deg, #0a0a0a 0%, #1a0a0a 100%)",
            color: "#fff",
            cardBg: "#111",
            border: "#dc3545",
            btnBg: "#dc3545",
            inputBg: "#000"
        },
        light: {
            bg: "linear-gradient(135deg, #f5f5f5 0%, #e0e0e0 100%)",
            color: "#000",
            cardBg: "#fff",
            border: "#dc3545",
            btnBg: "#dc3545",
            inputBg: "#fff"
        },
        matrix: {
            bg: "#000",
            color: "#0f0",
            cardBg: "#0a0a0a",
            border: "#0f0",
            btnBg: "#0f0",
            inputBg: "#000"
        },
        cyber: {
            bg: "linear-gradient(135deg, #0a0a2a 0%, #1a0a3a 100%)",
            color: "#0ff",
            cardBg: "#0a0a2a",
            border: "#0ff",
            btnBg: "#0ff",
            inputBg: "#0a0a2a"
        }
    };
    
    var t = themes[theme] || themes.dark;
    
    document.body.style.background = t.bg;
    document.body.style.color = t.color;
    
    document.querySelectorAll(".card").forEach(function(card) {
        card.style.background = t.cardBg;
        card.style.borderColor = t.border;
    });
    
    document.querySelectorAll(".btn-danger").forEach(function(btn) {
        btn.style.background = t.btnBg;
        btn.style.borderColor = t.btnBg;
    });
    
    document.querySelectorAll(".btn-outline-danger").forEach(function(btn) {
        btn.style.borderColor = t.border;
        btn.style.color = t.border;
    });
    
    document.querySelectorAll(".form-control").forEach(function(input) {
        input.style.background = t.inputBg;
        input.style.color = t.color;
        input.style.borderColor = t.border;
    });
    
    document.querySelectorAll(".table, .table-dark").forEach(function(table) {
        table.style.background = t.cardBg;
        table.style.color = t.color;
    });
    
    document.querySelectorAll(".table thead th").forEach(function(th) {
        th.style.background = t.border;
    });
    
    var navbar = document.querySelector(".navbar");
    if (navbar) navbar.style.background = "#000";
    
    var chatMessages = document.querySelector("#chatMessages");
    if (chatMessages) chatMessages.style.background = t.inputBg;
    
    var messagesList = document.querySelector("#messagesList");
    if (messagesList) messagesList.style.color = t.color;
    
    document.cookie = "user_theme=" + theme + "; path=/; max-age=" + (365 * 24 * 60 * 60);
};

window.loadTheme = function() {
    var match = document.cookie.match(/user_theme=([^;]+)/);
    var theme = match ? match[1] : "dark";
    window.applyTheme(theme);
};

window.setTheme = function(theme) {
    $.post("/Settings/UpdateTheme", { theme: theme }, function(data) {
        if (data.success) {
            window.applyTheme(theme);
        }
    });
};

$(document).ready(function() {
    window.loadTheme();
});