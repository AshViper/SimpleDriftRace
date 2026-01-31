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

            // Room 取得 or 作成（排他）
            lock (roomContextRepository)
            {
                roomContext = roomContextRepository.GetContext(roomName);
                if (roomContext == null)
                {
                    roomContext = roomContextRepository.CreateContext(roomName);
                    isRoomCreated = true;
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

            var joinedUser = new JoinedUser
            {
                ConnectionId = ConnectionId,
                UserName = userName
            };

            roomContext.RoomUserDataList[ConnectionId] =
                new RoomUserData
                {
                    JoinedUser = joinedUser,
                    Position = Vector3.zero
                };

            logger.LogInformation(
                "User joined. Room={Room}, User={User}, ConnectionId={ConnectionId}, Count={Count}",
                roomName, userName, ConnectionId,
                roomContext.RoomUserDataList.Count);

            // 他ユーザーへ通知
            roomContext.Group
                .Except([ConnectionId])
                .OnJoin(joinedUser);

            logger.LogInformation(
                "Join notification sent. Room={Room}, JoinedUser={User}",
                roomName, userName);

            // 参加者一覧を返却
            return roomContext.RoomUserDataList
                .Select(x => x.Value.JoinedUser)
                .ToArray();
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

        public Task MoveAsync(Vector3 pos, Quaternion rot, long tick)
        {
            if (roomContext == null) return Task.CompletedTask;

            if (!roomContext.RoomUserDataList.TryGetValue(ConnectionId, out var userData))return Task.CompletedTask;

            // 他クライアントに通知
            roomContext.Group
                .Except([ConnectionId])
                .OnMove(ConnectionId, pos, rot, tick);

            return Task.CompletedTask;
        }

        
    }
}
