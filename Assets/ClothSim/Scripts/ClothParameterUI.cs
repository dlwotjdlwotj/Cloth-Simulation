using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ImGuiNET;
#if UIMGUI_ENABLE_IMGUIZMO_QUAT
using ImGuizmoQuatNET;
#endif
using UImGui;
using UImGui.Assets;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;
using NVec3 = System.Numerics.Vector3;
using NVec4 = System.Numerics.Vector4;

public class ClothParameterUI : MonoBehaviour
{
    public ClothSimulation simulation;

    public static bool BlocksSceneInput { get; private set; }

    const float PanelWidth = 230f;
    const float HierarchyWidth = 240f;
    const float FoldTabW = 18f;
    const float FoldTabH = 56f;

    UImGui.UImGui imgui;
    bool showWindow = true;
    bool hierarchyOpen = true;
    bool paramsOpen = true;
    NVec2 hierFoldMin, hierFoldMax;
    NVec2 paramFoldMin, paramFoldMax;
    NVec2 quitMin, quitMax;
    bool styled;
#if UIMGUI_ENABLE_IMGUIZMO_QUAT
    NVec4 viewQuat = new NVec4(0f, 0f, 0f, 1f);
    bool viewGizmoActive;
#endif
    enum MoveAxis
    {
        None,
        X,
        Y,
        Z,
        YZ,
        XZ,
        XY
    }

    ClothCollider selected;
    MoveAxis moveHover;
    MoveAxis moveActive;
    Vector3 moveStartPos;
    Vector3 moveGrab;
    Material shapeMaterial;
    ClothOrbitCamera orbit;

    void Start()
    {
        if (simulation == null)
            simulation = FindObjectOfType<ClothSimulation>();
        if (simulation == null) return;
        orbit = Camera.main != null ? Camera.main.GetComponent<ClothOrbitCamera>() : null;
        SetupImGui();
    }

    void OnDestroy()
    {
        if (imgui != null)
            imgui.Layout -= OnLayout;
        BlocksSceneInput = false;
    }

    void SetupImGui()
    {
        var cam = Camera.main;
        if (cam == null) return;

        var go = new GameObject("UImGui");
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        imgui = go.AddComponent<UImGui.UImGui>();
        SetPrivate(imgui, "_camera", cam);
        SetPrivate(imgui, "_shaders", Resources.Load("DefaultShader"));
        SetPrivate(imgui, "_cursorShapes", Resources.Load("DefaultCursorShape"));
        TryAssignKoreanFont(imgui);
        imgui.Layout += OnLayout;
        go.SetActive(true);
    }

    static void TryAssignKoreanFont(UImGui.UImGui host)
    {
        string relative = EnsureKoreanFontFile();
        if (string.IsNullOrEmpty(relative)) return;

        var atlas = ScriptableObject.CreateInstance<FontAtlasConfigAsset>();
        var config = new FontConfig
        {
            FontDataOwnedByAtlas = true,
            SizeInPixels = 17f,
            OversampleH = 2,
            OversampleV = 1,
            PixelSnapH = true,
            GlyphMaxAdvanceX = float.MaxValue,
            RasterizerMultiply = 1.05f,
            GlyphRanges = ScriptGlyphRanges.Default | ScriptGlyphRanges.Custom,
            CustomGlyphRanges = UsedKoreanRanges()
        };
        atlas.Fonts = new[] { new FontDefinition { Path = relative, Config = config } };
        SetPrivate(host, "_fontAtlasConfiguration", atlas);
    }

    static Range[] UsedKoreanRanges()
    {
        const string used = "중력바람세기공기저항시작정지실행중일시숨김방향드래그초기화물체추가삭제구큐브실린더종료";
        var codes = new SortedSet<int>();
        foreach (char c in used)
        {
            if (c > 127)
                codes.Add(c);
        }

        var ranges = new List<Range>();
        int start = -1, prev = -1;
        foreach (int code in codes)
        {
            if (start < 0)
            {
                start = prev = code;
                continue;
            }
            if (code == prev + 1)
            {
                prev = code;
                continue;
            }
            ranges.Add(new Range { Start = (ushort)start, End = (ushort)prev });
            start = prev = code;
        }
        if (start >= 0)
            ranges.Add(new Range { Start = (ushort)start, End = (ushort)prev });
        return ranges.ToArray();
    }

    static void SetPrivate(object target, string field, object value)
    {
        if (value == null) return;
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    }

    static string EnsureKoreanFontFile()
    {
        const string name = "malgun.ttf";
        string dest = Path.Combine(Application.streamingAssetsPath, name);
        if (File.Exists(dest)) return name;

        string fonts = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
        foreach (var file in new[] { "malgun.ttf", "malgunsl.ttf", "NanumGothic.ttf" })
        {
            string src = Path.Combine(fonts, file);
            if (!File.Exists(src)) continue;
            Directory.CreateDirectory(Application.streamingAssetsPath);
            File.Copy(src, dest, true);
            return name;
        }
        return null;
    }

