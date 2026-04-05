using Microsoft.AspNetCore.SignalR;
using CodeDuelArena.Models;
using CodeDuelArena.Data;
using Microsoft.EntityFrameworkCore;

namespace CodeDuelArena.Hubs
{
    public class DuelHub : Hub
    {
        private readonly AppDbContext _db;
        private static readonly Dictionary<string, DuelRoom> _activeDuels = new();
        private static readonly Queue<string> _duelQueue = new();

        public DuelHub(AppDbContext db)
        {
            _db = db;
        }

        public async Task RegisterUser(string username)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (user == null)
            {
                user = new UserModel
                {
                    ConnectionId = Context.ConnectionId,
                    Username = username,
                    Score = 0,
                    Wins = 0,
                    Losses = 0,
                    CompletedQuests = new List<string>()
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
            await UpdateLeaderboard();
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
                await LogActivity(user.Username, "Отправил сообщение в чат", message);
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

                await LogActivity(user.Username, "Прошел квест", $"{quest.Title} +{quest.Points} очков");

                await Clients.Caller.SendAsync("QuestResult", new { success = true, message = $"✅ +{quest.Points} очков!", newScore = user.Score });
                await UpdateLeaderboard();
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

            if (user == null || user.IsInQueue || user.CurrentDuelId != -1)
            {
                await Clients.Caller.SendAsync("QueueError", "Вы уже в очереди или в дуэли");
                return;
            }

            user.IsInQueue = true;
            DataStorage.SaveUsers(users);

            await Clients.Caller.SendAsync("QueueJoined", "Ты в очереди на дуэль!");
            await Clients.All.SendAsync("SystemMessage", $"{user.Username} ищет противника...");

            _duelQueue.Enqueue(user.ConnectionId);
            await TryMatchUsers();
        }

        private async Task TryMatchUsers()
        {
            if (_duelQueue.Count < 2) return;

            var p1Id = _duelQueue.Dequeue();
            var p2Id = _duelQueue.Dequeue();

            var users = DataStorage.GetUsers();
            var p1 = users.FirstOrDefault(u => u.ConnectionId == p1Id);
            var p2 = users.FirstOrDefault(u => u.ConnectionId == p2Id);

            if (p1 == null || p2 == null)
            {
                if (p1 != null) p1.IsInQueue = false;
                if (p2 != null) p2.IsInQueue = false;
                DataStorage.SaveUsers(users);
                await TryMatchUsers();
                return;
            }

            // Получаем случайное задание из БД
            var tasks = await _db.DuelTasks.Where(t => t.IsActive).ToListAsync();
            if (!tasks.Any())
            {
                await Clients.Caller.SendAsync("SystemMessage", "Нет активных заданий для дуэли. Админ добавит скоро.");
                p1.IsInQueue = false;
                p2.IsInQueue = false;
                DataStorage.SaveUsers(users);
                return;
            }

            var random = new Random();
            var task = tasks[random.Next(tasks.Count)];
            int duelId = random.Next(10000, 99999);

            p1.IsInQueue = false;
            p2.IsInQueue = false;
            p1.CurrentDuelId = duelId;
            p2.CurrentDuelId = duelId;

            DataStorage.SaveUsers(users);

            var duelRoom = new DuelRoom
            {
                DuelId = duelId,
                Player1Id = p1.ConnectionId,
                Player2Id = p2.ConnectionId,
                Player1Name = p1.Username,
                Player2Name = p2.Username,
                Task = task,
                StartTime = DateTime.UtcNow,
                Player1Submitted = false,
                Player2Submitted = false
            };
            _activeDuels[duelId.ToString()] = duelRoom;

            await Clients.Client(p1.ConnectionId).SendAsync("DuelStarted", new
            {
                duelId,
                opponent = p2.Username,
                taskTitle = task.Title,
                taskDescription = task.Description,
                testCode = task.TestCode,
                expectedOutput = task.ExpectedOutput,
                timeLimit = 60
            });

            await Clients.Client(p2.ConnectionId).SendAsync("DuelStarted", new
            {
                duelId,
                opponent = p1.Username,
                taskTitle = task.Title,
                taskDescription = task.Description,
                testCode = task.TestCode,
                expectedOutput = task.ExpectedOutput,
                timeLimit = 60
            });

            await Clients.All.SendAsync("SystemMessage", $"⚔️ ДУЭЛЬ НАЧАЛАСЬ: {p1.Username} VS {p2.Username}!");
            await LogActivity(p1.Username, "Начал дуэль", $"Противник: {p2.Username}, задание: {task.Title}");
            await LogActivity(p2.Username, "Начал дуэль", $"Противник: {p1.Username}, задание: {task.Title}");

            // Таймер 60 секунд
            _ = Task.Run(async () =>
            {
                await Task.Delay(60000);
                if (_activeDuels.TryGetValue(duelId.ToString(), out var room))
                {
                    var currentUsers = DataStorage.GetUsers();
                    var u1 = currentUsers.FirstOrDefault(u => u.ConnectionId == room.Player1Id);
                    var u2 = currentUsers.FirstOrDefault(u => u.ConnectionId == room.Player2Id);

                    if (u1 != null && u1.CurrentDuelId == duelId)
                    {
                        u1.CurrentDuelId = -1;
                        u1.Losses++;
                        DataStorage.SaveUsers(currentUsers);
                        await Clients.Client(room.Player1Id).SendAsync("DuelTimeout", "Время вышло! Ты проиграл!");
                        await LogActivity(u1.Username, "Проиграл дуэль по таймауту", "");
                    }
                    if (u2 != null && u2.CurrentDuelId == duelId)
                    {
                        u2.CurrentDuelId = -1;
                        u2.Losses++;
                        DataStorage.SaveUsers(currentUsers);
                        await Clients.Client(room.Player2Id).SendAsync("DuelTimeout", "Время вышло! Ты проиграл!");
                        await LogActivity(u2.Username, "Проиграл дуэль по таймауту", "");
                    }
                    _activeDuels.Remove(duelId.ToString());
                    await UpdateLeaderboard();
                }
            });
        }

        public async Task SubmitDuelSolution(string solution, int duelId)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (user == null || user.CurrentDuelId != duelId) return;

            if (!_activeDuels.TryGetValue(duelId.ToString(), out var room)) return;

            bool isWinner = false;
            string winnerName = "";
            string loserName = "";

            if (room.Player1Id == Context.ConnectionId)
            {
                if (room.Player1Submitted) return;
                room.Player1Submitted = true;
                room.Player1Solution = solution;
                isWinner = room.Player2Submitted;
                winnerName = room.Player1Name;
                loserName = room.Player2Name;
            }
            else if (room.Player2Id == Context.ConnectionId)
            {
                if (room.Player2Submitted) return;
                room.Player2Submitted = true;
                room.Player2Solution = solution;
                isWinner = room.Player1Submitted;
                winnerName = room.Player2Name;
                loserName = room.Player1Name;
            }
            else return;

            if (isWinner)
            {
                // Проверка решений
                var p1Valid = ValidateSolution(room.Player1Solution, room.Task);
                var p2Valid = ValidateSolution(room.Player2Solution, room.Task);

                var winnerConn = p1Valid && !p2Valid ? room.Player1Id :
                                 p2Valid && !p1Valid ? room.Player2Id :
                                 p1Valid && p2Valid ? (room.Player1Submitted ? room.Player1Id : room.Player2Id) : null;

                if (winnerConn == null)
                {
                    // Оба проиграли или оба неправильно
                    await FinishDuel(duelId, null, null);
                }
                else
                {
                    var winnerUser = users.FirstOrDefault(u => u.ConnectionId == winnerConn);
                    var loserUser = users.FirstOrDefault(u => u.ConnectionId == (winnerConn == room.Player1Id ? room.Player2Id : room.Player1Id));

                    if (winnerUser != null && loserUser != null)
                    {
                        await FinishDuel(duelId, winnerUser, loserUser);
                    }
                }
            }
            else
            {
                _activeDuels[duelId.ToString()] = room;
                await Clients.Caller.SendAsync("DuelStatus", "Решение принято. Ожидаем ответа соперника...");
            }
        }

