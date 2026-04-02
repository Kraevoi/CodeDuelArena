using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeDuelArena.Models;
using Newtonsoft.Json;

namespace CodeDuelArena.Data
{
    public static class UserAccounts
    {
        // Используем постоянную директорию на Render
        private static readonly string DataDir = Environment.GetEnvironmentVariable("RENDER") != null 
            ? "/opt/render/project/src/data" 
            : Directory.GetCurrentDirectory();
        
        private static readonly string AccountsPath = Path.Combine(DataDir, "accounts.json");

        static UserAccounts()
        {
            // Создаем папку если не существует
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);
            
            if (!File.Exists(AccountsPath))
                File.WriteAllText(AccountsPath, "[]");
        }

        public static List<UserAccount> GetAll()
        {
            try
            {
                var json = File.ReadAllText(AccountsPath);
                return JsonConvert.DeserializeObject<List<UserAccount>>(json) ?? new List<UserAccount>();
            }
            catch
            {
                return new List<UserAccount>();
            }
        }

        public static void SaveAll(List<UserAccount> accounts)
        {
            try
            {
                var json = JsonConvert.SerializeObject(accounts, Formatting.Indented);
                File.WriteAllText(AccountsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения: {ex.Message}");
            }
        }

        public static bool Register(string username, string password, string email, out string error)
        {
            var accounts = GetAll();

            if (accounts.Any(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                error = "Имя пользователя уже занято";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(email) && accounts.Any(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                error = "Email уже используется";
                return false;
            }

            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                error = "Имя должно быть не менее 3 символов";
                return false;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                error = "Пароль должен быть не менее 4 символов";
                return false;
            }

            var account = new UserAccount
            {
                Username = username,
                PasswordHash = HashPassword(password),
                Email = email ?? "",
                Score = 0,
                Wins = 0,
                Losses = 0,
                CompletedQuests = new List<string>(),
                RegisteredAt = DateTime.Now,
                LastLogin = DateTime.Now
            };

            accounts.Add(account);
            SaveAll(accounts);
            error = "";
            return true;
        }

        public static UserAccount? Login(string username, string password, out string error)
        {
            var accounts = GetAll();
            var account = accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (account == null)
            {
                error = "Пользователь не найден";
                return null;
            }

            if (!VerifyPassword(password, account.PasswordHash))
            {
                error = "Неверный пароль";
                return null;
            }

            account.LastLogin = DateTime.Now;
            SaveAll(accounts);
            error = "";
            return account;
        }

        public static UserAccount? GetByUsername(string username)
        {
            return GetAll().FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        }

        public static void UpdateStats(string username, int scoreDelta = 0, bool win = false, bool loss = false, string? questId = null)
        {
            var accounts = GetAll();
            var account = accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (account != null)
            {
                account.Score += scoreDelta;
                if (win) account.Wins++;
                if (loss) account.Losses++;
                if (questId != null && !account.CompletedQuests.Contains(questId))
                    account.CompletedQuests.Add(questId);
                SaveAll(accounts);
            }
        }

        public static List<UserAccount> GetAllUsers()
        {
            return GetAll();
        }

        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}