using UnityEngine;

public class ClothSimBootstrap : MonoBehaviour
{
    public ComputeShader clothShader;
    public int width = 72;
    public int height = 72;

    const float ClothWorldSize = 4.9f;

    void Awake()
    {
        if (FindObjectOfType<ClothSimulation>() != null)
            return;

        if (clothShader == null)
            clothShader = Resources.Load<ComputeShader>("Cloth");

        var clothShaderGraph = Shader.Find("Custom/Cloth");
        var material = clothShaderGraph != null ? new Material(clothShaderGraph) { name = "ClothRuntime" } : null;

        SetupLighting();
        var cam = SetupCamera();
        CreateFloor();
        try { CreateAxisGizmo(); }
        catch (System.Exception) { }

        var white = MakeLit(new Color(0.91f, 0.91f, 0.92f));
        var sphere = CreateShape(PrimitiveType.Sphere, "Sphere", new Vector3(-1.65f, 0.8f, 0f), Vector3.one * 1.6f, white, ClothCollider.Kind.Sphere);
        var cube = CreateShape(PrimitiveType.Cube, "Cube", new Vector3(0f, 0.6f, 0.05f), Vector3.one * 1.2f, white, ClothCollider.Kind.Box);
        var cylinder = CreateShape(PrimitiveType.Cylinder, "Cylinder", new Vector3(1.7f, 1.15f, 0f), new Vector3(1.15f, 1.15f, 1.15f), white, ClothCollider.Kind.Cylinder);

        var clothGo = new GameObject("Cloth");
        var cloth = clothGo.AddComponent<ClothSimulation>();
        cloth.clothShader = clothShader;
        cloth.clothMaterial = material;
        cloth.RegisterCollider(sphere.GetComponent<ClothCollider>());
        cloth.RegisterCollider(cube.GetComponent<ClothCollider>());
        cloth.RegisterCollider(cylinder.GetComponent<ClothCollider>());
        cloth.width = 72;
        cloth.height = 72;
        cloth.paused = true;
        cloth.dropHeight = 3.4f;
        cloth.layoutMode = 1;
        cloth.randomRange = 0f;
        cloth.damping = 0.02f;
        cloth.airResistance = 0.55f;
        cloth.tension = 15f;
        cloth.spacing = ClothWorldSize / 71f;
        cloth.collisionThickness = 0.05f;
        cloth.collisionGain = 1f;
        cloth.clothColor = Color.white;
        cloth.showGrid = false;

        var uiGo = new GameObject("ClothParameterUI");
        uiGo.AddComponent<ClothParameterUI>().simulation = cloth;

        var orbit = cam.GetComponent<ClothOrbitCamera>() ?? cam.gameObject.AddComponent<ClothOrbitCamera>();
        orbit.target = clothGo.transform;
        orbit.targetOffset = new Vector3(0f, 1.15f, 0f);
        orbit.distance = 11.5f;
        orbit.yaw = 32f;
        orbit.pitch = 28f;
    }

    static GameObject CreateShape(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, ClothCollider.Kind kind)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.AddComponent<ClothCollider>().kind = kind;
        return go;
    }

    static Material MakeLit(Color color)
    {
        var shader = FindShader("Standard", "Diffuse", "Custom/UnlitColor", "Unlit/Color", "Custom/Cloth");
        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.28f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
        return mat;
    }

    static Shader FindShader(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var s = Shader.Find(names[i]);
            if (s != null) return s;
        }
        return Shader.Find("Hidden/InternalErrorShader");
    }

    static void CreateFloor()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "GridFloor";
        floor.transform.position = Vector3.zero;
        floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localScale = Vector3.one * 80f;
        var col = floor.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var shader = Shader.Find("Custom/GridFloor");
        if (shader != null)
            floor.GetComponent<MeshRenderer>().material = new Material(shader);
        else
            floor.GetComponent<MeshRenderer>().material.color = new Color(0.18f, 0.18f, 0.19f);
    }

    static void CreateAxisGizmo()
    {
        var root = new GameObject("AxisGizmo");
        root.transform.position = new Vector3(-0.15f, 0.02f, -0.15f);
        MakeAxis(root.transform, Vector3.right, new Color(0.86f, 0.22f, 0.22f), 0.55f);
        MakeAxis(root.transform, Vector3.up, new Color(0.28f, 0.72f, 0.32f), 0.55f);
        MakeAxis(root.transform, Vector3.forward, new Color(0.25f, 0.45f, 0.92f), 0.55f);
    }

    static void MakeAxis(Transform parent, Vector3 dir, Color color, float length)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Axis";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = parent.position + dir * (length * 0.5f);
        go.transform.up = dir;
        go.transform.localScale = new Vector3(0.025f, length * 0.5f, 0.025f);
        var shader = FindShader("Custom/UnlitColor", "Unlit/Color", "Standard", "Diffuse");
        if (shader == null) return;
        var mat = new Material(shader);
        mat.color = color;
        go.GetComponent<MeshRenderer>().material = mat;
    }

    static Camera SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.16f, 0.17f, 1f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 120f;
        cam.transform.position = new Vector3(5.5f, 6.2f, -8.5f);
        cam.transform.LookAt(new Vector3(0f, 1.1f, 0f));
        return cam;
    }

    static void SetupLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.34f);

        var light = FindObjectOfType<Light>();
        if (light == null)
        {
            var go = new GameObject("Directional Light");
            light = go.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.color = new Color(1f, 0.98f, 0.95f);
        light.intensity = 1.05f;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
    }
}

public static class ClothSimAutoStart
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindObjectOfType<ClothSimulation>() != null) return;
        if (Object.FindObjectOfType<ClothSimBootstrap>() != null) return;
        new GameObject("ClothSimBootstrap").AddComponent<ClothSimBootstrap>();
    }
}
