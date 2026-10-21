using UnityEngine;

/// <summary>
/// Season's end. You can only bank a limited amount each season, and the bank keeps a fee. Vault coins buy
/// permanent upgrades when the year ends. Withdrawing sends vault coins back to the purse for a fee that grows with the amount.
/// </summary>
public class BankScreen : IRunScreen
{
    private readonly RunDirector director;
    private string message = "";

    public BankScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        RunState run = director.Run;
        MetaProgress meta = director.Meta;
        BankConfig cfg = director.Db.bank;
        float s = ImGuiKit.Scale;

        SeasonData season = director.Db.Calendar.DateOf(Mathf.Max(0, run.DayIndex - 1)).season;
        string seasonName = season != null ? season.displayName : "the season";
        Rect panel = ImGuiKit.Modal(900f, 820f, $"End of {seasonName} - The Bank");

        float x = panel.x + 24 * s;
        float w = panel.width - 48 * s;
        ImGuiKit.Label(new Rect(x, panel.y + 80 * s, w, 80 * s),
            "Coins left in your purse at the end of the year are lost. Bank a limited amount each season: it goes into the vault to buy permanent upgrades. The bank keeps a fee. You can also take vault coins back into your purse, for a bigger fee the more you take.",
            19f, ImGuiKit.Muted, TextAnchor.UpperLeft);

        ImGuiKit.Label(new Rect(x, panel.y + 170 * s, w * 0.5f, 40 * s), $"Purse: {run.coins}", 34f, Color.white, TextAnchor.MiddleLeft, true);
        ImGuiKit.Label(new Rect(x + w * 0.5f, panel.y + 170 * s, w * 0.5f, 40 * s), $"Vault: {meta.vaultCoins} / {VaultLogic.Cap(meta, cfg)}", 34f, ImGuiKit.Accent, TextAnchor.MiddleRight, true);

        // How much you may still deposit this season (a hard limit, never your whole purse by default).
        int seasonLeft = cfg.maxDepositPerSeason > 0 ? Mathf.Max(0, cfg.maxDepositPerSeason - run.depositedThisSeason) : int.MaxValue;
        int limit = Mathf.Min(run.coins, seasonLeft);
        string allowance = cfg.maxDepositPerSeason > 0
            ? $"Deposit limit this season: {seasonLeft} of {cfg.maxDepositPerSeason} left   ·   Fee: {cfg.depositFeePercent:0}%"
            : $"Fee: {cfg.depositFeePercent:0}%";
        ImGuiKit.Label(new Rect(x, panel.y + 218 * s, w, 28 * s), allowance, 18f, seasonLeft == 0 ? new Color(1f, 0.55f, 0.5f) : ImGuiKit.Muted);

        bool vaultFull = meta.vaultCoins >= VaultLogic.Cap(meta, cfg);
        int[] amounts = { 1, 5, 10, -1 };
        float bw = (w - 3 * 16 * s) / 4f;
        for (int i = 0; i < amounts.Length; i++)
        {
            bool isMax = amounts[i] < 0;
            int amount = isMax ? limit : amounts[i];
            string label = isMax ? $"Deposit max ({limit})" : $"Deposit {amount}";
            Rect b = new Rect(x + i * (bw + 16 * s), panel.y + 270 * s, bw, 60 * s);
            bool can = limit > 0 && !vaultFull && (isMax || amount <= limit);
            if (ImGuiKit.Button(b, label, can, 20f))
            {
                DepositResult r = director.Deposit(amount);
                message = r.spent > 0 ? $"Deposited {r.spent}: {r.credited} in the vault, {r.fee} kept as fee." : "Nothing could be deposited.";
            }
        }
        if (vaultFull) ImGuiKit.Label(new Rect(x, panel.y + 336 * s, w, 26 * s), "The vault is full. Spend it on upgrades at the end of the year.", 18f, ImGuiKit.Muted);

        // Withdrawals: vault -> purse, for a fee that takes a bigger share the more you take.
        bool lastStop = run.endYearAfterBanking;
        ImGuiKit.Label(new Rect(x, panel.y + 372 * s, w, 30 * s), "Withdraw from the vault", 24f, Color.white, TextAnchor.MiddleLeft, true);
        string feeNote = lastStop
            ? "Coins in your purse are lost when the year ends, so withdrawing is closed on this last stop."
            : $"The more you take, the bigger the fee: {BankLogic.WithdrawFee(10, cfg)} coin(s) for 10, {BankLogic.WithdrawFee(50, cfg)} for 50, {BankLogic.WithdrawFee(100, cfg)} for 100.";
        ImGuiKit.Label(new Rect(x, panel.y + 404 * s, w, 28 * s), feeNote, 18f, lastStop ? new Color(1f, 0.55f, 0.5f) : ImGuiKit.Muted);

        int[] takes = { 10, 25, 50, -1 };
        for (int i = 0; i < takes.Length; i++)
        {
            bool isMax = takes[i] < 0;
            int amount = isMax ? meta.vaultCoins : takes[i];
            int fee = BankLogic.WithdrawFee(amount, cfg);
            string label = isMax ? $"Max {amount} (fee {fee})" : $"Take {amount} (fee {fee})";
            Rect b = new Rect(x + i * (bw + 16 * s), panel.y + 438 * s, bw, 60 * s);
            bool can = !lastStop && amount > fee && amount <= meta.vaultCoins;
            if (ImGuiKit.Button(b, label, can, 20f))
            {
                WithdrawResult r = director.Withdraw(amount);
                message = r.taken > 0 ? $"Withdrew {r.taken}: {r.received} added to your purse, {r.fee} kept as fee." : "Nothing could be withdrawn.";
            }
        }

        ImGuiKit.Label(new Rect(x, panel.y + 514 * s, w, 34 * s), message, 20f, ImGuiKit.Accent, TextAnchor.MiddleLeft);
        ImGuiKit.Label(new Rect(x, panel.y + 556 * s, w, 28 * s), $"Banked this year: {run.depositsThisRun} coins", 18f, ImGuiKit.Muted);

        if (ImGuiKit.Button(new Rect(x, panel.yMax - 90 * s, w, 60 * s), run.endYearAfterBanking ? "Done - end the year" : "Done - back to the map", true, 24f))
        {
            message = "";
            director.FinishBanking();
        }
    }
}