    void OnLayout(UImGui.UImGui _)
    {
        if (!styled)
        {
            ApplyStyle();
            styled = true;
        }

        var io = ImGui.GetIO();

        if (ImGui.IsKeyPressed(ImGuiKey.Tab) && !io.WantTextInput)
            showWindow = !showWindow;
        if (simulation == null) return;

        DrawMoveGizmo();
        DrawViewHud();
        if (showWindow)
        {
            DrawTransportBar(io);
            DrawHierarchyPanel();
            DrawParameterPanel();
            DrawFoldTabs();
            DrawQuitButton();
        }

        UpdateSceneInput(io);
    }

    void DrawParameterPanel()
    {
        if (!paramsOpen) return;

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new NVec2(vp.WorkPos.X + vp.WorkSize.X - PanelWidth, vp.WorkPos.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new NVec2(PanelWidth, vp.WorkSize.Y), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.96f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        var panelFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
                         ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar |
                         ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings |
                         ImGuiWindowFlags.NoDocking;
        if (!ImGui.Begin("##params", panelFlags))
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        Slider("중력", ref simulation.gravityY, -20f, 0f, "0.00");

        bool windOn = simulation.windEnabled;
        if (Checkbox("바람", ref windOn))
            simulation.windEnabled = windOn;

        Slider("바람 세기", ref simulation.maxWind, 0f, 4f, "0.00");
        DrawWindAxisGizmo();

        Slider("공기저항", ref simulation.airResistance, 0f, 2f, "0.00");

        if (simulation.maxWind > 0.01f)
            simulation.windEnabled = true;

        ImGui.Dummy(new NVec2(0, 8));
        if (ImGui.Button("초기화", new NVec2(-1, 0)))
            ResetEnvironment();

        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    void DrawHierarchyPanel()
    {
        if (!hierarchyOpen) return;

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new NVec2(HierarchyWidth, vp.WorkSize.Y), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.96f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NVec2(8, 8));

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking;
        if (!ImGui.Begin("##hierarchy", flags))
        {
            ImGui.End();
            ImGui.PopStyleVar(3);
            return;
        }

        float btnW = (ImGui.GetContentRegionAvail().X - 8f) / 3f;
        if (ImGui.Button("구", new NVec2(btnW, 0)))
            AddBody(ClothCollider.Kind.Sphere, PrimitiveType.Sphere, Vector3.one * 1.6f, 0.8f);
        ImGui.SameLine(0, 4);
        if (ImGui.Button("큐브", new NVec2(btnW, 0)))
            AddBody(ClothCollider.Kind.Box, PrimitiveType.Cube, Vector3.one * 1.2f, 0.6f);
        ImGui.SameLine(0, 4);
        if (ImGui.Button("실린더", new NVec2(btnW, 0)))
            AddBody(ClothCollider.Kind.Cylinder, PrimitiveType.Cylinder, new Vector3(1.15f, 1.15f, 1.15f), 1.15f);

        ImGui.Dummy(new NVec2(0, 4));
        ImGui.Separator();

        float deleteH = 32f;
        ImGui.BeginChild("##hierarchylist", new NVec2(0, -deleteH), ImGuiChildFlags.Borders);
        for (int i = 0; i < simulation.colliders.Count; i++)
        {
            var col = simulation.colliders[i];
            if (col == null) continue;
            bool on = selected == col;
            ImGui.PushStyleColor(ImGuiCol.Header, new NVec4(0.24f, 0.37f, 0.58f, 0.55f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new NVec4(0.28f, 0.42f, 0.64f, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new NVec4(0.30f, 0.48f, 0.72f, 0.85f));
            if (ImGui.Selectable("  " + col.gameObject.name, on, ImGuiSelectableFlags.SpanAllColumns))
                selected = col;
            ImGui.PopStyleColor(3);
        }
        ImGui.EndChild();

        if (ImGui.Button("삭제", new NVec2(-1, 0)))
            DeleteSelected();

        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    void DrawFoldTabs()
    {
        var vp = ImGui.GetMainViewport();
        float y = vp.WorkPos.Y + 8f;
        float leftX = hierarchyOpen ? vp.WorkPos.X + HierarchyWidth : vp.WorkPos.X;
        float rightX = paramsOpen
            ? vp.WorkPos.X + vp.WorkSize.X - PanelWidth - FoldTabW
            : vp.WorkPos.X + vp.WorkSize.X - FoldTabW;

        DrawFoldTab("##hierfold", new NVec2(leftX, y), hierarchyOpen ? ImGuiDir.Left : ImGuiDir.Right,
            () => hierarchyOpen = !hierarchyOpen, out hierFoldMin, out hierFoldMax);
        DrawFoldTab("##paramfold", new NVec2(rightX, y), paramsOpen ? ImGuiDir.Right : ImGuiDir.Left,
            () => paramsOpen = !paramsOpen, out paramFoldMin, out paramFoldMax);
    }

    static void DrawFoldTab(string id, NVec2 pos, ImGuiDir dir, System.Action toggle, out NVec2 min, out NVec2 max)
    {
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new NVec2(FoldTabW, FoldTabH), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.96f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, NVec2.Zero);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoFocusOnAppearing;
        if (!ImGui.Begin(id, flags))
        {
            min = pos;
            max = pos + new NVec2(FoldTabW, FoldTabH);
            ImGui.End();
            ImGui.PopStyleVar(3);
            return;
        }

        if (ImGui.InvisibleButton("##tog", ImGui.GetContentRegionAvail()))
            toggle();

        var dl = ImGui.GetWindowDrawList();
        min = ImGui.GetWindowPos();
        max = min + ImGui.GetWindowSize();
        var center = (min + max) * 0.5f;
        uint col = ColorU32(ImGui.IsItemHovered() ? new Color(0.92f, 0.93f, 0.95f) : new Color(0.72f, 0.74f, 0.78f));
        DrawChevron(dl, center, dir, col);

        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    static void DrawChevron(ImDrawListPtr dl, NVec2 center, ImGuiDir dir, uint col)
    {
        float s = 4.5f;
        NVec2 a, b, c;
        if (dir == ImGuiDir.Left)
        {
            a = center + new NVec2(s, -s);
            b = center + new NVec2(-s, 0f);
            c = center + new NVec2(s, s);
        }
        else
        {
            a = center + new NVec2(-s, -s);
            b = center + new NVec2(s, 0f);
            c = center + new NVec2(-s, s);
        }
        dl.AddTriangleFilled(a, b, c, col);
    }

    float HierarchyInset()
    {
        if (!showWindow) return 0f;
        return hierarchyOpen ? HierarchyWidth : FoldTabW;
    }

    static bool InRect(NVec2 p, NVec2 min, NVec2 max)
    {
        return p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y;
    }

    void DrawMoveGizmo()
    {
        if (selected == null)
        {
            moveHover = MoveAxis.None;
            moveActive = MoveAxis.None;
            return;
        }

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 origin = selected.transform.position;
        float len = Mathf.Clamp(Vector3.Distance(cam.transform.position, origin) * 0.14f, 0.4f, 3.5f);
        var io = ImGui.GetIO();
        var mouse = io.MousePos;
        var dl = ImGui.GetForegroundDrawList();

        if (moveActive == MoveAxis.None)
        {
            moveHover = HitMoveAxis(cam, origin, len, mouse);
            if (moveHover != MoveAxis.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                moveActive = moveHover;
                moveStartPos = origin;
                moveGrab = GrabPoint(cam, origin, moveActive);
            }
        }
        else
        {
            ApplyMoveDrag(cam);
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                moveActive = MoveAxis.None;
        }

        DrawAxisArrow(dl, cam, origin, Vector3.right, len, new Color(0.86f, 0.22f, 0.22f), moveHover == MoveAxis.X || moveActive == MoveAxis.X);
        DrawAxisArrow(dl, cam, origin, Vector3.up, len, new Color(0.28f, 0.78f, 0.32f), moveHover == MoveAxis.Y || moveActive == MoveAxis.Y);
        DrawAxisArrow(dl, cam, origin, Vector3.forward, len, new Color(0.28f, 0.48f, 0.95f), moveHover == MoveAxis.Z || moveActive == MoveAxis.Z);
        DrawAxisPlane(dl, cam, origin, Vector3.right, Vector3.up, len, new Color(0.28f, 0.48f, 0.95f), moveHover == MoveAxis.XY || moveActive == MoveAxis.XY);
        DrawAxisPlane(dl, cam, origin, Vector3.right, Vector3.forward, len, new Color(0.28f, 0.78f, 0.32f), moveHover == MoveAxis.XZ || moveActive == MoveAxis.XZ);
        DrawAxisPlane(dl, cam, origin, Vector3.up, Vector3.forward, len, new Color(0.86f, 0.22f, 0.22f), moveHover == MoveAxis.YZ || moveActive == MoveAxis.YZ);

        var center = WorldToImGui(cam, origin);
        if (Valid(center))
            dl.AddCircleFilled(center, 6f, ColorU32(new Color(0.92f, 0.92f, 0.92f)));
    }

    void UpdateSceneInput(ImGuiIOPtr io)
    {
        var vp = ImGui.GetMainViewport();
        var mouse = io.MousePos;
        bool inPanel = showWindow && ((paramsOpen && mouse.X >= vp.WorkPos.X + vp.WorkSize.X - PanelWidth) ||
                                     InRect(mouse, paramFoldMin, paramFoldMax));
        bool inHierarchy = showWindow && ((hierarchyOpen && mouse.X <= vp.WorkPos.X + HierarchyWidth) ||
                                          InRect(mouse, hierFoldMin, hierFoldMax));
        float hudLeft = vp.WorkPos.X + HierarchyInset() + 12f;
        bool inHud = mouse.X >= hudLeft && mouse.X <= hudLeft + 120f && mouse.Y >= vp.WorkPos.Y + vp.WorkSize.Y - 140f;
        bool inTransport = showWindow && Mathf.Abs(mouse.X - (vp.WorkPos.X + vp.WorkSize.X * 0.5f)) < 70f &&
                           mouse.Y <= vp.WorkPos.Y + 50f;
        bool inQuit = showWindow && InRect(mouse, quitMin, quitMax);
        bool gizmoBusy = moveHover != MoveAxis.None || moveActive != MoveAxis.None;
        bool viewBusy = false;
#if UIMGUI_ENABLE_IMGUIZMO_QUAT
        viewBusy = viewGizmoActive;
#endif
        BlocksSceneInput = inPanel || inHierarchy || inHud || inTransport || inQuit || gizmoBusy || viewBusy;

        if (selected != null && ImGui.IsKeyPressed(ImGuiKey.Delete) && !io.WantTextInput)
            DeleteSelected();

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !inPanel && !inHierarchy && !inHud && !inTransport && !inQuit && !gizmoBusy && !viewBusy)
            TryPickObject();
    }

    void TryPickObject()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit))
        {
            var col = hit.transform.GetComponent<ClothCollider>();
            if (col != null)
            {
                selected = col;
                return;
            }
        }
        selected = null;
    }

