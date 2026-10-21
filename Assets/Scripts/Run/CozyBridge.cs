using DistantLands.Cozy;
using UnityEngine;

/// <summary>
/// Pushes the day's calendar date and weather into COZY. It owns date, weather and snow;
/// <see cref="DayNightLighting"/> owns currentTime, so the two never fight. Null-safe without COZY.
/// </summary>
public class CozyBridge : MonoBehaviour
{
    [SerializeField] private SeasonAtmosphere seasonAtmosphere;
    private GameObject ambientFx;

    private void Awake()
    {
        if (seasonAtmosphere == null) seasonAtmosphere = GetComponent<SeasonAtmosphere>();
    }

    public void Apply(DayPlan plan, DisasterData disaster, CozyWeatherMap map)
    {
        if (ambientFx != null) Destroy(ambientFx);
        if (plan == null || plan.date.season == null) return;

        SeasonData season = plan.date.season;
        if (seasonAtmosphere != null && map != null) seasonAtmosphere.SetLook(map.LookFor(season), map.transitionSeconds);
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

            if (cozy.climateModule != null) cozy.climateModule.snowAmount = season.initialSnow01;
        }

        GameObject fx = map != null ? map.AmbientFxFor(disaster) : null;
        if (fx != null)
        {
            Camera cam = Camera.main;
            Vector3 pos = cam != null ? cam.transform.position : transform.position;
            ambientFx = Instantiate(fx, pos, Quaternion.identity, cam != null ? cam.transform : transform);
        }
    }
}
