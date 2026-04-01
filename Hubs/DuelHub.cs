using Microsoft.AspNetCore.SignalR;
using CodeDuelArena.Models;
using CodeDuelArena.Data;

namespace CodeDuelArena.Hubs
{
    public class DuelHub : Hub
    {
        public async Task RegisterUser(string username)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            
            if (user == null)
            {
                user = new UserModel { ConnectionId = Context.ConnectionId, Username = username };
                users.Add(user);
                DataStorage.SaveUsers(users);
            }
            else
            {
                user.Username = username;
                DataStorage.SaveUsers(users);
            }
            
            await Clients.Caller.SendAsync("UserRegistered", user);
            await Clients.All.SendAsync("UpdateLeaderboard", users.OrderByDescending(u => u.Score).Take(10));
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

            bool isCorrect = solutionCode.Contains(quest.SolutionCode) || 
                            solutionCode.Trim().Replace(" ", "") == quest.SolutionCode.Replace(" ", "");

            if (isCorrect && !user.CompletedQuests.Contains(quest.Id.ToString()))
            {
                user.Score += quest.Points;
                user.CompletedQuests.Add(quest.Id.ToString());
                DataStorage.SaveUsers(users);
                
                await Clients.Caller.SendAsync("QuestResult", new { success = true, message = $"✅ +{quest.Points} очков", newScore = user.Score });
                await Clients.All.SendAsync("UpdateLeaderboard", users.OrderByDescending(u => u.Score).Take(10));
            }
            else
            {
                await Clients.Caller.SendAsync("QuestResult", new { success = false, message = "❌ Неверно" });
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
                await Clients.Caller.SendAsync("QueueJoined", "В очереди...");
                await TryMatchUsers();
            }
        }

        private async Task TryMatchUsers()
        {
            var users = DataStorage.GetUsers();
            var queue = users.Where(u => u.IsInQueue && u.CurrentDuelId == -1).ToList();
            
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
                
                await Clients.Client(p1.ConnectionId).SendAsync("DuelStarted", new { duelId, opponent = p2.Username, task = "Напиши функцию суммы чисел от 1 до n" });
                await Clients.Client(p2.ConnectionId).SendAsync("DuelStarted", new { duelId, opponent = p1.Username, task = "Напиши функцию суммы чисел от 1 до n" });
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
                
                await Clients.Caller.SendAsync("DuelResult", new { success = true, message = "Победа! +100 очков", newScore = user.Score });
                await Clients.All.SendAsync("UpdateLeaderboard", users.OrderByDescending(u => u.Score).Take(10));
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            if (user != null)
            {
                users.Remove(user);
                DataStorage.SaveUsers(users);
                await Clients.All.SendAsync("UpdateLeaderboard", users.OrderByDescending(u => u.Score).Take(10));
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}