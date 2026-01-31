using Cysharp.Threading.Tasks;
using MagicOnion.Client;
using MagicOnion;
using Shared.Services;
using System;
using UnityEngine;

public class RoomModel : BaseModel, IRoomHubReceiver
{
    private GrpcChannelx channel;
    private IRoomHub roomHub;

    //　接続ID
    public Guid ConnectionId { get; set; }

    //　ユーザー接続通知
    public Action<JoinedUser> OnJoinedUser { get; set; }
    public Action<Guid> OnLeavedUser { get; set; }
    public Action<Guid, Vector3, Quaternion, long> OnMovedUser { get; set; }

    //　MagicOnion接続処理
    public async UniTask ConnectAsync()
    {
        try
        {
            Debug.Log($"[Connect] Try connect to server: {ServerURL}");

            channel = GrpcChannelx.ForAddress(ServerURL);

            roomHub = await StreamingHubClient
                .ConnectAsync<IRoomHub, IRoomHubReceiver>(channel, this);

            ConnectionId = await roomHub.GetConnectionId();

            Debug.Log($"[Connect] Connected! ConnectionId = {ConnectionId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Connect] Failed to connect server: {ex}");
            throw; // 呼び出し側で失敗を検知したい場合
        }
    }

    //　MagicOnion切断処理
    public async UniTask DisconnectAsync()
    {
        if (roomHub != null) await roomHub.DisposeAsync();
        if (channel != null) await channel.ShutdownAsync();
        roomHub = null; channel = null;
    }

    //　破棄処理 
    async void OnDestroy()
    {
        DisconnectAsync();
    }
    //　入室
    public async UniTask JoinAsync(string roomName, string userName)
    {
        JoinedUser[] users = await roomHub.JoinAsync(roomName, userName);
        foreach (var user in users)
        {
            if (OnJoinedUser != null)
            {
                OnJoinedUser(user);
            }
        }
    }

    public async UniTask LeaveAsync()
    {
        await roomHub.LeaveAsync();
    }

    public async UniTask MoveAsync(Vector3 pos, Quaternion rot, long tick)
    {
        await roomHub.MoveAsync(pos, rot, tick);
    }

    //　入室通知 (IRoomHubReceiverインタフェースの実装)
    public void OnJoin(JoinedUser user)
    {
        if (OnJoinedUser != null)
        {
            OnJoinedUser(user);
        }
    }

    public void OnLeave(Guid connectionId)
    {
        if (OnLeavedUser != null)
        {
            OnLeavedUser(connectionId);
        }
    }

    public void OnMove(Guid connectionId, Vector3 pos, Quaternion rot, long tick)
    {
        if (OnMovedUser != null)
        {
            OnMovedUser(connectionId, pos, rot, tick);
        }
    }
}
