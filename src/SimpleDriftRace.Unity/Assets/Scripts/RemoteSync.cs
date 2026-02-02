using UnityEngine;

public class RemoteSync : MonoBehaviour
{
    Vector3 prevPos, nextPos;
    Quaternion prevRot, nextRot;
    float lerpTimer;

    const float syncInterval = 0.025f;

    void Update()
    {
        lerpTimer += Time.deltaTime;
        float t = Mathf.Clamp01(lerpTimer / syncInterval);

        transform.position = Vector3.Lerp(prevPos, nextPos, t);
        transform.rotation = Quaternion.Slerp(prevRot, nextRot, t);
    }

    public void Apply(Vector3 pos, Quaternion rot)
    {
        prevPos = transform.position;
        prevRot = transform.rotation;
        nextPos = pos;
        nextRot = rot;
        lerpTimer = 0f;
    }
}