    void AddBody(ClothCollider.Kind kind, PrimitiveType type, Vector3 scale, float y)
    {
        if (simulation.colliders.Count >= ClothSimulation.MaxColliders) return;
        int n = 1;
        for (int i = 0; i < simulation.colliders.Count; i++)
        {
            if (simulation.colliders[i] != null && simulation.colliders[i].kind == kind)
                n++;
        }

        var go = GameObject.CreatePrimitive(type);
        go.name = kind + " " + n;
        go.transform.position = new Vector3(Random.Range(-1.4f, 1.4f), y, Random.Range(-0.8f, 0.8f));
        go.transform.localScale = scale;
        if (shapeMaterial == null)
            shapeMaterial = MakeShapeMaterial();
        go.GetComponent<MeshRenderer>().sharedMaterial = shapeMaterial;
        var col = go.AddComponent<ClothCollider>();
        col.kind = kind;
        simulation.RegisterCollider(col);
        selected = col;
    }

    void DeleteSelected()
    {
        if (selected == null) return;
        simulation.UnregisterCollider(selected);
        Destroy(selected.gameObject);
        selected = null;
    }

    static Material MakeShapeMaterial()
    {
        var shader = Shader.Find("Standard");
        var mat = shader != null ? new Material(shader) : new Material(Shader.Find("Diffuse"));
        mat.color = new Color(0.91f, 0.91f, 0.92f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.28f);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
        return mat;
    }

