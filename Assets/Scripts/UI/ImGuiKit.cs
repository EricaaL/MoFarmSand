using System.Collections.Generic;
using UnityEngine;

/// <summary>Small IMGUI helpers shared by the run screens. Sizes are authored for 1080p and scaled. Look comes from <see cref="UiTheme"/>.</summary>
public static class ImGuiKit
{
    public enum Face { Body, Bold, Italic, Display }
    /// <summary>Cream = cream face for orange / backdrop surfaces. Panel = grey-white face for cream surfaces (turns orange on hover).</summary>
    public enum PillKind { Cream, Primary, Light, Gray, Panel }

    private static Texture2D circle;
    private static GUIStyle roundSmall, roundLarge;
    private static readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();

    public const float MinUserScale = 0.6f;
    public const float MaxUserScale = 1.8f;
    private const string UserScaleKey = "ui.userScale";
    private static float userScale = -1f;

    private static UiTheme theme;
    /// <summary>The Canva look (fonts, palette, icons). Set by RunDirector from <c>GameDatabase.uiTheme</c>; every member below works without it.</summary>
    public static UiTheme Theme
    {
        get => theme;
        set { if (theme != value) { theme = value; styles.Clear(); } }
    }

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

    /// <summary>True while a full-screen page (menus, shop, map...) is up. Those are laid out on the 1080p canvas of the design and must fit it, so the player's UI-size slider only scales the in-game HUD.</summary>
    public static bool PageMode { get; set; }

    /// <summary>False for the scrolling route map, which uses the whole window instead of the centred canvas.</summary>
    public static bool PageCentered { get; set; } = true;

    public const float CanvasWidth = 1920f, CanvasHeight = 1080f;

    /// <summary>Pages fit the window whatever its shape: scaled by the tighter of width and height. The HUD scales by height and the HUD-size slider.</summary>
    public static float Scale => PageMode
        ? Mathf.Max(0.2f, Mathf.Min(Screen.height / CanvasHeight, Screen.width / CanvasWidth))
        : Mathf.Max(0.5f, Screen.height / CanvasHeight) * UserScale;

    /// <summary>Where the 1920x1080 canvas starts in the window when a page is up (it is centred). Zero for the HUD and the map.</summary>
    public static Vector2 PageOffset => PageMode && PageCentered
        ? new Vector2(Mathf.Max(0f, (Screen.width - CanvasWidth * Scale) * 0.5f), Mathf.Max(0f, (Screen.height - CanvasHeight * Scale) * 0.5f))
        : Vector2.zero;

    /// <summary>The whole window in the (possibly translated) page space: use it for backdrops and full-screen art.</summary>
    public static Rect ScreenLocal { get { Vector2 o = PageOffset; return new Rect(-o.x, -o.y, Screen.width, Screen.height); } }

    /// <summary>Size of the page canvas in GUI units (the window itself when there is no centred canvas).</summary>
    public static Vector2 CanvasSize => PageMode && PageCentered ? new Vector2(CanvasWidth * Scale, CanvasHeight * Scale) : new Vector2(Screen.width, Screen.height);
    public static float S(float v) => v * Scale;

    // ------------------------------------------------------------------ palette

    public static Color Orange => theme != null ? theme.orange : new Color(0.933f, 0.675f, 0.345f);
    public static Color Cream => theme != null ? theme.cream : new Color(0.996f, 0.843f, 0.647f);
    public static Color PanelLight => theme != null ? theme.panel : new Color(0.925f, 0.922f, 0.914f);
    public static Color Slate => theme != null ? theme.slate : new Color(0.282f, 0.271f, 0.302f);
    public static Color CardGray => theme != null ? theme.card : new Color(0.671f, 0.671f, 0.671f);
    public static Color Brown => theme != null ? theme.brown : new Color(0.482f, 0.337f, 0.204f);
    /// <summary>Text colour on the light panels.</summary>
    public static Color Text => theme != null ? theme.ink : new Color(0.067f, 0.067f, 0.067f);

    // Kept under their old names so every screen picks the new look up; retuned for light panels.
    public static readonly Color Ink = new Color(0.067f, 0.067f, 0.067f, 1f);
    public static readonly Color Panel = new Color(0.996f, 0.843f, 0.647f, 1f);
    public static readonly Color Accent = new Color(0.74f, 0.4f, 0.08f);
    public static readonly Color Muted = new Color(0.36f, 0.31f, 0.27f);

