using DistantLands.Cozy;
using UnityEngine;

/// <summary>
/// Pushes the day's calendar date and weather into COZY. It owns date, weather and snow;
/// <see cref="DayNightLighting"/> owns currentTime, so the two never fight. Null-safe without COZY.
/// </summary>
public class CozyBridge : MonoBehaviour
{
    [SerializeField] private SeasonAtmosphere seasonAtmosphere;
    [SerializeField] private SnowCover snowCover;
    [SerializeField] private LeafLitter leafLitter;
    [SerializeField] private SeasonTrees seasonTrees;
    private GameObject ambientFx;
    private GameObject seasonFx;

    private void Awake()
    {
        if (seasonAtmosphere == null) seasonAtmosphere = GetComponent<SeasonAtmosphere>();
        if (snowCover == null) snowCover = GetComponent<SnowCover>();
        if (leafLitter == null) leafLitter = GetComponent<LeafLitter>();
        if (seasonTrees == null) seasonTrees = GetComponent<SeasonTrees>();
    }

    /// <summary>Forwarded by <see cref="DayClock"/> each frame so the snow keeps building through the day.</summary>
    public void SetDayProgress(float progress01)
    {
        if (snowCover != null) snowCover.SetDayProgress(progress01);
    }

    public void Apply(DayPlan plan, DisasterData disaster, CozyWeatherMap map)
    {
        if (ambientFx != null) Destroy(ambientFx);
        if (seasonFx != null) Destroy(seasonFx);
        if (plan == null || plan.date.season == null) return;

        SeasonData season = plan.date.season;
        CozyWeatherMap.SeasonLook look = map != null ? map.LookFor(season) : CozyWeatherMap.SeasonLook.Neutral;
        if (seasonAtmosphere != null && map != null) seasonAtmosphere.SetLook(look, map.transitionSeconds);
        if (leafLitter != null) leafLitter.SetCount(look.groundLeaves);
        if (seasonTrees != null) seasonTrees.SetSnowy(look.snowyTrees);
        if (snowCover != null) snowCover.SetSeason(look, plan.date.seasonProgress01, 1f / Mathf.Max(1, season.lengthInDays));
        CozyWeather cozy = CozyWeather.instance;
        if (cozy != null)
        {
            CozyTimeModule time = cozy.timeModule;
            if (time != null)
            {
                float year01 = Mathf.Repeat(season.cozyYearStart01 + plan.date.seasonProgress01 * season.cozyYearSpan01, 1f);
                time.currentYear = 0;
                time.currentDay = Mathf.Clamp(Mathf.FloorToInt(year01 * time.DaysPerYear), 0, time.DaysPerYear - 1);
            }

            CozyEcosystem eco = cozy.weatherModule != null ? cozy.weatherModule.Ecosystem : null;
            if (eco != null && map != null)
            {
                // Forecast mode would overwrite the weather we set, so take manual control.
                eco.weatherSelectionMode = CozyEcosystem.EcosystemStyle.manual;
                DistantLands.Cozy.Data.WeatherProfile profile = map.ProfileFor(season, plan.weather.kind, disaster);
                if (profile != null) eco.SetWeather(profile, map.transitionSeconds);
            }

            // Snow cover (climateModule.snowAmount) is owned by SnowCover, from the season look.
        }

        ambientFx = SpawnFx(map != null ? map.AmbientFxFor(disaster) : null);
        seasonFx = SpawnFx(look.ambientFx);
    }

    private GameObject SpawnFx(GameObject prefab)
    {
        if (prefab == null) return null;
        Camera cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position : transform.position;
        return Instantiate(prefab, pos, Quaternion.identity, cam != null ? cam.transform : transform);
    }
}
