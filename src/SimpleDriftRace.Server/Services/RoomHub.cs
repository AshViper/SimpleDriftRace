using MagicOnion.Server.Hubs;
using Shared.Services;
using UnityEngine;


namespace Server.Services
{
    public class RoomHub
        : StreamingHubBase<IRoomHub, IRoomHubReceiver>, IRoomHub
    {
        private readonly ILogger<RoomHub> logger;
        private readonly RoomContextRepository roomContextRepository;

        private RoomContext roomContext;

        public RoomHub(
            ILogger<RoomHub> logger,
            RoomContextRepository roomContextRepository)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.roomContextRepository =
                roomContextRepository ?? throw new ArgumentNullException(nameof(roomContextRepository));
        }

        // =========================
        // 接続時
        // =========================
        protected override ValueTask OnConnected()
        {
            logger.LogInformation(
                "Client connected. ConnectionId={ConnectionId}",
                ConnectionId);

            return default;
        }

        // =========================
        // ルーム参加
        // =========================
        public async Task<JoinedUser[]> JoinAsync(string roomName, string userName)
        {
            bool isRoomCreated = false;

            logger.LogInformation(
                "Join request. Room={Room}, User={User}, ConnectionId={ConnectionId}",
                roomName, userName, ConnectionId);

            var joinedUser = new JoinedUser
            {
                ConnectionId = ConnectionId,
                UserName = userName,
                IsOwner = false,
                IsReady = false
            };

            // Room 取得 or 作成（排他）
            lock (roomContextRepository)
            {
                roomContext = roomContextRepository.GetContext(roomName);
                if (roomContext == null)
                {
                    roomContext = roomContextRepository.CreateContext(roomName);
                    isRoomCreated = true;
                    joinedUser = new JoinedUser
                    {
                        ConnectionId = ConnectionId,
                        UserName = userName,
                        IsOwner = true,
                        IsReady = true
                    };
                }
            }

            if (isRoomCreated)
            {
                logger.LogInformation("Room created. Room={Room}", roomName);
            }
            else
            {
                logger.LogInformation("Room found. Room={Room}", roomName);
            }

            // グループ参加
            roomContext.Group.Add(ConnectionId, Client);

            joinedUser.FstPos = new Vector3((float) -6.5 + (roomContext.RoomUserDataList.Count() * 3), 0, -10);

            roomContext.RoomUserDataList[ConnectionId] =
                new RoomUserData
                {
                    JoinedUser = joinedUser
                };

            logger.LogInformation(
                "User joined. Room={Room}, User={User}, ConnectionId={ConnectionId}, Count={Count}",
                roomName, userName, ConnectionId,
                roomContext.RoomUserDataList.Count);

            // 他ユーザーへ通知
            roomContext.Group.Except([ConnectionId]).OnJoin(joinedUser);

            logger.LogInformation(
                "Join notification sent. Room={Room}, JoinedUser={User}",
                roomName, userName);

            // 参加者一覧を返却
            return roomContext.RoomUserDataList.Select(x => x.Value.JoinedUser).ToArray();
        }

        // =========================
        // 切断時
        // =========================
        protected override ValueTask OnDisconnected()
        {
            if (roomContext != null)
            {
                if (!roomContext.RoomUserDataList.ContainsKey(ConnectionId))
                    return default;

                var userName =
                    roomContext.RoomUserDataList.TryGetValue(ConnectionId, out var data)
                        ? data.JoinedUser.UserName
                        : "Unknown";

                if (roomContext.RoomUserDataList.TryGetValue(ConnectionId, out var data2))
                {
                    roomContext.Group
                        .Except([ConnectionId])
                        .OnLeave(ConnectionId);
                }

                logger.LogInformation(
                    "Client disconnected. Room={Room}, User={User}, ConnectionId={ConnectionId}",
                    roomContext.Name,
                    userName,
                    ConnectionId);

                // グループ & データ削除
                roomContext.Group.Remove(ConnectionId);
                roomContext.RoomUserDataList.Remove(ConnectionId);

                // ルームが空になったら削除
                if (roomContext.RoomUserDataList.Count == 0)
                {
                    roomContextRepository.RemoveContext(roomContext.Name);

                    logger.LogInformation(
                        "Room removed (empty). Room={Room}",
                        roomContext.Name);
                }
            }
            else
            {
                logger.LogInformation(
                    "Client disconnected before join. ConnectionId={ConnectionId}",
                    ConnectionId);
            }

            return default;
        }

