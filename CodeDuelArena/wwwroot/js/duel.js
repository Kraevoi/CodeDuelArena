// ============ АУТЕНТИФИКАЦИЯ ============
$(document).ready(function() {
    // Проверка авторизации при загрузке
    $.get("/Auth/CheckAuth", function(data) {
        if (data.authenticated) {
            showLoggedInUI(data.username, data.score);
            if (window.connection) {
                connection.invoke("RegisterUser", data.username);
            }
        } else {
            showLoggedOutUI();
        }
    });
    
    $("#showAuthBtn").click(function() {
        $("#authModal").modal("show");
    });
    
    $("#loginBtn").click(function() {
        let username = $("#loginUsername").val().trim();
        let password = $("#loginPassword").val();
        let remember = $("#loginRemember").is(":checked");
        
        $.post("/Auth/Login", { username: username, password: password, rememberMe: remember })
            .done(function(data) {
                if (data.success) {
                    $("#authModal").modal("hide");
                    showLoggedInUI(data.username, data.score);
                    if (window.connection) {
                        connection.invoke("RegisterUser", data.username);
                    }
                    showNotification(`Добро пожаловать, ${data.username}!`, "success");
                } else {
                    $("#authError").text(data.error).removeClass("d-none");
                }
            });
    });
    
    $("#registerBtnModal").click(function() {
        let username = $("#regUsername").val().trim();
        let email = $("#regEmail").val().trim();
        let password = $("#regPassword").val();
        let remember = $("#regRemember").is(":checked");
        
        $.post("/Auth/Register", { username: username, password: password, email: email, rememberMe: remember })
            .done(function(data) {
                if (data.success) {
                    $("#authModal").modal("hide");
                    showLoggedInUI(data.username, 0);
                    if (window.connection) {
                        connection.invoke("RegisterUser", data.username);
                    }
                    showNotification(`Регистрация успешна! Добро пожаловать, ${data.username}!`, "success");
                } else {
                    $("#authError").text(data.error).removeClass("d-none");
                }
            });
    });
    
    $("#logoutBtn").on("click", function() {
        $.post("/Auth/Logout", function() {
            showLoggedOutUI();
            if (window.connection) {
                window.connection.stop();
                location.reload();
            }
        });
    });
    
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
});


let connection = null;
let currentUser = null;

$(function() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/duelHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();
    
    connection.on("UserRegistered", (user) => {
        currentUser = user;
        $("#registerBtn").html(`<i class="fas fa-user"></i> ${user.username}`).prop("disabled", true);
        $("#usernameInput").prop("disabled", true);
        
        // Обновляем статистику в главной
        updateUserStats(user);
        
        // Показываем уведомление
        showNotification(`Добро пожаловать, ${user.username}!`, "success");
    });
    
    connection.on("UpdateLeaderboard", (users) => {
        let html = '<table class="table table-dark table-striped">';
        html += '<thead class="bg-danger">\
                    <tr>\
                        <th>#</th>\
                        <th>Игрок</th>\
                        <th>Очки</th>\
                        <th>Победы</th>\
                    </tr>\
                </thead><tbody>';
        
        users.forEach((u, idx) => {
            html += `<tr>
                        <td class="fw-bold">${idx + 1}</td>
                        <td>${escapeHtml(u.username)}</td>
                        <td class="text-danger fw-bold">${u.score}</td>
                        <td>${u.wins}</td>
                    </tr>`;
        });
        
        html += '</tbody></table>';
        $("#leaderboardTable").html(html);
    });
    
    connection.on("ReceiveChatMessage", (msg) => {
        $("#messagesList").append(`<div class="mb-1"><b class="text-danger">${escapeHtml(msg.user)}</b> [${msg.time}]: ${escapeHtml(msg.text)}</div>`);
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("SystemMessage", (msg) => {
        $("#messagesList").append(`<div class="mb-1 text-info"><i class="fas fa-info-circle"></i> ${escapeHtml(msg)}</div>`);
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("QuestResult", (res) => {
        showNotification(res.message, res.success ? "success" : "error");
        if(res.success && res.newScore !== undefined && currentUser) {
            currentUser.score = res.newScore;
            updateUserStats(currentUser);
        }
    });
    
    connection.on("QueueJoined", (msg) => {
        showNotification(msg, "info");
    });
    
    connection.on("DuelStarted", (data) => {
        showNotification(`⚔️ ДУЭЛЬ НАЧАЛАСЬ! Противник: ${data.opponent}. Duel ID: ${data.duelId}`, "warning");
    });
    
    $("#registerBtn").click(() => {
        let name = $("#usernameInput").val().trim();
        if(name) {
            connection.invoke("RegisterUser", name);
        } else {
            showNotification("Введи никнейм, епта!", "error");
        }
    });
    
    connection.start()
        .then(() => {
            console.log("SignalR connected");
        })
        .catch(err => console.error(err));
    
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

function showNotification(message, type) {
    let bgColor = type === "success" ? "#28a745" : type === "error" ? "#dc3545" : "#17a2b8";
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