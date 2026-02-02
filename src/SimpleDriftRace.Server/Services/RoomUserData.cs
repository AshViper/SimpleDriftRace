using Shared.Services;
using UnityEngine;

namespace Server.Services
{
    // ルーム内のユーザー単体の情報
    public class RoomUserData
    {
        public JoinedUser JoinedUser;
        public Vector3 Position;
        public Quaternion Rotation;
        public long Tick;
        public int LapCount;
        public int CheckPoint;
        public float DistanceToNext;
        public bool IsGoaled = false;
    }
}
