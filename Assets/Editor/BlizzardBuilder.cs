using System.Collections.Generic;
using DistantLands.Cozy.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Turns the Frost disaster into a Blizzard: a denser, wind-driven snow prefab (FX_Blizzard, copied from FX_Snow) with a
/// <see cref="BlizzardFog"/> that thickens COZY's fog, COZY's Heavy Snow weather, and the new name. Additive: the prefab is only copied
/// once (tune it in the Inspector afterwards), the disaster keeps its id and only a still-"Frost" name is changed.
/// </summary>
public static class BlizzardBuilder
{
    private const string SnowPath = "Assets/Prefabs/Systems/FX/FX_Snow.prefab";
    private const string BlizzardPath = "Assets/Prefabs/Systems/FX/FX_Blizzard.prefab";
    private const string DisasterPath = "Assets/Data/Disasters/Disaster_Frost.asset";
    private const string MapPath = "Assets/Data/Presentation/CozyWeatherMap.asset";
    private const string HeavySnowPath = "Packages/com.distantlands.cozy.core/Content/Resources/Profiles/Weather Profiles/Legacy/Heavy Snow.asset";

    [MenuItem("MoFarmSand/Add Blizzard")]
    public static void AddBlizzard()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BlizzardPath) == null)
        {
            AssetDatabase.CopyAsset(SnowPath, BlizzardPath);
            TuneSnow();
        }

        DisasterData frost = AssetDatabase.LoadAssetAtPath<DisasterData>(DisasterPath);
        if (frost.displayName == "Frost")
        {
            frost.displayName = "Blizzard";
            frost.bannerText = "A howling whiteout. Crops barely grow and you can hardly see.";
            EditorUtility.SetDirty(frost);
        }

        CozyWeatherMap map = AssetDatabase.LoadAssetAtPath<CozyWeatherMap>(MapPath);
        WeatherProfile heavySnow = AssetDatabase.LoadAssetAtPath<WeatherProfile>(HeavySnowPath);
        GameObject fx = AssetDatabase.LoadAssetAtPath<GameObject>(BlizzardPath);
        List<CozyWeatherMap.DisasterEntry> entries = map.disasters;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].disaster != frost) continue;
            CozyWeatherMap.DisasterEntry e = entries[i];
            if (heavySnow != null) e.profile = heavySnow;
            e.ambientFx = fx;
            entries[i] = e;
        }
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();
        Debug.Log("Blizzard ready: " + BlizzardPath);
    }

    private static void TuneSnow()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BlizzardPath);
        ParticleSystem ps = root.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.maxParticles = 6000;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.gravityModifier = 0.12f;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 600f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.scale = new Vector3(36f, 0.1f, 36f);

        // A steady gale that shoves the flakes sideways, with a little swirl on top.
        ParticleSystem.VelocityOverLifetimeModule wind = ps.velocityOverLifetime;
        wind.enabled = true;
        wind.space = ParticleSystemSimulationSpace.World;
        wind.x = new ParticleSystem.MinMaxCurve(6f, 9f);
        wind.y = new ParticleSystem.MinMaxCurve(0f);
        wind.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.2f;
        noise.frequency = 0.35f;

        root.AddComponent<BlizzardFog>();
        PrefabUtility.SaveAsPrefabAsset(root, BlizzardPath);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
