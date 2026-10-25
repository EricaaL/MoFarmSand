using System.Collections.Generic;
using System.IO;
using DistantLands.Cozy.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds the seasons / disasters / map / economy / bank / save systems to the project and open scene.
/// Additive and idempotent: asset init only runs on create (so tuned values survive re-runs), registries
/// are "ensure contains", and the scene is never wiped (COZY stays). Entry point for -executeMethod.
/// </summary>
public static class RunSystemsBuilder
{
    private const string Data = "Assets/Data";
    private const string MatDir = "Assets/Art/Materials";
    private const string FxDir = "Assets/Prefabs/Systems/FX";
    private const string CozyProfiles = "Packages/com.distantlands.cozy.core/Content/Resources/Profiles/Weather Profiles";

    [MenuItem("MoFarmSand/Add Run Systems")]
    public static void AddRunSystems()
    {
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        foreach (string d in new[]
        {
            Data + "/Seasons", Data + "/Climate", Data + "/Seeds", Data + "/Crops", Data + "/Disasters", Data + "/Disasters/Effects",
            Data + "/Map", Data + "/Economy", Data + "/Run", Data + "/Run/Effects", Data + "/Meta", Data + "/Events",
            Data + "/Registries", Data + "/Presentation", MatDir, FxDir
        }) EnsureFolder(d);

        GameDatabase db = BuildAssets();
        BuildScene(db);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[RunSystemsBuilder] Run systems added. Set RunDirector.runMode to GoldenSlice for trailer takes.");
    }