    static Vector3 AxisDir(MoveAxis axis)
    {
        if (axis == MoveAxis.X) return Vector3.right;
        if (axis == MoveAxis.Y) return Vector3.up;
        return Vector3.forward;
    }

    static Vector3 PlaneNormal(MoveAxis axis)
    {
        if (axis == MoveAxis.XY) return Vector3.forward;
        if (axis == MoveAxis.XZ) return Vector3.up;
        return Vector3.right;
    }

    MoveAxis HitMoveAxis(Camera cam, Vector3 origin, float len, NVec2 mouse)
    {
        if (InsidePlane(cam, origin, Vector3.right, Vector3.up, len, mouse)) return MoveAxis.XY;
        if (InsidePlane(cam, origin, Vector3.right, Vector3.forward, len, mouse)) return MoveAxis.XZ;
        if (InsidePlane(cam, origin, Vector3.up, Vector3.forward, len, mouse)) return MoveAxis.YZ;

        float best = 14f;
        MoveAxis hit = MoveAxis.None;
        TryAxis(cam, origin, Vector3.right, len, MoveAxis.X, mouse, ref best, ref hit);
        TryAxis(cam, origin, Vector3.up, len, MoveAxis.Y, mouse, ref best, ref hit);
        TryAxis(cam, origin, Vector3.forward, len, MoveAxis.Z, mouse, ref best, ref hit);
        return hit;
    }

    static void TryAxis(Camera cam, Vector3 origin, Vector3 dir, float len, MoveAxis axis, NVec2 mouse, ref float best, ref MoveAxis hit)
    {
        var a = WorldToImGui(cam, origin + dir * (len * 0.38f));
        var b = WorldToImGui(cam, origin + dir * len);
        if (!Valid(a) || !Valid(b)) return;
        float d = DistToSegment(mouse, a, b);
        if (d < best)
        {
            best = d;
            hit = axis;
        }
    }

