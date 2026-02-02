using MagicOnion;
using System;
using System.Collections.Generic;
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
        void OnMove(Guid connectionId,  Vector3 pos, Quaternion rot, long tick, int ranking);
        void OnReady(JoinedUser user);
        void OnStart(List<JoinedUser> users);
        void OnGameFinish();
    }
}
