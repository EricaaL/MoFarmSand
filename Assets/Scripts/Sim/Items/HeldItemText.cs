/// <summary>What the bottom-right held-item card says. The view only draws it.</summary>
public struct HeldItemLabel
{
    /// <summary>False when there is nothing worth showing (empty slot).</summary>
    public bool visible;
    /// <summary>The big count, e.g. "7 / 10" or "x4".</summary>
    public string main;
    /// <summary>The small line under it, e.g. "+3" (spare crops) or "plots".</summary>
    public string sub;
    /// <summary>Ammo is nearly gone: draw it red.</summary>
    public bool low;
    /// <summary>Shooter with no rounds and no crop left to load: draw "Out of ammo" and pulse it.</summary>
    public bool outOfAmmo;
}

/// <summary>Single owner of the held-item card's wording and low-ammo rules (pure, unit-tested).</summary>
public static class HeldItemText
{
    /// <summary>Rounds left at or below which a shooter with no spare crops shows as low.</summary>
    public const int LowRounds = 2;

    /// <param name="count">Items in the active slot (spare crops for a shooter).</param>
    /// <param name="loadedRounds">Rounds loaded in the shooter's magazine (0 for anything else).</param>
    public static HeldItemLabel Format(ItemData item, int count, int loadedRounds)
    {
        if (item == null) return default;

        if (item is CropData crop && crop.type == CropType.Shooter)
        {
            bool nothing = loadedRounds <= 0 && count <= 0;
            // The magazine loads from a crop on the next shot, so a crop in the stack counts as a full magazine already.
            int rounds = loadedRounds, spare = count;
            if (rounds <= 0 && count > 0) { rounds = crop.ammoPerCrop; spare = count - 1; }
            return new HeldItemLabel
            {
                visible = true,
                main = nothing ? "Out of ammo" : $"{rounds} / {crop.ammoPerCrop}",
                sub = spare > 0 ? $"+{spare}" : string.Empty,
                low = nothing || (spare <= 0 && rounds <= LowRounds),
                outOfAmmo = nothing
            };
        }

        if (count <= 0) return default;
        return new HeldItemLabel
        {
            visible = true,
            main = "x" + count,
            // Seeds need no caption: the name already says "Seeds".
            sub = item is FarmlandPlotItem ? (count == 1 ? "plot" : "plots") : string.Empty
        };
    }
}
