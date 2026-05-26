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
        $("#userNameDisplay").html('<a href="/Profile/Index?username=' + encodeURIComponent(user.username) + '" class="text-danger fw-bold text-decoration-none">' + escapeHtml(user.username) + '</a>');
        $("#userTagDisplay").text("@" + (user.tag || "no_tag"));
        $("#userScoreDisplay").text("⭐ " + user.score);
        updateUserStats(user);
    });
    
    connection.on("UpdateLeaderboard", (users) => {
        let html = '<table class="table table-dark table-striped"><thead class="bg-danger"><tr><th>#</th><th>Player</th><th>Tag</th><th>⭐ Score</th><th>🏆 Wins</th><th>💀 Losses</th></tr></thead><tbody>';
        users.forEach((u, idx) => {
            html += '<tr>';
            html += '<td class="fw-bold">' + (idx + 1) + '</td>';
            html += '<td><a href="/Profile/Index?username=' + encodeURIComponent(u.username) + '" class="text-danger fw-bold text-decoration-none">' + escapeHtml(u.username) + '</a></td>';
            html += '<td><a href="/Profile/Index?username=' + encodeURIComponent(u.username) + '" class="text-warning text-decoration-none">@' + escapeHtml(u.tag || 'no_tag') + '</a></td>';
            html += '<td class="text-danger fw-bold">' + u.score + '</td>';
            html += '<td>' + u.wins + '</td>';
            html += '<td>' + u.losses + '</td>';
            html += '</tr>';
        });
        html += '</tbody></table>';
        $("#leaderboardTable").html(html);
    });
    
    connection.on("ReceiveChatMessage", (msg) => {
        $("#messagesList").append('<div style="margin-bottom: 5px;"><a href="/Profile/Index?username=' + encodeURIComponent(msg.user) + '" class="text-danger fw-bold text-decoration-none">' + escapeHtml(msg.user) + '</a> <span style="color: #888;">[' + msg.time + ']</span>: <span style="color: #fff;">' + escapeHtml(msg.text) + '</span></div>');
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("SystemMessage", (msg) => {
        $("#messagesList").append('<div style="color: #17a2b8; margin-bottom: 5px;"><i class="fas fa-info-circle"></i> ' + escapeHtml(msg) + '</div>');
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
    
    connection.on("QuestResult", (res) => {
        showNotification(res.message, res.success ? "success" : "error");
        if(res.success && res.newScore !== undefined && window.currentUser) {
            window.currentUser.score = res.newScore;
            updateUserStats(window.currentUser);
            $("#userScoreDisplay").text("⭐ " + window.currentUser.score);
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
            $("#userScoreDisplay").text("⭐ " + window.currentUser.score);
        }
        showNotification(res.message, res.success ? "success" : "error");
        $("#duelModal").remove();
    });
    
    connection.on("DuelTimeout", (msg) => {
        showNotification(msg, "error");
        $("#duelModal").remove();
    });
    
    connection.on("DuelCancelled", (msg) => {
        showNotification(msg, "warning");
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
    
    // Auth buttons
    $("#showAuthBtn").click(() => $("#authModal").modal("show"));
    
    // Login
    $("#loginBtn").click(function() {
        var username = $("#loginUsername").val().trim();
        var password = $("#loginPassword").val();
        var remember = $("#loginRemember").is(":checked");
        
        $.ajax({
            url: "/Auth/Login",
            type: "POST",
            data: { username: username, password: password, rememberMe: remember },
            success: function(data) {
                if(data.success) {
                    $("#authModal").modal("hide");
                    window.currentUser = { username: data.username, tag: data.tag, score: data.score };
                    $("#userInfo").removeClass("d-none");
                    $("#showAuthBtn").addClass("d-none");
                    $("#userNameDisplay").html('<a href="/Profile/Index?username=' + encodeURIComponent(data.username) + '" class="text-danger fw-bold text-decoration-none">' + escapeHtml(data.username) + '</a>');
                    $("#userTagDisplay").text("@" + (data.tag || "no_tag"));
                    $("#userScoreDisplay").text("⭐ " + data.score);
                    updateUserStats(window.currentUser);
                    connection.invoke("RegisterUser", data.username);
                } else {
                    $("#authError").text(data.error).removeClass("d-none");
                }
            },
            error: function() {
                $("#authError").text("Connection error").removeClass("d-none");
            }
        });
    });
    
    // Register
    $("#registerBtnModal").click(function() {
        var username = $("#regUsername").val().trim();
        var tag = $("#regTag").val().trim();
        var email = $("#regEmail").val().trim();
        var password = $("#regPassword").val();
        var remember = $("#regRemember").is(":checked");
        
        if (!tag) {
            $("#authError").text("Tag is required").removeClass("d-none");
            return;
        }
        
        $.ajax({
            url: "/Auth/Register",
            type: "POST",
            data: { username: username, tag: tag, password: password, email: email, rememberMe: remember },
            success: function(data) {
                if(data.success) {
                    $("#authModal").modal("hide");
                    window.currentUser = { username: data.username, tag: data.tag, score: data.score };
                    $("#userInfo").removeClass("d-none");
                    $("#showAuthBtn").addClass("d-none");
                    $("#userNameDisplay").html('<a href="/Profile/Index?username=' + encodeURIComponent(data.username) + '" class="text-danger fw-bold text-decoration-none">' + escapeHtml(data.username) + '</a>');
                    $("#userTagDisplay").text("@" + (data.tag || "no_tag"));
                    $("#userScoreDisplay").text("⭐ " + data.score);
                    updateUserStats(window.currentUser);
                    connection.invoke("RegisterUser", data.username);
                } else {
                    $("#authError").text(data.error).removeClass("d-none");
                }
            },
            error: function() {
                $("#authError").text("Connection error").removeClass("d-none");
            }
        });
    });
    
    // Logout
    $("#logoutBtn").click(() => {
        $.post("/Auth/Logout", () => {
            window.currentUser = null;
            $("#userInfo").addClass("d-none");
            $("#showAuthBtn").removeClass("d-none");
            location.reload();
        });
    });
    
    // Chat
    $("#sendChatBtn").click(() => {
        var msg = $("#chatInput").val();
        if(msg && connection) {
            connection.invoke("SendChatMessage", msg);
            $("#chatInput").val("");
        }
    });
    
    $("#chatInput").keypress((e) => {
        if(e.which == 13) $("#sendChatBtn").click();
    });
    
    // Duel queue
    $("#duelQueueBtn").click(() => {
        if(connection && window.currentUser) {
            connection.invoke("JoinDuelQueue");
        } else {
            showNotification("Please log in first", "error");
        }
    });
    
    window.connection = connection;
});

function updateUserStats(user) {
    if($("#userStats").length && user) {
        $("#userStats").html(`
            <div class="text-start">
                <p><i class="fas fa-user text-danger"></i> <strong>Username:</strong> <a href="/Profile/Index?username=${encodeURIComponent(user.username)}" class="text-danger fw-bold text-decoration-none">${escapeHtml(user.username)}</a></p>
                <p><i class="fas fa-tag text-warning"></i> <strong>Tag:</strong> @${escapeHtml(user.tag || 'not set')}</p>
                <p><i class="fas fa-star text-danger"></i> <strong>Score:</strong> ${user.score}</p>
                <p><i class="fas fa-trophy text-danger"></i> <strong>Wins:</strong> ${user.wins}</p>
                <p><i class="fas fa-skull text-danger"></i> <strong>Losses:</strong> ${user.losses}</p>
                <p><i class="fas fa-check-circle text-danger"></i> <strong>Quests completed:</strong> ${user.completedQuests?.length || 0}</p>
            </div>
        `);
    }
}

function showDuelModal(data) {
    var modalHtml = `
        <div class="modal fade" id="duelModal" tabindex="-1" data-bs-backdrop="static">
            <div class="modal-dialog modal-lg">
                <div class="modal-content bg-dark text-white border-danger">
                    <div class="modal-header border-danger">
                        <h5 class="modal-title">⚔️ DUEL vs ${escapeHtml(data.opponent)}</h5>
                        <div>
                            <span class="badge bg-warning me-2" id="duelTimer">${data.timeLimit || 60}s</span>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" onclick="cancelDuel(${data.duelId})"></button>
                        </div>
                    </div>
                    <div class="modal-body">
                        <div class="alert alert-danger text-center mb-3">
                            <i class="fas fa-hourglass-half me-2"></i>
                            <strong>First correct answer wins!</strong> Time limit: 60 seconds
                        </div>
                        <div class="card bg-black border-danger mb-3">
                            <div class="card-header bg-danger">TASK</div>
                            <div class="card-body">
                                <p class="text-info">${escapeHtml(data.taskDescription || data.task)}</p>
                                ${data.testCode ? '<pre class="bg-black text-warning p-2">' + escapeHtml(data.testCode) + '</pre>' : ''}
                                ${data.expectedOutput ? '<p><strong>Expected Output:</strong> ' + escapeHtml(data.expectedOutput) + '</p>' : ''}
                            </div>
                        </div>
                        <textarea id="duelSolution" class="form-control bg-black text-white border-danger" rows="6" placeholder="Write your solution here..."></textarea>
                        <div class="d-flex gap-2 mt-3">
                            <button id="submitDuelBtn" class="btn btn-danger flex-grow-1">
                                ⚔️ SUBMIT SOLUTION
                            </button>
                            <button class="btn btn-outline-warning" onclick="cancelDuel(${data.duelId})">
                                <i class="fas fa-door-open me-1"></i> LEAVE
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
    
    $("body").append(modalHtml);
    $("#duelModal").modal("show");
    
    var timeLeft = data.timeLimit || 60;
    var timer = setInterval(() => {
        timeLeft--;
        $("#duelTimer").text(timeLeft + "s");
        if(timeLeft <= 0) {
            clearInterval(timer);
            $("#submitDuelBtn").prop("disabled", true).text("TIME EXPIRED");
        }
    }, 1000);
    
    $("#submitDuelBtn").click(function() {
        var solution = $("#duelSolution").val();
        if(solution && window.connection && data.duelId) {
            window.connection.invoke("SubmitDuelSolution", solution, data.duelId);
            $("#submitDuelBtn").prop("disabled", true).html('<i class="fas fa-spinner fa-spin me-2"></i>SUBMITTED...');
        }
    });
    
    window.duelTimer = timer;
    window.currentDuelId = data.duelId;
    
    $("#duelModal").on("hidden.bs.modal", () => {
        $("#duelModal").remove();
        if(window.duelTimer) clearInterval(window.duelTimer);
    });
}

function cancelDuel(duelId) {
    if(confirm("Are you sure you want to leave this duel? Your opponent will win automatically.")) {
        if(window.connection) {
            window.connection.invoke("CancelDuel", duelId);
        }
        $("#duelModal").modal("hide");
    }
}

function showNotification(message, type) {
    var bgClass = type === "success" ? "success" : type === "error" ? "danger" : type === "warning" ? "warning" : "info";
    var notification = $('<div class="alert alert-' + bgClass + ' alert-dismissible fade show" style="position: fixed; top: 80px; right: 20px; z-index: 99999; min-width: 300px;" role="alert">' + message + '<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>');
    $("body").append(notification);
    setTimeout(() => notification.alert('close'), 5000);
}

function escapeHtml(str) {
    if(!str) return "";
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

window.submitQuest = function(questId) {
    var code = $("#code_" + questId).val();
    if(code && window.connection) {
        window.connection.invoke("SubmitQuestSolution", code, questId);
    } else if(!code) {
        alert("Write your solution!");
    }
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
    
    document.querySelectorAll(".form-control").forEach(function(input) {
        input.style.background = t.inputBg;
        input.style.color = t.color;
        input.style.borderColor = t.border;
    });
    
    document.cookie = "user_theme=" + theme + "; path=/; max-age=" + (365 * 24 * 60 * 60);
};

$(document).ready(function() {
    window.loadTheme();
});