    static bool InsidePlane(Camera cam, Vector3 origin, Vector3 a, Vector3 b, float len, NVec2 mouse)
    {
        float s = len * 0.32f;
        var p0 = WorldToImGui(cam, origin);
        var p1 = WorldToImGui(cam, origin + a * s);
        var p2 = WorldToImGui(cam, origin + a * s + b * s);
        var p3 = WorldToImGui(cam, origin + b * s);
        if (!Valid(p0) || !Valid(p1) || !Valid(p2) || !Valid(p3)) return false;
        return PointInPoly(mouse, p0, p1, p2, p3);
    }

    Vector3 GrabPoint(Camera cam, Vector3 origin, MoveAxis axis)
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (axis == MoveAxis.X || axis == MoveAxis.Y || axis == MoveAxis.Z)
        {
            ClosestOnAxis(ray, origin, AxisDir(axis), out Vector3 p);
            return p;
        }

        if (RaycastPlane(ray, PlaneNormal(axis), origin, out Vector3 hit))
            return hit;
        return origin;
    }

    void ApplyMoveDrag(Camera cam)
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (moveActive == MoveAxis.X || moveActive == MoveAxis.Y || moveActive == MoveAxis.Z)
        {
            Vector3 dir = AxisDir(moveActive);
            if (!ClosestOnAxis(ray, moveStartPos, dir, out Vector3 now)) return;
            selected.transform.position = moveStartPos + Vector3.Project(now - moveGrab, dir);
            return;
        }

        Vector3 n = PlaneNormal(moveActive);
        if (!RaycastPlane(ray, n, moveStartPos, out Vector3 planeHit)) return;
        selected.transform.position = moveStartPos + Vector3.ProjectOnPlane(planeHit - moveGrab, n);
    }

    static bool RaycastPlane(Ray ray, Vector3 normal, Vector3 point, out Vector3 hit)
    {
        float denom = Vector3.Dot(normal, ray.direction);
        if (Mathf.Abs(denom) < 1e-5f)
        {
            hit = point;
            return false;
        }
        float t = Vector3.Dot(point - ray.origin, normal) / denom;
        if (t < 0f)
        {
            hit = point;
            return false;
        }
        hit = ray.origin + ray.direction * t;
        return true;
    }

    static bool ClosestOnAxis(Ray ray, Vector3 origin, Vector3 axis, out Vector3 point)
    {
        axis.Normalize();
        Vector3 w0 = origin - ray.origin;
        float b = Vector3.Dot(axis, ray.direction);
        float c = Vector3.Dot(axis, w0);
        float d = Vector3.Dot(ray.direction, w0);
        float denom = 1f - b * b;
        if (Mathf.Abs(denom) < 1e-5f)
        {
            point = origin;
            return false;
        }
        float s = (b * d - c) / denom;
        point = origin + axis * s;
        return true;
    }

    static void DrawAxisArrow(ImDrawListPtr dl, Camera cam, Vector3 origin, Vector3 dir, float len, Color color, bool active)
    {
        var a = WorldToImGui(cam, origin);
        var b = WorldToImGui(cam, origin + dir * len);
        if (!Valid(a) || !Valid(b)) return;
        uint col = ColorU32(active ? Color.white : color);
        dl.AddLine(a, b, col, active ? 4.5f : 3.2f);
        NVec2 d = b - a;
        float mag = Mathf.Sqrt(d.X * d.X + d.Y * d.Y);
        if (mag < 2f) return;
        var n = new NVec2(d.X / mag, d.Y / mag);
        var perp = new NVec2(-n.Y, n.X);
        dl.AddTriangleFilled(b, b - n * 16f + perp * 7f, b - n * 16f - perp * 7f, col);
    }

    static void DrawAxisPlane(ImDrawListPtr dl, Camera cam, Vector3 origin, Vector3 a, Vector3 b, float len, Color color, bool active)
    {
        float s = len * 0.28f;
        var p0 = WorldToImGui(cam, origin);
        var p1 = WorldToImGui(cam, origin + a * s);
        var p2 = WorldToImGui(cam, origin + a * s + b * s);
        var p3 = WorldToImGui(cam, origin + b * s);
        if (!Valid(p0) || !Valid(p1) || !Valid(p2) || !Valid(p3)) return;
        Color fill = color;
        fill.a = active ? 0.45f : 0.18f;
        uint fillCol = ColorU32(fill);
        uint lineCol = ColorU32(color);
        dl.AddTriangleFilled(p0, p1, p2, fillCol);
        dl.AddTriangleFilled(p0, p2, p3, fillCol);
        dl.AddLine(p0, p1, lineCol, 1.5f);
        dl.AddLine(p1, p2, lineCol, 1.5f);
        dl.AddLine(p2, p3, lineCol, 1.5f);
        dl.AddLine(p3, p0, lineCol, 1.5f);
    }

    static NVec2 WorldToImGui(Camera cam, Vector3 world)
    {
        Vector3 s = cam.WorldToScreenPoint(world);
        if (s.z <= 0.05f) return new NVec2(float.NaN, float.NaN);
        var io = ImGui.GetIO();
        float x = s.x * io.DisplaySize.X / Mathf.Max(1f, Screen.width);
        float y = (Screen.height - s.y) * io.DisplaySize.Y / Mathf.Max(1f, Screen.height);
        return new NVec2(x, y);
    }

    static bool Valid(NVec2 p) => !float.IsNaN(p.X) && !float.IsNaN(p.Y);

    static float DistToSegment(NVec2 p, NVec2 a, NVec2 b)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float l2 = dx * dx + dy * dy;
        if (l2 < 1e-4f) return (p - a).Length();
        float t = Mathf.Clamp01(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2);
        float x = a.X + t * dx - p.X;
        float y = a.Y + t * dy - p.Y;
        return Mathf.Sqrt(x * x + y * y);
    }

    static bool PointInPoly(NVec2 p, NVec2 a, NVec2 b, NVec2 c, NVec2 d)
    {
        bool pos = true;
        bool neg = true;
        CrossSign(p, a, b, ref pos, ref neg);
        CrossSign(p, b, c, ref pos, ref neg);
        CrossSign(p, c, d, ref pos, ref neg);
        CrossSign(p, d, a, ref pos, ref neg);
        return pos || neg;
    }

    static void CrossSign(NVec2 p, NVec2 a, NVec2 b, ref bool pos, ref bool neg)
    {
        float cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        if (cross < -0.01f) pos = false;
        if (cross > 0.01f) neg = false;
    }

    void ResetEnvironment()
    {
        simulation.gravityY = -9.81f;
        simulation.airResistance = 0.55f;
        simulation.maxWind = 0f;
        simulation.windY = 0f;
        simulation.windDirection = new Vector3(-1f, 0f, 0f);
        simulation.windEnabled = false;
        simulation.gustEnabled = false;
    }

    void DrawViewHud()
    {
        const float size = 96f;
        const float pad = 10f;
        var vp = ImGui.GetMainViewport();
        float left = HierarchyInset();
        ImGui.SetNextWindowPos(new NVec2(vp.WorkPos.X + left + 12f, vp.WorkPos.Y + vp.WorkSize.Y - 12f), ImGuiCond.Always, new NVec2(0f, 1f));
        ImGui.SetNextWindowSize(new NVec2(size + pad * 2f, size + pad * 2f), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.42f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NVec2(pad, pad));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing;
        if (ImGui.Begin("##viewhud", flags))
            DrawViewGizmo3D("##viewgizmo", size);
        ImGui.End();
        ImGui.PopStyleVar(3);
    }

    void DrawWindAxisGizmo()
    {
        ImGui.Dummy(new NVec2(0, 4));
        ImGui.TextDisabled("방향");
        const float size = 108f;
        float indent = Mathf.Max(0f, (ImGui.GetContentRegionAvail().X - size) * 0.5f);
        if (indent > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + indent);
        DrawWindGizmo3D("##winddir", size);
        ImGui.PushStyleColor(ImGuiCol.Text, new NVec4(1f, 0.82f, 0.28f, 1f));
        ImGui.Text("바람");
        ImGui.PopStyleColor();
    }

    void DrawWindGizmo3D(string id, float size)
    {
#if UIMGUI_ENABLE_IMGUIZMO_QUAT
        Quaternion view = ScreenRotation();
        Vector3 viewWind = Quaternion.Inverse(view) * CurrentWind();
        // Widget faces the viewer. Unity camera +Z goes into the scene, so only Z is flipped.
        // XY stay as on-screen right/up — that undoes the 180° 2D-plane mismatch.
        var dir = new NVec3(-viewWind.x, -viewWind.y, viewWind.z);
        const uint modeDirection = 0x0002u;
        if (ImGuizmoQuat.gizmo3D(id, ref dir, size, modeDirection))
        {
            var incoming = new Vector3(-dir.X, -dir.Y, dir.Z);
            if (incoming.sqrMagnitude > 1e-8f)
                simulation.windDirection = (view * incoming.normalized).normalized;
            simulation.windEnabled = true;
            if (simulation.maxWind < 0.15f)
                simulation.maxWind = 0.8f;
        }
#else
        ImGui.Dummy(new NVec2(size, size));
#endif
    }

    void DrawViewGizmo3D(string id, float size)
    {
#if UIMGUI_ENABLE_IMGUIZMO_QUAT
        if (orbit == null && Camera.main != null)
            orbit = Camera.main.GetComponent<ClothOrbitCamera>();

        if (!viewGizmoActive)
            viewQuat = QuatToVec(Quaternion.Inverse(ViewRotation()));

        const uint flags = 0x0001u | 0x0100u;
        bool changed = ImGuizmoQuat.gizmo3D(id, ref viewQuat, size, flags);
        viewGizmoActive = changed || ImGui.IsItemActive();
        if (changed && orbit != null)
            orbit.SetOrbitRotation(Quaternion.Inverse(VecToQuat(viewQuat)));
        DrawWindArrowOnLastItem(VecToQuat(viewQuat) * CurrentWind());
#else
        ImGui.Dummy(new NVec2(size, size));
#endif
    }

