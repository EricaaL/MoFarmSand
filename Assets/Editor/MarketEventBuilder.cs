using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the two season-long market events (Inflation +50%, Deflation -40%, 5% a day each) and lists them in the EconomyConfig.
/// Additive: existing assets keep their tuned values.
/// </summary>
public static class MarketEventBuilder
{
    private const string Folder = "Assets/Data/Economy";
    private const string ConfigPath = Folder + "/EconomyConfig.asset";

    [MenuItem("MoFarmSand/Add Market Events")]
    public static void AddMarketEvents()
    {
        MarketEventData inflation = Make("MarketEvent_Inflation", "inflation", "Inflation", 1.5f,
            "Inflation! Everything cost more!");
        MarketEventData deflation = Make("MarketEvent_Deflation", "deflation", "Deflation", 0.6f,
            "Deflation! Everything cost less!");

        EconomyConfig config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(ConfigPath);
        foreach (MarketEventData e in new[] { inflation, deflation })
            if (!config.marketEvents.Contains(e)) config.marketEvents.Add(e);
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("Market events ready: inflation and deflation in " + ConfigPath);
    }

    private static MarketEventData Make(string file, string id, string name, float multiplier, string banner)
    {
        string path = $"{Folder}/{file}.asset";
        MarketEventData data = AssetDatabase.LoadAssetAtPath<MarketEventData>(path);
        if (data != null) return data;
        data = ScriptableObject.CreateInstance<MarketEventData>();
        data.eventId = id;
        data.displayName = name;
        data.priceMultiplier = multiplier;
        data.bannerText = banner;
        data.chancePerDay = 0.05f;
        AssetDatabase.CreateAsset(data, path);
        return data;
    }
}