    /// <summary>Creates/updates only the data assets (no scene needed), for headless migrations.</summary>
    internal static void BuildAssetsOnly()
    {
        AssetDatabase.SaveAssets();
        foreach (string d in new[]
        {
            Data + "/Seasons", Data + "/Climate", Data + "/Seeds", Data + "/Crops", Data + "/Disasters", Data + "/Disasters/Effects",
            Data + "/Map", Data + "/Economy", Data + "/Run", Data + "/Run/Effects", Data + "/Meta", Data + "/Events",
            Data + "/Registries", Data + "/Presentation", MatDir, FxDir
        }) EnsureFolder(d);
        BuildAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // =====================================================================================
    // Assets
    // =====================================================================================

    private static GameDatabase BuildAssets()
    {
        // ---- Seasons ----
        SeasonData spring = A<SeasonData>($"{Data}/Seasons/Season_Spring.asset", s =>
        {
            s.seasonId = "spring"; s.displayName = "Spring"; s.lengthInDays = 6; s.uiColor = new Color(0.55f, 0.85f, 0.5f);
            s.temperatureCurve = AnimationCurve.Linear(0f, 12f, 1f, 18f); s.temperatureVariance = 3f;
            s.precipitationCurve = AnimationCurve.Linear(0f, 0.55f, 1f, 0.45f); s.precipitationVariance = 0.2f;
            s.disasterChanceBonus = 0f; s.cozyYearStart01 = 0.0f; s.cozyYearSpan01 = 0.2f; s.initialSnow01 = 0f;
        });
        SeasonData summer = A<SeasonData>($"{Data}/Seasons/Season_Summer.asset", s =>
        {
            s.seasonId = "summer"; s.displayName = "Summer"; s.lengthInDays = 6; s.uiColor = new Color(1f, 0.82f, 0.3f);
            s.temperatureCurve = AnimationCurve.Linear(0f, 24f, 1f, 32f); s.temperatureVariance = 3f;
            s.precipitationCurve = AnimationCurve.Linear(0f, 0.25f, 1f, 0.2f); s.precipitationVariance = 0.2f;
            s.dayLengthMultiplier = 1.15f;
            s.disasterChanceBonus = 0.05f; s.cozyYearStart01 = 0.25f; s.cozyYearSpan01 = 0.2f; s.initialSnow01 = 0f;
        });
        SeasonData autumn = A<SeasonData>($"{Data}/Seasons/Season_Autumn.asset", s =>
        {
            s.seasonId = "autumn"; s.displayName = "Autumn"; s.lengthInDays = 6; s.uiColor = new Color(0.95f, 0.55f, 0.2f);
            s.temperatureCurve = AnimationCurve.Linear(0f, 16f, 1f, 6f); s.temperatureVariance = 3f;
            s.precipitationCurve = AnimationCurve.Linear(0f, 0.5f, 1f, 0.6f); s.precipitationVariance = 0.2f;
            s.dayLengthMultiplier = 0.75f;
            s.disasterChanceBonus = 0.05f; s.cozyYearStart01 = 0.5f; s.cozyYearSpan01 = 0.2f; s.initialSnow01 = 0f;
        });
        SeasonData winter = A<SeasonData>($"{Data}/Seasons/Season_Winter.asset", s =>
        {
            s.seasonId = "winter"; s.displayName = "Winter"; s.lengthInDays = 6; s.uiColor = new Color(0.55f, 0.75f, 1f);
            s.temperatureCurve = AnimationCurve.Linear(0f, 2f, 1f, -4f); s.temperatureVariance = 3f;
            s.precipitationCurve = AnimationCurve.Linear(0f, 0.4f, 1f, 0.5f); s.precipitationVariance = 0.2f;
            s.dayLengthMultiplier = 0.6f;
            s.disasterChanceBonus = 0.1f; s.cozyYearStart01 = 0.75f; s.cozyYearSpan01 = 0.2f; s.initialSnow01 = 0.6f;
        });

        CalendarConfig calendar = A<CalendarConfig>($"{Data}/Climate/CalendarConfig.asset", c =>
        {
            c.seasons = new List<SeasonData> { spring, summer, autumn, winter };
        });

        ClimateConfig climate = A<ClimateConfig>($"{Data}/Climate/ClimateConfig.asset", c =>
        {
            c.minGrowth = 0.12f; c.maxGrowth = 1.6f; c.fallbackKind = WeatherKind.Clear;
            c.seasonPenaltyExponent = 2f; c.offSeasonDayFraction = 0.83f; c.minQuality = 0.25f; c.maxQuality = 1.25f;
            c.rules = new List<ClimateConfig.WeatherKindRule>
            {
                Rule(WeatherKind.Snow, -100f, 1f, 0.3f, 1f),
                Rule(WeatherKind.Heat, 30f, 100f, 0f, 1f),
                Rule(WeatherKind.Storm, -100f, 100f, 0.8f, 1f),
                Rule(WeatherKind.Rain, -100f, 100f, 0.55f, 1f),
                Rule(WeatherKind.Cloudy, -100f, 100f, 0.4f, 1f),
            };
        });

        // ---- Crops (existing + new) and seeds ----
        CropData tomato = AssetDatabase.LoadAssetAtPath<CropData>($"{Data}/Crop_Tomato.asset");
        CropData turnip = AssetDatabase.LoadAssetAtPath<CropData>($"{Data}/Crop_Turnip.asset");
        if (tomato == null || turnip == null)
        {
            Debug.LogError("[RunSystemsBuilder] Crop_Tomato / Crop_Turnip missing. Run MoFarmSand/Build Golden Slice first.");
            return null;
        }
        GameObject projectile = tomato.projectilePrefab;

        CropData pumpkin = A<CropData>($"{Data}/Crops/Crop_Pumpkin.asset", c =>
        {
            c.cropId = "pumpkin"; c.displayName = "Pumpkin"; c.type = CropType.Projectile; c.growTime = 15f;
            c.fullnessValue = 35f; c.harvestYield = 1; c.projectilePrefab = projectile; c.color = new Color(0.95f, 0.5f, 0.1f);
        });
        // ---- New roster: shooters (peas, corn), shields (lettuce, spinach) and the garlic scare-thrower ----
        GameObject peaBullet = BulletPrefab("Pea_Bullet", projectile, new Color(0.45f, 0.85f, 0.3f), 0.22f, null);
        GameObject cornBullet = BulletPrefab("Corn_Bullet", projectile, new Color(1f, 0.85f, 0.2f), 0.26f, null);
        Material puffMat = Mat("GarlicPuffFX", new Color(0.85f, 1f, 0.8f), "Universal Render Pipeline/Particles/Unlit");
        GameObject garlicPuff = Fx("FX_GarlicPuff", puffMat, new Color(0.85f, 1f, 0.75f, 0.85f), loop: false, rate: 0f, life: 0.9f, size: 0.3f, speed: 1.6f, gravity: -0.15f, shape: ParticleSystemShapeType.Sphere, shapeScale: Vector3.one * 0.3f, offset: Vector3.zero, burst: 30);
        GameObject garlicProj = BulletPrefab("Garlic_Projectile", projectile, new Color(0.96f, 0.95f, 0.85f), 0.38f, garlicPuff);

        CropData peas = A<CropData>($"{Data}/Crops/Crop_Peas.asset", c =>
        {
            c.cropId = "peas"; c.displayName = "Peas"; c.type = CropType.Shooter; c.growTime = 12f;
            c.fullnessValue = 2.5f; c.harvestYield = 1; c.ammoPerCrop = 10; c.fireInterval = 0.15f; c.bulletSpeed = 22f;
            c.projectilePrefab = peaBullet; c.color = new Color(0.45f, 0.85f, 0.3f);
        });
        CropData lettuce = A<CropData>($"{Data}/Crops/Crop_Lettuce.asset", c =>
        {
            c.cropId = "lettuce"; c.displayName = "Lettuce"; c.type = CropType.Support; c.growTime = 12f;
            c.shieldCharges = 2; c.harvestYield = 2; c.color = new Color(0.6f, 0.9f, 0.45f);
        });
        CropData corn = A<CropData>($"{Data}/Crops/Crop_Corn.asset", c =>
        {
            c.cropId = "corn"; c.displayName = "Corn"; c.type = CropType.Shooter; c.growTime = 15f;
            c.fullnessValue = 3.5f; c.harvestYield = 1; c.ammoPerCrop = 10; c.fireInterval = 0.15f; c.bulletSpeed = 22f;
            c.projectilePrefab = cornBullet; c.color = new Color(1f, 0.85f, 0.2f);
        });
        CropData garlic = A<CropData>($"{Data}/Crops/Crop_Garlic.asset", c =>
        {
            c.cropId = "garlic"; c.displayName = "Garlic"; c.type = CropType.Projectile; c.growTime = 13f;
            c.fullnessValue = 15f; c.harvestYield = 2; c.scareSeconds = 3f; c.projectilePrefab = garlicProj; c.color = new Color(0.96f, 0.95f, 0.85f);
        });
        CropData spinach = A<CropData>($"{Data}/Crops/Crop_Spinach.asset", c =>
        {
            c.cropId = "spinach"; c.displayName = "Spinach"; c.type = CropType.Support; c.growTime = 12f;
            c.shieldCharges = 3; c.harvestYield = 2; c.color = new Color(0.2f, 0.55f, 0.3f);
        });

        // Affinities below 1 are raised to ClimateConfig.seasonPenaltyExponent, so wrong-season crops are slow AND weak.
        // Two crops per season: spring peas+lettuce, summer tomato+corn, autumn turnip+pumpkin, winter garlic+spinach.
        SetClimate(peas, 14f, 12f, 0.5f, 0.6f, (spring, 1.5f), (summer, 0.8f), (autumn, 0.7f), (winter, 0.4f));
        SetClimate(lettuce, 13f, 12f, 0.5f, 0.6f, (spring, 1.5f), (summer, 0.6f), (autumn, 0.9f), (winter, 0.5f));
        SetClimate(tomato, 24f, 14f, 0.4f, 0.6f, (spring, 0.8f), (summer, 1.3f), (autumn, 0.7f), (winter, 0.4f));
        SetClimate(corn, 26f, 12f, 0.35f, 0.6f, (spring, 0.6f), (summer, 1.6f), (autumn, 0.8f), (winter, 0.2f));
        SetClimate(turnip, 12f, 12f, 0.45f, 0.6f, (spring, 0.8f), (summer, 0.5f), (autumn, 1.4f), (winter, 0.8f));
        SetClimate(pumpkin, 18f, 12f, 0.4f, 0.6f, (spring, 0.7f), (summer, 0.9f), (autumn, 1.5f), (winter, 0.3f));
        SetClimate(garlic, 4f, 12f, 0.4f, 0.6f, (spring, 0.7f), (summer, 0.3f), (autumn, 0.9f), (winter, 1.6f));
        SetClimate(spinach, 5f, 12f, 0.45f, 0.6f, (spring, 0.8f), (summer, 0.3f), (autumn, 1.0f), (winter, 1.6f));

        SeedData seedPeas = Seed("peas", peas);
        SeedData seedLettuce = Seed("lettuce", lettuce);
        SeedData seedTomato = Seed("tomato", tomato);
        SeedData seedCorn = Seed("corn", corn);
        SeedData seedTurnip = Seed("turnip", turnip);
        SeedData seedPumpkin = Seed("pumpkin", pumpkin);
        SeedData seedGarlic = Seed("garlic", garlic);
        SeedData seedSpinach = Seed("spinach", spinach);

        ItemRegistry items = Registry<ItemRegistry, ItemData>($"{Data}/Registries/ItemRegistry.asset",
            peas, lettuce, tomato, corn, turnip, pumpkin, garlic, spinach,
            seedPeas, seedLettuce, seedTomato, seedCorn, seedTurnip, seedPumpkin, seedGarlic, seedSpinach);

        // ---- Bosses ----
        BossData caterpillar = AssetDatabase.LoadAssetAtPath<BossData>($"{Data}/Boss_Caterpillar.asset");
        BossData elite = A<BossData>($"{Data}/Boss_Caterpillar_Elite.asset", b =>
        {
            b.displayName = "Elder Caterpillar"; b.maxFullness = 60f; b.chaseSpeed = 3.2f; b.damage = 1; b.nextStage = null;
        });

        // ---- FX prefabs ----
        Material fxMat = Mat("RunFX", Color.white, "Universal Render Pipeline/Particles/Unlit");
        GameObject fxRain = Fx("FX_Rain", fxMat, new Color(0.6f, 0.75f, 1f, 0.8f), loop: true, rate: 220f, life: 1.1f, size: 0.05f, speed: 0f, gravity: 6f, shape: ParticleSystemShapeType.Box, shapeScale: new Vector3(24f, 0.1f, 24f), offset: new Vector3(0f, 10f, 4f), shards: false);
        GameObject fxSnow = Fx("FX_Snow", fxMat, new Color(1f, 1f, 1f, 0.9f), loop: true, rate: 120f, life: 6f, size: 0.12f, speed: 0f, gravity: 0.25f, shape: ParticleSystemShapeType.Box, shapeScale: new Vector3(24f, 0.1f, 24f), offset: new Vector3(0f, 10f, 4f));
        GameObject[] autumnLeafPrefabs = AutumnLeafPrefabs();
        GameObject fxAutumnLeaves = AutumnLeavesFx(autumnLeafPrefabs);
        GameObject fxSeasonSnow = SeasonSnowFx(fxMat);
        Material snowMoundMat = Mat("SnowMound", new Color(0.95f, 0.97f, 1f));
        GameObject fxHeat = Fx("FX_Heat", fxMat, new Color(1f, 0.7f, 0.3f, 0.35f), loop: true, rate: 30f, life: 4f, size: 0.25f, speed: 0.4f, gravity: -0.05f, shape: ParticleSystemShapeType.Box, shapeScale: new Vector3(18f, 0.1f, 18f), offset: new Vector3(0f, -4f, 6f));
        GameObject fxLocusts = Fx("FX_Locusts", fxMat, new Color(0.45f, 0.35f, 0.15f, 1f), loop: true, rate: 90f, life: 3f, size: 0.09f, speed: 3f, gravity: 0f, shape: ParticleSystemShapeType.Box, shapeScale: new Vector3(16f, 3f, 16f), offset: new Vector3(0f, 1f, 6f));
        GameObject fxSpores = Fx("FX_Spores", fxMat, new Color(0.65f, 0.3f, 0.75f, 0.7f), loop: true, rate: 40f, life: 5f, size: 0.14f, speed: 0.3f, gravity: -0.08f, shape: ParticleSystemShapeType.Box, shapeScale: new Vector3(18f, 0.1f, 18f), offset: new Vector3(0f, -4f, 6f));
        GameObject fxWither = Fx("FX_WitherPuff", fxMat, new Color(0.3f, 0.2f, 0.15f, 1f), loop: false, rate: 0f, life: 0.9f, size: 0.25f, speed: 2f, gravity: -0.2f, shape: ParticleSystemShapeType.Sphere, shapeScale: Vector3.one * 0.3f, offset: Vector3.zero, burst: 24);
        GameObject fxTelegraph = Fx("FX_StrikeTelegraph", fxMat, new Color(1f, 0.25f, 0.2f, 1f), loop: true, rate: 80f, life: 0.4f, size: 0.18f, speed: 0f, gravity: 0f, shape: ParticleSystemShapeType.Circle, shapeScale: Vector3.one * 1.6f, offset: new Vector3(0f, 0.1f, 0f), circleEdge: true);
        GameObject fxStrike = Fx("FX_StrikeBolt", fxMat, new Color(1f, 0.95f, 0.5f, 1f), loop: false, rate: 0f, life: 0.5f, size: 0.3f, speed: 7f, gravity: 1f, shape: ParticleSystemShapeType.Sphere, shapeScale: Vector3.one * 0.3f, offset: new Vector3(0f, 0.3f, 0f), burst: 45);
        GameObject fxFlood = Fx("FX_Flood", fxMat, new Color(0.35f, 0.55f, 0.95f, 0.8f), loop: true, rate: 35f, life: 1f, size: 0.14f, speed: 0.8f, gravity: 0.6f, shape: ParticleSystemShapeType.Circle, shapeScale: Vector3.one * 0.7f, offset: new Vector3(0f, 0.2f, 0f));

        // ---- Disaster effects ----
        GrowthMultiplierEffect gDrought = A<GrowthMultiplierEffect>($"{Data}/Disasters/Effects/Effect_Growth_Drought.asset", e => e.multiplierAtFullSeverity = 0.5f);
        GrowthMultiplierEffect gHeat = A<GrowthMultiplierEffect>($"{Data}/Disasters/Effects/Effect_Growth_Heat.asset", e => e.multiplierAtFullSeverity = 0.7f);
        GrowthMultiplierEffect gFrost = A<GrowthMultiplierEffect>($"{Data}/Disasters/Effects/Effect_Growth_Frost.asset", e => e.multiplierAtFullSeverity = 0.35f);
        WitherPlotsEffect wLocust = A<WitherPlotsEffect>($"{Data}/Disasters/Effects/Effect_Wither_Locust.asset", e => { e.firstDelay = 4f; e.interval = 5f; e.maxPerTick = 2; e.fxPrefab = fxWither; });
        WitherPlotsEffect wBlight = A<WitherPlotsEffect>($"{Data}/Disasters/Effects/Effect_Wither_Blight.asset", e => { e.firstDelay = 6f; e.interval = 8f; e.maxPerTick = 1; e.fxPrefab = fxWither; });
        StrikeHazardEffect strike = A<StrikeHazardEffect>($"{Data}/Disasters/Effects/Effect_Strike_Storm.asset", e => { e.telegraphFx = fxTelegraph; e.strikeFx = fxStrike; });
        BlockPlotsEffect flood = A<BlockPlotsEffect>($"{Data}/Disasters/Effects/Effect_Block_Flood.asset", e => { e.fractionAtFullSeverity = 0.4f; e.seconds = 25f; e.fxPrefab = fxFlood; });
        PriceShockEffect pDrought = A<PriceShockEffect>($"{Data}/Disasters/Effects/Effect_PriceShock_Drought.asset", e => { e.sellMultiplier = 1.4f; e.seedMultiplier = 1.2f; });
        PriceShockEffect pLocust = A<PriceShockEffect>($"{Data}/Disasters/Effects/Effect_PriceShock_Locust.asset", e => { e.sellMultiplier = 1.5f; e.seedMultiplier = 1.3f; });
        PlayerSlowEffect slow = A<PlayerSlowEffect>($"{Data}/Disasters/Effects/Effect_Slow_Heat.asset", e => e.speedAtFullSeverity = 0.75f);

        DisasterData drought = Disaster("drought", "Drought", "The soil cracks. Crops grow slowly.", new Color(0.85f, 0.6f, 0.25f), false, 3,
            new[] { (summer, 3f) }, gDrought, pDrought);
        DisasterData heatwave = Disaster("heatwave", "Heatwave", "Scorching heat slows everyone down.", new Color(1f, 0.45f, 0.2f), false, 3,
            new[] { (summer, 2f) }, gHeat, slow);
        DisasterData storm = Disaster("storm", "Storm", "Lightning strikes! Watch the red rings.", new Color(0.4f, 0.45f, 0.85f), true, 0,
            new[] { (spring, 3f), (summer, 1f), (autumn, 3f) }, strike, flood);
        DisasterData frost = Disaster("frost", "Frost", "A killing chill. Crops barely grow.", new Color(0.6f, 0.85f, 1f), false, 0,
            new[] { (spring, 0.5f), (autumn, 2f), (winter, 3f) }, gFrost);
        DisasterData locust = Disaster("locust", "Locust Swarm", "Hungry swarms devour your crops!", new Color(0.55f, 0.45f, 0.15f), false, 2,
            new[] { (summer, 2f), (autumn, 2f) }, wLocust, pLocust);
        DisasterData blight = Disaster("blight", "Blight", "A creeping rot withers the fields.", new Color(0.6f, 0.3f, 0.7f), false, 0,
            new[] { (spring, 2f), (summer, 1f), (autumn, 2f) }, wBlight);
        DisasterRegistry disasters = Registry<DisasterRegistry, DisasterData>($"{Data}/Registries/DisasterRegistry.asset",
            drought, heatwave, storm, frost, locust, blight);

        // ---- Run effects, events, modifiers, unlocks ----
        HealEffect healFull = A<HealEffect>($"{Data}/Run/Effects/Effect_HealFull.asset", e => e.amount = 99);
        MaxHpEffect maxHp1 = A<MaxHpEffect>($"{Data}/Run/Effects/Effect_MaxHp1.asset", e => e.delta = 1);
        DamageEffect damage1 = A<DamageEffect>($"{Data}/Run/Effects/Effect_Damage1.asset", e => e.amount = 1);
        AddCoinsEffect coins25 = A<AddCoinsEffect>($"{Data}/Run/Effects/Effect_Coins25.asset", e => e.amount = 25);
        AddCoinsEffect coins10 = A<AddCoinsEffect>($"{Data}/Run/Effects/Effect_Coins10.asset", e => e.amount = 10);
        AddItemEffect turnipSeeds3 =A<AddItemEffect>($"{Data}/Run/Effects/Effect_TurnipSeeds3.asset", e => { e.item = seedTurnip; e.count = 3; });
        AddItemEffect pumpkinSeeds3 = A<AddItemEffect>($"{Data}/Run/Effects/Effect_PumpkinSeeds3.asset", e => { e.item = seedPumpkin; e.count = 3; });
        AddItemEffect pumpkinSeeds4 = A<AddItemEffect>($"{Data}/Run/Effects/Effect_PumpkinSeeds4.asset", e => { e.item = seedPumpkin; e.count = 4; });
        AddCoinsEffect nestEgg = A<AddCoinsEffect>($"{Data}/Run/Effects/Effect_NestEgg25.asset", e => e.amount = 25);
        // Penalties and extra rewards for the bad and seasonal events below.
        LoseItemsEffect loseItems2 = A<LoseItemsEffect>($"{Data}/Run/Effects/Effect_LoseItems2.asset", e => { e.kind = LoseItemsEffect.Kind.SeedsOrCrops; e.count = 2; });
        LoseItemsEffect loseSeeds3 = A<LoseItemsEffect>($"{Data}/Run/Effects/Effect_LoseSeeds3.asset", e => { e.kind = LoseItemsEffect.Kind.SeedsOnly; e.count = 3; });
        LoseItemsEffect loseCrops3 = A<LoseItemsEffect>($"{Data}/Run/Effects/Effect_LoseCrops3.asset", e => { e.kind = LoseItemsEffect.Kind.CropsOnly; e.count = 3; });
        AddCoinsEffect loseCoins8 = A<AddCoinsEffect>($"{Data}/Run/Effects/Effect_LoseCoins8.asset", e => e.amount = -8);
        AddCoinsEffect loseCoins5 = A<AddCoinsEffect>($"{Data}/Run/Effects/Effect_LoseCoins5.asset", e => e.amount = -5);
        MaxHpEffect maxHpMinus1 = A<MaxHpEffect>($"{Data}/Run/Effects/Effect_MaxHpMinus1.asset", e => e.delta = -1);
        AddItemEffect peasSeeds2 = A<AddItemEffect>($"{Data}/Run/Effects/Effect_PeasSeeds2.asset", e => { e.item = seedPeas; e.count = 2; });
        AddItemEffect cornSeeds2 = A<AddItemEffect>($"{Data}/Run/Effects/Effect_CornSeeds2.asset", e => { e.item = seedCorn; e.count = 2; });
        AddItemEffect tomatoSeeds1 = A<AddItemEffect>($"{Data}/Run/Effects/Effect_TomatoSeeds1.asset", e => { e.item = seedTomato; e.count = 1; });
        AddItemEffect spinachSeeds2 = A<AddItemEffect>($"{Data}/Run/Effects/Effect_SpinachSeeds2.asset", e => { e.item = seedSpinach; e.count = 2; });
        AddItemEffect pumpkinSeeds1 = A<AddItemEffect>($"{Data}/Run/Effects/Effect_PumpkinSeeds1.asset", e => { e.item = seedPumpkin; e.count = 1; });
        AddCoinsEffect coins15 = A<AddCoinsEffect>($"{Data}/Run/Effects/Effect_Coins15.asset", e => e.amount = 15);

        EventData evPup = A<EventData>($"{Data}/Events/Event_StrayPup.asset", e =>
        {
            e.eventId = "stray_pup"; e.title = "A Stray Pup";
            e.body = "A scruffy pup watches you from the fence, tail wagging. It looks hungry.";
            e.options = new List<EventData.EventOption>
            {
                new EventData.EventOption { label = "Share your lunch", coinCost = 5, effects = new List<RunEffect> { maxHp1 } },
                new EventData.EventOption { label = "Pet it and move on", coinCost = 0, effects = new List<RunEffect>() },
            };
        });
        EventData evWell = A<EventData>($"{Data}/Events/Event_MossyWell.asset", e =>
        {
            e.eventId = "mossy_well"; e.title = "The Mossy Well";
            e.body = "An old well hums softly. The water smells of mint and starlight.";
            e.options = new List<EventData.EventOption>
            {
                new EventData.EventOption { label = "Drink deeply", coinCost = 0, effects = new List<RunEffect> { healFull } },
                new EventData.EventOption { label = "Toss in a coin and wish", coinCost = 3, effects = new List<RunEffect> { turnipSeeds3 } },
            };
        });
        EventData evCrow = A<EventData>($"{Data}/Events/Event_GreedyCrow.asset", e =>
        {
            e.eventId = "greedy_crow"; e.title = "The Greedy Crow";
            e.body = "A crow clutches a pouch of fat pumpkin seeds. \"Shiny coins. Fair trade, yes?\"";
            e.options = new List<EventData.EventOption>
            {
                new EventData.EventOption { label = "Trade 10 coins for seeds", coinCost = 10, effects = new List<RunEffect> { pumpkinSeeds4 } },
                new EventData.EventOption { label = "Reach into the hollow stump", coinCost = 0, effects = new List<RunEffect> { damage1, coins25 } },
                new EventData.EventOption { label = "Shoo the crow away", coinCost = 0, effects = new List<RunEffect>() },
            };
        });
        // The old Treasure map node is folded into the random events: a chest on the ground with one button.
        EventData evTreasure = A<EventData>($"{Data}/Events/Event_TreasureChest.asset", e =>
        {
            e.eventId = "treasure_chest"; e.title = "A Treasure Chest";
            e.body = "You found a treasure chest lying on the ground. It is full of shiny coins.";
            e.options = new List<EventData.EventOption>
            {
                new EventData.EventOption { label = "Take 10 coins", coinCost = 0, effects = new List<RunEffect> { coins10 }, hideEffects = true },
            };
        });

        // ---- Bad events: every choice costs something ----
        EventData evCat = Event("thieving_cat", "Event_ThievingCat", "The Thieving Cat",
            "A sleek black cat slinks off your porch with a sack of your things in its jaws.",
            Opt("Tussle with the cat for your crops", 0, damage1),
            Opt("Let the cat go", 0, loseItems2));
        EventData evGoose = Event("toll_goose", "Event_TollGoose", "The Toll Goose",
            "A huge goose plants itself in your farm gate and hisses. It clearly wants payment.",
            Opt("Pay the goose's toll", 0, loseCoins8),
            Opt("Shove past the goose", 0, damage1));
        EventData evMoles = Event("seed_moles", "Event_SeedMoles", "Moles in the Seed Shed",
            "Fresh mounds dot the floor of the seed shed. Something down there is chewing.",
            Opt("Dig them out by paw", 0, damage1),
            Opt("Leave them be", 0, loseSeeds3));
        EventData evHex = Event("scarecrow_hex", "Event_ScarecrowHex", "The Scarecrow's Hex",
            "The old scarecrow turns its sack head toward you. \"Feed me, or wear my curse.\"",
            Opt("Accept the hex", 0, maxHpMinus1),
            Opt("Offer it your harvest", 0, loseCrops3));

        // ---- Seasonal events: only appear in their season, good and bad choices mixed ----
        EventData evBunnies = Event("spring_bunnies", "Event_SpringBunnies", "The Bunny Burrow",
            "A family of bunnies has dug in under your lettuce rows. Tiny noses twitch at you.",
            new[] { spring },
            Opt("Let them nibble", 0, loseCrops3, maxHp1),
            Opt("Chase them off", 0, damage1, peasSeeds2));
        EventData evFair = Event("spring_fair", "Event_SpringFair", "The Spring Fair",
            "The village fair is in full bloom. A pie contest and a seed stall are calling your name.",
            new[] { spring },
            Opt("Enter the pie-eating contest", 0, damage1, coins15),
            Opt("Buy a bundle of pea seeds", 6, peasSeeds2),
            Opt("Head home", 0));
        EventData evLemonade = Event("summer_lemonade", "Event_SummerLemonade", "The Lemonade Frog",
            "A frog runs a lemonade stand in the shade. \"Ice cold, three coins!\"",
            new[] { summer },
            Opt("Buy a cold glass", 3, healFull),
            Opt("Swipe a jug and run", 0, damage1, coins10));
        EventData evMaze = Event("summer_maze", "Event_SummerMaze", "The Corn Maze",
            "Someone cut a maze into the cornfield. A golden ribbon flutters at its heart.",
            new[] { summer },
            Opt("Race to the centre", 0, damage1, coins15),
            Opt("Stroll the edge", 0, tomatoSeeds1, cornSeeds2));
        EventData evFeast = Event("autumn_feast", "Event_AutumnFeast", "The Harvest Moon Feast",
            "Your neighbours set a long table under the orange moon. Everyone brings a dish.",
            new[] { autumn },
            Opt("Bring a dish", 0, loseCrops3, maxHp1),
            Opt("Just grab a plate", 0, loseCoins5, healFull));
        EventData evLeaves = Event("autumn_leaves", "Event_AutumnLeaves", "The Leaf Pile",
            "A huge pile of crunchy leaves sits by the barn. Something glints inside.",
            new[] { autumn },
            Opt("Dive right in", 0, damage1, coins15),
            Opt("Rake it up slowly", 0, pumpkinSeeds1));
        EventData evPond = Event("winter_pond", "Event_WinterPond", "The Frozen Pond",
            "The pond has iced over. A coin pouch is stuck just under the surface.",
            new[] { winter },
            Opt("Chip it free", 0, damage1, coins15),
            Opt("Skate a few laps", 0, healFull));
        EventData evBadger = Event("winter_badger", "Event_WinterBadger", "The Snowed-In Badger",
            "A shivering badger knocks, asking to wait out the snow by your stove.",
            new[] { winter },
            Opt("Share your stores", 0, loseCrops3, maxHp1),
            Opt("Trade for its seeds", 5, spinachSeeds2),
            Opt("Point it to the inn", 0));

        EventRegistry events = Registry<EventRegistry, EventData>($"{Data}/Registries/EventRegistry.asset", evPup, evWell, evCrow, evTreasure,
            evCat, evGoose, evMoles, evHex, evBunnies, evFair, evLemonade, evMaze, evFeast, evLeaves, evPond, evBadger);

        RunModifierData scarecrow = Modifier("scarecrow", "Scarecrow", "Halves the severity of Locust Swarms.", 25, new[] { "locust" }, 0.5f, 1f, 0);
        RunModifierData rainBarrel = Modifier("rain_barrel", "Rain Barrel", "Halves the severity of Drought and Heatwave.", 25, new[] { "drought", "heatwave" }, 0.5f, 1f, 0);
        RunModifierData stormCellar = Modifier("storm_cellar", "Storm Cellar", "Halves the severity of Storms.", 25, new[] { "storm" }, 0.5f, 1f, 0);
        RunModifierData frostBlanket = Modifier("frost_blanket", "Frost Blanket", "Cuts Frost severity by 60%.", 25, new[] { "frost" }, 0.6f, 1f, 0);
        RunModifierData fertilizer = Modifier("fertilizer", "Fertilizer", "All crops grow 20% faster.", 35, new string[0], 0f, 1.2f, 0);
        RunModifierData luckyCollar = Modifier("lucky_collar", "Lucky Collar", "+1 max HP for the rest of the run.", 40, new string[0], 0f, 1f, 1);
        // Only comes from the Lucky Sprout permanent unlock, never from the merchant.
        RunModifierData seedFinder = A<RunModifierData>($"{Data}/Run/Modifier_seed_finder.asset", m =>
        {
            m.modifierId = "seed_finder"; m.displayName = "Lucky Sprout";
            m.description = "Each harvest has a 12% chance to also drop a random seed.";
            m.price = 0; m.harvestSeedChance = 0.12f; m.shopItem = false;
        });
        RunModifierRegistry modifiers = Registry<RunModifierRegistry, RunModifierData>($"{Data}/Registries/RunModifierRegistry.asset",
            scarecrow, rainBarrel, stormCellar, frostBlanket, fertilizer, luckyCollar, seedFinder);
        AddModifierEffect giveSeedFinder = A<AddModifierEffect>($"{Data}/Run/Effects/Effect_AddModifier_SeedFinder.asset", e => e.modifier = seedFinder);

        UnlockData uHardy = Unlock("hardy_paws", "Hardy Paws", "Start every run with +1 max HP.", 40, maxHp1);
        UnlockData uPouch = Unlock("seed_pouch", "Seed Pouch", "Start every run with 3 extra Turnip seeds.", 30, turnipSeeds3);
        UnlockData uEgg = Unlock("nest_egg", "Nest Egg", "Start every run with 25 extra coins.", 50, nestEgg);
        UnlockData uPumpkin = Unlock("pumpkin_patch", "Pumpkin Patch", "Start every run with 3 Pumpkin seeds.", 60, pumpkinSeeds3);
        UnlockData uSprout = Unlock("lucky_sprout", "Lucky Sprout", "Each harvest has a 12% chance to also drop a random seed.", 45, giveSeedFinder);
        UnlockRegistry unlocks = Registry<UnlockRegistry, UnlockData>($"{Data}/Registries/UnlockRegistry.asset",
            uHardy, uPouch, uEgg, uPumpkin, uSprout);

        // ---- Node types ----
        NodeTypeData nFarm = NodeType("farm", "Farm Day", "F", new Color(0.45f, 0.8f, 0.4f), "A quiet day of planting and harvesting.", n =>
        { n.disasterChance = 0.15f; n.coinReward = new Vector2Int(5, 10); });
        NodeTypeData nMerchant = NodeType("merchant", "Merchant", "M", new Color(0.95f, 0.75f, 0.25f), "Sell crops, buy seeds and charms.", n =>
        { n.merchantActive = true; n.disasterChance = 0.1f; n.coinReward = new Vector2Int(0, 0); });
        NodeTypeData nDisaster = NodeType("disaster", "Disaster", "!", new Color(0.9f, 0.3f, 0.3f), "Hazardous weather is certain. Big reward if you survive.", n =>
        { n.disasterChance = 1f; n.coinReward = new Vector2Int(15, 25); n.minLayer = 2; });
        // Elder Caterpillar (elite) and Rest Stop stay registered so old saves still validate, but the generator no longer places them (see the bands below).
        NodeTypeData nElite = NodeType("elite", "Elder Caterpillar", "E", new Color(0.7f, 0.35f, 0.85f), "A tougher night fight with a bigger reward.", n =>
        { n.encounter = elite; n.disasterChance = 0.1f; n.coinReward = new Vector2Int(25, 40); n.minLayer = 4; n.maxPerPath = 2; n.noConsecutive = true; });
        NodeTypeData nRest = NodeType("rest", "Rest Stop", "R", new Color(0.4f, 0.75f, 0.95f), "Heal fully. Crops grow faster today.", n =>
        { n.healOnStart = true; n.growthBonus = 1.4f; n.disasterChance = 0f; n.coinReward = new Vector2Int(3, 5); });
        NodeTypeData nEvent = NodeType("event", "Mystery", "?", new Color(0.85f, 0.85f, 0.95f), "Something unusual is happening on the farm.", n =>
        { n.eventPool = events; n.disasterChance = 0.1f; n.coinReward = new Vector2Int(0, 0); n.noConsecutive = true; });
        NodeTypeData nTreasure = NodeType("treasure", "Treasure", "$", new Color(1f, 0.9f, 0.3f), "A buried stash of coins.", n =>
        { n.disasterChance = 0.05f; n.coinReward = new Vector2Int(20, 35); n.minLayer = 3; n.maxPerPath = 1; });
        NodeTypeData nBoss = NodeType("boss", "The Caterpillar", "B", new Color(0.95f, 0.4f, 0.55f), "The final night. Feed the caterpillar until it bursts.", n =>
        { n.encounter = caterpillar; n.startsAtNight = true; n.disasterChance = 0f; n.coinReward = new Vector2Int(0, 0); n.endsRun = true; n.farmingAllowed = false; });
        NodeTypeData nSeasonBoss = NodeType("season_boss", "Hungry Caterpillar", "B", new Color(0.85f, 0.35f, 0.7f), "The season's mandatory night. Feed the caterpillar before dawn.", n =>
        { n.encounter = elite; n.startsAtNight = true; n.disasterChance = 0f; n.coinReward = new Vector2Int(20, 35); n.farmingAllowed = false; n.survivableLoss = false; n.lossGrowthMultiplier = 0.65f; });
        NodeTypeRegistry nodeTypes = Registry<NodeTypeRegistry, NodeTypeData>($"{Data}/Registries/NodeTypeRegistry.asset",
            nFarm, nMerchant, nDisaster, nElite, nRest, nEvent, nTreasure, nBoss, nSeasonBoss);

        MapGenConfig mapGen = A<MapGenConfig>($"{Data}/Map/MapGenConfig.asset", m =>
        {
            m.lanes = 7; m.pathCount = 6; m.maxAssignAttempts = 8;
            m.fallbackType = nFarm; m.bossType = nBoss; m.seasonBossType = nSeasonBoss;
            m.bands = new List<MapGenConfig.LayerBand>
            {
                Band(0, 1, (nFarm, 5f), (nMerchant, 3f), (nEvent, 2f)),
                // No Treasure nodes any more: treasure is one of the random events (Event_TreasureChest).
                Band(2, 5, (nFarm, 4f), (nMerchant, 2f), (nDisaster, 2f), (nEvent, 4f)),
                Band(6, 10, (nFarm, 3f), (nMerchant, 2f), (nDisaster, 3f), (nEvent, 4f)),
                Band(11, 99, (nFarm, 3f), (nMerchant, 2f), (nDisaster, 2f), (nEvent, 2f)),
            };
            m.forcedLayers = new List<MapGenConfig.ForcedLayer>
            {
                new MapGenConfig.ForcedLayer { fromEnd = false, index = 0, type = nFarm },
                new MapGenConfig.ForcedLayer { fromEnd = true, index = 0, type = nFarm },
            };
        });

        // ---- Economy ----
        EconomyConfig economy = A<EconomyConfig>($"{Data}/Economy/EconomyConfig.asset", e => { });
        BankConfig bank = A<BankConfig>($"{Data}/Economy/BankConfig.asset", b => { });
        PriceTable prices = A<PriceTable>($"{Data}/Economy/PriceTable.asset", p => p.rows = new List<PriceTable.PriceRow>());

        // Rows are ensured one by one so an existing table gains the new crops (designer edits to other rows are untouched).
        // Each crop is stocked in its own season and the one before it.
        EnsureRow(prices, Row(peas, seedPeas, 4, 6, new[] { (spring, 1f), (summer, 0.9f), (autumn, 1f), (winter, 1.2f) }, winter, spring));
        EnsureRow(prices, Row(lettuce, seedLettuce, 3, 5, new[] { (spring, 1f), (summer, 0.9f), (autumn, 1.1f), (winter, 1.2f) }, winter, spring));
        EnsureRow(prices, Row(tomato, seedTomato, 4, 6, new[] { (spring, 1f), (summer, 1.1f), (autumn, 0.9f), (winter, 1.3f) }, spring, summer));
        EnsureRow(prices, Row(corn, seedCorn, 5, 8, new[] { (spring, 1f), (summer, 1f), (autumn, 1.1f), (winter, 1.3f) }, spring, summer));
        EnsureRow(prices, Row(turnip, seedTurnip, 3, 5, new[] { (spring, 1f), (summer, 0.9f), (autumn, 1.1f), (winter, 1.2f) }, summer, autumn));
        EnsureRow(prices, Row(pumpkin, seedPumpkin, 6, 11, new[] { (spring, 1f), (summer, 1f), (autumn, 1.2f), (winter, 1.4f) }, summer, autumn));
        EnsureRow(prices, Row(garlic, seedGarlic, 5, 8, new[] { (spring, 1f), (summer, 0.9f), (autumn, 1.1f), (winter, 1.3f) }, autumn, winter));
        EnsureRow(prices, Row(spinach, seedSpinach, 4, 6, new[] { (spring, 1f), (summer, 0.8f), (autumn, 1.1f), (winter, 1.3f) }, autumn, winter));

        RunConfig run = A<RunConfig>($"{Data}/Run/RunConfig.asset", r =>
        {
            r.startingCoins = 5; r.startingMaxHp = 3; r.dayEndDelay = 2.5f; r.dayLengthSeconds = 50f; r.nightLengthSeconds = 75f; r.startingSeedPicks = new Vector2Int(2, 3);
            r.startingSeedPool = new List<RunConfig.SeedPoolEntry>();
        });
        EnsurePoolEntry(run, seedPeas, 2.5f, new Vector2Int(2, 4));
        EnsurePoolEntry(run, seedLettuce, 2.5f, new Vector2Int(2, 4));
        EnsurePoolEntry(run, seedTomato, 3f, new Vector2Int(2, 4));
        EnsurePoolEntry(run, seedCorn, 2f, new Vector2Int(2, 3));
        EnsurePoolEntry(run, seedTurnip, 3f, new Vector2Int(2, 4));
        EnsurePoolEntry(run, seedPumpkin, 1.5f, new Vector2Int(1, 2));
        EnsurePoolEntry(run, seedGarlic, 1.5f, new Vector2Int(1, 2));
        EnsurePoolEntry(run, seedSpinach, 1.5f, new Vector2Int(1, 2));

        // ---- Presentation: COZY mapping ----
        CozyWeatherMap cozy = A<CozyWeatherMap>($"{Data}/Presentation/CozyWeatherMap.asset", m =>
        {
            m.transitionSeconds = 4f;
            m.defaults = new List<CozyWeatherMap.DefaultEntry>
            {
                Default(null, WeatherKind.Clear, "Clear"),
                Default(spring, WeatherKind.Clear, "Mostly Clear"),
                Default(null, WeatherKind.Cloudy, "Partly Cloudy"),
                Default(autumn, WeatherKind.Cloudy, "Mostly Cloudy"),
                Default(null, WeatherKind.Rain, "Legacy/Light Rain"),
                Default(null, WeatherKind.Storm, "Legacy/Thunder Storm"),
                Default(null, WeatherKind.Snow, "Legacy/Light Snow"),
                Default(null, WeatherKind.Heat, "Clear"),
            };
            m.disasters = new List<CozyWeatherMap.DisasterEntry>
            {
                DisasterMap(drought, "Dust Storm", fxHeat),
                DisasterMap(heatwave, "Clear", fxHeat),
                DisasterMap(storm, "Legacy/Thunder Storm", fxRain),
                DisasterMap(frost, "Legacy/Light Snow", fxSnow),
                DisasterMap(locust, "Overcast", fxLocusts),
                DisasterMap(blight, "Dense Fog", fxSpores),
            };
        });

        // Season looks are backfilled (not part of the create-only initializer) so existing maps get them
        // while tuned values survive re-runs.
        if (cozy.seasonLooks.Count == 0)
        {
            cozy.seasonLooks = new List<CozyWeatherMap.SeasonLook>
            {
                Look(spring, "FFC8DC", "D8FFE0", "FFE6EE", "FFF2E0", "E6FFF0", 0.35f, 0.10f, 0.02f, 1.0f),
                Look(summer, "7FD8FF", "FFD27A", "FFFFFF", "FFD890", "FFE9B0", 0.40f, 0.20f, 0.05f, 0.8f),
                Look(autumn, "D8402A", "E0602E", "FF9A5A", "FF7A2E", "FF6A3A", 0.65f, 0.15f, 0.00f, 1.3f),
                Look(winter, "F2F6FF", "EEF3FA", "FFFFFF", "EAF2FF", "F2F8FF", 0.70f, -0.40f, 0.12f, 1.6f),
            };
            EditorUtility.SetDirty(cozy);
        }

        // Backfill the stronger-sun / season-FX / snow-cover fields. A look with sunIntensity 0 predates them; looks
        // that already have it are left alone, so values tuned in the Inspector survive re-runs.
        for (int i = 0; i < cozy.seasonLooks.Count; i++)
        {
            CozyWeatherMap.SeasonLook look = cozy.seasonLooks[i];
            if (look.sunIntensity > 0f) continue;
            look.sunIntensity = 1f;
            look.ambientIntensity = 1f;
            if (look.season == summer)
            {
                look.sunIntensity = 1.6f; look.ambientIntensity = 1.15f;
                look.saturation += 0.08f; look.brightness += 0.05f;
            }
            else if (look.season == autumn) look.ambientFx = fxAutumnLeaves;
            else if (look.season == winter)
            {
                look.ambientFx = fxSeasonSnow;
                look.snowStart01 = 0.35f; look.snowEnd01 = 1f;
                look.snowyTrees = true;
            }
            cozy.seasonLooks[i] = look;
            EditorUtility.SetDirty(cozy);
        }

        // Autumn starts with leaves already on the ground (backfilled separately so tuned counts survive re-runs).
        for (int i = 0; i < cozy.seasonLooks.Count; i++)
        {
            CozyWeatherMap.SeasonLook look = cozy.seasonLooks[i];
            if (look.season != autumn || look.groundLeaves > 0) continue;
            look.groundLeaves = 320;
            cozy.seasonLooks[i] = look;
            EditorUtility.SetDirty(cozy);
        }

        // ---- Difficulty: bosses grow stronger each year ----
        DifficultyConfig difficulty = A<DifficultyConfig>($"{Data}/Run/DifficultyConfig.asset", d => { });

        // ---- Farmland: buy extra columns of plots, each pricier than the last ----
        FarmlandPlotItem plotItem = A<FarmlandPlotItem>($"{Data}/Run/FarmlandPlot.asset", p => { });
        FarmlandConfig farmland = A<FarmlandConfig>($"{Data}/Run/FarmlandConfig.asset", f => { });
        if (farmland.plotItem == null) { farmland.plotItem = plotItem; EditorUtility.SetDirty(farmland); }
        StockroomConfig stockroom = A<StockroomConfig>($"{Data}/Run/StockroomConfig.asset", f => { });

        // ---- Aggregate ----
        GameDatabase db = A<GameDatabase>($"{Data}/GameDatabase.asset", d => { });
        db.difficulty = difficulty;
        db.farmland = farmland;
        db.stockroom = stockroom;
        db.items = items; db.disasters = disasters; db.nodeTypes = nodeTypes; db.modifiers = modifiers; db.events = events; db.unlocks = unlocks;
        db.calendar = calendar; db.climate = climate; db.mapGen = mapGen; db.economy = economy; db.prices = prices; db.bank = bank; db.run = run;
        db.cozyWeather = cozy;
        EditorUtility.SetDirty(db);

        // Validate what we just built so a bad asset is reported immediately.
        ReportValidation(items, disasters, nodeTypes, modifiers, events, unlocks, prices);
        return db;
    }

    private static void ReportValidation(ItemRegistry items, DisasterRegistry disasters, NodeTypeRegistry nodes, RunModifierRegistry mods,
        EventRegistry events, UnlockRegistry unlocks, PriceTable prices)
    {
        List<string> errors;
        if (!items.Validate(out errors)) foreach (string e in errors) Debug.LogError("[RunSystemsBuilder] " + e);
        if (!disasters.Validate(out errors)) foreach (string e in errors) Debug.LogError("[RunSystemsBuilder] " + e);
        if (!nodes.Validate(out errors)) foreach (string e in errors) Debug.LogError("[RunSystemsBuilder] " + e);
        if (!mods.Validate(out errors)) foreach (string e in errors) Debug.LogError("[RunSystemsBuilder] " + e);
        if (!events.Validate(out errors)) foreach (string e in errors) Debug.LogError("[RunSystemsBuilder] " + e);
        if (!unlocks.Validate(out errors)) foreach (string e in errors) Debug.LogError("[RunSystemsBuilder] " + e);
        if (!prices.Validate(out errors)) foreach (string e in errors) Debug.LogError("[RunSystemsBuilder] " + e);
    }

    // =====================================================================================
    // Scene
    // =====================================================================================

    private static void BuildScene(GameDatabase db)
    {
        if (db == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        BossHunger hunger = Object.FindFirstObjectByType<BossHunger>(FindObjectsInactive.Include);
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (player == null || hunger == null || gm == null)
        {
            Debug.LogError("[RunSystemsBuilder] Needs Player, Caterpillar and GameManager in the open scene.");
            return;
        }

        Transform managers = Find("--- MANAGERS ---");
        Transform runRoot = GetOrCreateRoot("--- RUN ---");

        PlayerWallet wallet = Ensure<PlayerWallet>(player);
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        PlayerController controller = player.GetComponent<PlayerController>();

        FarmConditions conditions = Ensure<FarmConditions>(GetOrCreate("FarmConditions", managers));
        SetRef(conditions, "climate", db.climate);

        CozyBridge bridge = Ensure<CozyBridge>(GetOrCreate("CozyBridge", managers));
        SetRef(bridge, "seasonAtmosphere", Ensure<SeasonAtmosphere>(bridge.gameObject));
        SnowCover snowCover = Ensure<SnowCover>(bridge.gameObject);
        SetRef(bridge, "snowCover", snowCover);
        LeafLitter leafLitter = Ensure<LeafLitter>(bridge.gameObject);
        SetRef(bridge, "leafLitter", leafLitter);
        GameObject[] autumnLeafPrefabs = AutumnLeafPrefabs();   // returns the existing prefabs once they are built
        if (autumnLeafPrefabs.Length > 0)
        {
            SerializedObject litterSo = new SerializedObject(leafLitter);
            SerializedProperty prefabsProp = litterSo.FindProperty("leafPrefabs");
            prefabsProp.arraySize = autumnLeafPrefabs.Length;
            for (int i = 0; i < autumnLeafPrefabs.Length; i++) prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = autumnLeafPrefabs[i];
            litterSo.ApplyModifiedPropertiesWithoutUndo();
        }
        SetRef(leafLitter, "player", player.transform);   // leaves scatter when the farmer walks through them

        // Not GameObject.Find: the BarnHouse also has a renderer-less child called "Ground".
        Transform env = Find("--- ENVIRONMENT ---");
        Transform groundT = env != null ? env.Find("Ground") : null;
        if (groundT != null) SetRef(snowCover, "ground", groundT.GetComponent<Renderer>());
        SetRef(snowCover, "moundMaterial", AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/SnowMound.mat"));

        DisasterDirector disaster = Ensure<DisasterDirector>(GetOrCreate("DisasterDirector", managers));
        SetRef(disaster, "conditions", conditions);
        SetRef(disaster, "registry", db.disasters);
        SetRef(disaster, "playerHealth", health);
        SetRef(disaster, "playerController", controller);
        SetRef(disaster, "feedback", Object.FindFirstObjectByType<GameFeedback>());

        DayClock clock = Ensure<DayClock>(GetOrCreate("DayClock", managers));
        SetRef(clock, "gameManager", gm);
        SetRef(clock, "lighting", Object.FindFirstObjectByType<DayNightLighting>());
        SetRef(clock, "cozyBridge", bridge);

        Ensure<PlayerPlotPlacer>(player);   // places bought farmland plots from the hotbar
        RunDirector director = Ensure<RunDirector>(GetOrCreate("RunDirector", managers));
        MerchantStall stall = BuildStall(runRoot, director);

        SerializedObject so = new SerializedObject(director);
        so.FindProperty("runMode").enumValueIndex = (int)RunMode.Roguelike;
        so.FindProperty("db").objectReferenceValue = db;
        so.FindProperty("gameManager").objectReferenceValue = gm;
        so.FindProperty("inventory").objectReferenceValue = player.GetComponent<PlayerInventory>();
        so.FindProperty("health").objectReferenceValue = health;
        so.FindProperty("wallet").objectReferenceValue = wallet;
        so.FindProperty("playerController").objectReferenceValue = controller;
        so.FindProperty("playerCombat").objectReferenceValue = player.GetComponent<PlayerCombat>();
        so.FindProperty("playerInteractor").objectReferenceValue = player.GetComponent<PlayerInteractor>();
        so.FindProperty("bossRoot").objectReferenceValue = hunger.gameObject;
        so.FindProperty("bossHunger").objectReferenceValue = hunger;
        so.FindProperty("conditions").objectReferenceValue = conditions;
        so.FindProperty("disaster").objectReferenceValue = disaster;
        so.FindProperty("cozyBridge").objectReferenceValue = bridge;
        so.FindProperty("merchantStall").objectReferenceValue = stall.gameObject;
        so.FindProperty("clock").objectReferenceValue = clock;
        so.ApplyModifiedPropertiesWithoutUndo();

        ShrinkStartingField();

        GameObject witherFx = AssetDatabase.LoadAssetAtPath<GameObject>($"{FxDir}/FX_WitherPuff.prefab");
        foreach (FarmTile tile in Object.FindObjectsByType<FarmTile>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SetRef(tile, "conditions", conditions);
            SetRef(tile, "witherFx", witherFx);
        }

        EditorSceneManager.MarkSceneDirty(player.scene);
        EditorSceneManager.SaveOpenScenes();
    }

    /// <summary>
    /// The starting field is a 2x2 block of plots (the farmland upgrade adds columns of 2). Idempotent: does nothing
    /// when the scene already has exactly 4 plots. Keeps the four corner plots and tightens them into a 2x2.
    /// </summary>
    [MenuItem("MoFarmSand/Shrink Starting Field To 2x2")]
    public static void ShrinkStartingField()
    {
        List<FarmTile> tiles = new List<FarmTile>(Object.FindObjectsByType<FarmTile>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        if (tiles.Count <= 4) return;

        tiles.Sort((a, b) =>
        {
            int byZ = a.transform.position.z.CompareTo(b.transform.position.z);
            return Mathf.Abs(a.transform.position.z - b.transform.position.z) > 0.05f ? byZ : a.transform.position.x.CompareTo(b.transform.position.x);
        });

        // Group into rows (same z), then keep the first two rows' outer plots.
        List<List<FarmTile>> rows = new List<List<FarmTile>>();
        foreach (FarmTile t in tiles)
        {
            if (rows.Count == 0 || Mathf.Abs(rows[rows.Count - 1][0].transform.position.z - t.transform.position.z) > 0.05f) rows.Add(new List<FarmTile>());
            rows[rows.Count - 1].Add(t);
        }

        float z0 = rows[0][0].transform.position.z;
        float spacing = 2.2f;
        List<FarmTile> doomed = new List<FarmTile>();
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Count; c++)
            {
                FarmTile t = rows[r][c];
                bool keep = r < 2 && (c == 0 || c == rows[r].Count - 1) && rows[r].Count >= 2;
                if (!keep) { doomed.Add(t); continue; }
                Undo.RecordObject(t.transform, "Shrink starting field");
                Vector3 p = t.transform.position;
                t.transform.position = new Vector3(c == 0 ? -spacing * 0.5f : spacing * 0.5f, p.y, z0 + r * spacing);
            }
        }
        foreach (FarmTile t in doomed) Undo.DestroyObjectImmediate(t.gameObject);

        if (rows.Count > 0) EditorSceneManager.MarkSceneDirty(rows[0][0].gameObject.scene);
        Debug.Log($"[RunSystemsBuilder] Starting field is now 2x2 ({doomed.Count} plots removed).");
    }

    private static MerchantStall BuildStall(Transform parent, RunDirector director)
    {
        Transform existing = parent.Find("MerchantStall");
        GameObject root;
        if (existing != null) root = existing.gameObject;
        else
        {
            root = new GameObject("MerchantStall");
            Undo.RegisterCreatedObjectUndo(root, "Create MerchantStall");
            root.transform.SetParent(parent, false);

            root.transform.position = StallPosition();

            Material wood = Mat("MerchantWood", new Color(0.45f, 0.3f, 0.18f));
            Material cloth = Mat("MerchantCloth", new Color(0.8f, 0.2f, 0.25f));
            Prim(PrimitiveType.Cube, "Counter", root.transform, new Vector3(0f, 0.6f, 0f), new Vector3(2.4f, 1.2f, 1.1f), wood);
            Prim(PrimitiveType.Cube, "Awning", root.transform, new Vector3(0f, 2.2f, 0f), new Vector3(2.8f, 0.15f, 1.6f), cloth);
            Prim(PrimitiveType.Cylinder, "PostL", root.transform, new Vector3(-1.2f, 1.1f, 0.6f), new Vector3(0.12f, 1.1f, 0.12f), wood);
            Prim(PrimitiveType.Cylinder, "PostR", root.transform, new Vector3(1.2f, 1.1f, 0.6f), new Vector3(0.12f, 1.1f, 0.12f), wood);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.9f, 0f);
            TextMesh tm = labelGo.AddComponent<TextMesh>();
            tm.text = "MERCHANT"; tm.anchor = TextAnchor.MiddleCenter; tm.characterSize = 0.18f; tm.fontSize = 48; tm.color = Color.white;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            // Wide trigger on the south side (toward the farm) where the player approaches from.
            trigger.isTrigger = true; trigger.radius = 2.6f; trigger.center = new Vector3(0f, 0.5f, -1.2f);
        }

        MerchantStall stall = Ensure<MerchantStall>(root);
        SetRef(stall, "director", director);
        // The booth is a permanent scene prop; RunDirector.MerchantOpen decides when it trades.
        root.SetActive(true);
        return stall;
    }

    /// <summary>North of the farm (the camera looks toward +Z), centered on the tiles.</summary>
    private static Vector3 StallPosition()
    {
        FarmTile[] tiles = Object.FindObjectsByType<FarmTile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (tiles.Length == 0) return new Vector3(0f, 0f, 11f);
        Bounds b = new Bounds(tiles[0].transform.position, Vector3.zero);
        foreach (FarmTile t in tiles) b.Encapsulate(t.transform.position);
        return new Vector3(b.center.x, 0f, b.max.z + 3.6f);
    }

    private static void Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(g.GetComponent<Collider>());
    }

