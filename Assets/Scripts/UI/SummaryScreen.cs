using UnityEngine;

/// <summary>End of a year: what happened, then the upgrade shop is right there so you can spend your vault before the next year.</summary>
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
        UiTheme theme = ImGuiKit.Theme;

        ImGuiKit.Backdrop(0f);

        // Left: what happened this year, on a cream card in an orange frame.
        Rect outer = new Rect(40f * s, 100f * s, 560f * s, 862f * s);
        ImGuiKit.Rounded(outer, ImGuiKit.Orange);
        Rect card = new Rect(outer.x + 16f * s, outer.y + 16f * s, outer.width - 32f * s, outer.height - 32f * s);
        ImGuiKit.Rounded(card, ImGuiKit.Cream);
        float x = card.x + 26f * s, w = card.width - 52f * s;

        ImGuiKit.Heading(new Rect(x, card.y + 14f * s, w, 80f * s), $"Year {year}", 64f, ImGuiKit.Text);
        ImGuiKit.Label(new Rect(x, card.y + 94f * s, w, 40f * s), won ? "You Survived Another Year..." : "The Night Consumed You", 28f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        ImGuiKit.DashedRule(x, card.y + 142f * s, w);

        ImGuiKit.Label(new Rect(x, card.y + 158f * s, w, 120f * s),
            won ? "The caterpillar burst into a butterfly and the farm is safe, for now."
                : run.stats.daysSurvived == 0 ? "The year ended before it began."
                : "The farm falls quiet, but the soil remembers.",
            24f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        RunStats st = run.stats;
        string stats =
            $"Days survived: {st.daysSurvived}\n" +
            $"Layers reached: {st.layersReached}\n" +
            $"Coins earned: {st.coinsEarned}\n" +
            $"Banked to the vault: {run.depositsThisRun}\n" +
            $"Coins lost: {st.coinsLost}  (left in your purse)";
        ImGuiKit.Label(new Rect(x, card.y + 292f * s, w, 230f * s), stats, 26f, ImGuiKit.Text, TextAnchor.UpperLeft);

        ImGuiKit.IconValue(new Rect(x, card.y + 540f * s, w, 72f * s), theme != null ? theme.piggyBank : null, $"{meta.vaultCoins} in the vault", 36f, ImGuiKit.Text, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(x, card.y + 624f * s, w, 130f * s),
            "Coins in your purse fade with the year. The vault is what the farm remembers: spend it on upgrades here, before the next year begins.", 22f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        // Right: the upgrade shop.
        Rect shop = new Rect(660f * s, 119f * s, 1220f * s, 842f * s);
        UpgradeList.Draw(director, shop);

        // Next steps.
        float y = 985f * s, h = 66f * s;
        if (ImGuiKit.Pill(new Rect(shop.x, y, 440f * s, h), $"Start Year {meta.runsStarted + 1}", ImGuiKit.PillKind.Primary, true, 30f)) director.StartNewRun();
        if (ImGuiKit.Pill(new Rect(shop.x + 480f * s, y, 340f * s, h), "Back to Title", ImGuiKit.PillKind.Cream, true, 28f)) director.QuitToTitle();
    }
}
