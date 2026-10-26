using UnityEngine;

/// <summary>The upgrade shop on a new year's map, before the first node is chosen. Back returns to the map.</summary>
public class UpgradesScreen : IRunScreen
{
    private readonly RunDirector director;

    public UpgradesScreen(RunDirector director) { this.director = director; }

    public void Draw()
    {
        float s = ImGuiKit.Scale;
        ImGuiKit.Backdrop(0f);

        if (ImGuiKit.BackButton(new Rect(42f * s, 36f * s, 130f * s, 130f * s))) { director.CloseUpgrades(); return; }

        UpgradeList.Draw(director, new Rect(209f * s, 119f * s, 1502f * s, 842f * s));

        if (ImGuiKit.Pill(new Rect(209f * s, 985f * s, 440f * s, 66f * s), "Back to the Map", ImGuiKit.PillKind.Primary, true, 28f)) director.CloseUpgrades();
        ImGuiKit.HudLabel(new Rect(1711f * s - 760f * s, 985f * s, 760f * s, 66f * s), "Upgrades are closed once you choose your first day.", 22f, TextAnchor.MiddleRight, true);
    }
}
