using UnityEngine;

/// <summary>Main-menu primer: a tab per topic (goal, farming, crops, days, coins, controls).</summary>
public class HowToPlayScreen : IRunScreen
{
    private readonly RunDirector director;
    private int page;

    // Controls has no body: it draws the shared key list from ControlsScreen.
    private static readonly (string title, string body)[] Pages =
    {
        ("The Goal",
            "You are a dog farmer protecting an enchanted farm. Grow crops by day. At night a gluttonous pest crawls out of the dark.\n\n" +
            "You don't hurt it, you FEED it. Every crop it eats fills its fullness bar, and a full pest bursts. Feed the caterpillar enough and it evolves into a butterfly for the next stage of the fight."),
        ("Farming",
            "Stand at an empty plot and press E to plant the seed in your hand. It grows over time; when it's ripe, press E or click to harvest it.\n\n" +
            "Seeds come from the merchant (buy them with coins) or from the start of a run. Plant crops in their own season: out-of-season crops grow slowly and come out weak. Better-grown crops hit harder and shield more."),
        ("Crops as Weapons",
            "Thrown crops (tomato, pumpkin, garlic): hold Left Click to aim, release to lob. Garlic also scares the boss away for 3 seconds.\n\n" +
            "Shooter crops (peas, corn): hold Left Click to auto-fire toward the cursor. One harvested crop is 10 shots.\n\n" +
            "Support crops (lettuce, turnip, spinach): Right Click to eat one for Bark Shield charges."),
        ("Days and Seasons",
            "Each year is a route map, and every stop on it is one farm day. Daylight runs out by itself and night falls.\n\n" +
            "Every season is 6 days: 5 normal days, then a boss night. Boss nights start in the dark, so bring crops you harvested earlier. If dawn breaks before the boss is fed, you lose. Falling off the farm costs a heart."),
        ("Coins and the Bank",
            "Sell crops to the merchant for coins, and spend coins on seeds. His shelf is limited and restocks on Merchant days.\n\n" +
            "At the end of each season you can deposit some coins into the vault. Vault coins buy permanent upgrades: more farmland, a bigger stockroom, a bigger vault. Coins left in your purse are lost when the year ends."),
        ("Controls", null),
    };

    public HowToPlayScreen(RunDirector director) { this.director = director; }

    public void OnOpen() { page = 0; }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        Rect panel = ImGuiKit.Modal(1100f, 720f, "How to Play");
        ImGuiKit.Label(new Rect(panel.xMax - 420 * s, panel.y + 26 * s, 396 * s, 30 * s), "Esc to go back", 18f, ImGuiKit.Muted, TextAnchor.MiddleRight);

        // Left column: one tab per page.
        float tabX = panel.x + 24 * s, tabW = 280 * s, tabH = 56 * s, tabGap = 10 * s;
        float y = panel.y + 100 * s;
        for (int i = 0; i < Pages.Length; i++)
        {
            Rect tab = new Rect(tabX, y, tabW, tabH);
            if (ImGuiKit.Button(tab, Pages[i].title, i != page, 20f)) page = i;
            if (i == page) ImGuiKit.Fill(new Rect(tab.x - 8 * s, tab.y, 6 * s, tab.height), ImGuiKit.Accent);
            y += tabH + tabGap;
        }

        // Right column: the open page.
        float x = tabX + tabW + 40 * s;
        float w = panel.xMax - x - 32 * s;
        float top = panel.y + 100 * s;
        ImGuiKit.Label(new Rect(x, top, w, 40 * s), Pages[page].title, 30f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);

        string body = Pages[page].body;
        if (body == null) ControlsScreen.DrawGroups(x, top + 50 * s, w);
        else ImGuiKit.Label(new Rect(x, top + 56 * s, w, panel.yMax - top - 56 * s - 90 * s), body, 22f, Color.white, TextAnchor.UpperLeft);

        if (ImGuiKit.Button(new Rect(panel.xMax - 190 * s, panel.yMax - 66 * s, 166 * s, 44 * s), "Back", true, 20f)) director.CloseHowToPlay();
    }
}
