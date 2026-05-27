var currentChat = null;
var refreshInterval = null;

$(function() {
    loadChats();
    refreshInterval = setInterval(loadChats, 5000);
});

function loadChats() {
    $.get("/Message/GetChats", function(chats) {
        var html = '';
        chats.forEach(function(c) {
            html += '<div class="chat-item' + (currentChat === c.username ? ' active' : '') + '" onclick="openChat(\'' + escapeHtml(c.username) + '\')">' +
                '<div class="chat-avatar">' + (c.username || '?')[0].toUpperCase() + '</div>' +
                '<div class="chat-info">' +
                    '<div class="chat-name">' + escapeHtml(c.username) + '</div>' +
                    '<div class="chat-preview">' + escapeHtml(c.lastMessage || '') + '</div>' +
                '</div>' +
                '<div class="chat-meta">' +
                    '<div class="chat-time">' + (c.lastTime || '') + '</div>' +
                    (c.unread > 0 ? '<div class="chat-unread">' + c.unread + '</div>' : '') +
                '</div>' +
            '</div>';
        });
        
        if (chats.length === 0) {
            html = '<div class="text-center text-muted py-4">No messages yet</div>';
        }
        
        $("#chatList").html(html);
    });
}

function openChat(username) {
    currentChat = username;
    loadChats();
    loadMessages(username);
    
    $("#chatHeader").html('<div style="display:flex;align-items:center;gap:12px;">' +
        '<div class="chat-avatar">' + (username || '?')[0].toUpperCase() + '</div>' +
        '<span>' + escapeHtml(username) + '</span>' +
    '</div>');
    
    $("#chatInputArea").show();
    $("#chatMessages").html('');
    $("#messageInput").focus();
}

function loadMessages(username) {
    $.get("/Message/GetMessages", { withUser: username }, function(messages) {
        var html = '';
        messages.forEach(function(m) {
            html += '<div class="message-bubble ' + (m.isMine ? 'message-mine' : 'message-other') + '">' +
                escapeHtml(m.text) +
                '<div class="message-time">' + m.time + '</div>' +
            '</div>';
        });
        $("#chatMessages").html(html);
        $("#chatMessages").scrollTop($("#chatMessages")[0].scrollHeight);
    });
}

function sendMessage() {
    var text = $("#messageInput").val().trim();
    if (!text || !currentChat) return;
    
    $.post("/Message/Send", { toUser: currentChat, message: text }, function(data) {
        if (data.success) {
            $("#messageInput").val('');
            loadMessages(currentChat);
            loadChats();
        }
    });
}

function showNewChat() {
    $("#userSearchInput").val('');
    $("#userSearchResults").html('');
    $("#newChatModal").modal("show");
}

function searchUsers() {
    var query = $("#userSearchInput").val().trim();
    if (query.length < 1) {
        $("#userSearchResults").html('');
        return;
    }
    
    $.get("/Message/SearchUsers", { query: query }, function(users) {
        var html = '';
        users.forEach(function(u) {
            html += '<div class="user-search-item" onclick="startChat(\'' + escapeHtml(u.username) + '\')">' +
                '<div class="user-search-avatar">' + (u.username || '?')[0].toUpperCase() + '</div>' +
                '<div>' +
                    '<strong>' + escapeHtml(u.username) + '</strong>' +
                    '<span class="text-warning ms-2">@' + escapeHtml(u.tag || '') + '</span>' +
                '</div>' +
                (u.isAdmin ? '<span class="badge bg-warning text-dark ms-auto">ADM</span>' : '') +
            '</div>';
        });
        
        if (users.length === 0) {
            html = '<div class="text-muted text-center py-3">No users found</div>';
        }
        
        $("#userSearchResults").html(html);
    });
}

function startChat(username) {
    $("#newChatModal").modal("hide");
    openChat(username);
}

function filterChats() {
    var query = ($("#chatSearch").val() || "").toLowerCase();
    $(".chat-item").each(function() {
        var name = ($(this).find(".chat-name").text() || "").toLowerCase();
        $(this).toggle(name.indexOf(query) !== -1);
    });
}

function escapeHtml(str) {
    if (!str) return "";
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}