    // =====================================================================================
    // Asset helpers
    // =====================================================================================

    private static ClimateConfig.WeatherKindRule Rule(WeatherKind kind, float minT, float maxT, float minP, float maxP)
        => new ClimateConfig.WeatherKindRule { kind = kind, minTemp = minT, maxTemp = maxT, minPrecip = minP, maxPrecip = maxP };

    private static void SetClimate(CropData crop, float idealTemp, float tempTol, float idealMoist, float moistTol, params (SeasonData season, float mult)[] affinity)
        => SetClimate(crop, false, idealTemp, tempTol, idealMoist, moistTol, affinity);

    /// <param name="force">Overwrite an existing profile (used once by the crop roster migration to move Turnip to autumn).</param>
    internal static void SetClimate(CropData crop, bool force, float idealTemp, float tempTol, float idealMoist, float moistTol, params (SeasonData season, float mult)[] affinity)
    {
        // Only fill in if untouched, so designer tuning survives re-runs.
        if (!force && crop.climate.seasonAffinity != null && crop.climate.seasonAffinity.Count > 0) return;
        List<SeasonAffinity> list = new List<SeasonAffinity>();
        foreach ((SeasonData season, float mult) a in affinity) list.Add(new SeasonAffinity { season = a.season, multiplier = a.mult });
        crop.climate = new CropClimate
        {
            idealTemp = idealTemp, tempTolerance = tempTol, idealMoisture = idealMoist, moistureTolerance = moistTol, seasonAffinity = list
        };
        EditorUtility.SetDirty(crop);
    }

