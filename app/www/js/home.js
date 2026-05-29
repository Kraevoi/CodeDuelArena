setInterval(function() {
    if (window.currentUser) {
        var statsEl = document.getElementById("userStats");
        if (statsEl) {
            statsEl.innerHTML =
                '<div class="text-start">' +
                '<p><i class="fas fa-user text-danger"></i> <strong>Username:</strong> ' + escapeHtml(window.currentUser.username) + '</p>' +
                '<p><i class="fas fa-tag text-warning"></i> <strong>Tag:</strong> @' + escapeHtml(window.currentUser.tag || 'not set') + '</p>' +
                '<p><i class="fas fa-star text-danger"></i> <strong>Score:</strong> ' + window.currentUser.score + '</p>' +
                '<p><i class="fas fa-trophy text-danger"></i> <strong>Wins:</strong> ' + window.currentUser.wins + '</p>' +
                '<p><i class="fas fa-skull text-danger"></i> <strong>Losses:</strong> ' + window.currentUser.losses + '</p>' +
                '<p><i class="fas fa-check-circle text-danger"></i> <strong>Quests completed:</strong> ' + (window.currentUser.completedQuests ? window.currentUser.completedQuests.length : 0) + '</p>' +
                '</div>';
        }
    }
}, 500);