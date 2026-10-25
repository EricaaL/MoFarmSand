using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the "A Wild Chocobo" random event (any season) and registers it. The chocobo prefab (ChocoboBuilder) is spawned on the farm
/// at <see cref="Position"/> / <see cref="Euler"/> while the event plays. Additive: an existing event asset keeps its edits, only the prefab link is repaired.
/// </summary>
public static class ChocoboEventBuilder
{
    private const string Data = "Assets/Data";
    private const string EventPath = Data + "/Events/Event_WildChocobo.asset";
    private const string RegistryPath = Data + "/Registries/EventRegistry.asset";
    private const string PrefabPath = "Assets/Prefabs/Characters/Chocobo.prefab";
    private static readonly Vector3 Position = new Vector3(2.76000214f, 0f, 7.07614708f);
    private static readonly Vector3 Euler = new Vector3(0f, 211.067474f, 0f);

    [MenuItem("MoFarmSand/Add Chocobo Event")]
    public static void AddChocoboEvent()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("Run 'MoFarmSand > Add Chocobo' first: " + PrefabPath + " is missing."); return; }

        EventData evt = AssetDatabase.LoadAssetAtPath<EventData>(EventPath);
        if (evt == null)
        {
            evt = ScriptableObject.CreateInstance<EventData>();
            evt.eventId = "wild_chocobo";
            evt.title = "A Wild Chocobo";
            evt.body = "A fluffy yellow chocobo is pecking at the dirt by your fence. It tilts its head at you, curious.";
            evt.options = new List<EventData.EventOption>
            {
                Option("Pet its fluffy head", Effect("HealFull")),
                Option("Share some of your harvest", Effect("LoseCrops3"), Effect("MaxHp1")),
                Option("Shoo it away"),
            };
            AssetDatabase.CreateAsset(evt, EventPath);
        }
        evt.stagePrefab = prefab;
        evt.stagePosition = Position;
        evt.stageEuler = Euler;
        EditorUtility.SetDirty(evt);

        EventRegistry registry = AssetDatabase.LoadAssetAtPath<EventRegistry>(RegistryPath);
        if (registry != null && registry.Ensure(evt)) EditorUtility.SetDirty(registry);

        AssetDatabase.SaveAssets();
        Debug.Log("Chocobo event ready: " + EventPath);
    }

    private static EventData.EventOption Option(string label, params RunEffect[] effects) =>
        new EventData.EventOption { label = label, coinCost = 0, effects = new List<RunEffect>(effects) };

    private static RunEffect Effect(string name) => AssetDatabase.LoadAssetAtPath<RunEffect>($"{Data}/Run/Effects/Effect_{name}.asset");
}