    private static SeedData Seed(string cropKey, CropData crop)
    {
        return A<SeedData>($"{Data}/Seeds/Seed_{char.ToUpper(cropKey[0])}{cropKey.Substring(1)}.asset", s =>
        {
            s.seedId = "seed_" + cropKey; s.displayName = crop.displayName + " Seeds"; s.grows = crop;
        });
    }

    private static DisasterData Disaster(string id, string name, string banner, Color color, bool activeAtNight, int minDay,
        (SeasonData season, float weight)[] weights, params DisasterEffect[] effects)
    {
        return A<DisasterData>($"{Data}/Disasters/Disaster_{char.ToUpper(id[0])}{id.Substring(1)}.asset", d =>
        {
            d.disasterId = id; d.displayName = name; d.bannerText = banner; d.uiColor = color; d.activeAtNight = activeAtNight; d.minDay = minDay;
            d.severityRange = new Vector2(0.4f, 1f);
            d.seasonWeights = new List<DisasterData.SeasonWeight>();
            foreach ((SeasonData season, float weight) w in weights) d.seasonWeights.Add(new DisasterData.SeasonWeight { season = w.season, weight = w.weight });
            d.effects = new List<DisasterEffect>(effects);
        });
    }

    private static RunModifierData Modifier(string id, string name, string desc, int price, string[] resist, float resistAmount, float growth, int hp)
    {
        return A<RunModifierData>($"{Data}/Run/Modifier_{id}.asset", m =>
        {
            m.modifierId = id; m.displayName = name; m.description = desc; m.price = price;
            m.resistDisasterIds = new List<string>(resist); m.resistAmount = resistAmount; m.growthMult = growth; m.bonusMaxHp = hp;
        });
    }

