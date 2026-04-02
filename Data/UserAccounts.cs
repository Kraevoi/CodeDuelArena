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
        private static readonly string AccountsPath = "accounts.json";

        static UserAccounts()
        {
            if (!File.Exists(AccountsPath))
                File.WriteAllText(AccountsPath, "[]");
        }

        public static List<UserAccount> GetAll()
        {
            return JsonConvert.DeserializeObject<List<UserAccount>>(File.ReadAllText(AccountsPath)) ?? new List<UserAccount>();
        }

        public static void SaveAll(List<UserAccount> accounts)
        {
            File.WriteAllText(AccountsPath, JsonConvert.SerializeObject(accounts, Formatting.Indented));
        }

        public static bool Register(string username, string password, string email, out string error)
        {
            var accounts = GetAll();

            if (accounts.Any(a => a.Username.ToLower() == username.ToLower()))
            {
                error = "Имя пользователя уже занято";
                return false;
            }

            if (accounts.Any(a => a.Email.ToLower() == email.ToLower() && !string.IsNullOrWhiteSpace(email)))
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
            var account = accounts.FirstOrDefault(a => a.Username.ToLower() == username.ToLower());

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
            return GetAll().FirstOrDefault(a => a.Username.ToLower() == username.ToLower());
        }

        public static void UpdateStats(string username, int scoreDelta = 0, bool win = false, bool loss = false, string? questId = null)
        {
            var accounts = GetAll();
            var account = accounts.FirstOrDefault(a => a.Username.ToLower() == username.ToLower());
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