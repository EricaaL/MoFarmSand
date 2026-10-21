using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-off, idempotent move to the 8-crop roster (spring peas+lettuce, summer tomato+corn, autumn turnip+pumpkin,
/// winter garlic+spinach). Deletes Frostberry and the four season variants (with their seeds and vault unlocks),
/// prunes the registries / price table / starting pool, moves Turnip to autumn, then builds the new crops.
/// Entry point for -executeMethod CropRosterMigration.Apply.
/// </summary>
public static class CropRosterMigration
{
    private const string Data = "Assets/Data";

    private static readonly string[] Doomed =
    {
        Data + "/Crops/Crop_Frostberry.asset", Data + "/Crops/Crop_HoneyTurnip.asset", Data + "/Crops/Crop_SunfireTomato.asset",
        Data + "/Crops/Crop_GoldenPumpkin.asset", Data + "/Crops/Crop_AuroraFrostberry.asset",
        Data + "/Seeds/Seed_Frostberry.asset", Data + "/Seeds/Seed_Honey_turnip.asset", Data + "/Seeds/Seed_Sunfire_tomato.asset",
        Data + "/Seeds/Seed_Golden_pumpkin.asset", Data + "/Seeds/Seed_Aurora_frostberry.asset",
        Data + "/Meta/Unlock_crop_honey_turnip.asset", Data + "/Meta/Unlock_crop_sunfire_tomato.asset",
        Data + "/Meta/Unlock_crop_golden_pumpkin.asset", Data + "/Meta/Unlock_crop_aurora_frostberry.asset",
    };

    [MenuItem("MoFarmSand/Migrate Crop Roster")]
    public static void Apply()
    {
        AssetDatabase.SaveAssets();

        foreach (string path in Doomed)
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.Refresh();

        Prune<ItemRegistry>(Data + "/Registries/ItemRegistry.asset");
        Prune<UnlockRegistry>(Data + "/Registries/UnlockRegistry.asset");

        PriceTable prices = AssetDatabase.LoadAssetAtPath<PriceTable>(Data + "/Economy/PriceTable.asset");
        if (prices != null)
        {
            prices.rows.RemoveAll(r => r == null || r.crop == null || r.seed == null);
            EditorUtility.SetDirty(prices);
        }

        RunConfig run = AssetDatabase.LoadAssetAtPath<RunConfig>(Data + "/Run/RunConfig.asset");
        if (run != null)
        {
            run.startingSeedPool.RemoveAll(e => e.seed == null);
            EditorUtility.SetDirty(run);
        }

        MoveTurnipToAutumn();

        // Creates the five new crops/seeds/prefabs, price rows and pool entries, and backfills the registries.
        RunSystemsBuilder.BuildAssetsOnly();
        Debug.Log("[CropRosterMigration] Crop roster migrated.");
    }

    private static void Prune<T>(string path) where T : ScriptableObject
    {
        T reg = AssetDatabase.LoadAssetAtPath<T>(path);
        if (reg == null) return;
        if (reg is ItemRegistry items) items.PruneMissing();
        else if (reg is UnlockRegistry unlocks) unlocks.PruneMissing();
        EditorUtility.SetDirty(reg);
    }

    /// <summary>SetClimate never overwrites a tuned profile, so Turnip (spring crop before) is forced to the autumn profile here.</summary>
    private static void MoveTurnipToAutumn()
    {
        CropData turnip = AssetDatabase.LoadAssetAtPath<CropData>(Data + "/Crop_Turnip.asset");
        SeasonData spring = AssetDatabase.LoadAssetAtPath<SeasonData>(Data + "/Seasons/Season_Spring.asset");
        SeasonData summer = AssetDatabase.LoadAssetAtPath<SeasonData>(Data + "/Seasons/Season_Summer.asset");
        SeasonData autumn = AssetDatabase.LoadAssetAtPath<SeasonData>(Data + "/Seasons/Season_Autumn.asset");
        SeasonData winter = AssetDatabase.LoadAssetAtPath<SeasonData>(Data + "/Seasons/Season_Winter.asset");
        if (turnip == null || spring == null || summer == null || autumn == null || winter == null)
        {
            Debug.LogError("[CropRosterMigration] Turnip or season assets missing; Turnip was not moved to autumn.");
            return;
        }
        RunSystemsBuilder.SetClimate(turnip, true, 12f, 12f, 0.45f, 0.6f, (spring, 0.8f), (summer, 0.5f), (autumn, 1.4f), (winter, 0.8f));
    }
}