    private static UnlockData Unlock(string id, string name, string desc, int cost, RunEffect effect)
    {
        return A<UnlockData>($"{Data}/Meta/Unlock_{id}.asset", u =>
        {
            u.unlockId = id; u.displayName = name; u.description = desc; u.cost = cost; u.onRunStart = new List<RunEffect> { effect };
        });
    }

    private static void EnsurePoolEntry(RunConfig run, SeedData seed, float weight, Vector2Int count)
    {
        foreach (RunConfig.SeedPoolEntry e in run.startingSeedPool) if (e.seed == seed) return;
        run.startingSeedPool.Add(new RunConfig.SeedPoolEntry { seed = seed, weight = weight, countRange = count });
        EditorUtility.SetDirty(run);
    }

    /// <summary>A small projectile cloned from the tomato prefab: own color/size, optional hit puff. Created once.</summary>
    private static GameObject BulletPrefab(string name, GameObject template, Color color, float size, GameObject splat)
    {
        string path = $"Assets/Prefabs/{name}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        if (template == null) { Debug.LogError($"[RunSystemsBuilder] No tomato projectile to clone for {name}."); return null; }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(template);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.localScale = Vector3.one * size;
        go.GetComponent<Renderer>().sharedMaterial = Mat("Bullet_" + name, color);
        // Fast bullets must not tunnel through the caterpillar's collider.
        go.GetComponent<Rigidbody>().collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (splat != null) SetRef(go.GetComponent<CropProjectile>(), "splatPrefab", splat);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved;
    }

