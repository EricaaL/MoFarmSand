using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class InventoryAndFarmTests
{
    private readonly List<Object> created = new List<Object>();

    private T Track<T>(T o) where T : Object { created.Add(o); return o; }

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
        created.Clear();
    }

    private CropData Crop(string id, int yield = 2)
    {
        CropData c = Track(TestUtil.Make<CropData>());
        c.cropId = id; c.displayName = id; c.growTime = 0f; c.harvestYield = yield;
        return c;
    }

    private SeedData Seed(CropData crop)
    {
        SeedData s = Track(TestUtil.Make<SeedData>());
        s.seedId = "seed_" + crop.cropId; s.grows = crop;
        return s;
    }

    private PlayerInventory NewInventory(out GameObject go)
    {
        go = Track(new GameObject("Player"));
        return go.AddComponent<PlayerInventory>();
    }

    [Test]
    public void SeedsAndCropsStackSeparately()
    {
        CropData tomato = Crop("tomato"); SeedData seed = Seed(tomato);
        PlayerInventory inv = NewInventory(out _);

        inv.AddItem(tomato, 3);
        inv.AddItem(seed, 2);
        inv.AddItem(tomato, 1);

        Assert.AreEqual(4, inv.CountOf(tomato));
        Assert.AreEqual(2, inv.CountOf(seed));
        Assert.AreEqual(4, inv.TotalCount(), "only crops count as ammo");
    }

    [Test]
    public void TryRemoveIsAllOrNothing()
    {
        CropData tomato = Crop("tomato");
        PlayerInventory inv = NewInventory(out _);
        inv.AddItem(tomato, 3);

        Assert.IsFalse(inv.TryRemove(tomato, 4));
        Assert.AreEqual(3, inv.CountOf(tomato));
        Assert.IsTrue(inv.TryRemove(tomato, 2));
        Assert.AreEqual(1, inv.CountOf(tomato));
    }

    [Test]
    public void SnapshotAndRestoreRoundTripThroughTheRegistry()
    {
        CropData tomato = Crop("tomato"); SeedData seed = Seed(tomato);
        ItemRegistry reg = Track(TestUtil.Make<ItemRegistry>());
        reg.Ensure(tomato); reg.Ensure(seed);

        PlayerInventory a = NewInventory(out _);
        a.AddItem(seed, 4); a.AddItem(tomato, 2); a.SetActive(1);
        List<SlotSave> snap = a.Snapshot();

        PlayerInventory b = NewInventory(out _);
        b.Restore(snap, a.ActiveIndex, reg);

        Assert.AreEqual(4, b.CountOf(seed));
        Assert.AreEqual(2, b.CountOf(tomato));
        Assert.AreEqual(1, b.ActiveIndex);
        Assert.AreEqual(PlayerInventory.SlotCount, snap.Count);
    }

    [Test]
    public void EmptiedSeedSlotsFreeUpTheSlot()
    {
        CropData tomato = Crop("tomato"); SeedData seed = Seed(tomato);
        PlayerInventory inv = NewInventory(out _);
        inv.AddItem(seed, 1);
        Assert.IsTrue(inv.TryConsume(seed, 1));
        Assert.IsNull(inv.GetSlot(0).item);
    }

    private static void RunUpdate(FarmTile tile)
    {
        typeof(FarmTile).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tile, null);
    }

    private FarmTile NewTile(bool freePlanting)
    {
        GameObject go = Track(new GameObject("Tile"));
        FarmTile tile = go.AddComponent<FarmTile>();
        tile.SetFreePlanting(freePlanting);
        return tile;
    }

    [Test]
    public void PlantingConsumesOneSeedAndHarvestGivesTheCropNotASeed()
    {
        CropData tomato = Crop("tomato", yield: 2); SeedData seed = Seed(tomato);
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(seed, 2);
        FarmTile tile = NewTile(false);

        tile.Interact(player);
        Assert.AreEqual(FarmTile.TileState.Growing, tile.State);
        Assert.AreEqual(1, inv.CountOf(seed), "exactly one seed used");
        Assert.AreSame(tomato, tile.Crop);

        RunUpdate(tile);
        Assert.AreEqual(FarmTile.TileState.Harvestable, tile.State);

        tile.Interact(player);
        Assert.AreEqual(FarmTile.TileState.Empty, tile.State);
        Assert.AreEqual(2, inv.CountOf(tomato), "harvest yields the crop");
        Assert.AreEqual(1, inv.CountOf(seed), "harvest drops no seeds");
    }

    [Test]
    public void CannotPlantWithoutASeedWhenFreePlantingIsOff()
    {
        CropData tomato = Crop("tomato");
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(tomato, 3);
        FarmTile tile = NewTile(false);

        tile.Interact(player);

        Assert.AreEqual(FarmTile.TileState.Empty, tile.State, "a harvested crop is not a seed");
        Assert.AreEqual(3, inv.CountOf(tomato));
    }

    [Test]
    public void LegacyFreePlantingStillWorksForTheTrailerScene()
    {
        CropData tomato = Crop("tomato");
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(tomato, 3);
        FarmTile tile = NewTile(true);

        tile.Interact(player);

        Assert.AreEqual(FarmTile.TileState.Growing, tile.State);
        Assert.AreEqual(3, inv.CountOf(tomato), "legacy planting doesn't consume the crop");
    }

    [Test]
    public void WitherEmptiesAGrowingTileAndBlockPreventsInteraction()
    {
        CropData tomato = Crop("tomato"); SeedData seed = Seed(tomato);
        PlayerInventory inv = NewInventory(out GameObject player);
        inv.AddItem(seed, 2);
        FarmTile tile = NewTile(false);

        tile.Interact(player);
        Assert.IsTrue(tile.IsGrowing);
        tile.Wither();
        Assert.AreEqual(FarmTile.TileState.Empty, tile.State);

        tile.Block(5f);
        Assert.IsTrue(tile.IsBlocked);
        Assert.IsFalse(tile.CanInteract(player));
        tile.Interact(player);
        Assert.AreEqual(1, inv.CountOf(seed), "a flooded tile can't be planted");
    }

    [Test]
    public void GrowthRateFromConditionsScalesTheTimer()
    {
        GameObject go = Track(new GameObject("Conditions"));
        FarmConditions cond = go.AddComponent<FarmConditions>();
        cond.Growth.Set("test", 0.5f);

        CropData tomato = Crop("tomato");
        Assert.AreEqual(0.5f, cond.GrowthRateFor(tomato), 1e-5f, "with no date set, only modifiers apply");
    }
}
