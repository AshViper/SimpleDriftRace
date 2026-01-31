using MagicOnion;
using System;
using UnityEngine;

namespace Shared.Services
{
    /// <summary>
    /// サーバーからクライアントへの通知関連
    /// </summary>
    public interface IRoomHubReceiver
    {
        // [クライアントに実装]
        // [サーバーから呼び出す]

        // ユーザーの入室通知
        void OnJoin(JoinedUser user);
        void OnLeave(Guid connectionId);
        void OnMove(Guid connectionId,  Vector3 pos, Quaternion rot, long tick);
    }
}