    private static void EnsureRow(PriceTable table, PriceTable.PriceRow row)
    {
        if (table.RowForCrop(row.crop.Id) != null) return;
        table.rows.Add(row);
        EditorUtility.SetDirty(table);
    }

    private static NodeTypeData NodeType(string id, string name, string glyph, Color color, string desc, System.Action<NodeTypeData> extra)
    {
        return A<NodeTypeData>($"{Data}/Map/Node_{id}.asset", n =>
        {
            n.nodeTypeId = id; n.displayName = name; n.glyph = glyph; n.color = color; n.description = desc;
            extra(n);
        });
    }

    private static MapGenConfig.LayerBand Band(int from, int to, params (NodeTypeData type, float weight)[] weights)
    {
        MapGenConfig.LayerBand band = new MapGenConfig.LayerBand { fromLayer = from, toLayer = to, weights = new List<MapGenConfig.NodeWeight>() };
        foreach ((NodeTypeData type, float weight) w in weights) band.weights.Add(new MapGenConfig.NodeWeight { type = w.type, weight = w.weight });
        return band;
    }

    private static PriceTable.PriceRow Row(CropData crop, SeedData seed, int seedCost, int baseSell, (SeasonData season, float mult)[] mults, params SeasonData[] stocked)
    {
        PriceTable.PriceRow row = new PriceTable.PriceRow { crop = crop, seed = seed, seedCost = seedCost, baseSell = baseSell };
        foreach ((SeasonData season, float mult) m in mults) row.seasonMults.Add(new PriceTable.SeasonMult { season = m.season, mult = m.mult });
        row.availableSeasons = new List<SeasonData>(stocked);
        return row;
    }

