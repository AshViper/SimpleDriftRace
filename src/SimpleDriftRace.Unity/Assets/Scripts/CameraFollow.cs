using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Offset")]
    public Vector3 offset = new Vector3(0f, 2f, -8f);

    [Header("Smooth")]
    public float positionSmooth = 0.15f;
    public float rotationSmooth = 8f;

    [Header("Speed Zoom")]
    public float minDistance = -4f;
    public float maxDistance = -4f;
    public float maxSpeedKmh = 200f;

    [Header("FOV")]
    public float minFov = 60f;   // 停止〜低速時
    public float maxFov = 80f;   // 最高速時
    public float fovSmooth = 5f;

    Vector3 velocity;
    Camera cam;
    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null)
        {
            transform.position = new Vector3(0, 3.64f, -19.05f);
            transform.rotation = Quaternion.Euler(12.516f, 0, 0);
            return;
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        float speed = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;

        // --- 後方確認の入力チェック ---
        bool isLookingBack = Input.GetKey(KeyCode.C);

        float z = Mathf.Lerp(minDistance, maxDistance, speed / maxSpeedKmh);

        // Cキーを押している間は、Z軸（距離）の向きを反転させ、X軸も微調整（必要に応じて）
        Vector3 dynamicOffset = new Vector3(offset.x, offset.y, isLookingBack ? -z : z);

        Vector3 desiredPos = target.position + target.TransformDirection(dynamicOffset);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref velocity,
            positionSmooth
        );

        // --- 注視点の計算 ---
        // 通常は target.position を見るが、後ろを向くときは車体の「後ろ」を見るようにする
        Vector3 lookAtPos = isLookingBack
            ? target.position - target.forward * 10f // 後ろを確認
            : target.position + target.forward * 2f;  // 前方を確認（少し前を注視すると安定する）

        Quaternion targetRot = Quaternion.LookRotation(
            lookAtPos - transform.position,
            Vector3.up
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * rotationSmooth
        );

        // ===== FOV（速度連動）=====
        if (cam != null)
        {
            float speedRatio = Mathf.Clamp01(speed / maxSpeedKmh);
            float targetFov = Mathf.Lerp(minFov, maxFov, speedRatio);

            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                targetFov,
                Time.deltaTime * fovSmooth
            );
        }
    }

    public void SetTarget(GameObject target)
    {
        if (target == null) return;
        this.target = target.transform;
    }
}
