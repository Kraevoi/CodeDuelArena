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
        private static readonly Queue<PlayerInfo> _duelQueue = new();

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
            }
            else
            {
                user.Username = username;
            }
            DataStorage.SaveUsers(users);

            await Clients.Caller.SendAsync("UserRegistered", user);
            await UpdateLeaderboard();
            await Clients.All.SendAsync("SystemMessage", $"{user.Username} has entered the arena!");
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
                await LogActivity(user.Username, "Chat message sent", message);
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

                await LogActivity(user.Username, "Quest completed", $"{quest.Title} +{quest.Points} points");

                await Clients.Caller.SendAsync("QuestResult", new { success = true, message = $"+{quest.Points} points!", newScore = user.Score });
                await UpdateLeaderboard();
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} completed quest '{quest.Title}' and earned {quest.Points} points!");
            }
            else if (isCorrect && user.CompletedQuests.Contains(questIdStr))
            {
                await Clients.Caller.SendAsync("QuestResult", new { success = false, message = "You have already completed this quest!" });
            }
            else
            {
                await Clients.Caller.SendAsync("QuestResult", new { success = false, message = "Incorrect solution. Try again!" });
            }
        }

        public async Task JoinDuelQueue()
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (user == null || user.IsInQueue || user.CurrentDuelId != -1)
            {
                await Clients.Caller.SendAsync("QueueError", "You are already in queue or in a duel");
                return;
            }

            user.IsInQueue = true;
            DataStorage.SaveUsers(users);

            _duelQueue.Enqueue(new PlayerInfo { ConnectionId = user.ConnectionId, Username = user.Username });

            await Clients.Caller.SendAsync("QueueJoined", "Searching for opponent...");
            await Clients.All.SendAsync("SystemMessage", $"{user.Username} is looking for a duel...");

            await TryMatchUsers();
        }

        private async Task TryMatchUsers()
        {
            if (_duelQueue.Count < 2) return;

            var p1 = _duelQueue.Dequeue();
            var p2 = _duelQueue.Dequeue();

            var users = DataStorage.GetUsers();
            var user1 = users.FirstOrDefault(u => u.ConnectionId == p1.ConnectionId);
            var user2 = users.FirstOrDefault(u => u.ConnectionId == p2.ConnectionId);

            if (user1 == null || user2 == null)
            {
                if (user1 != null) user1.IsInQueue = false;
                if (user2 != null) user2.IsInQueue = false;
                DataStorage.SaveUsers(users);
                await TryMatchUsers();
                return;
            }

            var tasks = await _db.DuelTasks.Where(t => t.IsActive).ToListAsync();
            if (!tasks.Any())
            {
                await Clients.Client(p1.ConnectionId).SendAsync("QueueError", "No active duel tasks available.");
                await Clients.Client(p2.ConnectionId).SendAsync("QueueError", "No active duel tasks available.");
                user1.IsInQueue = false;
                user2.IsInQueue = false;
                DataStorage.SaveUsers(users);
                return;
            }

            var random = new Random();
            var task = tasks[random.Next(tasks.Count)];
            int duelId = random.Next(10000, 99999);

            user1.IsInQueue = false;
            user2.IsInQueue = false;
            user1.CurrentDuelId = duelId;
            user2.CurrentDuelId = duelId;

            DataStorage.SaveUsers(users);

            var duelRoom = new DuelRoom
            {
                DuelId = duelId,
                Player1Id = user1.ConnectionId,
                Player2Id = user2.ConnectionId,
                Player1Name = user1.Username,
                Player2Name = user2.Username,
                Task = task,
                StartTime = DateTime.UtcNow,
                Player1Submitted = false,
                Player2Submitted = false,
                WinnerDeclared = false
            };
            _activeDuels[duelId.ToString()] = duelRoom;

            await Clients.Client(user1.ConnectionId).SendAsync("DuelStarted", new
            {
                duelId,
                opponent = user2.Username,
                taskTitle = task.Title,
                taskDescription = task.Description,
                testCode = task.TestCode,
                expectedOutput = task.ExpectedOutput,
                timeLimit = 60
            });

            await Clients.Client(user2.ConnectionId).SendAsync("DuelStarted", new
            {
                duelId,
                opponent = user1.Username,
                taskTitle = task.Title,
                taskDescription = task.Description,
                testCode = task.TestCode,
                expectedOutput = task.ExpectedOutput,
                timeLimit = 60
            });

            await Clients.All.SendAsync("SystemMessage", $"DUEL STARTED: {user1.Username} vs {user2.Username}!");
            await LogActivity(user1.Username, "Duel started", $"Opponent: {user2.Username}, Task: {task.Title}");
            await LogActivity(user2.Username, "Duel started", $"Opponent: {user1.Username}, Task: {task.Title}");

            _ = Task.Run(async () =>
            {
                await Task.Delay(60000);
                if (_activeDuels.TryGetValue(duelId.ToString(), out var room) && !room.WinnerDeclared)
                {
                    var currentUsers = DataStorage.GetUsers();
                    var u1 = currentUsers.FirstOrDefault(u => u.ConnectionId == room.Player1Id);
                    var u2 = currentUsers.FirstOrDefault(u => u.ConnectionId == room.Player2Id);

                    if (u1 != null && u1.CurrentDuelId == duelId)
                    {
                        u1.CurrentDuelId = -1;
                        u1.Losses++;
                        DataStorage.SaveUsers(currentUsers);
                        await Clients.Client(room.Player1Id).SendAsync("DuelTimeout", "Time is up! You lost!");
                        await LogActivity(u1.Username, "Duel lost by timeout", "");
                    }
                    if (u2 != null && u2.CurrentDuelId == duelId)
                    {
                        u2.CurrentDuelId = -1;
                        u2.Losses++;
                        DataStorage.SaveUsers(currentUsers);
                        await Clients.Client(room.Player2Id).SendAsync("DuelTimeout", "Time is up! You lost!");
                        await LogActivity(u2.Username, "Duel lost by timeout", "");
                    }
                    _activeDuels.Remove(duelId.ToString());
                    await UpdateLeaderboard();
                }
            });
        }

        public async Task LeaveDuelQueue()
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (user != null && user.IsInQueue)
            {
                user.IsInQueue = false;
                DataStorage.SaveUsers(users);
                var newQueue = new Queue<PlayerInfo>(_duelQueue.Where(q => q.ConnectionId != Context.ConnectionId));
                _duelQueue.Clear();
                foreach (var item in newQueue) _duelQueue.Enqueue(item);
                await Clients.Caller.SendAsync("QueueLeft", "You have left the duel queue.");
            }
        }

        public async Task CancelDuel(int duelId)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (user == null || user.CurrentDuelId != duelId) return;

            if (_activeDuels.TryGetValue(duelId.ToString(), out var room) && !room.WinnerDeclared)
            {
                room.WinnerDeclared = true;
                var opponentId = room.Player1Id == Context.ConnectionId ? room.Player2Id : room.Player1Id;
                var opponent = users.FirstOrDefault(u => u.ConnectionId == opponentId);

                if (opponent != null)
                {
                    opponent.CurrentDuelId = -1;
                    opponent.Wins++;
                    opponent.Score += 50;
                }

                user.CurrentDuelId = -1;
                user.Losses++;
                DataStorage.SaveUsers(users);

                await Clients.Client(Context.ConnectionId).SendAsync("DuelCancelled", "You have left the duel. Opponent wins!");
                await Clients.Client(opponentId).SendAsync("DuelResult", new { success = true, message = "Opponent left! You win! +50 points!", newScore = opponent?.Score ?? 0 });
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} has left the duel. {opponent?.Username ?? "Opponent"} wins!");

                _activeDuels.Remove(duelId.ToString());
                await UpdateLeaderboard();
            }
        }

        public async Task SubmitDuelSolution(string solution, int duelId)
        {
            var users = DataStorage.GetUsers();
            var user = users.FirstOrDefault(u => u.ConnectionId == Context.ConnectionId);

            if (user == null || user.CurrentDuelId != duelId) return;
            if (!_activeDuels.TryGetValue(duelId.ToString(), out var room)) return;
            if (room.WinnerDeclared) return;

            bool isMySolution = false;

            if (room.Player1Id == Context.ConnectionId)
            {
                if (room.Player1Submitted) return;
                room.Player1Submitted = true;
                room.Player1Solution = solution;
                isMySolution = true;
            }
            else if (room.Player2Id == Context.ConnectionId)
            {
                if (room.Player2Submitted) return;
                room.Player2Submitted = true;
                room.Player2Solution = solution;
                isMySolution = true;
            }
            else return;

            if (isMySolution)
            {
                bool mySolutionValid = ValidateSolution(solution, room.Task);

                if (mySolutionValid && !room.WinnerDeclared)
                {
                    room.WinnerDeclared = true;

                    var winnerUser = user;
                    var loserId = room.Player1Id == Context.ConnectionId ? room.Player2Id : room.Player1Id;
                    var loserUser = users.FirstOrDefault(u => u.ConnectionId == loserId);

                    winnerUser.CurrentDuelId = -1;
                    winnerUser.Wins++;
                    winnerUser.Score += 100;

                    if (loserUser != null)
                    {
                        loserUser.CurrentDuelId = -1;
                        loserUser.Losses++;
                    }

                    DataStorage.SaveUsers(users);

                    await Clients.Client(Context.ConnectionId).SendAsync("DuelResult", new { success = true, message = "Victory! +100 points!", newScore = winnerUser.Score });
                    await Clients.Client(loserId).SendAsync("DuelResult", new { success = false, message = "Defeat! Opponent answered first!", newScore = loserUser?.Score ?? 0 });
                    await Clients.All.SendAsync("SystemMessage", $"{winnerUser.Username} wins the duel against {loserUser?.Username ?? "opponent"}!");

                    await LogActivity(winnerUser.Username, "Duel won", $"Opponent: {loserUser?.Username}");
                    await LogActivity(loserUser?.Username ?? "Unknown", "Duel lost", $"Opponent: {winnerUser.Username}");

                    _activeDuels.Remove(duelId.ToString());
                    await UpdateLeaderboard();
                }
                else if (!mySolutionValid)
                {
                    await Clients.Caller.SendAsync("DuelStatus", "Incorrect solution! Try again...");
                    if (room.Player1Id == Context.ConnectionId) room.Player1Submitted = false;
                    else room.Player2Submitted = false;
                }
                else
                {
                    _activeDuels[duelId.ToString()] = room;
                    await Clients.Caller.SendAsync("DuelStatus", "Solution submitted! Waiting for opponent...");
                }
            }
        }

        private bool ValidateSolution(string solution, DuelTask task)
        {
            if (string.IsNullOrWhiteSpace(solution)) return false;
            return solution.Contains(task.ExpectedOutput) ||
                   solution.Trim().Replace(" ", "").Replace("\n", "").Replace("\r", "") == task.ExpectedOutput.Replace(" ", "").Replace("\n", "").Replace("\r", "");
        }

        private async Task UpdateLeaderboard()
        {
            var users = DataStorage.GetUsers();
            var leaderboard = users.OrderByDescending(u => u.Score).Take(20);
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
                    var newQueue = new Queue<PlayerInfo>(_duelQueue.Where(q => q.ConnectionId != user.ConnectionId));
                    _duelQueue.Clear();
                    foreach (var item in newQueue) _duelQueue.Enqueue(item);
                }

                users.Remove(user);
                DataStorage.SaveUsers(users);
                await UpdateLeaderboard();
                await Clients.All.SendAsync("SystemMessage", $"{user.Username} has left the arena...");
                await LogActivity(user.Username, "Disconnected", "");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }

    public class PlayerInfo
    {
        public string ConnectionId { get; set; } = "";
        public string Username { get; set; } = "";
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
        public bool WinnerDeclared { get; set; }
    }
}