    private static CozyWeatherMap.DefaultEntry Default(SeasonData season, WeatherKind kind, string profile)
        => new CozyWeatherMap.DefaultEntry { season = season, kind = kind, profile = LoadProfile(profile) };

    private static CozyWeatherMap.SeasonLook Look(SeasonData season, string sky, string fog, string cloud, string light, string ambient,
        float strength, float saturation, float brightness, float fogDensity)
    {
        return new CozyWeatherMap.SeasonLook
        {
            season = season,
            skyTint = Hex(sky), fogTint = Hex(fog), cloudTint = Hex(cloud), lightTint = Hex(light), ambientTint = Hex(ambient),
            strength = strength, nightStrength = 0.5f, saturation = saturation, brightness = brightness, fogDensity = fogDensity,
        };
    }

    private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;

    private static CozyWeatherMap.DisasterEntry DisasterMap(DisasterData d, string profile, GameObject fx)
        => new CozyWeatherMap.DisasterEntry { disaster = d, profile = LoadProfile(profile), ambientFx = fx };

    private static WeatherProfile LoadProfile(string name)
    {
        WeatherProfile p = AssetDatabase.LoadAssetAtPath<WeatherProfile>($"{CozyProfiles}/{name}.asset");
        if (p == null) Debug.LogWarning($"[RunSystemsBuilder] COZY weather profile '{name}' not found.");
        return p;
    }

