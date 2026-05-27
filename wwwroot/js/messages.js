var allUsers = [];

$(function() {
    $.get("/Message/GetAllUsers", function(data) {
        allUsers = data;
        renderAllUsers(allUsers);
    }).fail(function() {
        $("#allUsersList").html('<div class="text-center text-muted py-4">Failed to load users</div>');
    });

    $("#sendBtn").click(function() {
        var toUser = $("#toUser").val().trim();
        var message = $("#messageText").val().trim();
        
        if (!toUser) { showResult("Select a recipient", "error"); return; }
        if (!message) { showResult("Enter a message", "error"); return; }
        
        $("#sendBtn").prop("disabled", true).html('<i class="fas fa-spinner fa-spin me-2"></i>Sending...');
        
        $.post("/Message/Send", { toUser: toUser, message: message }, function(data) {
            if (data.success) {
                showResult('<i class="fas fa-check-circle me-2"></i>Message sent!', "success");
                $("#messageText").val("");
                $("#toUser").val("");
                $("#userSearch").val("");
            } else {
                showResult(data.error || "Error sending message", "error");
            }
            $("#sendBtn").prop("disabled", false).html('<i class="fas fa-paper-plane me-2"></i>Send Message');
        }).fail(function() {
            showResult("Connection error", "error");
            $("#sendBtn").prop("disabled", false).html('<i class="fas fa-paper-plane me-2"></i>Send Message');
        });
    });

    $("#userSearch").on("keyup", searchUsers);
    $("#allUsersSearch").on("keyup", filterAllUsers);

    $(document).click(function(e) {
        if (!$(e.target).closest("#searchResults").length && !$(e.target).is("#userSearch")) {
            $("#searchResults").hide();
        }
    });
});

function searchUsers() {
    var query = ($("#userSearch").val() || "").toLowerCase();
    var results = $("#searchResults");
    
    if (query.length < 1) {
        results.hide();
        return;
    }
    
    var filtered = allUsers.filter(function(u) {
        var uname = (u.username || "").toLowerCase();
        var utag = (u.tag || "").toLowerCase();
        return uname.indexOf(query) !== -1 || utag.indexOf(query) !== -1;
    });
    
    if (filtered.length > 0) {
        var html = '';
        filtered.slice(0, 8).forEach(function(u) {
            html += '<div class="search-item" onclick="selectUser(\'' + escapeHtml(u.username) + '\')">' +
                '<span class="user-avatar-xs">' + (u.username || "?")[0].toUpperCase() + '</span>' +
                '<span class="fw-bold">' + escapeHtml(u.username) + '</span>' +
                '<span class="text-warning ms-2">@' + escapeHtml(u.tag || "") + '</span>' +
                (u.isAdmin ? '<span class="badge bg-warning text-dark ms-auto">ADM</span>' : '') +
            '</div>';
        });
        results.html(html).show();
    } else {
        results.html('<div class="search-item text-muted">No users found</div>').show();
    }
}

function selectUser(username) {
    $("#toUser").val(username);
    $("#userSearch").val(username);
    $("#searchResults").hide();
}

function filterAllUsers() {
    var query = ($("#allUsersSearch").val() || "").toLowerCase();
    var filtered = query ? allUsers.filter(function(u) {
        var uname = (u.username || "").toLowerCase();
        var utag = (u.tag || "").toLowerCase();
        return uname.indexOf(query) !== -1 || utag.indexOf(query) !== -1;
    }) : allUsers;
    renderAllUsers(filtered);
}

function renderAllUsers(users) {
    if (users.length === 0) {
        $("#allUsersList").html('<div class="text-center text-muted py-4">No users found</div>');
        return;
    }
    
    var html = '';
    users.forEach(function(u) {
        html += '<div class="user-list-item" onclick="selectUser(\'' + escapeHtml(u.username) + '\')">' +
            '<span class="user-avatar-xs">' + (u.username || "?")[0].toUpperCase() + '</span>' +
            '<div>' +
                '<span class="fw-bold">' + escapeHtml(u.username) + '</span>' +
                '<span class="text-warning ms-2">@' + escapeHtml(u.tag || "") + '</span>' +
            '</div>' +
            (u.isAdmin ? '<span class="badge bg-warning text-dark ms-auto">ADM</span>' : '') +
        '</div>';
    });
    $("#allUsersList").html(html);
}

function showResult(msg, type) {
    var cls = type === "success" ? "alert-success-custom" : "alert-error-custom";
    $("#sendResult").attr("class", cls).html(msg).fadeIn().delay(3000).fadeOut();
}

function escapeHtml(str) {
    if (!str) return "";
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}