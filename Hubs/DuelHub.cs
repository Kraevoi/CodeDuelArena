using Microsoft.AspNetCore.SignalR;
using CodeDuelArena.Models;
using CodeDuelArena.Data;
using System.Threading.Tasks;
using System.Linq;
using System;

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
                user = new UserModel 
                { 
                    ConnectionId = Context.ConnectionId, 
                    Username = string.IsNullOrWhiteSpace(username) ? $"Warrior_{new Random().Next(1000,9999)}" : username 
                };
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
                
                await Clients.Client(p1.ConnectionId).SendAsync("DuelStarted", new { 
                    duelId, 
                    opponent = p2.Username,
                    task = GenerateDuelTask()
                });
                await Clients.Client(p2.ConnectionId).SendAsync("DuelStarted", new { 
                    duelId, 
                    opponent = p1.Username,
                    task = GenerateDuelTask()
                });
                await Clients.All.SendAsync("SystemMessage", $"⚔️ ДУЭЛЬ НАЧАЛАСЬ: {p1.Username} VS {p2.Username}!");
                
                // Запускаем таймер на 60 секунд
                _ = Task.Run(async () => {
                    await Task.Delay(60000);
                    var currentUsers = DataStorage.GetUsers();
                    var u1 = currentUsers.FirstOrDefault(u => u.ConnectionId == p1.ConnectionId);
                    var u2 = currentUsers.FirstOrDefault(u => u.ConnectionId == p2.ConnectionId);
                    if (u1 != null && u1.CurrentDuelId == duelId)
                    {
                        u1.CurrentDuelId = -1;
                        u1.Losses++;
                        DataStorage.SaveUsers(currentUsers);
                        await Clients.Client(p1.ConnectionId).SendAsync("DuelTimeout", "Время вышло! Ты проиграл!");
                    }
                    if (u2 != null && u2.CurrentDuelId == duelId)
                    {
                        u2.CurrentDuelId = -1;
                        u2.Losses++;
                        DataStorage.SaveUsers(currentUsers);
                        await Clients.Client(p2.ConnectionId).SendAsync("DuelTimeout", "Время вышло! Ты проиграл!");
                    }
                });
            }
        }

        private string GenerateDuelTask()
        {
            var tasks = new[]
            {
                "Напиши функцию, которая возвращает сумму чисел от 1 до n",
                "Напиши функцию, которая проверяет, является ли число простым",
                "Напиши функцию, которая возвращает n-ное число Фибоначчи",
                "Напиши функцию, которая разворачивает строку"
            };
            return tasks[new Random().Next(tasks.Length)];
        }

        public async Task SubmitDuelSolution(string solution, int duelId)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            
            if (user != null && user.CurrentDuelId == duelId)
            {
                // Простая проверка решения
                bool isCorrect = !string.IsNullOrWhiteSpace(solution) && solution.Length > 5;
                
                if (isCorrect)
                {
                    user.CurrentDuelId = -1;
                    user.Wins++;
                    user.Score += 100;
                    DataStorage.SaveUsers(users);
                    
                    await Clients.Caller.SendAsync("DuelResult", new { 
                        success = true, 
                        message = "Ты победил в дуэли! +100 очков",
                        newScore = user.Score
                    });
                    await Clients.All.SendAsync("UpdateLeaderboard", users.OrderByDescending(u => u.Score).Take(10));
                }
                else
                {
                    await Clients.Caller.SendAsync("DuelResult", new { 
                        success = false, 
                        message = "Решение неверное! Попробуй еще."
                    });
                }
            }
        }

        public async Task SubmitQuestSolution(string solutionCode, int questId)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            var quest = DataStorage.GetQuests().FirstOrDefault(q => q.Id == questId);
            
            if (user == null || quest == null)
                return;

            // Проверка решения
            bool isCorrect = solutionCode.Contains(quest.SolutionCode) || 
                            solutionCode.Trim().Replace(" ", "") == quest.SolutionCode.Replace(" ", "");

            if (isCorrect && !user.CompletedQuests.Contains(quest.Id.ToString()))
            {
                user.Score += quest.Points;
                user.CompletedQuests.Add(quest.Id.ToString());
                DataStorage.SaveUsers(users);
                
                await Clients.Caller.SendAsync("QuestResult", new 
                { 
                    success = true, 
                    message = $"✅ Квест выполнен! +{quest.Points} очков", 
                    newScore = user.Score 
                });
                await Clients.All.SendAsync("UpdateLeaderboard", users.OrderByDescending(u => u.Score).Take(10));
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} прошел квест '{quest.Title}' и получил {quest.Points} очков!");
            }
            else if (isCorrect && user.CompletedQuests.Contains(quest.Id.ToString()))
            {
                await Clients.Caller.SendAsync("QuestResult", new { success = false, message = "Ты уже прошел этот квест!" });
            }
            else
            {
                await Clients.Caller.SendAsync("QuestResult", new { success = false, message = "❌ Решение неверное. Попробуй еще." });
            }
        }

        public async Task SendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
                
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

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);
            
            if (user != null)
            {
                users.Remove(user);
                DataStorage.SaveUsers(users);
                await Clients.All.SendAsync("UpdateLeaderboard", users.OrderByDescending(u => u.Score).Take(10));
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} покинул арену...");
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}