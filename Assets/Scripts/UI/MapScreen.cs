using System.Collections.Generic;
using UnityEngine;

/// <summary>The run map: a vertical layered graph on a season-coloured gradient, with pulsing choices. Scroll with the mouse wheel.</summary>
public class MapScreen : IRunScreen
{
    private readonly RunDirector director;
    private readonly bool readOnly;
    private float scroll;
    private bool scrollInit;

    private const float LayerHeight = 150f;
    private const float LaneWidth = 150f;
    private const float NodeRadius = 44f;
    private const float MapShiftLeft = 170f;   // the whole graph sits a little left of centre, clear of the legend

    // Order of the legend rows (node type ids); rows without an icon or label are skipped.
    private static readonly string[] LegendOrder = { "farm", "merchant", "event", "disaster", "boss" };

    /// <param name="readOnly">The in-run "Your route" view: look but don't pick a node.</param>
    public MapScreen(RunDirector director, bool readOnly = false)
    {
        this.director = director;
        this.readOnly = readOnly;
    }

    /// <summary>Re-centers the scroll on the next draw. Call when the screen is opened.</summary>
    public void OnOpen() { scrollInit = false; }

    public void Draw()
    {
        RunState run = director.Run;
        MapGraph map = run.map;
        GameDatabase db = director.Db;
        float s = ImGuiKit.Scale;
        float w = Screen.width, h = Screen.height;

        int maxLayer = map.layerCount;
        float content = (maxLayer + 2) * LayerHeight * s;
        if (!scrollInit)
        {
            // Start with today's (or the next) layer near the bottom third of the screen.
            MapNode today0 = run.phase == RunPhase.InDay ? map.Get(run.pendingNodeId) : null;
            MapNode here = run.freeRoam && run.currentNodeId >= 0 ? map.Get(run.currentNodeId) : null;
            int focusLayer = today0 != null ? today0.layer : here != null ? here.layer : run.DayIndex;
            float target = focusLayer * LayerHeight * s - h * 0.35f;
            scroll = Mathf.Clamp(target, 0f, Mathf.Max(0f, content - h));
            scrollInit = true;
        }
        Event e = Event.current;
        if (e.type == EventType.ScrollWheel)
        {
            scroll = Mathf.Clamp(scroll - e.delta.y * 40f, 0f, Mathf.Max(0f, content - h));
            e.Use();
        }

        // Layer 0 sits at the bottom; higher layers go up the screen.
        System.Func<int, float> yOf = layer => h - (LayerHeight * 0.9f * s) - layer * LayerHeight * s + scroll;
        System.Func<MapNode, Vector2> pos = n => new Vector2(w * 0.5f - MapShiftLeft * s + (n.lane - (map.lanes - 1) * 0.5f) * LaneWidth * s, yOf(n.layer));

        DrawBackground(db, maxLayer, h, s);
        DrawSeasonLabels(db, maxLayer, yOf, s);

        MapNode current = run.currentNodeId >= 0 ? map.Get(run.currentNodeId) : null;

        // Edges.
        foreach (MapNode n in map.nodes)
            foreach (int id in n.next)
            {
                MapNode c = map.Get(id);
                if (c == null) continue;
                bool walked = run.visited.Contains(n.id) && (run.visited.Contains(c.id));
                bool reachable = current != null && n.id == current.id;
                Color col = walked ? ImGuiKit.Brown : reachable ? new Color(0.07f, 0.07f, 0.07f, 0.95f) : new Color(0.07f, 0.07f, 0.07f, 0.55f);
                ImGuiKit.Line(pos(n), pos(c), walked || reachable ? 6f * s : 4f * s, col);
            }

        // Nodes.
        MapNode hovered = null;
        Vector2 youAreHere = new Vector2(-1f, -1f);
        Vector2 mouse = e.mousePosition;
        foreach (MapNode n in map.nodes)
        {
            db.nodeTypes.TryGet(n.nodeTypeId, out NodeTypeData type);
            Vector2 p = pos(n);
            if (p.y < -120f * s || p.y > h + 120f * s) continue;
            bool available = RunLogic.CanEnter(run, n.id);
            bool visited = run.visited.Contains(n.id);
            float r = NodeRadius * s * (n.id == map.bossId ? 1.3f : 1f);
            bool hover = (mouse - p).sqrMagnitude <= r * r * 1.4f;
            if (hover) hovered = n;

            // "You are here": the day in progress.
            if (run.phase == RunPhase.InDay && n.id == run.pendingNodeId)
            {
                float pulseT = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f);
                ImGuiKit.Circle(p, r + (12f + 7f * pulseT) * s, new Color(0.35f, 0.85f, 0.4f, 0.75f));
                youAreHere = new Vector2(p.x - r - 18 * s, p.y);
            }
            if (available)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
                ImGuiKit.Circle(p, r + (7f + 7f * pulse) * s, new Color(1f, 0.95f, 0.5f, 0.85f));
            }

