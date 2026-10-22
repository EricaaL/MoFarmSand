using UnityEngine;

/// <summary>Three save slots, then a per-slot menu: continue the year, start a new year, shop upgrades, delete.</summary>
public class TitleScreen : IRunScreen
{
    private readonly RunDirector director;
    private int pendingDelete = -1;
    private bool confirmNewYear;

    public TitleScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        if (RunSession.HasActiveSlot) DrawSlotMenu();
        else DrawSlotList();
    }

    private void DrawSlotList()
    {
        Rect panel = ImGuiKit.Modal(1100f, 620f, "DOGSEND");
        float s = ImGuiKit.Scale;
        ImGuiKit.Label(new Rect(panel.x + 24 * s, panel.y + 70 * s, panel.width - 48 * s, 30 * s),
            "Choose a save slot", 24f, ImGuiKit.Muted);

        float cardW = (panel.width - 24 * s * 4) / 3f;
        for (int i = 0; i < SaveService.SlotCount; i++)
        {
            Rect card = new Rect(panel.x + 24 * s + i * (cardW + 24 * s), panel.y + 120 * s, cardW, 380 * s);
            ImGuiKit.Fill(card, new Color(0.1f, 0.09f, 0.16f, 1f));
            SlotSummary sum = director.Service.Peek(i);

            ImGuiKit.Label(new Rect(card.x + 16 * s, card.y + 12 * s, card.width - 32 * s, 36 * s), $"Slot {i + 1}", 30f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);

            string body;
            switch (sum.status)
            {
                case LoadStatus.Empty: body = "Empty\nStart a new game."; break;
                case LoadStatus.Corrupt: body = "This save could not be read."; break;
                case LoadStatus.TooNew: body = "Saved by a newer version of the game."; break;
                default:
                    body = (sum.hasRun ? $"Year {Mathf.Max(1, sum.year)} in progress\nDay {sum.dayIndex + 1}  ·  {sum.coins} coins" : "No year in progress")
                         + $"\n\nVault: {sum.vaultCoins} coins\nUpgrades: {sum.unlockCount}\nYears played: {sum.runsStarted}";
                    break;
            }
            ImGuiKit.Label(new Rect(card.x + 16 * s, card.y + 64 * s, card.width - 32 * s, 220 * s), body, 22f, Color.white, TextAnchor.UpperLeft);

            bool canOpen = sum.status == LoadStatus.Empty || sum.status == LoadStatus.Ok || sum.status == LoadStatus.Corrupt;
            if (ImGuiKit.Button(new Rect(card.x + 16 * s, card.yMax - 120 * s, card.width - 32 * s, 48 * s), sum.status == LoadStatus.Empty ? "New Game" : "Open", canOpen))
            {
                if (sum.status == LoadStatus.Corrupt) director.DeleteSlot(i);
                director.OpenSlot(i);
            }

            bool hasData = sum.status != LoadStatus.Empty;
            bool armed = pendingDelete == i;
            if (ImGuiKit.Button(new Rect(card.x + 16 * s, card.yMax - 60 * s, card.width - 32 * s, 40 * s), armed ? "Click again to delete" : "Delete", hasData, 18f))
            {
                if (armed) { director.DeleteSlot(i); pendingDelete = -1; }
                else pendingDelete = i;
            }
        }

        if (ImGuiKit.Button(new Rect(panel.x + 24 * s, panel.yMax - 66 * s, 166 * s, 44 * s), "Back", true, 20f))
        {
            pendingDelete = -1;
            director.ShowMainMenu();
        }
    }

    private void DrawSlotMenu()
    {
        float s = ImGuiKit.Scale;
        SaveFile file = director.File;
        MetaProgress meta = file.meta;
        Rect panel = ImGuiKit.Modal(1180f, 760f, $"Slot {RunSession.ActiveSlot + 1}");

        // Left column: the farm's history and year actions.
        float x = panel.x + 24 * s;
        float y = panel.y + 90 * s;
        ImGuiKit.Label(new Rect(x, y, 440 * s, 36 * s), $"Vault: {meta.vaultCoins} coins", 28f, ImGuiKit.Accent, TextAnchor.MiddleLeft, true);
        y += 40 * s;
        ImGuiKit.Label(new Rect(x, y, 440 * s, 96 * s),
            "Deposit coins at the bank at the end of a season to fill the vault. Vault coins buy permanent upgrades, or can be withdrawn for a fee. Your purse always starts fresh each year.",
            18f, ImGuiKit.Muted, TextAnchor.UpperLeft);
        y += 104 * s;
        ImGuiKit.Label(new Rect(x, y, 440 * s, 30 * s), $"Years played: {meta.runsStarted}   ·   Best: layer {meta.bestLayer}   ·   Won: {meta.runsWon}", 18f, Color.white, TextAnchor.MiddleLeft);
        y += 48 * s;

        bool inProgress = file.hasRun && file.run.phase != RunPhase.Won && file.run.phase != RunPhase.Lost;
        if (file.hasRun)
        {
            int year = file.run.YearNumber(meta);
            string label = inProgress ? $"Continue Year {year}" : $"View Year {year} Summary";
            if (ImGuiKit.Button(new Rect(x, y, 440 * s, 54 * s), label)) { director.ContinueRun(); return; }
            y += 66 * s;
        }

        string newLabel = inProgress && confirmNewYear ? "Abandon this year and start fresh?" : $"Start Year {meta.runsStarted + 1}";
        if (ImGuiKit.Button(new Rect(x, y, 440 * s, 54 * s), newLabel))
        {
            if (inProgress && !confirmNewYear) confirmNewYear = true;
            else { confirmNewYear = false; director.StartNewRun(); return; }
        }
        y += 66 * s;
        if (ImGuiKit.Button(new Rect(x, y, 440 * s, 44 * s), "Back to Slots", true, 20f))
        {
            confirmNewYear = false;
            director.CloseSlot();
            return;
        }

        // Right column: permanent upgrades bought with the vault.
        UpgradeList.Draw(director, new Rect(panel.x + 520 * s, panel.y + 90 * s, panel.width - 520 * s - 24 * s, panel.height - 120 * s));
    }
}
