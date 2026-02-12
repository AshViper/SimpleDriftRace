using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [System.Serializable]
    public class Wheel
    {
        public WheelCollider collider;
        public Transform mesh;
    }

    [Header("Wheels")]
    public Wheel wheelFL;
    public Wheel wheelFR;
    public Wheel wheelRL;
    public Wheel wheelRR;

    [Header("Engine & Brake")]
    public float motorPower = 2500f; // 少しパワーアップ
    public float brakePower = 4000f;
    public float maxSteerAngle = 35f;
    public float maxSpeedKmh = 180f;

    [Header("Grip Settings")]
    public float forwardStiffness = 2.0f;
    public float sidewaysStiffness = 2.2f;
    public float driftSideStiffness = 0.8f;

    [Header("Advanced Physics")]
    public float downForce = 50f;      // 高速時の安定性
    public float antiRollForce = 1500f; // 転倒防止
    public Vector3 centerOfMassOffset = new Vector3(0, -0.7f, 0f); // 重心を低く、少し前に

    [Header("Audio Settings")]
    public AudioSource engineAudioSource;
    public float minPitch = 0.7f;      // アイドリング時のピッチ
    public float maxPitch = 2.5f;      // レブリミット時のピッチ
    public float maxRPMKmh = 200f;     // 最高出力が出る速度（擬似RPM用）
    public float volumeSmoothSpeed = 8f;
    public float pitchSmoothSpeed = 10f;

    Rigidbody rb;
    GameDirector gameDirector;

    float motorInput;
    float steerInput;
    bool isBraking;
    private int getCheckPoint = 0;

    public int GetCheckPoint()
    {
        return getCheckPoint;
    }

    public void SetCheckPoint(int value)
    {
        getCheckPoint = value;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 800f; // 現実的な重さ（軽すぎると跳ねるため）
        rb.centerOfMass = centerOfMassOffset;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Start()
    {
        getCheckPoint = 0;
        gameDirector = GameObject.Find("GameDirector")?.GetComponent<GameDirector>();
        // 初期の摩擦設定
        ApplyFriction(wheelFL.collider, forwardStiffness, sidewaysStiffness);
        ApplyFriction(wheelFR.collider, forwardStiffness, sidewaysStiffness);
        ApplyFriction(wheelRL.collider, forwardStiffness, sidewaysStiffness);
        ApplyFriction(wheelRR.collider, forwardStiffness, sidewaysStiffness);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("CheckPoint"))
        {
            // チェックポイントの正面方向
            Vector3 checkpointForward = other.transform.forward;

            // 車の「実際の移動方向」を取得 (正規化して向きだけ取り出す)
            // 止まっている時のエラー防止に magnitude をチェック
            Vector3 moveDirection = rb.linearVelocity.normalized;

            if (rb.linearVelocity.magnitude < 0.1f)
            {
                // ほとんど動いていない場合は判定をスキップ
                return;
            }

            // チェックポイントの向きと、移動方向を比較
            float directionMatch = Vector3.Dot(checkpointForward, moveDirection);

            if (directionMatch > 0)
            {
                getCheckPoint++;
                Debug.Log($"<color=green>順走:</color> チェックポイント通過！: {getCheckPoint}");
            }
            else
            {
                // バックで通過しても、進行方向が逆ならここに来る
                getCheckPoint--;
                Debug.Log("<color=red>逆走警告:</color> 移動方向が逆です！");
            }
        }
    }
    void Update()
    {
        motorInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        isBraking = Input.GetKey(KeyCode.Space);
    }

    void FixedUpdate()
    {
        // ゲーム開始判定（GameDirectorがない場合は動くように安全策）
        if (gameDirector != null)
        {
            rb.isKinematic = !gameDirector.GetStartFrag();
            engineAudioSource.volume = 0;
            if (!gameDirector.GetStartFrag()) return;
        }

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;
        bool isDrifting = isBraking && motorInput > 0.1f && Mathf.Abs(steerInput) > 0.2f;

        HandleSteering(speedKmh, isDrifting);
        HandleMotor(speedKmh);
        HandleBrakes(isDrifting);
        ApplyExtraForces();
        UpdateAllWheels();
        UpdateEngineAudio();
    }

    // --- ハンドリング制御 ---
    void HandleSteering(float speed, bool drifting)
    {
        // ドリフト中は制限を緩める（35度のまま、あるいは少しだけ絞る）
        float minAngle = drifting ? 25f : 10f;
        float steerLimit = Mathf.Lerp(maxSteerAngle, minAngle, speed / 200f);
        float finalSteer = steerInput * steerLimit;

        wheelFL.collider.steerAngle = finalSteer;
        wheelFR.collider.steerAngle = finalSteer;
    }

    // --- 駆動制御 ---
    void HandleMotor(float speedKmh)
    {
        float torque = 0f;
        if (speedKmh < maxSpeedKmh)
        {
            torque = motorInput * motorPower;
        }

        wheelRL.collider.motorTorque = torque;
        wheelRR.collider.motorTorque = torque;
    }

    // --- ブレーキ & ドリフト摩擦 ---
    void HandleBrakes(bool drifting)
    {
        float currentBrake = isBraking ? brakePower : 0f;

        // ドリフト中は後輪の横滑り摩擦を下げる
        float sStiffness = drifting ? driftSideStiffness : sidewaysStiffness;
        ApplyFriction(wheelRL.collider, forwardStiffness, sStiffness);
        ApplyFriction(wheelRR.collider, forwardStiffness, sStiffness);

        wheelFL.collider.brakeTorque = currentBrake;
        wheelFR.collider.brakeTorque = currentBrake;
        wheelRL.collider.brakeTorque = currentBrake;
        wheelRR.collider.brakeTorque = currentBrake;
    }

    // --- 安定化フォース (ダウンフォース & アンチロールバー) ---
    void ApplyExtraForces()
    {
        // 1. ダウンフォース
        rb.AddForce(-transform.up * downForce * rb.linearVelocity.magnitude);

        // 2. アンチロールバー (左右のサスペンションの差を利用して傾きを抑える)
        ApplyAntiRollBar(wheelFL.collider, wheelFR.collider);
        ApplyAntiRollBar(wheelRL.collider, wheelRR.collider);
    }

    void ApplyAntiRollBar(WheelCollider left, WheelCollider right)
    {
        WheelHit hit;
        float travelL = 1.0f;
        float travelR = 1.0f;

        // サスペンション距離が0だとエラーになるのでチェック
        if (left.suspensionDistance <= 0) return;

        bool groundedL = left.GetGroundHit(out hit);
        if (groundedL)
            travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;

        bool groundedR = right.GetGroundHit(out hit);
        if (groundedR)
            travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;

        float antiRollForceAmount = (travelL - travelR) * antiRollForce;

        // ガタつき防止：計算結果が異常な場合は適用しない
        if (float.IsNaN(antiRollForceAmount) || float.IsInfinity(antiRollForceAmount)) return;

        if (groundedL)
            rb.AddForceAtPosition(left.transform.up * -antiRollForceAmount, left.transform.position);
        if (groundedR)
            rb.AddForceAtPosition(right.transform.up * antiRollForceAmount, right.transform.position);
    }

    // --- ユーティリティ ---
    void ApplyFriction(WheelCollider wc, float forward, float side)
    {
        WheelFrictionCurve f = wc.forwardFriction;
        f.stiffness = forward;
        wc.forwardFriction = f;

        WheelFrictionCurve s = wc.sidewaysFriction;
        s.stiffness = side;
        wc.sidewaysFriction = s;
    }

    void UpdateAllWheels()
    {
        UpdateWheelMesh(wheelFL);
        UpdateWheelMesh(wheelFR);
        UpdateWheelMesh(wheelRL);
        UpdateWheelMesh(wheelRR);
    }

    void UpdateWheelMesh(Wheel wheel)
    {
        Vector3 pos;
        Quaternion rot;
        wheel.collider.GetWorldPose(out pos, out rot);
        wheel.mesh.position = pos;
        wheel.mesh.rotation = rot;
    }

    void UpdateEngineAudio()
    {
        if (engineAudioSource == null) return;

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        // 1. 速度に基づいた擬似的なRPM（回転数）の計算
        // 単純な1速シミュレーションですが、maxRPMKmh を超えても少しピッチが上がるように設定
        float rpmFactor = Mathf.Clamp01(speedKmh / maxRPMKmh);

        // 2. アクセルを踏んでいるかどうかの影響
        // アクセルを踏むとわずかにピッチが上がり、音に力強さが出ます
        float throttleInfluence = Mathf.Abs(motorInput) * 0.15f;
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, rpmFactor) + throttleInfluence;

        // 3. 音量の動的変化
        // アイドリングでも少し音を出し、アクセルを踏むとフルボリュームになるよう調整
        float targetVolume = Mathf.Lerp(0.3f, 0.5f, Mathf.Abs(motorInput));

        // バック走行時も少し音を出す
        if (speedKmh < 5f && Mathf.Abs(motorInput) > 0.1f) targetVolume = 0.6f;

        // 滑らかに変化させる（急激な変化によるノイズ防止）
        engineAudioSource.pitch = Mathf.Lerp(engineAudioSource.pitch, targetPitch, Time.fixedDeltaTime * pitchSmoothSpeed);
        engineAudioSource.volume = Mathf.Lerp(engineAudioSource.volume, targetVolume, Time.fixedDeltaTime * volumeSmoothSpeed);
    }
}