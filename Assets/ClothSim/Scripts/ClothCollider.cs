using UnityEngine;

public class ClothCollider : MonoBehaviour
{
    public enum Kind
    {
        Sphere = 0,
        Box = 1,
        Cylinder = 2
    }

    public Kind kind;
    public Vector3 PrevPosition { get; set; }

    void Awake()
    {
        var drag = GetComponent<SphereDrag>();
        if (drag != null)
            drag.enabled = false;
    }

    public void SyncPrev()
    {
        PrevPosition = transform.position;
    }

    public Vector3 HalfExtents
    {
        get
        {
            Vector3 s = transform.lossyScale;
            if (kind == Kind.Sphere)
                return new Vector3(s.x * 0.5f, 0f, 0f);
            if (kind == Kind.Cylinder)
                return new Vector3(s.x * 0.5f, s.y, 0f);
            return s * 0.5f;
        }
    }
}