#if UIMGUI_ENABLE_IMGUIZMO_QUAT
    Quaternion ViewRotation()
    {
        if (orbit != null)
            return orbit.OrbitRotation;
        return ScreenRotation();
    }

    static Quaternion ScreenRotation()
    {
        return Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
    }

    static NVec4 QuatToVec(Quaternion q) => new NVec4(q.x, q.y, q.z, q.w);
    static Quaternion VecToQuat(NVec4 q) => new Quaternion(q.X, q.Y, q.Z, q.W);

    void DrawWindArrowOnLastItem(Vector3 widgetDir)
    {
        if (widgetDir.sqrMagnitude < 1e-6f) return;
        widgetDir.Normalize();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var center = (min + max) * 0.5f;
        float scale = (max.X - min.X) * 0.42f;

        var tip = ProjectGizmo(center, scale, widgetDir);
        Vector3 side = Vector3.Cross(widgetDir, Vector3.forward);
        if (side.sqrMagnitude < 1e-4f)
            side = Vector3.Cross(widgetDir, Vector3.right);
        side.Normalize();
        var left = ProjectGizmo(center, scale, widgetDir * 0.72f + side * 0.16f);
        var right = ProjectGizmo(center, scale, widgetDir * 0.72f - side * 0.16f);

        var dl = ImGui.GetWindowDrawList();
        uint col = ColorU32(new Color(1f, 0.78f, 0.16f, 1f));
        dl.AddLine(center, tip, col, 4.2f);
        dl.AddTriangleFilled(tip, left, right, col);
        dl.AddText(tip + new NVec2(7f, -10f), col, "W");
    }

    static NVec2 ProjectGizmo(NVec2 center, float scale, Vector3 p)
    {
        return center + new NVec2(p.x, -p.y) * scale;
    }
