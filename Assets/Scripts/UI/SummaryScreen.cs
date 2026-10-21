using UnityEngine;

/// <summary>End of a year: what happened, then the upgrade shop pops up so you can spend your vault before the next year.</summary>
public class SummaryScreen : IRunScreen
{
    private readonly RunDirector director;

    public SummaryScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        RunState run = director.Run;
        MetaProgress meta = director.Meta;
        bool won = run.phase == RunPhase.Won;
        int year = run.YearNumber(meta);
        float s = ImGuiKit.Scale;

        Rect panel = ImGuiKit.Modal(1240f, 760f, won ? $"Year {year} - The Miracle Berry Blooms!" : $"Year {year} - The Night Consumed You");
        float x = panel.x + 24 * s;
        float leftW = 480 * s;

        // Left: what happened this year.
        ImGuiKit.Label(new Rect(x, panel.y + 80 * s, leftW, 88 * s),
            won ? "The caterpillar burst into a butterfly and the farm is safe, for now."
                : run.stats.daysSurvived == 0 ? "The year ended before it began."
                : "The farm falls quiet. Next year, plant in season and bring crops to the night.",
            20f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        RunStats st = run.stats;
        string stats =
            $"Days survived: {st.daysSurvived}\n" +
            $"Layers reached: {st.layersReached}\n" +
            $"Coins earned: {st.coinsEarned}\n" +
            $"Banked to the vault: {run.depositsThisRun}\n" +
            $"Coins lost: {st.coinsLost}  (left in your purse)";
        ImGuiKit.Label(new Rect(x, panel.y + 180 * s, leftW, 190 * s), stats, 24f, Color.white, TextAnchor.UpperLeft);

        ImGuiKit.Label(new Rect(x, panel.y + 390 * s, leftW, 40 * s), $"Vault: {meta.vaultCoins} coins", 34f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(x, panel.y + 432 * s, leftW, 80 * s),
            "Your purse always starts fresh. Spend the vault on upgrades here, before the next year begins.", 18f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        // Right: the upgrade shop.
        UpgradeList.Draw(director, new Rect(x + leftW + 40 * s, panel.y + 80 * s, panel.width - leftW - 112 * s, panel.height - 200 * s));

        float bw = (panel.width - 48 * s - 16 * s) / 2f;
        if (ImGuiKit.Button(new Rect(x, panel.yMax - 76 * s, bw, 56 * s), $"Start Year {meta.runsStarted + 1}", true, 24f)) director.StartNewRun();
        if (ImGuiKit.Button(new Rect(x + bw + 16 * s, panel.yMax - 76 * s, bw, 56 * s), "Back to Title", true, 22f)) director.QuitToTitle();
    }
}
