using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CodeDuelArena.Data
{
    public static class AdminLogs
    {
        private static readonly string LogsPath = "admin_logs.json";
        private static readonly List<AdminLogEntry> _logs = new();
        
        static AdminLogs()
        {
            if (File.Exists(LogsPath))
            {
                try
                {
                    var json = File.ReadAllText(LogsPath);
                    var loaded = JsonConvert.DeserializeObject<List<AdminLogEntry>>(json);
                    if (loaded != null) _logs = loaded;
                }
                catch { }
            }
        }
        
        public static void Add(string action, string adminName, string details = "")
        {
            var entry = new AdminLogEntry
            {
                Id = _logs.Count + 1,
                Timestamp = DateTime.Now,
                Action = action,
                AdminName = adminName,
                Details = details
            };
            _logs.Add(entry);
            Save();
        }
        
        public static List<AdminLogEntry> GetRecent(int count = 50)
        {
            return _logs.OrderByDescending(l => l.Timestamp).Take(count).ToList();
        }
        
        public static List<AdminLogEntry> GetAll()
        {
            return _logs.OrderByDescending(l => l.Timestamp).ToList();
        }
        
        private static void Save()
        {
            File.WriteAllText(LogsPath, JsonConvert.SerializeObject(_logs, Formatting.Indented));
        }
    }
    
    public class AdminLogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }
}