#endif

    Vector3 CurrentWind()
    {
        return simulation != null && simulation.windDirection.sqrMagnitude > 0.0001f
            ? simulation.windDirection.normalized
            : Vector3.left;
    }

    void DrawQuitButton()
    {
        const float w = 72f;
        const float h = 32f;
        var vp = ImGui.GetMainViewport();
        float right = paramsOpen ? PanelWidth : FoldTabW;
        ImGui.SetNextWindowPos(new NVec2(vp.WorkPos.X + vp.WorkSize.X - right - 8f, vp.WorkPos.Y + 8f), ImGuiCond.Always, new NVec2(1f, 0f));
        ImGui.SetNextWindowSize(new NVec2(w, h), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.92f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NVec2(4, 4));
        ImGui.PushStyleColor(ImGuiCol.Button, new NVec4(0.42f, 0.16f, 0.16f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NVec4(0.58f, 0.20f, 0.20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new NVec4(0.70f, 0.22f, 0.22f, 1f));
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoDocking;
        if (ImGui.Begin("##quit", flags))
        {
            if (ImGui.Button("종료", ImGui.GetContentRegionAvail()))
                QuitApp();
            quitMin = ImGui.GetWindowPos();
            quitMax = quitMin + ImGui.GetWindowSize();
        }
        ImGui.End();
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
    }

    static void QuitApp()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void DrawTransportBar(ImGuiIOPtr io)
    {
        const float btn = 28f;
        const float gap = 6f;
        const float pad = 2f;
        float width = pad * 2f + btn * 3f + gap * 2f;
        float height = pad * 2f + btn;

        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new NVec2(vp.WorkPos.X + vp.WorkSize.X * 0.5f, vp.WorkPos.Y + 8f), ImGuiCond.Always, new NVec2(0.5f, 0f));
        ImGui.SetNextWindowSize(new NVec2(width, height), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NVec2(pad, pad));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoBackground;
        if (!ImGui.Begin("##transport", flags))
        {
            ImGui.PopStyleVar(2);
            ImGui.End();
            return;
        }

        if (IconButton("##play", btn, DrawPlay, !simulation.paused))
            simulation.paused = false;
        ImGui.SameLine(0, gap);
        if (IconButton("##pause", btn, DrawPause, false))
            simulation.paused = true;
        ImGui.SameLine(0, gap);
        if (IconButton("##stop", btn, DrawStop, false))
        {
            simulation.ResetSimulation();
            simulation.paused = true;
        }

        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    static bool IconButton(string id, float size, System.Action<ImDrawListPtr, NVec2, float> draw, bool active)
    {
        var p = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton(id, new NVec2(size, size));
        var dl = ImGui.GetWindowDrawList();
        bool hover = ImGui.IsItemHovered();
        uint bg = ColorU32(active ? new Color(0.22f, 0.48f, 0.42f) : hover ? new Color(0.24f, 0.24f, 0.26f) : new Color(0.17f, 0.17f, 0.18f));
        dl.AddRectFilled(p, p + new NVec2(size, size), bg, 6f);
        draw(dl, p, size);
        return clicked;
    }

    static void DrawPlay(ImDrawListPtr dl, NVec2 p, float size)
    {
        float s = size * 0.22f;
        var a = p + new NVec2(size * 0.38f, size * 0.30f);
        var b = p + new NVec2(size * 0.38f, size * 0.70f);
        var c = p + new NVec2(size * 0.70f, size * 0.50f);
        dl.AddTriangleFilled(a, b, c, ColorU32(Color.white));
        _ = s;
    }

    static void DrawPause(ImDrawListPtr dl, NVec2 p, float size)
    {
        float x = size * 0.32f;
        float y = size * 0.30f;
        float w = size * 0.11f;
        float h = size * 0.40f;
        uint col = ColorU32(Color.white);
        dl.AddRectFilled(p + new NVec2(x, y), p + new NVec2(x + w, y + h), col, 2f);
        dl.AddRectFilled(p + new NVec2(size - x - w, y), p + new NVec2(size - x, y + h), col, 2f);
    }

    static void DrawStop(ImDrawListPtr dl, NVec2 p, float size)
    {
        float pad = size * 0.30f;
        dl.AddRectFilled(p + new NVec2(pad, pad), p + new NVec2(size - pad, size - pad), ColorU32(Color.white), 3f);
    }

    static bool Checkbox(string label, ref bool value)
    {
        bool v = value;
        bool changed = ImGui.Checkbox(label, ref v);
        if (changed)
            value = v;
        return changed;
    }

    static void Slider(string label, ref float value, float min, float max, string format)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.Text(label);
        ImGui.SameLine(78);
        ImGui.SetNextItemWidth(-58);
        float v = value;
        if (ImGui.SliderFloat("##" + label, ref v, min, max, ""))
            value = v;
        ImGui.SameLine();
        ImGui.Text(v.ToString(format));
    }

    static void ApplyStyle()
    {
        var s = ImGui.GetStyle();
        s.WindowRounding = 14f;
        s.ChildRounding = 10f;
        s.FrameRounding = 8f;
        s.GrabRounding = 8f;
        s.PopupRounding = 10f;
        s.ScrollbarRounding = 8f;
        s.WindowBorderSize = 0f;
        s.FrameBorderSize = 0f;
        s.WindowPadding = new NVec2(14, 12);
        s.FramePadding = new NVec2(8, 4);
        s.ItemSpacing = new NVec2(8, 6);
        s.GrabMinSize = 14f;
        s.WindowTitleAlign = new NVec2(0.02f, 0.5f);

        Set(ImGuiCol.Text, 0.93f, 0.93f, 0.94f);
        Set(ImGuiCol.TextDisabled, 0.55f, 0.56f, 0.58f);
        Set(ImGuiCol.WindowBg, 0.11f, 0.11f, 0.12f, 0.96f);
        Set(ImGuiCol.TitleBg, 0.13f, 0.13f, 0.14f);
        Set(ImGuiCol.TitleBgActive, 0.15f, 0.16f, 0.16f);
        Set(ImGuiCol.Border, 0.20f, 0.20f, 0.22f, 0.4f);
        Set(ImGuiCol.FrameBg, 0.16f, 0.16f, 0.18f);
        Set(ImGuiCol.FrameBgHovered, 0.20f, 0.21f, 0.22f);
        Set(ImGuiCol.FrameBgActive, 0.22f, 0.24f, 0.24f);
        Set(ImGuiCol.SliderGrab, 0.78f, 0.82f, 0.80f);
        Set(ImGuiCol.SliderGrabActive, 0.90f, 0.93f, 0.91f);
        Set(ImGuiCol.Button, 0.17f, 0.17f, 0.18f);
        Set(ImGuiCol.ButtonHovered, 0.24f, 0.24f, 0.26f);
        Set(ImGuiCol.ButtonActive, 0.22f, 0.48f, 0.42f);
        Set(ImGuiCol.Header, 0.16f, 0.16f, 0.17f);
        Set(ImGuiCol.HeaderHovered, 0.20f, 0.22f, 0.22f);
        Set(ImGuiCol.HeaderActive, 0.22f, 0.48f, 0.42f, 0.35f);
        Set(ImGuiCol.Separator, 0.22f, 0.22f, 0.24f);
        Set(ImGuiCol.ResizeGrip, 0.22f, 0.48f, 0.42f, 0.25f);
        Set(ImGuiCol.ResizeGripHovered, 0.22f, 0.48f, 0.42f, 0.55f);
        Set(ImGuiCol.ResizeGripActive, 0.22f, 0.48f, 0.42f, 0.8f);
        Set(ImGuiCol.CheckMark, 0.45f, 0.78f, 0.68f);
    }

    static void Set(ImGuiCol col, float r, float g, float b, float a = 1f)
    {
        ImGui.GetStyle().Colors[(int)col] = new NVec4(r, g, b, a);
    }

    static uint ColorU32(Color c)
    {
        return ImGui.ColorConvertFloat4ToU32(new NVec4(c.r, c.g, c.b, c.a));
    }
}