        private bool ValidateSolution(string solution, DuelTask task)
        {
            if (string.IsNullOrWhiteSpace(solution)) return false;
            return solution.Contains(task.ExpectedOutput) || 
                   solution.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "") == task.ExpectedOutput.Replace(" ", "").Replace("\n", "").Replace("\r", "");
        }

        private async Task FinishDuel(int duelId, UserModel? winner, UserModel? loser)
        {
            if (!_activeDuels.TryGetValue(duelId.ToString(), out var room)) return;

            var users = DataStorage.GetUsers();

            if (winner != null && loser != null)
            {
                winner.CurrentDuelId = -1;
                winner.Wins++;
                winner.Score += 100;
                loser.CurrentDuelId = -1;
                loser.Losses++;
                DataStorage.SaveUsers(users);

                await Clients.Client(room.Player1Id).SendAsync("DuelResult", new
                {
                    success = winner.ConnectionId == room.Player1Id,
                    message = winner.ConnectionId == room.Player1Id ? "Победа! +100 очков!" : "Поражение!",
                    newScore = winner.ConnectionId == room.Player1Id ? winner.Score : loser.Score
                });

                await Clients.Client(room.Player2Id).SendAsync("DuelResult", new
                {
                    success = winner.ConnectionId == room.Player2Id,
                    message = winner.ConnectionId == room.Player2Id ? "Победа! +100 очков!" : "Поражение!",
                    newScore = winner.ConnectionId == room.Player2Id ? winner.Score : loser.Score
                });

                await LogActivity(winner.Username, "Победил в дуэли", $"Противник: {loser.Username}");
                await Clients.All.SendAsync("SystemMessage", $"🏆 {winner.Username} победил в дуэли!");
            }
            else
            {
                // Ничья или оба проиграли
                var u1 = users.FirstOrDefault(u => u.ConnectionId == room.Player1Id);
                var u2 = users.FirstOrDefault(u => u.ConnectionId == room.Player2Id);

                if (u1 != null) u1.CurrentDuelId = -1;
                if (u2 != null) u2.CurrentDuelId = -1;
                DataStorage.SaveUsers(users);

                await Clients.Client(room.Player1Id).SendAsync("DuelResult", new { success = false, message = "Ничья! Оба решения неверны.", newScore = 0 });
                await Clients.Client(room.Player2Id).SendAsync("DuelResult", new { success = false, message = "Ничья! Оба решения неверны.", newScore = 0 });
                await Clients.All.SendAsync("SystemMessage", "Ничья в дуэли! Оба решения неверны.");
            }

            _activeDuels.Remove(duelId.ToString());
            await UpdateLeaderboard();
        }

