using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [System.Serializable]
    public class Wheel
    {
        public WheelCollider collider; // 絶対に動かさない
        public Transform mesh;         // 見た目用
    }

    [Header("Wheels")]
    public Wheel wheelFL;
    public Wheel wheelFR;
    public Wheel wheelRL;
    public Wheel wheelRR;

    [Header("Engine")]
    public float motorPower = 2000f;
    public float brakePower = 3000f;
    public float maxSteerAngle = 30f;

    [Header("Grip (Normal)")]
    public float forwardStiffness = 2.5f;
    public float sidewaysStiffness = 2.8f;

    [Header("Grip (Drift)")]
    public float driftForwardStiffness = 1.0f;
    public float driftSideStiffness = 0.7f;
    public float driftSteerBoost = 1.2f;

    [Header("Torque Limit")]
    public float maxSpeedKmh = 180f;   // 最高速
    public float torqueFalloff = 2.0f; // 高速減衰カーブ


    Rigidbody rb;

    float motorInput;
    float steerInput;
    bool brake;

    float currentTorque;
    float wheelRotation;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // レース車必須設定
        rb.mass = 150f;
        rb.centerOfMass = new Vector3(0f, -0.6f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void Start()
    {
        // 初期摩擦設定
        SetWheelFriction(wheelFL.collider, forwardStiffness, sidewaysStiffness);
        SetWheelFriction(wheelFR.collider, forwardStiffness, sidewaysStiffness);
        SetWheelFriction(wheelRL.collider, forwardStiffness, sidewaysStiffness);
        SetWheelFriction(wheelRR.collider, forwardStiffness, sidewaysStiffness);
    }

    void Update()
    {
        motorInput = Input.GetAxis("Vertical");     // W / S
        steerInput = Input.GetAxis("Horizontal");   // A / D
        brake = Input.GetKey(KeyCode.Space);
    }

    void FixedUpdate()
    {
        float speed = rb.linearVelocity.magnitude * 3.6f; // km/h
        bool drifting = IsDrifting();

        // ===== ステアリング =====
        float steerLimit = Mathf.Lerp(maxSteerAngle, 12f, speed / 150f);
        float steer = steerInput * steerLimit * (drifting ? driftSteerBoost : 1f);

        wheelFL.collider.steerAngle = steer;
        wheelFR.collider.steerAngle = steer;

        // ===== 駆動（後輪）=====
        ApplyMotor(wheelRL.collider);
        ApplyMotor(wheelRR.collider);

        // ===== ブレーキ =====
        float brakeTorque = drifting ? brakePower * 0.3f : (brake ? brakePower : 0f);
        ApplyBrake(brakeTorque);

        // ===== 摩擦切り替え（ドリフト）=====
        ApplyDriftFriction(drifting);

        // ===== 見た目ホイール更新 =====
        UpdateWheel(wheelFL);
        UpdateWheel(wheelFR);
        UpdateWheel(wheelRL);
        UpdateWheel(wheelRR);

        LimitMaxSpeed();
    }

    // --------------------------------------------------
    // 内部ロジック
    // --------------------------------------------------
    void LimitMaxSpeed()
    {
        float maxSpeed = maxSpeedKmh / 3.6f; // m/s
        float currentSpeed = rb.linearVelocity.magnitude;

        if (currentSpeed > maxSpeed)
        {
            rb.linearVelocity =
                rb.linearVelocity.normalized * maxSpeed;
        }
    }


    bool IsDrifting()
    {
        return brake &&
               motorInput > 0.1f &&
               Mathf.Abs(steerInput) > 0.1f;
    }

    void ApplyMotor(WheelCollider wc)
    {
        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        // 速度比率（0〜1）
        float speedRatio = Mathf.Clamp01(speedKmh / maxSpeedKmh);

        // 高速になるほどトルクを落とす（指数カーブ）
        float torqueLimitFactor = 1f - Mathf.Pow(speedRatio, torqueFalloff);

        float targetTorque =
            motorInput * motorPower * torqueLimitFactor;

        if (Mathf.Abs(motorInput) > 0.01f)
        {
            currentTorque = Mathf.Lerp(
                currentTorque,
                targetTorque,
                Time.fixedDeltaTime * 6f
            );
        }
        else
        {
            currentTorque = Mathf.Lerp(
                currentTorque,
                0f,
                Time.fixedDeltaTime * 2f
            );
        }

        wc.motorTorque = currentTorque;
    }


    void ApplyBrake(float torque)
    {
        wheelFL.collider.brakeTorque = torque;
        wheelFR.collider.brakeTorque = torque;
        wheelRL.collider.brakeTorque = torque;
        wheelRR.collider.brakeTorque = torque;
    }

    void ApplyDriftFriction(bool drifting)
    {
        if (drifting)
        {
            SetWheelFriction(wheelRL.collider, driftForwardStiffness, driftSideStiffness);
            SetWheelFriction(wheelRR.collider, driftForwardStiffness, driftSideStiffness);
        }
        else
        {
            SetWheelFriction(wheelRL.collider, forwardStiffness, sidewaysStiffness);
            SetWheelFriction(wheelRR.collider, forwardStiffness, sidewaysStiffness);
        }
    }

    void SetWheelFriction(WheelCollider wc, float forward, float side)
    {
        var f = wc.forwardFriction;
        f.stiffness = forward;
        wc.forwardFriction = f;

        var s = wc.sidewaysFriction;
        s.stiffness = side;
        wc.sidewaysFriction = s;
    }

    void UpdateWheel(Wheel wheel)
    {
        // 位置だけは WheelCollider に従う
        wheel.collider.GetWorldPose(out Vector3 pos, out Quaternion _);

        // ===== 見た目回転は自前で制御 =====
        float speed = rb.linearVelocity.magnitude; // m/s
        float radius = wheel.collider.radius;

        float rotationSpeed =
            (speed / (2f * Mathf.PI * radius)) * 360f;

        // ブレーキ or ドリフト中は回転を抑える
        if (brake)
            rotationSpeed *= 0.3f;

        wheelRotation += rotationSpeed * Time.fixedDeltaTime;

        // 向き（ステア）は collider から取る
        Quaternion steerRot = Quaternion.Euler(
            0f,
            wheel.collider.steerAngle,
            0f
        );

        wheel.mesh.rotation =
            transform.rotation *
            steerRot *
            Quaternion.Euler(wheelRotation, 0f, 0f);
    }

}
