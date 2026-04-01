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
        
        $.ajax({
            url: "/Auth/Login",
            type: "POST",
            contentType: "application/json",
            data: JSON.stringify({ username: username, password: password, rememberMe: remember }),
            success: function(data) {
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
            contentType: "application/json",
            data: JSON.stringify({ username: username, password: password, email: email, rememberMe: remember }),
            success: function(data) {
                if (data.success) {
                    $("#authModal").modal("hide");
                    showLoggedInUI(data.username, data.score);
                    if (window.connection) {
                        connection.invoke("RegisterUser", data.username);
                    }
                    showNotification(`Регистрация успешна! Добро пожаловать, ${data.username}!`, "success");
                } else {
                    $("#authError").text(data.error).removeClass("d-none");
                }
            }
        });
    });
    
    $("#logoutBtn").on("click", function() {
        $.post("/Auth/Logout", function() {
            showLoggedOutUI();
            if (window.connection) {
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
        $("#usernameInput").val("").prop("disabled", false);
        $("#registerBtn").html('<i class="fas fa-sign-in-alt"></i> Войти').prop("disabled", false);
    }
    
    window.showLoggedInUI = showLoggedInUI;
});