        private async Task UpdateLeaderboard()
        {
            var users = DataStorage.GetUsers();
            var leaderboard = users.OrderByDescending(u => u.Score).Take(10);
            await Clients.All.SendAsync("UpdateLeaderboard", leaderboard);
        }

        private async Task LogActivity(string username, string action, string details)
        {
            var log = new ActivityLog
            {
                UserName = username,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow,
                IpAddress = "SignalR"
            };
            _db.ActivityLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (user != null)
            {
                if (user.IsInQueue)
                {
                    var newQueue = new Queue<string>();
                    while (_duelQueue.Count > 0)
                    {
                        var item = _duelQueue.Dequeue();
                        if (item != user.ConnectionId) newQueue.Enqueue(item);
                    }
                    while (newQueue.Count > 0) _duelQueue.Enqueue(newQueue.Dequeue());
                }

                users.Remove(user);
                DataStorage.SaveUsers(users);
                await UpdateLeaderboard();
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} покинул арену...");
                await LogActivity(user.Username, "Покинул сайт", "");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }

    public class DuelRoom
    {
        public int DuelId { get; set; }
        public string Player1Id { get; set; } = string.Empty;
        public string Player2Id { get; set; } = string.Empty;
        public string Player1Name { get; set; } = string.Empty;
        public string Player2Name { get; set; } = string.Empty;
        public DuelTask Task { get; set; } = new DuelTask();
        public DateTime StartTime { get; set; }
        public bool Player1Submitted { get; set; }
        public bool Player2Submitted { get; set; }
        public string Player1Solution { get; set; } = string.Empty;
        public string Player2Solution { get; set; } = string.Empty;
    }
}