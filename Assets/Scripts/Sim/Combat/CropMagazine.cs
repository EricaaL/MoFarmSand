using System;

/// <summary>
/// Loaded rounds for shooter crops (peas, corn). One harvested crop loads <c>ammoPerCrop</c> rounds; the magazine
/// belongs to the crop it was loaded from. Pure: the caller supplies the inventory side as a callback.
/// </summary>
public class CropMagazine
{
    private string loadedId = string.Empty;
    private int rounds;
    private float quality = 1f;

    /// <summary>Rounds currently loaded for this crop (0 if the magazine holds another crop).</summary>
    public int Loaded(CropData crop) => crop != null && crop.Id == loadedId ? rounds : 0;

    /// <summary>True if a shot is possible: rounds are loaded or a crop is available to load.</summary>
    public bool CanFire(CropData crop, int cropsInStack) => crop != null && (Loaded(crop) > 0 || cropsInStack > 0);

    /// <summary>
    /// Spends one round, loading a fresh crop first if the magazine is empty. <paramref name="loadOneCrop"/> must
    /// consume exactly one crop from the inventory and return false if it cannot; <paramref name="stackQuality"/> is
    /// the strength of the crop being loaded.
    /// </summary>
    public bool TryFire(CropData crop, Func<bool> loadOneCrop, float stackQuality, out float shotQuality)
    {
        shotQuality = 1f;
        if (crop == null) return false;

        if (Loaded(crop) <= 0)
        {
            if (loadOneCrop == null || !loadOneCrop()) return false;
            loadedId = crop.Id;
            rounds = Math.Max(1, crop.ammoPerCrop);
            quality = stackQuality > 0f ? stackQuality : 1f;
        }

        rounds--;
        shotQuality = quality;
        return true;
    }

    public void Clear()
    {
        loadedId = string.Empty;
        rounds = 0;
        quality = 1f;
    }
}
