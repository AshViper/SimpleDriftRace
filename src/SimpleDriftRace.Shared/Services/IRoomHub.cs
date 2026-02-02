using MagicOnion;
using System;
using UnityEngine;
using System.Threading.Tasks;
namespace Shared.Services
{
    /// <summary>
    /// クライアントから呼び出す処理を実装するクラス用インターフェース
    /// </summary>
    public interface IRoomHub : IStreamingHub<IRoomHub, IRoomHubReceiver>
    {
        // [サーバーに実装]
        // [クライアントから呼び出す]

        // ユーザー入室
        Task<JoinedUser[]> JoinAsync(string roomName, string userName);

        Task LeaveAsync();

        Task<Guid> GetConnectionId();

        Task MoveAsync(Vector3 pos, Quaternion rot, long tick, int lapCount, int CheckPoint, float distanceToNext);

        Task ReadyAsync(Guid connectionId);

        Task StartAsync();

        Task GoalAsync(Guid connectionId);
    }
}
