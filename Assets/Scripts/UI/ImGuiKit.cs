using System.Collections.Generic;
using UnityEngine;

/// <summary>Small IMGUI helpers shared by the run screens. Sizes are authored for 1080p and scaled.</summary>
public static class ImGuiKit
{
    private static Texture2D circle;
    private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();

    public const float MinUserScale = 0.6f;
    public const float MaxUserScale = 1.8f;
    private const string UserScaleKey = "ui.userScale";
    private static float userScale = -1f;

    /// <summary>Player-tuned multiplier on top of the resolution scale (the pause menu's "HUD size" slider). Saved in PlayerPrefs.</summary>
    public static float UserScale
    {
        get
        {
            if (userScale < 0f) userScale = Mathf.Clamp(PlayerPrefs.GetFloat(UserScaleKey, 1f), MinUserScale, MaxUserScale);
            return userScale;
        }
        set
        {
            userScale = Mathf.Clamp(value, MinUserScale, MaxUserScale);
            PlayerPrefs.SetFloat(UserScaleKey, userScale);
        }
    }

    public static float Scale => Mathf.Max(0.5f, Screen.height / 1080f) * UserScale;
    public static float S(float v) => v * Scale;

    public static readonly Color Ink = new Color(0.08f, 0.07f, 0.12f, 0.92f);
    public static readonly Color Panel = new Color(0.16f, 0.14f, 0.22f, 0.96f);
    public static readonly Color Accent = new Color(1f, 0.85f, 0.35f);
    public static readonly Color Muted = new Color(0.75f, 0.73f, 0.82f);

    public static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static void Label(Rect r, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
    {
        GUIStyle st = Style(Mathf.RoundToInt(S(size)), anchor, bold);
        Color old = GUI.contentColor;
        GUI.contentColor = color;
        GUI.Label(r, text, st);
        GUI.contentColor = old;
    }

    public static readonly Color HudText = Color.white;
    public static readonly Color HudOutline = Color.black;

    /// <summary>In-game HUD text: white with a black outline so it reads over the bright 3D world. <paramref name="alpha"/> fades both.</summary>
    public static void HudLabel(Rect r, string text, float size, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, float alpha = 1f)
    {
        Outlined(r, new GUIContent(text), Style(Mathf.RoundToInt(S(size)), anchor, bold), alpha);
    }

    /// <summary>HUD text with a coloured fill (still black-outlined), e.g. red for low ammo.</summary>
    public static void HudLabel(Rect r, string text, float size, Color fill, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, float alpha = 1f)
    {
        Outlined(r, new GUIContent(text), Style(Mathf.RoundToInt(S(size)), anchor, bold), alpha, fill);
    }

    /// <summary>Same look for callers that own an unscaled <see cref="GUIStyle"/>.</summary>
    public static void HudLabel(Rect r, string text, GUIStyle style, float alpha = 1f)
    {
        Outlined(r, new GUIContent(text), style, alpha);
    }

    private static readonly Vector2[] OutlineDirs =
    {
        new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f),
        new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f),
    };

    private static void Outlined(Rect r, GUIContent content, GUIStyle style, float alpha) => Outlined(r, content, style, alpha, HudText);

    private static void Outlined(Rect r, GUIContent content, GUIStyle style, float alpha, Color fillColor)
    {
        Color old = GUI.contentColor;
        float d = Mathf.Max(1f, S(1.5f));
        Color outline = HudOutline; outline.a = alpha;
        GUI.contentColor = outline;
        for (int i = 0; i < OutlineDirs.Length; i++)
            GUI.Label(new Rect(r.x + OutlineDirs[i].x * d, r.y + OutlineDirs[i].y * d, r.width, r.height), content, style);
        Color fill = fillColor; fill.a = alpha;
        GUI.contentColor = fill;
        GUI.Label(r, content, style);
        GUI.contentColor = old;
    }

    /// <summary>Height the text needs when word-wrapped to <paramref name="width"/> pixels. <paramref name="size"/> is authored (1080p) size.</summary>
    public static float TextHeight(string text, float size, float width, bool bold = false)
    {
        GUIStyle st = Style(Mathf.RoundToInt(S(size)), TextAnchor.UpperLeft, bold);
        return st.CalcHeight(new GUIContent(text ?? string.Empty), Mathf.Max(1f, width));
    }

    /// <summary>Shortens text with "..." until it fits in <paramref name="maxLines"/> wrapped lines of <paramref name="width"/> pixels.</summary>
    public static string Fit(string text, float size, float width, int maxLines, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        GUIStyle st = Style(Mathf.RoundToInt(S(size)), TextAnchor.UpperLeft, bold);
        float limit = st.CalcHeight(new GUIContent("Ag"), 1000f) * Mathf.Max(1, maxLines) + 1f;
        if (st.CalcHeight(new GUIContent(text), Mathf.Max(1f, width)) <= limit) return text;

        string cut = text;
        while (cut.Length > 1)
        {
            cut = cut.Substring(0, cut.Length - 1);
            if (st.CalcHeight(new GUIContent(cut.TrimEnd() + "..."), Mathf.Max(1f, width)) <= limit) return cut.TrimEnd() + "...";
        }
        return "...";
    }

    public static bool Button(Rect r, string text, bool enabled = true, float size = 22f)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Fill(r, !enabled ? new Color(0.25f, 0.24f, 0.3f, 0.8f) : hover ? new Color(1f, 0.85f, 0.35f, 0.95f) : new Color(0.32f, 0.28f, 0.45f, 0.95f));
        Label(r, text, size, !enabled ? new Color(0.6f, 0.6f, 0.65f) : hover ? Ink : Color.white, TextAnchor.MiddleCenter, true);
        if (!enabled) return false;
        return Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && ConsumeClick();
    }

    private static bool ConsumeClick() { Event.current.Use(); return true; }

    public static void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        Matrix4x4 saved = GUI.matrix;
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle, a);
        Fill(new Rect(a.x, a.y - width * 0.5f, len, width), c);
        GUI.matrix = saved;
    }

    public static void Circle(Vector2 center, float radius, Color c)
    {
        if (circle == null) circle = MakeCircle(64);
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), circle);
        GUI.color = old;
    }

    private static Texture2D MakeCircle(int size)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float a = Mathf.Clamp01((r - d) / 1.5f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    private static GUIStyle Style(int size, TextAnchor anchor, bool bold)
    {
        int key = size * 100 + (int)anchor * 2 + (bold ? 1 : 0);
        if (!styles.TryGetValue(key, out GUIStyle st) || st == null)
        {
            st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, wordWrap = true, richText = false };
            st.normal.textColor = Color.white;   // pure white base so contentColor (incl. the HUD outline) is exact
            styles[key] = st;
        }
        return st;
    }

    /// <summary>Dim backdrop plus a centered panel. Returns the panel rect.</summary>
    public static Rect Modal(float width, float height, string title)
    {
        Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0.02f, 0.72f));
        Rect r = new Rect((Screen.width - S(width)) * 0.5f, (Screen.height - S(height)) * 0.5f, S(width), S(height));
        Fill(r, Panel);
        Fill(new Rect(r.x, r.y, r.width, S(6f)), Accent);
        Label(new Rect(r.x + S(24f), r.y + S(18f), r.width - S(48f), S(54f)), title, 40f, Accent, TextAnchor.MiddleLeft, true);
        return r;
    }
}
