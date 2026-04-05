let connection = null;
let currentUser = null;

$(function() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/duelHub")
        .configureLogging(signalR.LogLevel.Information)
        .withAutomaticReconnect()
        .build();
    
    connection.on("UserRegistered", (user) => {
        currentUser = user;
        $("#userInfo").removeClass("d-none");
        $("#showAuthBtn").addClass("d-none");
        $("#userNameDisplay").text(user.username);
        $("#userScoreDisplay").text(`⭐ ${user.score}`);
        updateUserStats(user);
        showNotification(`Добро пожаловать, ${user.username}!`, "success");
    });
    
    connection.on("UpdateLeaderboard", (users) => {
        let html = '<table class="table table-dark table-striped"><thead class="bg-danger"><tr><th>#</th><th>Игрок</th><th>⭐ Очки</th><th>🏆 Победы</th><th>💀 Поражения</th><tr></thead><tbody>';
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
        if(res.success && res.newScore !== undefined && currentUser) {
            currentUser.score = res.newScore;
            updateUserStats(currentUser);
            $("#userScoreDisplay").text(`⭐ ${currentUser.score}`);
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
        if(res.success && res.newScore !== undefined && currentUser) {
            currentUser.score = res.newScore;
            updateUserStats(currentUser);
            $("#userScoreDisplay").text(`⭐ ${currentUser.score}`);
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
        if(connection && currentUser) {
            connection.invoke("JoinDuelQueue");
        } else {
            showNotification("Сначала войдите в систему", "error");
        }
    });
    
    window.connection = connection;
});

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
                            <h3>⏱️ <span id="duelTimer">${data.timeLimit || 60}</span> секунд</h3>
                        </div>
                        <div class="card bg-black border-danger mb-3">
                            <div class="card-header bg-danger">${escapeHtml(data.taskTitle)}</div>
                            <div class="card-body">
                                <p>${escapeHtml(data.taskDescription)}</p>
                                <pre class="bg-black text-warning p-2">${escapeHtml(data.testCode)}</pre>
                                <p><strong>Ожидаемый вывод:</strong> ${escapeHtml(data.expectedOutput)}</p>
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