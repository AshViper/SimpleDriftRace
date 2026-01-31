using MessagePack;
using System;

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
        public int JoinOrder { get; set; } // 参加順番
    }
}
