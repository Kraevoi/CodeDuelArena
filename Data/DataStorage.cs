using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeDuelArena.Models;
using Newtonsoft.Json;

namespace CodeDuelArena.Data
{
    public static class DataStorage
    {
        private static readonly string DataPath = Directory.GetCurrentDirectory();
        private static readonly string UsersPath = Path.Combine(DataPath, "users.json");
        private static readonly string QuestsPath = Path.Combine(DataPath, "quests.json");
        
        static DataStorage()
        {
            if (!File.Exists(UsersPath))
                File.WriteAllText(UsersPath, "[]");
            if (!File.Exists(QuestsPath))
                SeedQuests();
        }

        private static void SeedQuests()
        {
            var quests = new List<QuestModel>
            {
                new QuestModel
                {
                    Id = 1,
                    Title = "Легаси: Факториал через жопу",
                    Type = "LegacyFix",
                    Description = "Функция считает факториал, но использует глобальные переменные. Исправь.",
                    LegacyCode = "int result; void Fact(int n) { if(n==0) return; result*=n; Fact(n-1); }",
                    ExpectedOutput = "120",
                    SolutionCode = "int Fact(int n) { if(n<=1) return 1; return n * Fact(n-1); }",
                    Points = 150
                },
                new QuestModel
                {
                    Id = 2,
                    Title = "Взлом: Брут пароля",
                    Type = "Hack",
                    Description = "Взломай функцию проверки пароля.",
                    LegacyCode = "bool CheckPass(string p) { return p == \"admin123\"; }",
                    ExpectedOutput = "true",
                    SolutionCode = "return true;",
                    Points = 200
                }
            };
            File.WriteAllText(QuestsPath, JsonConvert.SerializeObject(quests, Formatting.Indented));
        }

        public static List<UserModel> GetUsers()
        {
            return JsonConvert.DeserializeObject<List<UserModel>>(File.ReadAllText(UsersPath)) ?? new List<UserModel>();
        }

        public static void SaveUsers(List<UserModel> users)
        {
            File.WriteAllText(UsersPath, JsonConvert.SerializeObject(users, Formatting.Indented));
        }

        public static List<QuestModel> GetQuests()
        {
            return JsonConvert.DeserializeObject<List<QuestModel>>(File.ReadAllText(QuestsPath)) ?? new List<QuestModel>();
        }
    }
}