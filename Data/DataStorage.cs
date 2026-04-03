using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeDuelArena.Models;
using Newtonsoft.Json;

namespace CodeDuelArena.Data
{
    public static class DataStorage
    {
        private static readonly string UsersPath = "users.json";
        private static readonly string QuestsPath = "quests.json";

        static DataStorage()
        {
            if (!File.Exists(UsersPath)) File.WriteAllText(UsersPath, "[]");
            if (!File.Exists(QuestsPath)) SeedQuests();
        }

        private static void SeedQuests()
        {
            var quests = new List<QuestModel>
            {
                new QuestModel { Id = 1, Title = "Легаси: Факториал", Type = "LegacyFix", 
                    Description = "Исправь функцию факториала. Ожидается: Fact(5) = 120",
                    LegacyCode = "int result; void Fact(int n) { if(n==0) return; result*=n; Fact(n-1); }",
                    SolutionCode = "int Fact(int n) { if(n<=1) return 1; return n * Fact(n-1); }", Points = 150 },
                new QuestModel { Id = 2, Title = "Взлом: Брут пароля", Type = "Hack",
                    Description = "Функция проверяет пароль. Нужно вернуть true",
                    LegacyCode = "bool CheckPass(string p) { return p == \"admin123\"; }",
                    SolutionCode = "return true;", Points = 200 },
                new QuestModel { Id = 3, Title = "Легаси: Сумма массива", Type = "LegacyFix",
                    Description = "Функция должна возвращать сумму всех элементов массива",
                    LegacyCode = "int Sum(int[] arr) { int s; for(int i=0;i<arr.Length;i++) s+=arr[i]; return s; }",
                    SolutionCode = "int Sum(int[] arr) { int s=0; for(int i=0;i<arr.Length;i++) s+=arr[i]; return s; }", Points = 100 },
                new QuestModel { Id = 4, Title = "Легаси: Поиск максимума", Type = "LegacyFix",
                    Description = "Найди максимальное число в массиве",
                    LegacyCode = "int Max(int[] arr) { int m; for(int i=0;i<arr.Length;i++) if(arr[i]>m) m=arr[i]; return m; }",
                    SolutionCode = "int Max(int[] arr) { int m=arr[0]; for(int i=1;i<arr.Length;i++) if(arr[i]>m) m=arr[i]; return m; }", Points = 120 },
                new QuestModel { Id = 5, Title = "Взлом: SQL инъекция", Type = "Hack",
                    Description = "Обойди проверку логина. Нужно вернуть true",
                    LegacyCode = "bool CheckLogin(string login) { return login == \"admin\"; }",
                    SolutionCode = "return true;", Points = 180 },
                new QuestModel { Id = 6, Title = "Легаси: Пузырьковая сортировка", Type = "LegacyFix",
                    Description = "Отсортируй массив пузырьком",
                    LegacyCode = "void Sort(int[] arr) { for(int i=0;i<arr.Length;i++) for(int j=0;j<arr.Length;j++) if(arr[j]>arr[j+1]) swap(arr[j],arr[j+1]); }",
                    SolutionCode = "void Sort(int[] arr) { for(int i=0;i<arr.Length-1;i++) for(int j=0;j<arr.Length-i-1;j++) if(arr[j]>arr[j+1]) { int t=arr[j]; arr[j]=arr[j+1]; arr[j+1]=t; } }", Points = 250 },
                new QuestModel { Id = 7, Title = "Легаси: Палиндром", Type = "LegacyFix",
                    Description = "Проверь, является ли строка палиндромом",
                    LegacyCode = "bool IsPal(string s) { for(int i=0;i<s.Length;i++) if(s[i]!=s[s.Length-i]) return false; return true; }",
                    SolutionCode = "bool IsPal(string s) { for(int i=0;i<s.Length/2;i++) if(s[i]!=s[s.Length-1-i]) return false; return true; }", Points = 130 },
                new QuestModel { Id = 8, Title = "Взлом: JWT токен", Type = "Hack",
                    Description = "Подделай проверку токена. Нужно вернуть true",
                    LegacyCode = "bool CheckToken(string token) { return token == \"secret123\"; }",
                    SolutionCode = "return true;", Points = 220 }
            };
            File.WriteAllText(QuestsPath, JsonConvert.SerializeObject(quests, Formatting.Indented));
        }

        public static List<UserModel> GetUsers() => JsonConvert.DeserializeObject<List<UserModel>>(File.ReadAllText(UsersPath)) ?? new List<UserModel>();
        public static void SaveUsers(List<UserModel> users) => File.WriteAllText(UsersPath, JsonConvert.SerializeObject(users, Formatting.Indented));
        public static List<QuestModel> GetQuests() => JsonConvert.DeserializeObject<List<QuestModel>>(File.ReadAllText(QuestsPath)) ?? new List<QuestModel>();
    }
}