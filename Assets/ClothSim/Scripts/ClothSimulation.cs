using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClothSimulation : MonoBehaviour
{
    public const int MaxColliders = 24;

    [StructLayout(LayoutKind.Sequential)]
    struct GpuCollider
    {
        public Vector3 pos;
        public float type;
        public Vector3 posPrev;
        public float pad0;
        public Vector3 halfExt;
        public float pad1;
    }

    public ComputeShader clothShader;
    public Material clothMaterial;
    public readonly List<ClothCollider> colliders = new List<ClothCollider>();

    [Header("Grid")]
    public int width = 72;
    public int height = 72;
    public float spacing = 0.06901f;
    public float randomRange = 0f;
    public int layoutMode = 1;
    public float dropHeight = 3.4f;

    [Header("Cloth")]
    public float mass = 0.2f;
    public float tension = 15f;
    public float bending = 0.35f;
    public float damping = 0.02f;

    [Header("Collision")]
    public float friction = 0.45f;
    public float restitution = 0f;
    public float collisionGain = 1f;
    public float collisionThickness = 0.05f;

    [Header("Simulation")]
    public float gravityY = -9.81f;
    public float timeStep = 0.016f;
    public int substeps = 60;
    public int pinMode = 3;
    public bool paused = true;

    [Header("Wind")]
    public float airResistance = 0.55f;
    public float maxWind = 0f;
    public float windY = 0f;
    public Vector3 windDirection = new Vector3(-1f, 0f, 0f);
    public bool gustEnabled = false;
    public bool windEnabled = false;

    [Header("Look")]
    public Color clothColor = Color.white;
    public bool showPattern;
    public bool showGrid;

    ComputeBuffer posBuffer, velBuffer, newPosBuffer, newVelBuffer, colliderBuffer;
    GpuCollider[] colliderData = new GpuCollider[MaxColliders];
    Mesh mesh;
    int kernel;
    Vector3 wind;
    bool ready;
    int activeWidth;
    int activeHeight;
    bool hasColliderPrev;

    public Vector3 Wind => wind;
    public bool IsReady => ready;

    public Vector3 ClothCenter => new Vector3(0f, 1.15f, 0f);

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (clothShader == null)
        {
            Debug.LogError("Cloth compute shader is missing.");
            return;
        }

        ReleaseBuffers();
        activeWidth = Mathf.Max(2, width);
        activeHeight = Mathf.Max(2, height);
        int count = activeWidth * activeHeight;

        posBuffer = new ComputeBuffer(count, sizeof(float) * 3);
        velBuffer = new ComputeBuffer(count, sizeof(float) * 3);
        newPosBuffer = new ComputeBuffer(count, sizeof(float) * 3);
        newVelBuffer = new ComputeBuffer(count, sizeof(float) * 3);

        Vector3[] positions = CreateInitialPositions();
        posBuffer.SetData(positions);
        velBuffer.SetData(new Vector3[count]);

        CreateMesh(positions);
        kernel = clothShader.FindKernel("UpdateCloth");
        GetComponent<MeshFilter>().mesh = mesh;
        if (clothMaterial != null)
        {
            var renderer = GetComponent<MeshRenderer>();
            renderer.material = clothMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        wind = Vector3.zero;
        ApplyMaterial();
        hasColliderPrev = false;
        var found = FindObjectsOfType<ClothCollider>();
        for (int i = 0; i < found.Length; i++)
            RegisterCollider(found[i]);
        ready = true;
    }

    public void ResetSimulation()
    {
        Initialize();
    }

    Vector3[] CreateInitialPositions()
    {
        var positions = new Vector3[activeWidth * activeHeight];
        float originX = -((activeWidth - 1) * spacing) * 0.5f;
        float originZ = -((activeHeight - 1) * spacing) * 0.5f;
        for (int y = 0; y < activeHeight; y++)
        {
            for (int x = 0; x < activeWidth; x++)
            {
                int i = y * activeWidth + x;
                Vector3 jitter = randomRange <= 0f
                    ? Vector3.zero
                    : new Vector3(
                        Random.Range(-randomRange, randomRange),
                        Random.Range(-randomRange, randomRange),
                        Random.Range(-randomRange, randomRange));

                if (layoutMode == 1)
                    positions[i] = new Vector3(originX + x * spacing, dropHeight, originZ + y * spacing) + jitter;
                else
                    positions[i] = new Vector3(originX + x * spacing, y * spacing, 0f) + jitter;
            }
        }
        return positions;
    }

    void CreateMesh(Vector3[] positions)
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "ClothMesh" };
            mesh.MarkDynamic();
        }
        else
        {
            mesh.Clear();
        }

        var uvs = new Vector2[activeWidth * activeHeight];
        var triangles = new int[(activeWidth - 1) * (activeHeight - 1) * 6];

        for (int y = 0; y < activeHeight; y++)
            for (int x = 0; x < activeWidth; x++)
                uvs[y * activeWidth + x] = new Vector2((float)x / (activeWidth - 1), (float)y / (activeHeight - 1));

        int t = 0;
        for (int y = 0; y < activeHeight - 1; y++)
        {
            for (int x = 0; x < activeWidth - 1; x++)
            {
                int i = y * activeWidth + x;
                triangles[t++] = i;
                triangles[t++] = i + activeWidth;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + activeWidth;
                triangles[t++] = i + activeWidth + 1;
            }
        }

        mesh.indexFormat = activeWidth * activeHeight > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = positions;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    void Update()
    {
        if (!ready || clothShader == null) return;

        ApplyMaterial();
        if (paused)
        {
            CacheColliderPrev();
            return;
        }

        UpdateWind();
        PushStaticParameters();
        PruneColliders();
        if (!hasColliderPrev)
            CacheColliderPrev();

        int groupsX = (activeWidth + 7) / 8;
        int groupsY = (activeHeight + 7) / 8;
        float maxMove = 0f;
        float minR = 0.35f;
        for (int c = 0; c < colliders.Count; c++)
        {
            var col = colliders[c];
            maxMove = Mathf.Max(maxMove, (col.transform.position - col.PrevPosition).magnitude);
            minR = Mathf.Min(minR, Mathf.Max(0.05f, col.HalfExtents.x));
        }
        int need = Mathf.CeilToInt(maxMove / Mathf.Max(0.06f, minR * 0.2f));
        int steps = Mathf.Clamp(Mathf.Max(substeps, need), 1, 120);
        float dt = timeStep / steps;

        for (int i = 0; i < steps; i++)
        {
            float ta = i / (float)steps;
            float tb = (i + 1) / (float)steps;
            PushColliders(ta, tb);

            clothShader.SetFloat("deltaTime", dt);
            clothShader.SetBuffer(kernel, "prevPosition", posBuffer);
            clothShader.SetBuffer(kernel, "prevVelocity", velBuffer);
            clothShader.SetBuffer(kernel, "newPosition", newPosBuffer);
            clothShader.SetBuffer(kernel, "newVelocity", newVelBuffer);
            clothShader.Dispatch(kernel, groupsX, groupsY, 1);

            var temp = posBuffer;
            posBuffer = newPosBuffer;
            newPosBuffer = temp;
            temp = velBuffer;
            velBuffer = newVelBuffer;
            newVelBuffer = temp;
        }

        CacheColliderPrev();

        var vertices = new Vector3[activeWidth * activeHeight];
        posBuffer.GetData(vertices);
        bool valid = true;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (float.IsNaN(vertices[i].x) || float.IsNaN(vertices[i].y) || float.IsNaN(vertices[i].z) ||
                float.IsInfinity(vertices[i].x) || float.IsInfinity(vertices[i].y) || float.IsInfinity(vertices[i].z))
            {
                valid = false;
                break;
            }
        }
        if (!valid)
        {
            Debug.LogWarning("Cloth simulation became unstable. Resetting.");
            ResetSimulation();
            paused = true;
            return;
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    void UpdateWind()
    {
        if (!windEnabled)
        {
            wind = Vector3.zero;
            return;
        }

        Vector3 dir = windDirection.sqrMagnitude > 0.0001f ? windDirection.normalized : Vector3.left;
        wind = dir * maxWind;
    }

    public void RegisterCollider(ClothCollider collider)
    {
        if (collider == null || colliders.Contains(collider)) return;
        if (colliders.Count >= MaxColliders) return;
        collider.SyncPrev();
        colliders.Add(collider);
    }

    public void UnregisterCollider(ClothCollider collider)
    {
        colliders.Remove(collider);
    }

    void PruneColliders()
    {
        for (int i = colliders.Count - 1; i >= 0; i--)
        {
            if (colliders[i] == null)
                colliders.RemoveAt(i);
        }
    }

    void CacheColliderPrev()
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            if (colliders[i] != null)
                colliders[i].SyncPrev();
        }
        hasColliderPrev = true;
    }

    void PushStaticParameters()
    {
        clothShader.SetInt("width", activeWidth);
        clothShader.SetInt("height", activeHeight);
        clothShader.SetInt("pinMode", pinMode);
        clothShader.SetFloat("mass", Mathf.Max(0.05f, mass));
        float massScale = 0.2f / Mathf.Max(0.05f, mass);
        clothShader.SetFloat("ks", tension * 320f * massScale);
        clothShader.SetFloat("kb", bending * tension * 20f * massScale);
        clothShader.SetFloat("kd", damping * 25000f);
        clothShader.SetFloat("r", Mathf.Max(0.05f, spacing));
        clothShader.SetFloat("k_air", airResistance);
        clothShader.SetVector("gravity", new Vector4(0f, gravityY * 0.35f, 0f, 0f));
        clothShader.SetVector("wind", wind);
        clothShader.SetFloat("friction", friction);
        clothShader.SetFloat("kr", restitution);
        clothShader.SetFloat("collisionGain", collisionGain);
        clothShader.SetFloat("collisionThickness", collisionThickness);
    }

    void PushColliders(float tWas, float tNow)
    {
        EnsureColliderBuffer();
        int n = Mathf.Min(colliders.Count, MaxColliders);
        for (int i = 0; i < n; i++)
        {
            var col = colliders[i];
            Vector3 curr = col.transform.position;
            Vector3 prev = col.PrevPosition;
            colliderData[i].pos = Vector3.Lerp(prev, curr, tNow);
            colliderData[i].type = (float)(int)col.kind;
            colliderData[i].posPrev = Vector3.Lerp(prev, curr, tWas);
            colliderData[i].pad0 = 0f;
            colliderData[i].halfExt = col.HalfExtents;
            colliderData[i].pad1 = 0f;
        }
        colliderBuffer.SetData(colliderData, 0, 0, Mathf.Max(1, n));
        clothShader.SetBuffer(kernel, "colliders", colliderBuffer);
        clothShader.SetInt("colliderCount", n);
    }

    void EnsureColliderBuffer()
    {
        if (colliderBuffer != null && colliderBuffer.IsValid()) return;
        colliderBuffer = new ComputeBuffer(MaxColliders, Marshal.SizeOf<GpuCollider>());
    }

    void ApplyMaterial()
    {
        if (clothMaterial == null) return;
        clothMaterial.SetColor("_Color", clothColor);
        clothMaterial.SetFloat("_ShowPattern", showPattern ? 1f : 0f);
        clothMaterial.SetFloat("_ShowGrid", showGrid ? 1f : 0f);
        if (clothMaterial.HasProperty("_Smoothness"))
            clothMaterial.SetFloat("_Smoothness", 0.2f);
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    void ReleaseBuffers()
    {
        posBuffer?.Release();
        velBuffer?.Release();
        newPosBuffer?.Release();
        newVelBuffer?.Release();
        colliderBuffer?.Release();
        posBuffer = velBuffer = newPosBuffer = newVelBuffer = colliderBuffer = null;
        ready = false;
    }
}