            Color tint = visited ? new Color(1f, 1f, 1f, 0.55f) : available ? Color.white : new Color(0.62f, 0.62f, 0.62f, 0.8f);
            if (hover && available) r *= 1.08f;
            if (type != null && type.icon != null)
            {
                // Dimmed icons are see-through: put the background colour under them so the edges don't show through.
                if (tint.a < 1f) ImGuiKit.Circle(p, r, BandColor(db, n.layer));
                Color old = GUI.color;
                GUI.color = tint;
                GUI.DrawTexture(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), type.icon, ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
            else
            {
                // Node types without art keep a coloured circle with their glyph.
                Color baseColor = type != null ? type.color : Color.gray;
                ImGuiKit.Circle(p, r + 3f * s, new Color(0f, 0f, 0f, 0.6f));
                ImGuiKit.Circle(p, r, Color.Lerp(baseColor, Color.white, hover && available ? 0.35f : 0f) * tint);
                ImGuiKit.Label(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), type != null ? type.glyph : "?", n.id == map.bossId ? 34f : 30f, Color.white, TextAnchor.MiddleCenter, true);
            }
            if (visited) DrawTick(new Vector2(p.x + r * 0.7f, p.y + r * 0.7f), 16f * s);

            if (!readOnly && available && hover && e.type == EventType.MouseDown && e.button == 0)
            {
                ImGuiKit.ConsumeSmallClick();
                director.ChooseNode(n.id);
                return;
            }
        }

        // Drawn after the nodes so a neighbouring icon never covers it.
        if (youAreHere.x >= 0f)
        {
            Rect tag = new Rect(youAreHere.x - 250 * s, youAreHere.y - 24 * s, 250 * s, 48 * s);
            ImGuiKit.Rounded(tag, ImGuiKit.Cream);
            ImGuiKit.Label(tag, "YOU ARE HERE", 24f, new Color(0.08f, 0.4f, 0.12f), TextAnchor.MiddleCenter, true);
        }

        DrawLegend(db, s);
        DrawHeader(run, w, s);
        if (director.IsBeta && !readOnly) DrawBetaPanel(run, db, h, s, content);
        if (hovered != null) DrawTooltip(hovered, db, mouse, s, readOnly || RunLogic.CanEnter(run, hovered.id),
            BossScaling.ForYear(run.YearNumber(director.Meta), db.difficulty));

        // Back: the read-only route returns to the pause menu, the choosing map opens it.
        if (ImGuiKit.BackButton(new Rect(36f * s, 30f * s, 110f * s, 110f * s)))
        {
            if (readOnly) director.CloseRoute(); else director.ShowPause();
        }
    }

    // ------------------------------------------------------------------ background

    private static Color BandColor(GameDatabase db, int layer)
    {
        CalendarDate d = db.Calendar.DateOf(Mathf.Max(0, layer));
        if (d.season == null) return new Color(0.85f, 0.8f, 0.7f);
        if (d.season.mapColor.a > 0.01f) return d.season.mapColor;
        return Color.Lerp(d.season.uiColor, new Color(0.98f, 0.92f, 0.82f), 0.65f);   // no mapColor set: a washed-out uiColor
    }

    /// <summary>Vertical gradient that follows the seasons as you scroll up the map.</summary>
    private void DrawBackground(GameDatabase db, int maxLayer, float h, float s)
    {
        float step = 10f * s;
        for (float y = 0f; y < h; y += step)
        {
            // Which (fractional) layer this screen row is at: the inverse of yOf.
            float layerF = (h - LayerHeight * 0.9f * s + scroll - (y + step * 0.5f)) / (LayerHeight * s);
            int lo = Mathf.FloorToInt(layerF);
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(layerF - lo));
            Color c = Color.Lerp(BandColor(db, Mathf.Clamp(lo, 0, maxLayer)), BandColor(db, Mathf.Clamp(lo + 1, 0, maxLayer)), t);
            ImGuiKit.Fill(new Rect(0f, y, Screen.width, step + 1f), c);
        }
    }

    /// <summary>Season icon and name beside the middle of each season's band, like the design.</summary>
    private void DrawSeasonLabels(GameDatabase db, int maxLayer, System.Func<int, float> yOf, float s)
    {
        int first = 0;
        for (int layer = 1; layer <= maxLayer + 1; layer++)
        {
            CalendarDate prev = db.Calendar.DateOf(layer - 1);
            CalendarDate cur = layer <= maxLayer ? db.Calendar.DateOf(layer) : default;
            bool boundary = layer > maxLayer || cur.seasonIndex != prev.seasonIndex;
            if (!boundary) continue;
            if (prev.season != null)
            {
                float y = (yOf(first) + yOf(layer - 1)) * 0.5f;
                if (y > -100f * s && y < Screen.height + 100f * s)
                {
                    ImGuiKit.Icon(new Rect(24f * s, y - 56f * s, 112f * s, 112f * s), prev.season.icon, 0.9f);
                    // The season's own colour, a little darker so it reads on the pastel band.
                    Color label = Color.Lerp(prev.season.uiColor, Color.black, 0.35f);
                    ImGuiKit.Label(new Rect(150f * s, y - 24f * s, 260f * s, 48f * s), prev.season.displayName, 38f, label, TextAnchor.MiddleLeft, true);
                }
            }
            first = layer;
        }
    }

    private static void DrawTick(Vector2 c, float r)
    {
        ImGuiKit.Circle(c, r, new Color(0.2f, 0.65f, 0.3f));
        float t = Mathf.Max(2f, r * 0.28f);
        Vector2 a = c + new Vector2(-r * 0.45f, r * 0.05f), b = c + new Vector2(-r * 0.1f, r * 0.4f), d = c + new Vector2(r * 0.5f, -r * 0.35f);
        ImGuiKit.Line(a, b, t, Color.white);
        ImGuiKit.Line(b, d, t, Color.white);
    }

    // ------------------------------------------------------------------ legend

    private void DrawLegend(GameDatabase db, float s)
    {
        UiTheme theme = ImGuiKit.Theme;
        if (theme == null || theme.legend == null) return;

        // The scroll is the bottom-right of a 1920x1080 sheet; pin that sheet to the window's bottom-right corner.
        Vector2 o = new Vector2(Screen.width - ImGuiKit.CanvasWidth * s, Screen.height - ImGuiKit.CanvasHeight * s);
        GUI.DrawTexture(new Rect(o.x, o.y, ImGuiKit.CanvasWidth * s, ImGuiKit.CanvasHeight * s), theme.legend, ScaleMode.StretchToFill, true);

        ImGuiKit.Heading(new Rect(o.x + 1460f * s, o.y + 522f * s, 300f * s, 76f * s), "Legend", 62f, ImGuiKit.Text, TextAnchor.MiddleCenter);

        float y = o.y + 612f * s, step = 79f * s;
        foreach (string id in LegendOrder)
        {
            if (!db.nodeTypes.TryGet(id, out NodeTypeData type) || type.icon == null) continue;
            ImGuiKit.Icon(new Rect(o.x + 1470f * s, y, 66f * s, 66f * s), type.icon);
            ImGuiKit.Label(new Rect(o.x + 1554f * s, y, 280f * s, 66f * s), string.IsNullOrEmpty(type.legendLabel) ? type.displayName : type.legendLabel, 38f, ImGuiKit.Text, TextAnchor.MiddleLeft, false, false);
            y += step;
        }
    }

    // ------------------------------------------------------------------ chrome

    /// <summary>Beta controls: boss strength (year) and season shortcuts. Seasons scroll the map to their first layer.</summary>
    private void DrawBetaPanel(RunState run, GameDatabase db, float h, float s, float content)
    {
        Rect panel = new Rect(24 * s, h - 300 * s, 360 * s, 276 * s);
        ImGuiKit.Rounded(panel, ImGuiKit.Orange);
        ImGuiKit.Rounded(new Rect(panel.x + 8 * s, panel.y + 8 * s, panel.width - 16 * s, panel.height - 16 * s), ImGuiKit.Cream);
        float x = panel.x + 20 * s, y = panel.y + 18 * s;
        int year = run.YearNumber(director.Meta);
        string scaling = BossScaling.Describe(BossScaling.ForYear(year, db.difficulty));
        ImGuiKit.Label(new Rect(x, y, 320 * s, 28 * s), scaling.Length > 0 ? $"Boss year {year}: {scaling}" : $"Boss year {year}: base strength", 18f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
        y += 40 * s;
        if (ImGuiKit.Button(new Rect(x, y, 150 * s, 44 * s), "Year -", year > 1, 20f)) director.SetBetaYear(year - 1);
        if (ImGuiKit.Button(new Rect(x + 168 * s, y, 150 * s, 44 * s), "Year +", true, 20f)) director.SetBetaYear(year + 1);
        y += 62 * s;
        ImGuiKit.Label(new Rect(x, y, 320 * s, 26 * s), "Jump to season", 18f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
        y += 32 * s;

        // The first layer of each season is where its band starts on the map.
        int shown = 0, lastSeason = -1;
        for (int layer = 0; layer <= run.map.layerCount; layer++)
        {
            CalendarDate d = db.Calendar.DateOf(layer);
            if (d.season == null || d.seasonIndex == lastSeason) continue;
            lastSeason = d.seasonIndex;
            Rect b = new Rect(x + (shown % 2) * 168 * s, y + (shown / 2) * 52 * s, 150 * s, 44 * s);
            if (ImGuiKit.Button(b, d.season.displayName, true, 20f))
                scroll = Mathf.Clamp(layer * LayerHeight * s - Screen.height * 0.35f, 0f, Mathf.Max(0f, content - Screen.height));
            shown++;
        }
    }

    private void DrawHeader(RunState run, float w, float s)
    {
        int year = run.YearNumber(director.Meta);
        UiTheme theme = ImGuiKit.Theme;

        // Title pill, top centre.
        Rect title = new Rect(w * 0.5f - 400 * s, 20 * s, 800 * s, 96 * s);
        ImGuiKit.Rounded(title, new Color(ImGuiKit.Cream.r, ImGuiKit.Cream.g, ImGuiKit.Cream.b, 0.94f));
        ImGuiKit.Label(new Rect(title.x + 24 * s, title.y + 6 * s, title.width - 48 * s, 44 * s), readOnly ? $"Year {year} - Your route" : $"Year {year} - Choose your next day", 32f, ImGuiKit.Text, TextAnchor.MiddleCenter, true);
        ImGuiKit.Label(new Rect(title.x + 24 * s, title.y + 50 * s, title.width - 48 * s, 36 * s),
            run.freeRoam ? "BETA - free roam: click any node, in any order. Esc opens the menu."
            : readOnly ? "Scroll to look ahead. Hover a node for details." : "Scroll to explore. Glowing nodes are your options. Esc opens the menu.",
            20f, run.freeRoam ? ImGuiKit.Accent : ImGuiKit.Muted, TextAnchor.MiddleCenter);

        // During a day the live wallet and health are the truth (the saved run is only updated at day's end).
        bool live = director.IsDayRunning;
        int purse = live ? director.Wallet.Coins : run.coins;
        int hp = live && director.Health != null ? director.Health.Hp : run.hp;
        Rect stats = new Rect(w - 520 * s, 20 * s, 496 * s, 80 * s);
        ImGuiKit.Rounded(stats, new Color(ImGuiKit.Cream.r, ImGuiKit.Cream.g, ImGuiKit.Cream.b, 0.94f));
        float cx = stats.x + 18 * s, cell = (stats.width - 36 * s) / 3f;
        ImGuiKit.IconValue(new Rect(cx, stats.y + 12 * s, cell, 56 * s), theme != null ? theme.coin : null, purse.ToString(), 30f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
        ImGuiKit.IconValue(new Rect(cx + cell, stats.y + 12 * s, cell, 56 * s), theme != null ? theme.piggyBank : null, director.Meta.vaultCoins.ToString(), 30f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
        ImGuiKit.IconValue(new Rect(cx + cell * 2f, stats.y + 12 * s, cell, 56 * s), theme != null ? theme.heartFull : null, $"{hp}/{run.maxHp}", 30f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
    }

    private static void DrawTooltip(MapNode n, GameDatabase db, Vector2 mouse, float s, bool available, BossScale bossScale)
    {
        db.nodeTypes.TryGet(n.nodeTypeId, out NodeTypeData type);
        if (type == null) return;
        CalendarDate d = db.Calendar.DateOf(n.layer);
        float chance = Mathf.Clamp01(type.disasterChance + (type.disasterChance > 0f && d.season != null ? d.season.disasterChanceBonus : 0f));

        string body = type.description;
        body += $"\n{(d.season != null ? d.season.displayName : "")} · Day {d.dayInSeason + 1}";
        if (type.encounter != null)
        {
            body += $"\nNight fight: {type.encounter.displayName}";
            string stronger = BossScaling.Describe(bossScale);
            if (stronger.Length > 0) body += $"\nThis year it {stronger}";
        }
        if (type.survivableLoss) body += "\nLose this and next season's crops grow slower.";
        if (type.startsAtNight) body += "\nNo daytime: the fight starts at once. Bring crops!";
        if (type.merchantActive) body += "\nMerchant open";
        body += chance >= 0.995f ? "\nDisaster guaranteed" : chance > 0f ? $"\nDisaster chance {Mathf.RoundToInt(chance * 100f)}%" : "\nNo disasters";
        if (!available) body += "\n(not reachable yet)";

        float boxW = 380f * s;
        float bodyH = ImGuiKit.TextHeight(body, 20f, boxW - 36 * s);
        float boxH = 62f * s + bodyH + 14f * s;
        Rect r = new Rect(Mathf.Min(mouse.x + 24f * s, Screen.width - boxW - 20f * s), Mathf.Min(mouse.y + 12f * s, Screen.height - boxH - 10f * s), boxW, boxH);
        ImGuiKit.Rounded(new Rect(r.x - 6 * s, r.y - 6 * s, r.width + 12 * s, r.height + 12 * s), ImGuiKit.Orange);
        ImGuiKit.Rounded(r, ImGuiKit.Cream);
        ImGuiKit.Label(new Rect(r.x + 18 * s, r.y + 10 * s, r.width - 36 * s, 40 * s), type.displayName, 28f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(r.x + 18 * s, r.y + 54 * s, r.width - 36 * s, bodyH), body, 20f, ImGuiKit.Text, TextAnchor.UpperLeft);
    }
}
