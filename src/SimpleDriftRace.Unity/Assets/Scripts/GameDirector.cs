using Shared.Services;
using System;
using System.Collections;
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
    public InputField roomName;
    public CameraFollow CameraFollow;
    public List<Transform> checkPointPositions;

    [Header("UI")]
    public List<GameObject> uis;

    Dictionary<Guid, GameObject> characterList = new Dictionary<Guid, GameObject>();
    [SerializeField] GameObject player;
    private string myName;
    private float sendInterval = 0.025f; // 20Hz
    private float timer;
    private bool isStart = false;
    private bool isSet = false;
    Dictionary<Guid, bool> readyMap = new Dictionary<Guid, bool>();
    bool isOwner = false;
    private int lapCount = 0;
    private bool isGoaled = false;
    // クラスのメンバ変数に追加
    private List<Guid> spectatingKeys = new List<Guid>();
    private int currentSpectateIndex = 0;

    public bool GetStartFrag()
    {
        return isStart;
    }

    async void Start()
    {
        init();
        //ユーザーが入室した時にOnJoinedUserメソッドを実行するよう、モデルに登録しておく
        roomModel.OnJoinedUser += this.OnJoinedUser;
        roomModel.OnLeavedUser += this.OnLeavedUser;
        roomModel.OnMovedUser += this.OnMovedUser;
        roomModel.OnReadyUser += this.OnReadyUser;
        roomModel.OnStartUser += this.OnStartUser;
        roomModel.OnGameFinishUser += this.OnGameFinish;
        //接続
        await roomModel.ConnectAsync();
    }

    public void init()
    {
        isStart = false;
        isSet = false;
        isGoaled = false;
        lapCount = 0;
        if (player) player.GetComponent<CarController>().SetCheckPoint(0);
        foreach (GameObject button in uis) button.SetActive(false);
        uis[2].SetActive(true);
    }

    async void FixedUpdate()
    {
        if (isSet) CameraFollow.SetTarget(player);
        if (player == null) return;

        // --- Rigidbody を取得 ---
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb == null) return;

        timer += Time.fixedDeltaTime;
        if (timer < sendInterval) return;
        timer = 0f;

        CarController car = player.GetComponent<CarController>();
        int currentCP = car.GetCheckPoint();

        if (currentCP == checkPointPositions.Count) currentCP = checkPointPositions.Count - 1;

        // --- ラップ更新ロジック ---
        if (currentCP >= checkPointPositions.Count)
        {
            lapCount++;
            car.SetCheckPoint(1);
            currentCP = 0;
        }

        // --- 逆走判定ロジック ---
        Vector3 currentCPPos = checkPointPositions[currentCP].position;
        int nextCPIndex = (currentCP + 1) % checkPointPositions.Count;
        Vector3 nextCPPos = checkPointPositions[nextCPIndex].position;

        // 次のCPへの方向ベクトル
        Vector3 dirToNext = (nextCPPos - player.transform.position).normalized;
        // 車の進行方向
        Vector3 carMoveDir = playerRb.linearVelocity.normalized;

        // 1. 次のCPまでの距離
        float distToNext = Vector3.Distance(player.transform.position, nextCPPos);

        // 2. 「次のCPへの方向」と「自分の進んでいる方向」が逆（内積がマイナス）か判定
        float directionDot = Vector3.Dot(dirToNext, carMoveDir);

        // 逆走フラグ
        bool isWrongWay = false;

        // 条件：ある程度の速度が出ていて、かつ次のCPと逆方向に進んでいる場合
        if (playerRb.linearVelocity.magnitude > 3f) // 3m/s以上の時だけ判定
        {
            // 内積が-0.5より小さい＝目標に対して120度以上背を向けて走っている
            if (directionDot < -0.5f)
            {
                isWrongWay = true;
            }
        }

        // UI表示
        uis[7].SetActive(isWrongWay);
        if (isWrongWay)
        {
            Debug.Log("<color=red>WRONG WAY!</color> 次のCPから遠ざかっています");
        }

        // --- ゴール判定 ---
        if (lapCount >= 3 && !isGoaled)
        { 
            GoalAsync();
        }

        // UI更新
        uis[5].GetComponent<Text>().text = $"{Mathf.Min(lapCount + 1, 3)}/3";

        // サーバー送信
        await roomModel.MoveAsync(
            player.transform.position,
            player.transform.rotation,
            DateTime.UtcNow.Ticks,
            lapCount,
            currentCP,
            distToNext
        );
    }
    void Update()
    {
        // ゴールしていて、かつ観戦対象がいる場合
        if (isGoaled && characterList.Count > 0)
        {
            // 例えば、マウスの左クリックや特定のボタンで次のプレイヤーに切り替え
            if (Input.GetMouseButtonDown(0))
            {
                SwitchSpectateTarget();
            }
        }
    }

    private void SwitchSpectateTarget()
    {
        if (characterList.Count == 0) return;

        // characterListのキー（Guid）をリスト化
        spectatingKeys = new List<Guid>(characterList.Keys);

        currentSpectateIndex = (currentSpectateIndex + 1) % spectatingKeys.Count;
        Guid targetId = spectatingKeys[currentSpectateIndex];

        if (characterList.TryGetValue(targetId, out GameObject targetObj))
        {
            CameraFollow.SetTarget(targetObj);
            Debug.Log($"観戦対象を切り替え: {targetId}");
        }
    }
    public async void JoinRoom()
    {
        //入室
        if (userName.text == "" || userName.text == null)return;
        if (roomName.text == "" || roomName.text == null) return;
        myName = userName.text;
        await roomModel.JoinAsync(roomName.text, userName.text);
        uis[2].SetActive(false);
        uis[3].SetActive(true);
    }
    public async void LeaveRoom()
    {
        await roomModel.LeaveAsync();
        init();
        Destroy(player);
        foreach (var obj in characterList.Values) Destroy(obj);
        characterList.Clear();
    }

    public async void Ready()
    {
        await roomModel.ReadyAsync();
        uis[1].SetActive(false); 
    }

    public async void GameStart()
    {
        await roomModel.StartAsync();
    }

    private async void GoalAsync()
    {
        isGoaled = true;

        // 自分の車を消去
        if (player != null)
        {
            player.transform.position = Vector3.zero;
            player = null;
        }

        // 他にプレイヤーがいれば、最初の1人を映す
        if (characterList.Count > 0)
        {
            spectatingKeys = new List<Guid>(characterList.Keys);
            currentSpectateIndex = 0;
            CameraFollow.SetTarget(characterList[spectatingKeys[0]]);

            // UIなどで「観戦中」と出すと親切です
            uis[6].SetActive(true);
            uis[6].GetComponentInChildren<Text>().text = "GOAL! Spectating...";
        }
        else
        {
            // 自分一人しかいない場合はそのまま終了
            uis[6].SetActive(true);
            uis[6].GetComponentInChildren<Text>().text = "FINISH!";
        }

        await roomModel.GoalAsync(roomModel.ConnectionId);
    }

    //ユーザーが入室した時の処理
    private void OnJoinedUser(JoinedUser user)
    {
        if (readyMap.ContainsKey(user.ConnectionId) == false)
        {
            readyMap[user.ConnectionId] = false;
        }

        uis[0].SetActive(false); // Start

        if (user.UserName == myName)
        {
            if (player) Destroy(player);
            player = Instantiate(playerPrefab, user.FstPos, Quaternion.Euler(0, 165, 0));
            readyMap[user.ConnectionId] = true;
            isOwner = user.IsOwner;
            uis[isOwner ? 0 : 1].SetActive(true);
        }
        else
        {
            GameObject characterObject =
                Instantiate(characterPrefab, user.FstPos, Quaternion.Euler(0, 165, 0));
            characterList[user.ConnectionId] = characterObject;
            readyMap[user.ConnectionId] = false;
            CheckAllReady();
        }
    }

    private void OnLeavedUser(Guid connectionId)
    {
        if (characterList.TryGetValue(connectionId, out var obj))
        {
            Destroy(obj);
            characterList.Remove(connectionId);
        }

        readyMap.Remove(connectionId);
        CheckAllReady();
    }

    private void OnMovedUser(Guid connectionId, Vector3 pos, Quaternion rot, long tick, int ranking)
    {
        // 1. 自分のデータが更新された場合
        if (connectionId == roomModel.ConnectionId) // 事前に自分のGuidを保持しておく必要があります
        {
            // 自分の順位UIのみを更新
            if (uis[4] != null)
            {
                uis[4].GetComponent<Text>().text = $"{ranking}位";
            }

            // 自分の位置は自分のCarControllerが制御しているので、Applyは不要なことが多いです
            return;
        }

        // 2. 他人のデータが更新された場合
        if (!characterList.TryGetValue(connectionId, out var go)) return;

        var sync = go.GetComponent<RemoteSync>();
        if (sync != null)
        {
            sync.Apply(pos, rot);
        }

        // (任意) 他人の頭上に順位を出したい場合はここで go に対して処理を行う
    }
    private void OnReadyUser(JoinedUser user)
    {
        if (!readyMap.ContainsKey(user.ConnectionId)) return;

        readyMap[user.ConnectionId] = true;
        Debug.Log($"{user.UserName} is Ready");

        CheckAllReady();
    }

    void CheckAllReady()
    {
        foreach (var ready in readyMap.Values)
        {
            if (!ready) return; // 誰か未Ready
        }

        // 全員Ready
        if (isOwner)
        {
            uis[0].SetActive(true);  // Start ボタン有効化
        }
    }

    private void OnStartUser(List<JoinedUser> users)
    {
        foreach (var user in users)
        {
            if (user.UserName == myName)
            {
                player.transform.position = user.FstPos;
                player.transform.rotation = Quaternion.Euler(0, -90, 0);
            }
        }

        isSet = true;

        foreach (GameObject button in uis) button.SetActive(false);
        uis[3].SetActive(true);
        uis[4].SetActive(true);
        uis[5].SetActive(true);
        uis[6].SetActive(true);
        StartCoroutine(StartAfterDelay(3f));

        Debug.Log("Game Start (countdown)");
    }
    IEnumerator StartAfterDelay(float delay)
    {
        Text countdownText = uis[6].GetComponent<Text>();
        float remainingTime = delay;

        // delayが3秒なら、3 -> 2 -> 1 とカウント
        while (remainingTime > 0)
        {
            countdownText.text = Mathf.CeilToInt(remainingTime).ToString();
            yield return new WaitForSeconds(1f);
            remainingTime -= 1f;
        }

        // 開始時の表示
        countdownText.text = "GO!";
        isStart = true;

        // 1秒後に「GO!」を消す
        yield return new WaitForSeconds(1f);
        countdownText.text = "";
        uis[6].SetActive(false);
    }
    private void OnGameFinish()
    {
        isStart = false; // 車の動きを止める

        // UIの表示
        foreach (GameObject ui in uis) ui.SetActive(false);
        uis[6].SetActive(true); // リザルト画面
        uis[6].GetComponentInChildren<Text>().text = "FINISH!";

        Debug.Log("All players goaled. Game Finished.");

        StartCoroutine(GoalAfterDelay(2f));
    }

    IEnumerator GoalAfterDelay(float delay)
    {
        yield return new WaitForSeconds(1f);
        init();
        LeaveRoom();
    }
}
