using UnityEngine;

public static class UISprites
{
    public static Sprite Rounded { get; private set; }
    public static Sprite Circle { get; private set; }
    public static Sprite Play { get; private set; }
    public static Sprite Pause { get; private set; }
    public static Sprite Stop { get; private set; }
    public static Sprite Reset { get; private set; }
    public static Sprite Cube { get; private set; }

    public static void Ensure()
    {
        if (Rounded != null) return;
        Rounded = MakeRounded(64, 16);
        Circle = MakeCircle(128);
        Play = MakePlay(64);
        Pause = MakePause(64);
        Stop = MakeStop(64);
        Reset = MakeReset(64);
        Cube = MakeCube(64);
    }

    static Sprite MakeRounded(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float ax = CornerDist(x, size, radius);
                float ay = CornerDist(y, size, radius);
                float a = 1f;
                if (ax > 0f && ay > 0f)
                {
                    float d = Mathf.Sqrt(ax * ax + ay * ay) - radius;
                    a = Mathf.Clamp01(0.5f - d);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        var border = Vector4.one * radius;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
    }

    static float CornerDist(int p, int size, int radius)
    {
        if (p < radius) return radius - p;
        if (p >= size - radius) return p - (size - radius - 1);
        return 0f;
    }

    static Sprite MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        float r = c - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - r;
                float a = Mathf.Clamp01(0.5f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakePlay(int size)
    {
        var tex = Clear(size);
        int m = size / 2;
        for (int y = 12; y < size - 12; y++)
        {
            float t = (y - 12) / (float)(size - 25);
            int w = Mathf.RoundToInt(Mathf.Lerp(0, size * 0.42f, t < 0.5f ? t * 2f : (1f - t) * 2f));
            for (int x = m - 8; x < m - 8 + w; x++)
                if (x >= 0 && x < size) tex.SetPixel(x, y, Color.white);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakePause(int size)
    {
        var tex = Clear(size);
        FillRect(tex, 18, 12, 10, size - 24);
        FillRect(tex, 36, 12, 10, size - 24);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeStop(int size)
    {
        var tex = Clear(size);
        int pad = 16;
        FillRect(tex, pad, pad, size - pad * 2, size - pad * 2);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeReset(int size)
    {
        var tex = Clear(size);
        float c = (size - 1) * 0.5f;
        float r = size * 0.28f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c;
                float dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                bool ring = Mathf.Abs(d - r) < 3.2f && ang > -2.2f && ang < 2.5f;
                if (ring) tex.SetPixel(x, y, Color.white);
            }
        }
        for (int i = 0; i < 14; i++)
        {
            int x = Mathf.RoundToInt(c + r - 2 + i * 0.15f);
            int y = Mathf.RoundToInt(c + r - i * 0.9f);
            FillRect(tex, x - 1, y - 1, 4, 4);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeCube(int size)
    {
        var tex = Clear(size);
        int s = 18;
        int ox = 14, oy = 16;
        DrawLine(tex, ox, oy + 8, ox + s, oy + 8);
        DrawLine(tex, ox, oy + 8, ox, oy + 8 + s);
        DrawLine(tex, ox, oy + 8 + s, ox + s, oy + 8 + s);
        DrawLine(tex, ox + s, oy + 8, ox + s, oy + 8 + s);
        DrawLine(tex, ox, oy + 8 + s, ox + 10, oy + 8 + s + 8);
        DrawLine(tex, ox + s, oy + 8 + s, ox + s + 10, oy + 8 + s + 8);
        DrawLine(tex, ox, oy + 8, ox + 10, oy + 16);
        DrawLine(tex, ox + s, oy + 8, ox + s + 10, oy + 16);
        DrawLine(tex, ox + 10, oy + 16, ox + s + 10, oy + 16);
        DrawLine(tex, ox + 10, oy + 16, ox + 10, oy + 16 + s);
        DrawLine(tex, ox + s + 10, oy + 16, ox + s + 10, oy + 16 + s);
        DrawLine(tex, ox + 10, oy + 16 + s, ox + s + 10, oy + 16 + s);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Texture2D Clear(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var clear = new Color(1, 1, 1, 0);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, clear);
        return tex;
    }

    static void FillRect(Texture2D tex, int x, int y, int w, int h)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                if (xx >= 0 && yy >= 0 && xx < tex.width && yy < tex.height)
                    tex.SetPixel(xx, yy, Color.white);
    }

    static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1)
    {
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            FillRect(tex, x0 - 1, y0 - 1, 3, 3);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}
