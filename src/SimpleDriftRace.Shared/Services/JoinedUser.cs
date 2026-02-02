using MessagePack;
using System;
using UnityEngine;

namespace Shared.Services
{
    /// <summary>
    /// 入室済みユーザー
    /// </summary>
    [MessagePackObject]
    public class JoinedUser
    {
        [Key(0)]
        public Guid ConnectionId { get; set; }// 接続ID
        [Key(1)]
        public string UserName { get; set; }// ユーザーの名前
        [Key(2)]
        public bool IsOwner { get; set; } // ホストかどうか
        [Key(3)]
        public bool IsReady { get; set; } // Ready
        [Key(4)]
        public Vector3 FstPos { get; set; }
    }
}
