using UnityEngine;

public class ClothOrbitCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
    public float distance = 9f;
    public float minDistance = 3f;
    public float maxDistance = 24f;
    public float yaw = 20f;
    public float pitch = 12f;
    public float rotateSpeed = 140f;
    public float zoomSpeed = 4f;
    public float minPitch = -10f;
    public float maxPitch = 75f;

    public Quaternion OrbitRotation => Quaternion.Euler(pitch, yaw, 0f);

    public void SetOrbitRotation(Quaternion rotation)
    {
        Vector3 toCam = -(rotation * Vector3.forward);
        yaw = Mathf.Atan2(-toCam.x, -toCam.z) * Mathf.Rad2Deg;
        pitch = Mathf.Asin(Mathf.Clamp(toCam.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (!ClothParameterUI.BlocksSceneInput && Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotateSpeed * Time.deltaTime;
            pitch -= Input.GetAxis("Mouse Y") * rotateSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        float scroll = ClothParameterUI.BlocksSceneInput ? 0f : Input.mouseScrollDelta.y;
        distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 focus = target.position + targetOffset;
        transform.position = focus + rot * new Vector3(0f, 0f, -distance);
        transform.LookAt(focus);
    }
}
