using Microsoft.AspNetCore.SignalR;
using CodeDuelArena.Models;
using CodeDuelArena.Data;

namespace CodeDuelArena.Hubs
{
    public class DuelHub : Hub
    {
        public async Task RegisterUser(string username)
        {
            var account = UserAccounts.GetByUsername(username);
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            
            if (user == null)
            {
                user = new UserModel 
                { 
                    ConnectionId = Context.ConnectionId, 
                    Username = username,
                    Score = account?.Score ?? 0,
                    Wins = account?.Wins ?? 0,
                    Losses = account?.Losses ?? 0,
                    CompletedQuests = account?.CompletedQuests ?? new List<string>()
                };
                users.Add(user);
                DataStorage.SaveUsers(users);
            }
            else
            {
                user.Username = username;
                user.Score = account?.Score ?? 0;
                user.Wins = account?.Wins ?? 0;
                user.Losses = account?.Losses ?? 0;
                user.CompletedQuests = account?.CompletedQuests ?? new List<string>();
                DataStorage.SaveUsers(users);
            }
            
            await Clients.Caller.SendAsync("UserRegistered", user);
            await UpdateAllUsersLeaderboard();
            await Clients.All.SendAsync("SystemMessage", $"{user.Username} вступил на арену!");
        }

        public async Task SendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            
            if (user != null)
            {
                var chatMsg = new ChatMessage 
                { 
                    User = user.Username, 
                    Text = message, 
                    Time = DateTime.Now.ToString("HH:mm") 
                };
                await Clients.All.SendAsync("ReceiveChatMessage", chatMsg);
            }
        }

        public async Task SubmitQuestSolution(string solutionCode, int questId)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            var quest = DataStorage.GetQuests().FirstOrDefault(q => q.Id == questId);
            
            if (user == null || quest == null) return;

            string questIdStr = questId.ToString();
            
            bool isCorrect = solutionCode.Contains(quest.SolutionCode) || 
                            solutionCode.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "") == quest.SolutionCode.Replace(" ", "").Replace("\n", "").Replace("\r", "");

            if (isCorrect && !user.CompletedQuests.Contains(questIdStr))
            {
                user.Score += quest.Points;
                user.CompletedQuests.Add(questIdStr);
                DataStorage.SaveUsers(users);
                
                UserAccounts.UpdateStats(user.Username, quest.Points, false, false, questIdStr);
                
                await Clients.Caller.SendAsync("QuestResult", new { success = true, message = $"✅ +{quest.Points} очков!", newScore = user.Score });
                await UpdateAllUsersLeaderboard();
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} прошел квест '{quest.Title}' и получил {quest.Points} очков!");
            }
            else if (isCorrect && user.CompletedQuests.Contains(questIdStr))
            {
                await Clients.Caller.SendAsync("QuestResult", new { success = false, message = "Ты уже прошел этот квест!" });
            }
            else
            {
                await Clients.Caller.SendAsync("QuestResult", new { success = false, message = "❌ Решение неверное. Попробуй еще!" });
            }
        }

        public async Task JoinDuelQueue()
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            
            if (user != null && !user.IsInQueue && user.CurrentDuelId == -1)
            {
                user.IsInQueue = true;
                DataStorage.SaveUsers(users);
                await Clients.Caller.SendAsync("QueueJoined", "Ты в очереди на дуэль!");
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} ищет противника...");
                await TryMatchUsers();
            }
        }

        private async Task TryMatchUsers()
        {
            var users = DataStorage.GetUsers();
            var queue = users.Where(u => u.IsInQueue && u.CurrentDuelId == -1).ToList();
            
            var tasks = new[] { 
                "Напиши функцию, которая возвращает сумму чисел от 1 до n",
                "Напиши функцию, которая проверяет, является ли число простым",
                "Напиши функцию, которая возвращает n-ное число Фибоначчи",
                "Напиши функцию, которая разворачивает строку"
            };
            
            for (int i = 0; i < queue.Count - 1; i += 2)
            {
                var p1 = queue[i];
                var p2 = queue[i + 1];
                
                p1.IsInQueue = false;
                p2.IsInQueue = false;
                int duelId = new Random().Next(10000, 99999);
                p1.CurrentDuelId = duelId;
                p2.CurrentDuelId = duelId;
                DataStorage.SaveUsers(users);
                
                var task = tasks[new Random().Next(tasks.Length)];
                
                await Clients.Client(p1.ConnectionId).SendAsync("DuelStarted", new { duelId, opponent = p2.Username, task });
                await Clients.Client(p2.ConnectionId).SendAsync("DuelStarted", new { duelId, opponent = p1.Username, task });
                await Clients.All.SendAsync("SystemMessage", $"⚔️ ДУЭЛЬ НАЧАЛАСЬ: {p1.Username} VS {p2.Username}!");
            }
        }

        public async Task SubmitDuelSolution(string solution, int duelId)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            
            if (user != null && user.CurrentDuelId == duelId)
            {
                user.CurrentDuelId = -1;
                user.Wins++;
                user.Score += 100;
                DataStorage.SaveUsers(users);
                
                UserAccounts.UpdateStats(user.Username, 100, true, false);
                
                await Clients.Caller.SendAsync("DuelResult", new { success = true, message = "Победа! +100 очков!", newScore = user.Score });
                await UpdateAllUsersLeaderboard();
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} победил в дуэли!");
            }
        }

        private async Task UpdateAllUsersLeaderboard()
        {
            var allUsers = UserAccounts.GetAllUsers();
            var leaderboard = allUsers.OrderByDescending(u => u.Score).Take(20).Select(u => new 
            { 
                u.Username, 
                u.Score, 
                u.Wins, 
                u.Losses,
                CompletedQuests = u.CompletedQuests
            });
            await Clients.All.SendAsync("UpdateLeaderboard", leaderboard);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            if (user != null)
            {
                users.Remove(user);
                DataStorage.SaveUsers(users);
                await UpdateAllUsersLeaderboard();
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} покинул арену...");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}