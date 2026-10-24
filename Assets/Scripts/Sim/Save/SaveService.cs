using System;
using System.Collections.Generic;
using UnityEngine;

public enum LoadStatus { Ok, Empty, Corrupt, TooNew }

public struct LoadResult
{
    public LoadStatus status;
    public SaveFile file;
}

public struct SlotSummary
{
    public LoadStatus status;
    public bool hasRun;
    public int dayIndex;
    public int coins;
    public int vaultCoins;
    /// <summary>Bigger Vault level, so a slot card can show the limit (VaultLogic.CapFor).</summary>
    public int vaultLevel;
    public int unlockCount;
    public int runsStarted;
    /// <summary>Year number of the run in this slot (0 if none or from an old save).</summary>
    public int year;
    /// <summary>ISO-8601 UTC time of the slot's last save (empty if never saved); sorts as plain text.</summary>
    public string savedAtUtc;
}

/// <summary>Three independent slots with atomic writes, .bak fallback and version migration. Never throws on bad data.</summary>
public sealed class SaveService
{
    public const int SlotCount = 3;
    /// <summary>Hidden fourth slot (slot3.json) used only by the beta test scene; the title screen never lists it.</summary>
    public const int BetaSlot = SlotCount;

    private readonly ISaveStore store;

    public SaveService(ISaveStore store) { this.store = store; }

    public static string NameOf(int slot) => $"slot{slot}.json";

    public LoadResult Load(int slot)
    {
        string name = NameOf(slot);
        bool mainExists = store.Exists(name);
        bool bakExists = store.Exists(name + ".bak");
        if (!mainExists && !bakExists) return new LoadResult { status = LoadStatus.Empty };

        if (TryParse(name, out SaveFile file) || TryParse(name + ".bak", out file))
        {
            if (file.version > SaveFile.CurrentVersion) return new LoadResult { status = LoadStatus.TooNew, file = file };
            SaveMigrations.Apply(file);
            return new LoadResult { status = LoadStatus.Ok, file = file };
        }

        Debug.LogWarning($"SaveService: slot {slot} is corrupt and no usable backup exists.");
        return new LoadResult { status = LoadStatus.Corrupt };
    }

    private bool TryParse(string name, out SaveFile file)
    {
        file = null;
        if (!store.TryRead(name, out string text) || string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            file = JsonUtility.FromJson<SaveFile>(text);
            return file != null && file.version > 0;
        }
        catch (Exception) { file = null; return false; }
    }

    /// <summary>Returns false (and writes nothing) if the slot holds a save from a newer game version.</summary>
    public bool Save(int slot, SaveFile file)
    {
        LoadResult existing = Load(slot);
        if (existing.status == LoadStatus.TooNew) return false;

        file.version = SaveFile.CurrentVersion;
        file.savedAtUtc = DateTime.UtcNow.ToString("o");
        store.WriteAtomic(NameOf(slot), JsonUtility.ToJson(file));
        return true;
    }

    public void Delete(int slot) { store.Delete(NameOf(slot)); }

    public SlotSummary Peek(int slot, SeasonCalendar calendar = null)
    {
        LoadResult r = Load(slot);
        SlotSummary s = new SlotSummary { status = r.status };
        if (r.status != LoadStatus.Ok) return s;
        s.hasRun = r.file.hasRun;
        s.vaultCoins = r.file.meta.vaultCoins;
        s.vaultLevel = r.file.meta.vaultLevel;
        s.unlockCount = r.file.meta.unlockedIds.Count;
        s.runsStarted = r.file.meta.runsStarted;
        s.savedAtUtc = r.file.savedAtUtc ?? string.Empty;
        if (r.file.hasRun)
        {
            s.dayIndex = r.file.run.DayIndex;
            s.coins = r.file.run.coins;
            s.year = r.file.run.YearNumber(r.file.meta);
        }
        return s;
    }

    /// <summary>Index of the readable slot with a year in progress that was saved last, or -1 if there is none.</summary>
    public static int MostRecentRunSlot(IReadOnlyList<SlotSummary> slots)
    {
        int best = -1;
        for (int i = 0; i < slots.Count; i++)
        {
            SlotSummary s = slots[i];
            if (s.status != LoadStatus.Ok || !s.hasRun) continue;
            if (best < 0 || string.CompareOrdinal(s.savedAtUtc ?? string.Empty, slots[best].savedAtUtc ?? string.Empty) > 0) best = i;
        }
        return best;
    }

    /// <summary>Drops ids the game no longer knows, clamps values and rejects broken maps (guards against edited saves).</summary>
    public static void Sanitize(SaveFile file, GameDatabase db)
    {
        MetaProgress meta = file.meta;
        meta.vaultLevel = TierPricing.ClampOwned(meta.vaultLevel, 0);
        meta.vaultCoins = Mathf.Clamp(meta.vaultCoins, 0, db.bank != null ? VaultLogic.Cap(meta, db.bank) : int.MaxValue);
        if (db.unlocks != null) meta.unlockedIds.RemoveAll(id => !db.unlocks.TryGet(id, out _));
        meta.farmlands = TierPricing.ClampOwned(meta.farmlands, db.farmland != null ? db.farmland.maxFarmlands : 0);
        if (meta.placedPlots == null) meta.placedPlots = new List<Vector3>();
        meta.placedPlots.RemoveAll(p => float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)
            || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));
        meta.stockroomLevel = TierPricing.ClampOwned(meta.stockroomLevel, db.stockroom != null ? db.stockroom.maxLevel : 0);

        if (!file.hasRun) return;
        RunState run = file.run;
        run.maxHp = Mathf.Max(1, run.maxHp);
        run.hp = Mathf.Clamp(run.hp, 1, run.maxHp);
        run.coins = Mathf.Max(0, run.coins);
        run.farmlands = TierPricing.ClampOwned(run.farmlands, db.farmland != null ? db.farmland.maxFarmlands : 0);
        run.stockroomLevel = TierPricing.ClampOwned(run.stockroomLevel, db.stockroom != null ? db.stockroom.maxLevel : 0);

        InventoryOps.EnsureSize(run.inventory);
        for (int i = 0; i < run.inventory.Count; i++)
        {
            SlotSave s = run.inventory[i];
            if (s.count > 0 && (db.items == null || !db.items.TryGet(s.itemId, out _)))
            {
                Debug.LogWarning($"SaveService: dropped unknown item '{s.itemId}'.");
                run.inventory[i] = new SlotSave(string.Empty, 0);
            }
        }
        run.activeSlot = Mathf.Clamp(run.activeSlot, 0, InventoryOps.Slots - 1);
        if (db.modifiers != null) run.modifierIds.RemoveAll(id => !db.modifiers.TryGet(id, out _));
        if (db.unlocks != null) run.unlockedIds.RemoveAll(id => !db.unlocks.TryGet(id, out _));
        // Saves from before crop unlocks have no snapshot: use what the player owns now.
        if (run.unlockedIds.Count == 0 && meta.unlockedIds.Count > 0) run.unlockedIds.AddRange(meta.unlockedIds);
        run.shopStock.RemoveAll(e => e == null || e.stock <= 0 || string.IsNullOrEmpty(e.id)
            || (e.kind == OfferKind.Seed ? db.prices == null || db.prices.RowForSeed(e.id) == null
                                          : db.modifiers == null || !db.modifiers.TryGet(e.id, out _)));

        if (MapValidator.Validate(run.map, db.mapGen, db.nodeTypes).Count > 0)
        {
            Debug.LogWarning("SaveService: saved map failed validation; discarding the run.");
            file.hasRun = false;
        }
    }
}
