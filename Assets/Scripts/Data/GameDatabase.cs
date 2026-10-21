using UnityEngine;

/// <summary>Single reference to every registry and config the run systems need.</summary>
[CreateAssetMenu(fileName = "GameDatabase", menuName = "MoFarmSand/Game Database")]
public class GameDatabase : ScriptableObject
{
    [Header("Registries")]
    public ItemRegistry items;
    public DisasterRegistry disasters;
    public NodeTypeRegistry nodeTypes;
    public RunModifierRegistry modifiers;
    public EventRegistry events;
    public UnlockRegistry unlocks;

    [Header("Configs")]
    public CalendarConfig calendar;
    public ClimateConfig climate;
    public MapGenConfig mapGen;
    public EconomyConfig economy;
    public PriceTable prices;
    public BankConfig bank;
    public RunConfig run;
    public DifficultyConfig difficulty;
    public FarmlandConfig farmland;
    public StockroomConfig stockroom;

    [Header("Presentation")]
    public CozyWeatherMap cozyWeather;

    private SeasonCalendar calendarModel;

    /// <summary>Pure calendar built from <see cref="calendar"/>. Rebuilt if the config reference changes.</summary>
    public SeasonCalendar Calendar => calendarModel ??= new SeasonCalendar(calendar);

    private void OnEnable() { calendarModel = null; }
    private void OnValidate() { calendarModel = null; }
}