    public static readonly Color HudText = Color.white;
    public static readonly Color HudOutline = Color.black;

    // ------------------------------------------------------------------ primitives

    public static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    /// <summary>Filled rounded rectangle (9-sliced, so the corner radius stays constant at any size).</summary>
    public static void Rounded(Rect r, Color c)
    {
        if (Event.current.type != EventType.Repaint) return;
        float min = Mathf.Min(r.width, r.height);
        if (min < 14f) { Fill(r, c); return; }
        GUIStyle st = RoundStyle(min >= 46f);
        Color old = GUI.color;
        GUI.color = c;
        st.Draw(r, GUIContent.none, false, false, false, false);
        GUI.color = old;
    }

    private static GUIStyle RoundStyle(bool large)
    {
        if (large ? roundLarge == null : roundSmall == null)
        {
            int size = large ? 48 : 28, radius = large ? 18 : 10, border = large ? 22 : 13;
            GUIStyle st = new GUIStyle { border = new RectOffset(border, border, border, border) };
            st.normal.background = MakeRounded(size, radius);
            if (large) roundLarge = st; else roundSmall = st;
        }
        return large ? roundLarge : roundSmall;
    }

    private static Texture2D MakeRounded(int size, int radius)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
        };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
            }
        t.Apply();
        return t;
    }

    /// <summary>Texture fitted into <paramref name="box"/> keeping its aspect ratio.</summary>
    public static void Icon(Rect box, Texture tex, float alpha = 1f)
    {
        if (tex == null) return;
        float fit = Mathf.Min(box.width / tex.width, box.height / tex.height);
        Vector2 size = new Vector2(tex.width * fit, tex.height * fit);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(box.center.x - size.x * 0.5f, box.center.y - size.y * 0.5f, size.x, size.y), tex, ScaleMode.StretchToFill, true);
        GUI.color = old;
    }

    /// <summary>Icon followed by a value, e.g. a coin and "10". The icon is as tall as the row.</summary>
    public static void IconValue(Rect r, Texture tex, string value, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
    {
        float gap = S(10f);
        if (anchor == TextAnchor.MiddleRight || anchor == TextAnchor.UpperRight || anchor == TextAnchor.LowerRight)
        {
            // Right-aligned: the icon hugs the number instead of sitting at the far left of the row.
            float tw = TextWidth(value, size, bold ? Face.Bold : Face.Body) + S(6f);
            Icon(new Rect(r.xMax - tw - gap - r.height, r.y, r.height, r.height), tex);
            Label(new Rect(r.xMax - tw, r.y, tw, r.height), value, size, color, anchor, bold, false);
            return;
        }
        Icon(new Rect(r.x, r.y, r.height, r.height), tex);
        Label(new Rect(r.x + r.height + gap, r.y, r.width - r.height - gap, r.height), value, size, color, anchor, bold, false);
    }

    /// <summary>A "- - - - -" divider like the one under every heading in the mockups.</summary>
    public static void DashedRule(float x, float y, float width, Color? color = null, float thickness = 3f)
    {
        Color c = color ?? Text;
        float dash = S(11f), gap = S(7f), t = Mathf.Max(1f, S(thickness));
        for (float dx = 0f; dx < width; dx += dash + gap)
            Fill(new Rect(x + dx, y, Mathf.Min(dash, width - dx), t), c);
    }

    public static void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        Matrix4x4 saved = GUI.matrix;
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.01f) return;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        // Rotate about 'a' in the current GUI space. (GUIUtility.RotateAroundPivot reads its pivot as a screen position, which is wrong
        // once a page is translated to the middle of the window.)
        GUI.matrix = saved * Matrix4x4.TRS(new Vector3(a.x, a.y, 0f), Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(new Vector3(-a.x, -a.y, 0f), Quaternion.identity, Vector3.one);
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

    // ------------------------------------------------------------------ text

    /// <param name="wrap">False for short tags (counts, prices, hotkeys) that must never wrap or clip when their box is a little tight.</param>
    public static void Label(Rect r, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, bool wrap = true)
        => Draw(r, text, Style(Mathf.RoundToInt(S(size)), anchor, bold ? Face.Bold : Face.Body, wrap), color);

    /// <summary>Big heading in the display font (Beautiful Magic), e.g. "Corn Seed", "BUY", "Year 1".</summary>
    public static void Heading(Rect r, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        int px = Mathf.RoundToInt(S(size));
        if (string.IsNullOrEmpty(text)) return;
        if (theme == null || theme.display == null || theme.displayHasDigits || !NeedsFallback(text))
        {
            Draw(r, text, Style(px, anchor, Face.Display), color);
            return;
        }

        // The trial display font has a watermark where digits and '?' belong: draw those runs in Bold, the rest in the display font.
        GUIStyle disp = Style(px, TextAnchor.MiddleLeft, Face.Display), alt = Style(px, TextAnchor.MiddleLeft, Face.Bold);
        var runs = new List<KeyValuePair<string, bool>>();
        int start = 0;
        for (int i = 1; i <= text.Length; i++)
            if (i == text.Length || IsFallbackChar(text[i]) != IsFallbackChar(text[start]))
            {
                runs.Add(new KeyValuePair<string, bool>(text.Substring(start, i - start), IsFallbackChar(text[start])));
                start = i;
            }

        float total = 0f;
        float[] widths = new float[runs.Count];
        for (int i = 0; i < runs.Count; i++)
        {
            widths[i] = (runs[i].Value ? alt : disp).CalcSize(new GUIContent(runs[i].Key)).x + 2f;
            total += widths[i];
        }
        float x = r.x;
        if (anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.LowerCenter) x = r.center.x - total * 0.5f;
        else if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight) x = r.xMax - total;
        for (int i = 0; i < runs.Count; i++)
        {
            Draw(new Rect(x, r.y, widths[i] + 4f, r.height), runs[i].Key, runs[i].Value ? alt : disp, color);
            x += widths[i];
        }
    }

    private static bool IsFallbackChar(char c) => (c >= '0' && c <= '9') || c == '?';
    private static bool NeedsFallback(string text)
    {
        for (int i = 0; i < text.Length; i++) if (IsFallbackChar(text[i])) return true;
        return false;
    }

    /// <summary>Italic text, used for "Summer", "Day 1/6" and the tab names.</summary>
    /// <param name="wrap">False for one-line labels, so a descender (the g in "Spring") can't be clipped by a tight box.</param>
    public static void Italic(Rect r, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = true)
        => Draw(r, text, Style(Mathf.RoundToInt(S(size)), anchor, Face.Italic, wrap), color);

    private static void Draw(Rect r, string text, GUIStyle st, Color color)
    {
        Color old = GUI.contentColor;
        GUI.contentColor = color;
        GUI.Label(r, text, st);
        GUI.contentColor = old;
    }

    /// <summary>In-game HUD text: white with a black outline so it reads over the bright 3D world. <paramref name="alpha"/> fades both.</summary>
    public static void HudLabel(Rect r, string text, float size, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, float alpha = 1f)
    {
        Outlined(r, new GUIContent(text), Style(Mathf.RoundToInt(S(size)), anchor, bold ? Face.Bold : Face.Body), alpha);
    }

    /// <summary>HUD text with a coloured fill (still black-outlined), e.g. red for low ammo.</summary>
    public static void HudLabel(Rect r, string text, float size, Color fill, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, float alpha = 1f)
    {
        Outlined(r, new GUIContent(text), Style(Mathf.RoundToInt(S(size)), anchor, bold ? Face.Bold : Face.Body), alpha, fill);
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
        GUIStyle st = Style(Mathf.RoundToInt(S(size)), TextAnchor.UpperLeft, bold ? Face.Bold : Face.Body);
        return st.CalcHeight(new GUIContent(text ?? string.Empty), Mathf.Max(1f, width));
    }

    /// <summary>Width of a single line of text at the authored size.</summary>
    public static float TextWidth(string text, float size, Face face = Face.Body)
    {
        GUIStyle st = Style(Mathf.RoundToInt(S(size)), TextAnchor.UpperLeft, face);
        return st.CalcSize(new GUIContent(text ?? string.Empty)).x;
    }

    /// <summary>Shortens text with "..." until it fits in <paramref name="maxLines"/> wrapped lines of <paramref name="width"/> pixels.</summary>
    public static string Fit(string text, float size, float width, int maxLines, bool bold = false)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        GUIStyle st = Style(Mathf.RoundToInt(S(size)), TextAnchor.UpperLeft, bold ? Face.Bold : Face.Body);
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

    private static Font FontFor(Face face)
    {
        if (theme == null) return null;
        switch (face)
        {
            case Face.Display: return theme.display != null ? theme.display : theme.bold;
            case Face.Bold: return theme.bold;
            case Face.Italic: return theme.italic;
            default: return theme.body;
        }
    }

    private static GUIStyle Style(int size, TextAnchor anchor, Face face, bool wrap = true)
    {
        int key = ((size * 100 + (int)anchor) * 4 + (int)face) * 2 + (wrap ? 1 : 0);
        if (!styles.TryGetValue(key, out GUIStyle st) || st == null)
        {
            Font font = FontFor(face);
            st = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = wrap, richText = false, clipping = wrap ? TextClipping.Clip : TextClipping.Overflow };
            if (font != null) { st.font = font; st.fontStyle = FontStyle.Normal; }
            else st.fontStyle = face == Face.Bold || face == Face.Display ? FontStyle.Bold : face == Face.Italic ? FontStyle.Italic : FontStyle.Normal;
            st.normal.textColor = Color.white;   // pure white base so contentColor (incl. the HUD outline) is exact
            styles[key] = st;
        }
        return st;
    }

    // ------------------------------------------------------------------ controls

    /// <summary>The standard button for panels (the old Modal look): a grey-white pill that turns orange on hover.</summary>
    public static bool Button(Rect r, string text, bool enabled = true, float size = 22f) => Pill(r, text, PillKind.Panel, enabled, size);

    /// <summary>A rounded button with the offset "lip" shadow from the mockups. Returns true on click.</summary>
    public static bool Pill(Rect r, string text, PillKind kind, bool enabled = true, float size = 22f, bool selected = false, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        bool lit = hover || selected;
        Color face, shadow;
        switch (kind)
        {
            case PillKind.Primary: face = lit ? Color.Lerp(Orange, Color.white, 0.22f) : Orange; shadow = Color.Lerp(Orange, Color.black, 0.18f); break;
            case PillKind.Light: face = lit ? Cream : PanelLight; shadow = lit ? Orange : CardGray; break;
            case PillKind.Gray: face = lit ? Color.Lerp(CardGray, Color.white, 0.25f) : CardGray; shadow = Color.Lerp(CardGray, Color.black, 0.2f); break;
            case PillKind.Panel: face = lit ? Color.Lerp(PanelLight, Orange, 0.5f) : PanelLight; shadow = lit ? Orange : CardGray; break;
            default: face = lit ? Orange : Cream; shadow = lit ? Color.Lerp(Orange, Color.black, 0.2f) : Orange; break;
        }
        if (!enabled) { face = Color.Lerp(CardGray, PanelLight, 0.4f); shadow = CardGray; }

        float lip = S(7f);
        Rounded(new Rect(r.x - lip * 0.6f, r.y + lip, r.width, r.height), shadow);
        Rounded(r, face);
        float padX = anchor == TextAnchor.MiddleCenter ? S(10f) : S(26f);
        Label(new Rect(r.x + padX, r.y, r.width - padX * 2f, r.height), text, size, enabled ? Text : Muted, anchor, anchor == TextAnchor.MiddleCenter);
        if (!enabled) return false;
        return Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && ConsumeClick();
    }

    /// <summary>Deeper than the palette orange so it still reads on the orange pause-menu gradient.</summary>
    public static readonly Color HoverOrange = new Color(0.88f, 0.38f, 0.05f);

    /// <summary>A button that is just left-aligned text, e.g. the pause menu list. The text turns orange when hovered (no shadow).</summary>
    public static bool TextItem(Rect r, string text, float size, Color color, bool enabled = true)
    {
        bool hover = enabled && r.Contains(Event.current.mousePosition);
        Color shown = hover ? HoverOrange : enabled ? color : new Color(color.r, color.g, color.b, 0.5f);
        Label(r, text, size, shown, TextAnchor.MiddleLeft, false, false);
        return enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && ConsumeClick();
    }

    /// <summary>Round light button with a "back" arrow, top-left of every full-screen page.</summary>
    public static bool BackButton(Rect r)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        Vector2 c = r.center;
        float rad = r.width * 0.5f;
        Circle(c, rad, hover ? Color.white : PanelLight);
        float t = Mathf.Max(2f, S(7f));
        Color ink = Text;
        Vector2 head = new Vector2(c.x - rad * 0.30f, c.y - rad * 0.22f);
        Vector2 tail = new Vector2(c.x + rad * 0.28f, c.y - rad * 0.22f);
        Vector2 down = new Vector2(c.x + rad * 0.28f, c.y + rad * 0.30f);
        Vector2 back = new Vector2(c.x - rad * 0.12f, c.y + rad * 0.30f);
        Line(tail, down, t, ink); Line(down, back, t, ink);
        Line(tail, head, t, ink);
        Line(head, head + new Vector2(rad * 0.26f, -rad * 0.26f), t, ink);
        Line(head, head + new Vector2(rad * 0.26f, rad * 0.26f), t, ink);
        Circle(tail, t * 0.5f, ink); Circle(down, t * 0.5f, ink); Circle(back, t * 0.5f, ink); Circle(head, t * 0.5f, ink);
        return Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && ConsumeClick();
    }

    /// <summary>Width every key cap in a group should share, so their words line up.</summary>
    public static float KeyCapWidth(params string[] keys)
    {
        float w = 0f;
        foreach (string k in keys) w = Mathf.Max(w, TextWidth(k, 22f, Face.Body) + S(36f));
        return w;
    }

    /// <summary>A keyboard hint like [ESC] Menu: white pill with a dark outline and the action beside it. Pass the same <paramref name="capW"/> to a group.</summary>
    public static void KeyCap(Rect r, string key, string action, bool outlinedAction, float capW = -1f)
    {
        if (capW < 0f) capW = Mathf.Max(r.height * 1.5f, KeyCapWidth(key));
        Rect cap = new Rect(r.x, r.y, capW, r.height);
        Rounded(cap, Color.black);
        Rounded(new Rect(cap.x + S(3f), cap.y + S(3f), cap.width - S(6f), cap.height - S(6f)), Color.white);
        Label(cap, key, 22f, Text, TextAnchor.MiddleCenter, false, false);
        Rect actionRect = new Rect(cap.xMax + S(12f), r.y, Mathf.Max(S(80f), r.width - capW - S(12f)), r.height);
        if (outlinedAction) HudLabel(actionRect, action, 24f, TextAnchor.MiddleLeft, true);
        else Label(actionRect, action, 24f, Text);
    }

    /// <summary>Round button with an X: close / delete. <paramref name="armed"/> turns it red (a pending confirm).</summary>
    public static bool CloseButton(Rect r, bool armed = false, float alpha = 1f)
    {
        bool hover = r.Contains(Event.current.mousePosition);
        Color face = armed ? new Color(0.85f, 0.25f, 0.2f, alpha) : hover ? new Color(1f, 1f, 1f, alpha) : new Color(0.96f, 0.95f, 0.93f, alpha);
        Circle(r.center, r.width * 0.5f, face);
        Color ink = armed ? new Color(1f, 1f, 1f, alpha) : new Color(Text.r, Text.g, Text.b, alpha);
        float a = r.width * 0.2f, t = Mathf.Max(2f, r.width * 0.12f);
        Line(r.center + new Vector2(-a, -a), r.center + new Vector2(a, a), t, ink);
        Line(r.center + new Vector2(-a, a), r.center + new Vector2(a, -a), t, ink);
        Circle(r.center + new Vector2(-a, -a), t * 0.5f, ink); Circle(r.center + new Vector2(a, a), t * 0.5f, ink);
        Circle(r.center + new Vector2(-a, a), t * 0.5f, ink); Circle(r.center + new Vector2(a, -a), t * 0.5f, ink);
        return alpha > 0.5f && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && ConsumeClick();
    }

    /// <summary>Folder tabs sitting on top of a panel's top edge. Returns the (possibly new) selected index.</summary>
    public static int Tabs(float x, float panelTop, string[] names, int selected, float size = 24f)
    {
        float h = S(54f), pad = S(26f), gap = S(4f);
        int result = selected;
        for (int i = 0; i < names.Length; i++)
        {
            float w = TextWidth(names[i], size, Face.Italic) + pad * 2f;
            bool sel = i == selected;
            // The selected tab is taller and joins the frame; the others sit lower.
            Rect tab = new Rect(x, panelTop - h + (sel ? S(6f) : S(14f)), w, h);
            // The coloured shape runs well down behind the panel (which is drawn after it), so tab and panel read as one folder.
            Rounded(new Rect(tab.x, tab.y, tab.width, tab.height + S(40f)), sel ? Orange : Cream);
            Italic(tab, names[i], size, sel ? Text : Muted, TextAnchor.MiddleCenter, false);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && tab.Contains(Event.current.mousePosition))
            {
                result = i;
                Event.current.Use();
            }
            x += w + gap;
        }
        return result;
    }

    /// <summary>One selected tab whose right edge lines up with the right edge of <paramref name="panel"/> (Inventory, Upgrades).</summary>
    public static void TabRight(Rect panel, string name, float size = 24f)
    {
        float w = TextWidth(name, size, Face.Italic) + S(26f) * 2f;
        Tabs(panel.xMax - w, panel.y, new[] { name }, 0, size);
    }

    /// <summary>Horizontal slider drawn in the theme (grey track, orange fill, round knob). Returns the new value.</summary>
    public static float Slider(Rect r, float value, float min, float max)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive, r);
        Event e = Event.current;
        float knob = r.height * 0.5f;
        Rect track = new Rect(r.x + knob, r.center.y - S(5f), r.width - knob * 2f, S(10f));
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { GUIUtility.hotControl = id; e.Use(); }
        if (GUIUtility.hotControl == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
        {
            value = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / Mathf.Max(1f, track.width)));
            e.Use();
        }
        if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }

        float f = Mathf.InverseLerp(min, max, value);
        Rounded(track, CardGray);
        Rounded(new Rect(track.x, track.y, Mathf.Max(track.height, track.width * f), track.height), Orange);
        Circle(new Vector2(track.x + track.width * f, r.center.y), knob, Color.white);
        Circle(new Vector2(track.x + track.width * f, r.center.y), knob * 0.62f, Orange);
        return value;
    }

    private static bool ConsumeClick() { Event.current.Use(); return true; }

    // ------------------------------------------------------------------ frames

    /// <summary>Full-screen backdrop: the blurred live game view, or a plain dim if it can't be captured.</summary>
    public static void Backdrop(float refreshSeconds = 0f)
    {
        if (!UiBackdrop.Draw(refreshSeconds))
            Fill(ScreenLocal, new Color(0.32f, 0.45f, 0.55f, 1f));
    }

    /// <summary>Orange rounded frame around <paramref name="inner"/> (grown by the frame width). Returns the outer rect.</summary>
    public static Rect OrangeFrame(Rect inner, float pad = 22f)
    {
        Rect outer = new Rect(inner.x - S(pad), inner.y - S(pad), inner.width + S(pad) * 2f, inner.height + S(pad) * 2f);
        Rounded(outer, Orange);
        return outer;
    }

    /// <summary>Blurred backdrop plus a centered orange-framed cream panel with a display-font title. Returns the cream rect.</summary>
    public static Rect Modal(float width, float height, string title)
    {
        Backdrop(0f);
        Fill(ScreenLocal, new Color(0.1f, 0.06f, 0.02f, 0.28f));
        Vector2 canvas = CanvasSize;
        Rect r = new Rect((canvas.x - S(width)) * 0.5f, (canvas.y - S(height)) * 0.5f, S(width), S(height));
        OrangeFrame(r);
        Rounded(r, Cream);
        Heading(new Rect(r.x + S(24f), r.y + S(14f), r.width - S(48f), S(58f)), title, 42f, Text, TextAnchor.MiddleLeft);
        DashedRule(r.x + S(24f), r.y + S(74f), Mathf.Min(r.width - S(48f), S(520f)));
        return r;
    }
}
