using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-50)]
public class SphereDrag : MonoBehaviour
{
    Camera cam;
    bool dragging;
    float planeZ;

    public void Setup(Camera camera)
    {
        cam = camera;
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || !gameObject.activeInHierarchy) return;

        if (Input.GetMouseButtonDown(0) &&
            !ClothParameterUI.BlocksSceneInput &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit) && hit.transform == transform)
            {
                dragging = true;
                planeZ = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
            }
        }

        if (Input.GetMouseButtonUp(0))
            dragging = false;

        if (!dragging || !Input.GetMouseButton(0)) return;

        var dragRay = cam.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(cam.transform.forward, cam.transform.position + cam.transform.forward * planeZ);
        if (plane.Raycast(dragRay, out float enter))
            transform.position = dragRay.GetPoint(enter);
    }
}
