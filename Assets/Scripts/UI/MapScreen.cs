using UnityEngine;

/// <summary>The run map: a vertical layered graph, season-banded, with pulsing choices. Scroll with the mouse wheel.</summary>
public class MapScreen : IRunScreen
{
    private readonly RunDirector director;
    private readonly bool readOnly;
    private float scroll;
    private bool scrollInit;

    private const float LayerHeight = 120f;
    private const float LaneWidth = 120f;
    private const float NodeRadius = 28f;

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

        ImGuiKit.Fill(new Rect(0f, 0f, w, h), new Color(0.05f, 0.04f, 0.09f, 0.94f));

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
            scroll = Mathf.Clamp(scroll + e.delta.y * 40f, 0f, Mathf.Max(0f, content - h));
            e.Use();
        }

        // Layer 0 sits at the bottom; higher layers go up the screen.
        System.Func<int, float> yOf = layer => h - (LayerHeight * 0.9f * s) - layer * LayerHeight * s + scroll;
        System.Func<MapNode, Vector2> pos = n => new Vector2(w * 0.5f + (n.lane - (map.lanes - 1) * 0.5f) * LaneWidth * s, yOf(n.layer));

        // Season bands behind each layer.
        for (int layer = 0; layer <= maxLayer; layer++)
        {
            CalendarDate d = db.Calendar.DateOf(layer);
            Color c = d.season != null ? d.season.uiColor : Color.gray;
            c.a = 0.13f;
            float y = yOf(layer);
            ImGuiKit.Fill(new Rect(0f, y - LayerHeight * 0.5f * s, w, LayerHeight * s), c);
            if (d.season != null)
                ImGuiKit.Label(new Rect(24 * s, y - 14 * s, 260 * s, 28 * s),
                    layer == maxLayer ? "Harvest Moon" : d.IsLastDayOfSeason ? $"{d.season.displayName} · Boss Night" : $"{d.season.displayName} · Day {d.dayInSeason + 1}", 20f, new Color(c.r, c.g, c.b, 0.9f), TextAnchor.MiddleLeft, true);
        }

        MapNode current = run.currentNodeId >= 0 ? map.Get(run.currentNodeId) : null;

        // Edges.
        foreach (MapNode n in map.nodes)
            foreach (int id in n.next)
            {
                MapNode c = map.Get(id);
                if (c == null) continue;
                bool walked = run.visited.Contains(n.id) && (run.visited.Contains(c.id));
                bool reachable = current != null && n.id == current.id;
                Color col = walked ? ImGuiKit.Accent : reachable ? new Color(1f, 1f, 1f, 0.8f) : new Color(1f, 1f, 1f, 0.18f);
                ImGuiKit.Line(pos(n), pos(c), walked || reachable ? 5f * s : 3f * s, col);
            }

        // Nodes.
        MapNode hovered = null;
        Vector2 mouse = e.mousePosition;
        foreach (MapNode n in map.nodes)
        {
            db.nodeTypes.TryGet(n.nodeTypeId, out NodeTypeData type);
            Vector2 p = pos(n);
            bool available = RunLogic.CanEnter(run, n.id);
            bool visited = run.visited.Contains(n.id);
            float r = NodeRadius * s;
            bool hover = (mouse - p).sqrMagnitude <= r * r * 1.4f;
            if (hover) hovered = n;

            Color baseColor = type != null ? type.color : Color.gray;
            if (!available && !visited) baseColor = Color.Lerp(baseColor, new Color(0.15f, 0.14f, 0.2f), 0.65f);
            if (n.id == map.bossId) r *= 1.4f;

            // "You are here": the day in progress.
            if (run.phase == RunPhase.InDay && n.id == run.pendingNodeId)
            {
                float pulseT = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 3f);
                ImGuiKit.Circle(p, r + (10f + 6f * pulseT) * s, new Color(0.4f, 1f, 0.5f, 0.6f));
                ImGuiKit.Label(new Rect(p.x + r + 14 * s, p.y - 14 * s, 200 * s, 28 * s), "YOU ARE HERE", 18f, new Color(0.6f, 1f, 0.65f), TextAnchor.MiddleLeft, true);
            }

            if (available)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 4f);
                ImGuiKit.Circle(p, r + (6f + 6f * pulse) * s, new Color(1f, 0.9f, 0.4f, 0.55f));
            }
            ImGuiKit.Circle(p, r + 3f * s, visited ? ImGuiKit.Accent : new Color(0f, 0f, 0f, 0.7f));
            ImGuiKit.Circle(p, r, hover && available ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor);
            ImGuiKit.Label(new Rect(p.x - r, p.y - r, r * 2f, r * 2f), visited ? "✓" : (type != null ? type.glyph : "?"), n.id == map.bossId ? 30f : 24f,
                visited ? ImGuiKit.Ink : Color.white, TextAnchor.MiddleCenter, true);

            if (!readOnly && available && hover && e.type == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                director.ChooseNode(n.id);
                return;
            }
        }

        DrawHeader(run, w, s);
        if (director.IsBeta && !readOnly) DrawBetaPanel(run, db, h, s, content);
        if (hovered != null) DrawTooltip(hovered, db, mouse, s, readOnly || RunLogic.CanEnter(run, hovered.id),
            BossScaling.ForYear(run.YearNumber(director.Meta), db.difficulty));

        if (readOnly && ImGuiKit.Button(new Rect(24 * s, h - 72 * s, 220 * s, 50 * s), "Close (Esc)", true, 22f))
            director.CloseRoute();
    }

    /// <summary>Beta controls: boss strength (year) and season shortcuts. Seasons scroll the map to their first layer.</summary>
    private void DrawBetaPanel(RunState run, GameDatabase db, float h, float s, float content)
    {
        Rect panel = new Rect(24 * s, h - 260 * s, 300 * s, 236 * s);
        ImGuiKit.Fill(panel, new Color(0.04f, 0.03f, 0.08f, 0.95f));
        float x = panel.x + 12 * s, y = panel.y + 10 * s;
        int year = run.YearNumber(director.Meta);
        string scaling = BossScaling.Describe(BossScaling.ForYear(year, db.difficulty));
        ImGuiKit.Label(new Rect(x, y, 276 * s, 26 * s), scaling.Length > 0 ? $"Boss year {year}: {scaling}" : $"Boss year {year}: base strength", 16f, Color.white, TextAnchor.MiddleLeft, true);
        y += 32 * s;
        if (ImGuiKit.Button(new Rect(x, y, 132 * s, 36 * s), "Year -", year > 1, 18f)) director.SetBetaYear(year - 1);
        if (ImGuiKit.Button(new Rect(x + 144 * s, y, 132 * s, 36 * s), "Year +", true, 18f)) director.SetBetaYear(year + 1);
        y += 48 * s;
        ImGuiKit.Label(new Rect(x, y, 276 * s, 24 * s), "Jump to season", 16f, ImGuiKit.Muted, TextAnchor.MiddleLeft);
        y += 28 * s;

        // The first layer of each season is where its band starts on the map.
        int shown = 0, lastSeason = -1;
        for (int layer = 0; layer <= run.map.layerCount; layer++)
        {
            CalendarDate d = db.Calendar.DateOf(layer);
            if (d.season == null || d.seasonIndex == lastSeason) continue;
            lastSeason = d.seasonIndex;
            Rect b = new Rect(x + (shown % 2) * 144 * s, y + (shown / 2) * 44 * s, 132 * s, 36 * s);
            if (ImGuiKit.Button(b, d.season.displayName, true, 18f))
                scroll = Mathf.Clamp(layer * LayerHeight * s - Screen.height * 0.35f, 0f, Mathf.Max(0f, content - Screen.height));
            shown++;
        }
    }

    private void DrawHeader(RunState run, float w, float s)
    {
        int year = run.YearNumber(director.Meta);
        ImGuiKit.Fill(new Rect(0f, 0f, w, 78f * s), new Color(0.05f, 0.04f, 0.09f, 0.97f));
        ImGuiKit.Label(new Rect(24 * s, 8 * s, 700 * s, 36 * s), readOnly ? $"Year {year} - Your route" : $"Year {year} - Choose your next day", 32f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(24 * s, 44 * s, 700 * s, 26 * s),
            run.freeRoam ? "BETA - free roam: click any node, in any order. Esc opens the menu."
            : readOnly ? "Scroll to look ahead. Hover a node for details." : "Scroll to explore. Glowing nodes are your options. Esc opens the menu.", 18f, run.freeRoam ? ImGuiKit.Accent : ImGuiKit.Muted);
        // During a day the live wallet and health are the truth (the saved run is only updated at day's end).
        bool live = director.IsDayRunning;
        int purse = live ? director.Wallet.Coins : run.coins;
        ImGuiKit.Label(new Rect(w - 520 * s, 8 * s, 496 * s, 34 * s), $"Purse: {purse}   ·   Vault: {director.Meta.vaultCoins}", 26f, Color.white, TextAnchor.MiddleRight, true);
        if (!live) ImGuiKit.Label(new Rect(w - 520 * s, 44 * s, 496 * s, 26 * s), $"HP {run.hp}/{run.maxHp}", 20f, ImGuiKit.Muted, TextAnchor.MiddleRight);
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

        float boxW = 320f * s;
        float bodyH = ImGuiKit.TextHeight(body, 16f, boxW - 24 * s);
        float boxH = 48f * s + bodyH + 8f * s;
        Rect r = new Rect(Mathf.Min(mouse.x + 20f * s, Screen.width - boxW - 20f * s), Mathf.Min(mouse.y + 10f * s, Screen.height - boxH - 10f * s), boxW, boxH);
        ImGuiKit.Fill(r, new Color(0.04f, 0.03f, 0.08f, 0.97f));
        ImGuiKit.Fill(new Rect(r.x, r.y, r.width, 4f * s), type.color);
        ImGuiKit.Label(new Rect(r.x + 12 * s, r.y + 8 * s, r.width - 24 * s, 30 * s), type.displayName, 24f, type.color, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(r.x + 12 * s, r.y + 40 * s, r.width - 24 * s, bodyH), body, 16f, Color.white, TextAnchor.UpperLeft);
    }
}
