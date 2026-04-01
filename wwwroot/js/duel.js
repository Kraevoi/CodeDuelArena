let connection = null;
let currentUser = null;

$(function() {
    connection = new signalR.HubConnectionBuilder().withUrl("/duelHub").build();
    
    connection.on("UserRegistered", (user) => {
        currentUser = user;
        $("#userInfo").text(user.username).removeClass("d-none");
        $("#authBtn").addClass("d-none");
        $("#logoutBtn").removeClass("d-none");
    });
    
    connection.on("UpdateLeaderboard", (users) => {
        let html = '<table class="table table-dark"><tr><th>#</th><th>Игрок</th><th>Очки</th></tr>';
        users.forEach((u,i) => html += `<tr><td>${i+1}</td><td>${u.username}</td><td>${u.score}</td></tr>`);
        $("#leaderboardTable").html(html);
    });
    
    connection.on("ReceiveChatMessage", (msg) => {
        $("#chatMessages").append(`<div><b>${msg.user}</b> [${msg.time}]: ${msg.text}</div>`);
        $("#chatBox").scrollTop($("#chatBox")[0].scrollHeight);
    });
    
    connection.on("SystemMessage", (msg) => {
        $("#chatMessages").append(`<div class="text-info">${msg}</div>`);
    });
    
    connection.on("QuestResult", (res) => {
        alert(res.message);
        if(res.success && currentUser) currentUser.score = res.newScore;
    });
    
    connection.on("QueueJoined", (msg) => alert(msg));
    connection.on("DuelStarted", (data) => {
        let code = prompt(`Дуэль с ${data.opponent}! Задание: ${data.task}\nВведи решение:`);
        if(code) connection.invoke("SubmitDuelSolution", code, data.duelId);
    });
    connection.on("DuelResult", (res) => alert(res.message));
    
    connection.start().then(() => {
        $.get("/Auth/CheckAuth", (data) => {
            if(data.authenticated) connection.invoke("RegisterUser", data.username);
        });
    });
    
    $("#authBtn").click(() => $("#authModal").modal("show"));
    $("#loginSubmit").click(() => {
        $.post("/Auth/Login", { username: $("#loginUser").val(), password: $("#loginPass").val() }, (data) => {
            if(data.success) { location.reload(); }
            else $("#authError").text(data.error);
        });
    });
    $("#regSubmit").click(() => {
        $.post("/Auth/Register", { username: $("#regUser").val(), password: $("#regPass").val(), email: $("#regEmail").val() }, (data) => {
            if(data.success) { location.reload(); }
            else $("#authError").text(data.error);
        });
    });
    $("#logoutBtn").click(() => $.post("/Auth/Logout", () => location.reload()));
    $("#chatSend").click(() => {
        let msg = $("#chatInput").val();
        if(msg && connection) connection.invoke("SendChatMessage", msg);
        $("#chatInput").val("");
    });
});

window.submitQuest = (id) => {
    let code = $(`#code_${id}`).val();
    if(code && connection) connection.invoke("SubmitQuestSolution", code, id);
};