    private static R Registry<R, T>(string path, params T[] items) where R : IdRegistry<T> where T : ScriptableObject, IHasId
    {
        R reg = AssetDatabase.LoadAssetAtPath<R>(path);
        if (reg == null)
        {
            reg = ScriptableObject.CreateInstance<R>();
            AssetDatabase.CreateAsset(reg, path);
        }
        foreach (T item in items) reg.Ensure(item);
        EditorUtility.SetDirty(reg);
        return reg;
    }

    private static EventData.EventOption Opt(string label, int coinCost, params RunEffect[] effects) =>
        new EventData.EventOption { label = label, coinCost = coinCost, effects = new List<RunEffect>(effects) };

    private static EventData Event(string id, string file, string title, string body, params EventData.EventOption[] options) =>
        Event(id, file, title, body, null, options);

    /// <summary>An event asset that appears all year, or only in the given seasons.</summary>
    private static EventData Event(string id, string file, string title, string body, SeasonData[] seasons, params EventData.EventOption[] options) =>
        A<EventData>($"{Data}/Events/{file}.asset", e =>
        {
            e.eventId = id; e.title = title; e.body = body;
            e.options = new List<EventData.EventOption>(options);
            e.seasons = seasons != null ? new List<SeasonData>(seasons) : new List<SeasonData>();
        });

    /// <summary>Loads the asset, or creates it and runs init. Existing assets are never overwritten.</summary>
    private static T A<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        T so = ScriptableObject.CreateInstance<T>();
        init(so);
        AssetDatabase.CreateAsset(so, path);
        return so;
    }

    internal static GameObject Fx(string name, Material mat, Color color, bool loop, float rate, float life, float size, float speed, float gravity,
        ParticleSystemShapeType shape, Vector3 shapeScale, Vector3 offset, int burst = 0, bool circleEdge = false, bool shards = true)
    {
        string path = $"{FxDir}/{name}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = loop; main.duration = loop ? 5f : 0.6f; main.startLifetime = life; main.startSpeed = speed; main.startSize = size;
        main.gravityModifier = gravity; main.startColor = color; main.playOnAwake = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 2000;

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = rate;
        if (burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = shape; sh.position = offset;
        if (shape == ParticleSystemShapeType.Box) sh.scale = shapeScale;
        else { sh.radius = shapeScale.x; if (circleEdge) sh.radiusThickness = 0f; }

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        if (shards) TriangleFxBuilder.MakeShards(ps);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved;
    }

    /// <summary>Random red / orange / yellow per leaf; the same range as LeafLitter.leafColors on the ground.</summary>
    private static ParticleSystem.MinMaxGradient AutumnLeafColors()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.85f, 0.18f, 0.12f), 0f),
                new GradientColorKey(new Color(0.95f, 0.50f, 0.15f), 0.5f),
                new GradientColorKey(new Color(1.00f, 0.85f, 0.30f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
    }

    private const string LeafPackDir ="Assets/Low Poly Trees Pack/Tree Assets";
    private const string LeafPrefabDir = "Assets/Prefabs/Environment/AutumnLeaves";

    /// <summary>
    /// Autumn variants of the Low Poly Trees palm leaves (OneSided 01..05): the same meshes with the pack's autumn
    /// material. They are prefab variants, so the pack itself is untouched. Existing prefabs are kept.
    /// </summary>
    private static GameObject[] AutumnLeafPrefabs()
    {
        Material autumn = AssetDatabase.LoadAssetAtPath<Material>($"{LeafPackDir}/Materials/Trees_Autumn.mat");
        if (autumn == null)
        {
            Debug.LogWarning("[RunSystemsBuilder] Trees_Autumn.mat not found (is the Low Poly Trees Pack imported?). No autumn leaf prefabs built.");
            return new GameObject[0];
        }

        EnsureFolder(LeafPrefabDir);
        List<GameObject> leaves = new List<GameObject>();
        for (int i = 1; i <= 5; i++)
        {
            string dst = $"{LeafPrefabDir}/Leaf_Autumn_0{i}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
            if (existing != null) { leaves.Add(existing); continue; }

            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>($"{LeafPackDir}/Prefabs/Leaves/Palm_Leaves/OneSided/Palm_Leave_OneSided_0{i}.prefab");
            if (src == null) { Debug.LogWarning($"[RunSystemsBuilder] Palm_Leave_OneSided_0{i} not found."); continue; }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
            inst.name = $"Leaf_Autumn_0{i}";
            MeshRenderer r = inst.GetComponent<MeshRenderer>();
            r.sharedMaterial = autumn;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // a flat leaf on the ground needs no shadow
            leaves.Add(PrefabUtility.SaveAsPrefabAsset(inst, dst));
            Object.DestroyImmediate(inst);
        }
        return leaves.ToArray();
    }

    /// <summary>
    /// Particle-lit twin of the pack's autumn material (same atlas). The plain Lit shader ignores a particle's start
    /// colour, so the falling leaves need this one to get their red-to-yellow variation.
    /// </summary>
    private static Material AutumnLeafParticleMaterial(Material packMaterial)
    {
        Material m = Mat("AutumnLeafParticle", Color.white, "Universal Render Pipeline/Particles/Lit");
        Texture atlas = packMaterial != null && packMaterial.HasProperty("_BaseMap") ? packMaterial.GetTexture("_BaseMap") : null;
        if (atlas != null) m.SetTexture("_BaseMap", atlas);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Autumn leaf meshes that tumble down, land on the ground and shrink away at the end of their life.</summary>
    private static GameObject AutumnLeavesFx(GameObject[] leafPrefabs)
    {
        string path = $"{FxDir}/FX_AutumnLeaves.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        if (leafPrefabs == null || leafPrefabs.Length == 0) return null;

        List<Mesh> meshes = new List<Mesh>();
        foreach (GameObject leaf in leafPrefabs)
        {
            MeshFilter mf = leaf != null ? leaf.GetComponent<MeshFilter>() : null;
            if (mf != null && mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
        }
        if (meshes.Count == 0) return null;

        GameObject go = new GameObject("FX_AutumnLeaves");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true; main.duration = 5f; main.playOnAwake = true; main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 11f);
        main.startSpeed = 0f;
        // The meshes are about 3 units long, so this makes a leaf roughly 0.35-0.65 units, like the ground litter.
        main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.2f);
        main.gravityModifier = 0.12f;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = AutumnLeafColors();

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 14f;   // a light drift; the ground litter (LeafLitter) carries the autumn look

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box; sh.position = new Vector3(0f, 10f, 4f); sh.scale = new Vector3(26f, 0.1f, 26f);

        // Sideways drift and flutter while falling.
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true; noise.strength = 1.4f; noise.frequency = 0.45f; noise.scrollSpeed = 0.6f; noise.damping = true;

        // Tumble on every axis while falling.
        ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
        rot.enabled = true; rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

        // Land on the ground and stay put.
        ParticleSystem.CollisionModule col = ps.collision;
        col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
        col.bounce = 0f; col.dampen = 1f; col.lifetimeLoss = 0f; col.radiusScale = 0.5f;

        // The Lit leaf material ignores particle alpha, so shrink out instead of fading in the last 10% of life.
        ParticleSystem.SizeOverLifetimeModule shrink = ps.sizeOverLifetime;
        shrink.enabled = true;
        shrink.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.9f, 1f), new Keyframe(1f, 0f)));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.SetMeshes(meshes.GetRange(0, Mathf.Min(4, meshes.Count)).ToArray());   // a particle renderer takes at most 4 meshes
        renderer.meshDistribution = ParticleSystemMeshDistribution.UniformRandom;
        renderer.sharedMaterial = AutumnLeafParticleMaterial(leafPrefabs[0].GetComponent<MeshRenderer>().sharedMaterial);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved;
    }

    /// <summary>Soft, always-on winter snowfall with a slow sideways drift (lighter than the Frost disaster's FX_Snow).</summary>
    private static GameObject SeasonSnowFx(Material mat)
    {
        string path = $"{FxDir}/FX_SeasonSnow.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject go = new GameObject("FX_SeasonSnow");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true; main.duration = 5f; main.playOnAwake = true; main.maxParticles = 1500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
        main.gravityModifier = 0.08f;
        main.startColor = new Color(1f, 1f, 1f, 0.9f);

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 60f;

        ParticleSystem.ShapeModule sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box; sh.position = new Vector3(0f, 10f, 4f); sh.scale = new Vector3(26f, 0.1f, 26f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f; noise.scrollSpeed = 0.3f; noise.damping = true;

        ParticleSystem.CollisionModule col = ps.collision;
        col.enabled = true; col.type = ParticleSystemCollisionType.World; col.mode = ParticleSystemCollisionMode.Collision3D;
        col.bounce = 0f; col.dampen = 1f; col.lifetimeLoss = 1f;   // flakes vanish where they land; SnowCover builds the drifts

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        TriangleFxBuilder.MakeShards(ps);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return saved;
    }

    internal static Material Mat(string name, Color color, string shaderName = "Universal Render Pipeline/Lit")
    {
        string path = $"{MatDir}/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find(shaderName)) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // =====================================================================================
    // Scene helpers (same pattern as Batch2Builder)
    // =====================================================================================

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static Transform Find(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.transform : GetOrCreateRoot(name);
    }

    private static Transform GetOrCreateRoot(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        return go.transform;
    }

    private static GameObject GetOrCreate(string name, Transform parent)
    {
        Transform existing = parent != null ? parent.Find(name) : null;
        if (existing != null) return existing.gameObject;
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : Undo.AddComponent<T>(go);
    }

    private static T Ensure<T>(Component c) where T : Component => Ensure<T>(c.gameObject);

    private static void SetRef(Object target, string field, Object value)
    {
        if (target == null) return;
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError($"[RunSystemsBuilder] Missing field {field} on {target.GetType().Name}"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
