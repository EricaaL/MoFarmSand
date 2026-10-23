using UnityEngine;

/// <summary>The How to Play tab of the Options page: a button per topic (goal, farming, crops, days, coins, controls) and its text.</summary>
public class HowToPlayScreen
{
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

    public void OnOpen() { page = 0; }

    /// <summary>Draws into <paramref name="area"/> (the cream inner panel).</summary>
    public void DrawBody(Rect area)
    {
        float s = ImGuiKit.Scale;

        // Left: one button per page.
        float bx = area.x + 36f * s, bw = 330f * s, bh = 72f * s, gap = 14f * s;
        float y = area.y + 36f * s;
        for (int i = 0; i < Pages.Length; i++)
        {
            if (ImGuiKit.Pill(new Rect(bx, y, bw, bh), Pages[i].title, i == page ? ImGuiKit.PillKind.Primary : ImGuiKit.PillKind.Light, true, 26f, i == page)) page = i;
            y += bh + gap;
        }

        // Right: the open page.
        float x = bx + bw + 60f * s;
        float w = area.xMax - x - 50f * s;
        float top = area.y + 30f * s;
        ImGuiKit.Heading(new Rect(x, top, w, 80f * s), Pages[page].title, 56f, ImGuiKit.Text);
        ImGuiKit.DashedRule(x, top + 86f * s, Mathf.Min(w, 560f * s));

        string body = Pages[page].body;
        if (body == null) ControlsScreen.DrawGroups(x, top + 104f * s, w);
        else ImGuiKit.Label(new Rect(x, top + 106f * s, w, area.yMax - top - 120f * s), body, 28f, ImGuiKit.Text, TextAnchor.UpperLeft);
    }
}
