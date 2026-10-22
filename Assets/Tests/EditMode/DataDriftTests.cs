using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>Guards the single-owner rules: assets stay consistent, and the pure core stays pure.</summary>
public class DataDriftTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    private static void AssertValid<T>(IdRegistry<T> reg) where T : ScriptableObject, IHasId
    {
        Assert.IsNotNull(reg, typeof(T).Name + " registry is missing");
        Assert.IsTrue(reg.Validate(out List<string> errors), string.Join("; ", errors));
        Assert.Greater(reg.All.Count, 0, typeof(T).Name + " registry is empty");
    }

    [Test]
    public void EveryRegistryIsPopulatedWithUniqueIds()
    {
        AssertValid(db.items);
        AssertValid(db.disasters);
        AssertValid(db.nodeTypes);
        AssertValid(db.modifiers);
        AssertValid(db.events);
        AssertValid(db.unlocks);
        AssertValid(db.tips);
    }

    [Test]
    public void EveryTipIdHasATipAsset()
    {
        foreach (string id in TipIds.All)
        {
            TipData tip = db.tips.Get(id);
            Assert.IsNotNull(tip, $"no TipData for '{id}'");
            Assert.IsFalse(string.IsNullOrWhiteSpace(tip.body), $"tip '{id}' has no text");
        }
    }

    [Test]
    public void EveryThrownCropHasItsOwnPhysicalProjectile()
    {
        HashSet<UnityEngine.GameObject> lobbed = new HashSet<UnityEngine.GameObject>();
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop) || (crop.type != CropType.Projectile && crop.type != CropType.Shooter)) continue;
            UnityEngine.GameObject prefab = crop.projectilePrefab;
            Assert.IsNotNull(prefab, $"{crop.Id} has no projectile prefab");
            Assert.IsNotNull(prefab.GetComponent<UnityEngine.Rigidbody>(), $"{crop.Id}: projectile has no Rigidbody");
            Assert.IsNotNull(prefab.GetComponent<UnityEngine.Collider>(), $"{crop.Id}: projectile has no Collider");
            Assert.IsNotNull(prefab.GetComponent<CropProjectile>(), $"{crop.Id}: projectile has no CropProjectile");
            if (crop.type == CropType.Projectile)
                Assert.IsTrue(lobbed.Add(prefab), $"{crop.Id} shares its projectile prefab with another crop (each needs its own look)");
        }
    }

    [Test]
    public void ThrownCropsThatHavePackModelsShowThem()
    {
        foreach (string id in new[] { "tomato", "pumpkin", "garlic", "corn" })
        {
            CropData crop = (CropData)db.items.Get(id);
            UnityEngine.Transform model = crop.projectilePrefab.transform.Find("Model");
            Assert.IsNotNull(model, $"{id}: projectile has no Model child (run MoFarmSand > Rebuild Crop Projectiles)");
            Assert.IsNotNull(model.GetComponentInChildren<UnityEngine.MeshRenderer>(), $"{id}: projectile model has no mesh");
            Assert.IsNull(crop.projectilePrefab.GetComponent<UnityEngine.MeshRenderer>(), $"{id}: projectile still has the old sphere on its root");
        }
    }

    [Test]
    public void EveryCropHasThreeGrowthModels()
    {
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            Assert.AreEqual(3, crop.growthStages.Length, $"{crop.Id} needs sprout, growing and ripe models");
            foreach (UnityEngine.GameObject model in crop.growthStages)
            {
                Assert.IsNotNull(model, $"{crop.Id} has an empty growth stage");
                Assert.AreEqual(9, model.transform.childCount, $"{crop.Id}: '{model.name}' should be a 3x3 plot of plants (run MoFarmSand > Rebuild 3x3 Crop Plots)");
            }
        }
    }

    [Test]
    public void EveryCropAndSeedIsRegisteredAndEveryCropHasExactlyOnePriceRow()
    {
        foreach (PriceTable.PriceRow row in db.prices.rows)
        {
            Assert.AreSame(row.crop, db.items.Get(row.crop.Id), $"crop {row.crop.Id} not in the item registry");
            Assert.AreSame(row.seed, db.items.Get(row.seed.Id), $"seed {row.seed.Id} not in the item registry");
            Assert.AreSame(row.seed.grows, row.crop);
        }
        foreach (ItemData item in db.items.All)
        {
            if (item is CropData crop) Assert.IsNotNull(db.prices.RowForCrop(crop.Id), $"no price row for {crop.Id}");
            if (item is SeedData seed) Assert.IsNotNull(db.prices.RowForSeed(seed.Id), $"no price row for {seed.Id}");
        }
    }

    [Test]
    public void EveryNodeTypeUsedByTheMapGeneratorIsRegistered()
    {
        Assert.IsNotNull(db.nodeTypes.Get(db.mapGen.bossType.Id));
        Assert.IsNotNull(db.nodeTypes.Get(db.mapGen.fallbackType.Id));
        foreach (MapGenConfig.LayerBand band in db.mapGen.bands)
            foreach (MapGenConfig.NodeWeight w in band.weights)
                Assert.AreSame(w.type, db.nodeTypes.Get(w.type.Id), $"{w.type.name} is not registered");
        foreach (MapGenConfig.ForcedLayer f in db.mapGen.forcedLayers)
            Assert.IsNotNull(db.nodeTypes.Get(f.type.Id));
    }

    [Test]
    public void EveryDisasterHasWeightsInSeasonsThatExistAndEffects()
    {
        foreach (DisasterData d in db.disasters.All)
        {
            Assert.IsNotEmpty(d.effects, $"{d.Id} has no effects");
            Assert.IsTrue(d.seasonWeights.Exists(w => w.weight > 0f), $"{d.Id} can never roll");
            foreach (DisasterData.SeasonWeight w in d.seasonWeights)
                Assert.IsTrue(db.calendar.seasons.Contains(w.season), $"{d.Id} references a season outside the calendar");
        }
    }

    [Test]
    public void EverySeasonAndDisasterHasACozyPresentationMapping()
    {
        foreach (SeasonData s in db.calendar.seasons)
        {
            bool any = db.cozyWeather.defaults.Exists(e => (e.season == s || e.season == null) && e.profile != null);
            Assert.IsTrue(any, $"no COZY default weather for {s.displayName}");
        }
        foreach (DisasterData d in db.disasters.All)
            Assert.IsNotNull(db.cozyWeather.disasters.Find(e => e.disaster == d).profile, $"no COZY profile for {d.Id}");
    }

    [Test]
    public void MapLayersMatchTheCalendarAndEverySeasonEndsInABankStop()
    {
        Assert.AreEqual(24, db.Calendar.TotalDays);
        Assert.AreEqual(db.Calendar.TotalDays - 1, db.Calendar.MapLayerCount, "the final boss is appended as the last day");
        Assert.AreEqual(db.calendar.seasons.Count - 1, db.Calendar.SeasonBossDays().Count, "a boss night ends every season but the last");
        int seasonEnds = 0;
        for (int d = 0; d < db.Calendar.TotalDays; d++) if (db.Calendar.DateOf(d).IsLastDayOfSeason) seasonEnds++;
        Assert.AreEqual(db.calendar.seasons.Count, seasonEnds);
    }

    [Test]
    public void OnlySeasonBossesAreSurvivableToLose()
    {
        NodeTypeData seasonBoss = db.nodeTypes.Get("season_boss"), finalBoss = db.nodeTypes.Get("boss");
        Assert.IsTrue(seasonBoss.survivableLoss);
        Assert.IsFalse(finalBoss.survivableLoss);
        Assert.Greater(seasonBoss.lossGrowthMultiplier, 0f);
        Assert.Less(seasonBoss.lossGrowthMultiplier, 1f);
    }

    [Test]
    public void EveryCropHasClimateAffinityForEverySeason()
    {
        foreach (ItemData item in db.items.All)
        {
            if (!(item is CropData crop)) continue;
            foreach (SeasonData s in db.calendar.seasons)
                Assert.IsTrue(crop.climate.seasonAffinity.Exists(a => a.season == s), $"{crop.Id} has no affinity for {s.displayName}");
        }
    }

    // ------------------------------------------------------------------ pure core boundary

    private static readonly (string pattern, string why)[] Forbidden =
    {
        (@"\bMonoBehaviour\b", "scene components"),
        (@"UnityEngine\.Random|\bRandom\.(Range|value)", "unseeded randomness"),
        (@"(?<![A-Za-z])Time\.(time|deltaTime|unscaledTime|realtimeSinceStartup)", "frame time"),
        (@"DistantLands", "COZY"),
        (@"FindObject|FindFirstObject|GameObject\.Find|FindWithTag", "scene lookups"),
        (@"PlayerPrefs", "player prefs"),
    };

    [Test]
    public void SimFolderStaysPure()
    {
        string root = Path.Combine(Application.dataPath, "Scripts", "Sim");
        Assert.IsTrue(Directory.Exists(root));
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string text = Regex.Replace(File.ReadAllText(file), @"//.*|/\*[\s\S]*?\*/", string.Empty);
            foreach ((string pattern, string why) in Forbidden)
                Assert.IsFalse(Regex.IsMatch(text, pattern), $"{Path.GetFileName(file)} uses {why} ({pattern})");

            if (Path.GetFileName(file) != "FileSaveStore.cs")
                Assert.IsFalse(text.Contains("System.IO"), $"{Path.GetFileName(file)} touches System.IO");
        }
    }
}
