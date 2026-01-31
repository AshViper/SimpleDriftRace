using Shared.Services;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameDirector : MonoBehaviour
{
    [Header("content")]
    public GameObject playerPrefab;
    public GameObject characterPrefab;
    public RoomModel roomModel;
    public InputField userName;
    public GameObject spawnPoint;
    public CameraFollow CameraFollow;

    Dictionary<Guid, GameObject> characterList = new Dictionary<Guid, GameObject>();
    [SerializeField] GameObject player;
    private string myName;
    private float sendInterval = 0.05f; // 20Hz
    private float timer;

    async void Start()
    {
        //ユーザーが入室した時にOnJoinedUserメソッドを実行するよう、モデルに登録しておく
        roomModel.OnJoinedUser += this.OnJoinedUser;
        roomModel.OnLeavedUser += this.OnLeavedUser;
        roomModel.OnMovedUser += this.OnMovedUser;
        //接続
        await roomModel.ConnectAsync();
    }

    async void FixedUpdate()
    {
        CameraFollow.SetTarget(player);

        if (player == null) return;

        timer += Time.fixedDeltaTime;
        if (timer < sendInterval) return;

        timer = 0f;

        await roomModel.MoveAsync(player.transform.position, player.transform.rotation, DateTime.UtcNow.Ticks);
    }
    public async void JoinRoom()
    {
        //入室
        if (userName.text == "" || userName.text == null)return;
        myName = userName.text;
        await roomModel.JoinAsync("sampleRoom", userName.text);
    }
    public async void LeaveRoom()
    {
        await roomModel.LeaveAsync();
        Destroy(player);
        foreach (var obj in characterList.Values) Destroy(obj);
        characterList.Clear();
    }

    //ユーザーが入室した時の処理
    private void OnJoinedUser(JoinedUser user)
    {
        if (characterList.ContainsKey(user.ConnectionId)) return;
        if (user.UserName == myName)
        {
            if (player) Destroy(player);
            Transform spawn = spawnPoint.transform;
            player = Instantiate(playerPrefab, spawn.position, spawn.rotation);  //インスタンス生成
        }
        else
        {
            GameObject characterObject = Instantiate(characterPrefab);  //インスタンス生成
            characterObject.transform.position = new Vector3(0, 0, 0);
            characterList[user.ConnectionId] = characterObject;  //フィールドで保持
        }
        
    }
    private void OnLeavedUser(Guid connectionId)
    {
        Destroy(characterList[connectionId]);
        characterList.Remove(connectionId);
    }
    private void OnMovedUser(Guid connectionId, Vector3 pos, Quaternion rot, long tick)
    {
        if (!characterList.TryGetValue(connectionId, out var go)) return;

        var sync = go.GetComponent<RemoteSync>();
        if (sync == null) return;

        sync.Apply(pos, rot);
    }
}
