using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CodeDuelArena.Admin.Data
{
    public static class AdminLogs
    {
        private static readonly string LogsPath = "admin_logs.json";
        private static List<AdminLogEntry> _logs = new();
        
        static AdminLogs()
        {
            if (File.Exists(LogsPath))
            {
                try
                {
                    var json = File.ReadAllText(LogsPath);
                    _logs = JsonConvert.DeserializeObject<List<AdminLogEntry>>(json) ?? new List<AdminLogEntry>();
                }
                catch { }
            }
        }
        
        public static void Add(string action, string adminName, string details = "")
        {
            _logs.Insert(0, new AdminLogEntry
            {
                Id = _logs.Count + 1,
                Timestamp = DateTime.Now,
                Action = action,
                AdminName = adminName,
                Details = details
            });
            Save();
        }
        
        public static List<AdminLogEntry> GetRecent(int count = 50)
        {
            return _logs.Take(count).ToList();
        }
        
        public static List<AdminLogEntry> GetAll()
        {
            return _logs;
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
        public string Action { get; set; } = "";
        public string AdminName { get; set; } = "";
        public string Details { get; set; } = "";
    }
}