        // =========================
        // 接続ID取得
        // =========================
        public Task<Guid> GetConnectionId()
        {
            return Task.FromResult(ConnectionId);
        }
        public Task LeaveAsync()
        {
            if (roomContext != null)
            {
                if (!roomContext.RoomUserDataList.ContainsKey(ConnectionId))
                    return default;

                var userName =
                    roomContext.RoomUserDataList.TryGetValue(ConnectionId, out var data)
                        ? data.JoinedUser.UserName
                        : "Unknown";

                if (roomContext.RoomUserDataList.TryGetValue(ConnectionId, out var data2))
                {
                    roomContext.Group
                        .Except([ConnectionId])
                        .OnLeave(ConnectionId);
                }

                logger.LogInformation(
                    "Client disconnected. Room={Room}, User={User}, ConnectionId={ConnectionId}",
                    roomContext.Name,
                    userName,
                    ConnectionId);

                // グループ & データ削除
                roomContext.Group.Remove(ConnectionId);
                roomContext.RoomUserDataList.Remove(ConnectionId);

                // ルームが空になったら削除
                if (roomContext.RoomUserDataList.Count == 0)
                {
                    roomContextRepository.RemoveContext(roomContext.Name);

                    logger.LogInformation(
                        "Room removed (empty). Room={Room}",
                        roomContext.Name);
                }
            }
            else
            {
                logger.LogInformation(
                    "Client disconnected before join. ConnectionId={ConnectionId}",
                    ConnectionId);
            }

            return Task.CompletedTask;
        }

        public Task MoveAsync(Vector3 pos, Quaternion rot, long tick, int lapCount, int checkPoint, float distanceToNext)
        {
            if (roomContext == null) return Task.CompletedTask;

            if (!roomContext.RoomUserDataList.TryGetValue(ConnectionId, out var userData))
                return Task.CompletedTask;

            // 1. サーバー側のユーザーデータを更新
            // (RoomUserDataクラスにLapCountとCheckPointプロパティがあると仮定)
            userData.LapCount = lapCount;
            userData.CheckPoint = checkPoint;
            userData.DistanceToNext = distanceToNext;

            // 2. 全ユーザーをソートして順位を決定
            var sortedList = roomContext.RoomUserDataList.Values
                .OrderByDescending(u => u.LapCount)      // 1. 周回数が多い順
                .ThenByDescending(u => u.CheckPoint)    // 2. 同じ周回ならCPが進んでいる順
                .ThenBy(u => u.DistanceToNext)          // 3. 同じCPなら次の点に近い順
                .ToList();

            // 自分の順位を取得 (Listは0から始まるので +1 する)
            int ranking = sortedList.IndexOf(userData) + 1;

            // 3. 他クライアントに通知（算出した順位を含める）
            roomContext.Group.All.OnMove(ConnectionId, pos, rot, tick, ranking);

            return Task.CompletedTask;
        }

        public Task ReadyAsync(Guid connectionId)
        {
            roomContext.RoomUserDataList[connectionId].JoinedUser.IsReady = true;
            roomContext.Group.Except([ConnectionId]).OnReady(roomContext.RoomUserDataList[connectionId].JoinedUser);
            return Task.CompletedTask;
        }

        public Task StartAsync()
        {
            logger.LogInformation("Game Start");

            Vector3 startBasePos = new Vector3(-36f, 0f, -31.5f);
            Vector3 startOffset = new Vector3(2f, 0f, 5f);

            int index = 0;

            foreach (var kv in roomContext.RoomUserDataList)
            {
                var userData = kv.Value;
                Vector3 startPos = startBasePos + startOffset * index;

                userData.JoinedUser.FstPos = startPos;
                index++;
            }

            // 全員に「スタート通知 + 位置確定」を送信
            roomContext.Group.All.OnStart(roomContext.RoomUserDataList.Values
                .Select(u => u.JoinedUser)
                .ToList());

            return Task.CompletedTask;
        }

        public Task GoalAsync(Guid connectionId)
        {
            if (roomContext == null) return Task.CompletedTask;

            if (roomContext.RoomUserDataList.TryGetValue(connectionId, out var userData))
            {
                userData.IsGoaled = true;

                // 1. ゴールした本人に通知（個別のリザルト表示用など）
                // roomContext.Group.Id(connectionId).OnGoalMember(connectionId);
            }

            // 2. 全員がゴールしたかチェック
            int totalUsers = roomContext.RoomUserDataList.Count;
            int goaledUsers = roomContext.RoomUserDataList.Values.Count(u => u.IsGoaled);

            if (goaledUsers >= totalUsers)
            {
                // 全員ゴールしたので、全員に終了合図（OnGameFinish）を送る
                // 引数として最終的なランキングリストなどを送ると親切です
                roomContext.Group.All.OnGameFinish();
            }

            return Task.CompletedTask;
        }
    }
}
