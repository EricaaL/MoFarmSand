using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SaveServiceTests
{
    private sealed class MemStore : ISaveStore
    {
        public readonly Dictionary<string, string> files = new Dictionary<string, string>();
        public bool Exists(string name) => files.ContainsKey(name);
        public bool TryRead(string name, out string text) => files.TryGetValue(name, out text);

        public void WriteAtomic(string name, string text)
        {
            if (files.TryGetValue(name, out string old)) files[name + ".bak"] = old;
            files[name] = text;
        }

        public void Delete(string name) { files.Remove(name); files.Remove(name + ".bak"); }
    }

    private GameDatabase db;
    private MemStore store;
    private SaveService service;

    [SetUp]
    public void SetUp()
    {
        db = TestUtil.LoadDb();
        store = new MemStore();
        service = new SaveService(store);
    }

    private SaveFile SampleFile(int seed = 5, int vault = 12)
    {
        SaveFile f = new SaveFile { hasRun = true };
        f.meta.vaultCoins = vault;
        f.meta.unlockedIds.Add("hardy_paws");
        f.run = RunLogic.NewRun(seed, db, new MetaProgress());
        return f;
    }

    [Test]
    public void SaveThenLoadRoundTrips()
    {
        SaveFile saved = SampleFile();
        Assert.IsTrue(service.Save(1, saved));

        LoadResult r = service.Load(1);
        Assert.AreEqual(LoadStatus.Ok, r.status);
        Assert.AreEqual(JsonUtility.ToJson(saved.run), JsonUtility.ToJson(r.file.run));
        Assert.AreEqual(12, r.file.meta.vaultCoins);
        Assert.AreEqual(SaveFile.CurrentVersion, r.file.version);
        Assert.IsTrue(r.file.hasRun);
    }

    [Test]
    public void EmptyAndGarbageSlotsDoNotThrow()
    {
        Assert.AreEqual(LoadStatus.Empty, service.Load(0).status);

        store.files[SaveService.NameOf(0)] = "{ this is not json";
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("corrupt"));
        Assert.AreEqual(LoadStatus.Corrupt, service.Load(0).status);
    }

    [Test]
    public void CorruptMainFileFallsBackToTheBackup()
    {
        service.Save(2, SampleFile(vault: 10));
        service.Save(2, SampleFile(vault: 20));
        Assert.IsTrue(store.Exists(SaveService.NameOf(2) + ".bak"), "the previous save is kept as .bak");

        store.files[SaveService.NameOf(2)] = "garbage";
        LoadResult r = service.Load(2);

        Assert.AreEqual(LoadStatus.Ok, r.status);
        Assert.AreEqual(10, r.file.meta.vaultCoins, "restored from the previous save");
    }

    [Test]
    public void SlotsAreIndependentAndDeleteWorks()
    {
        service.Save(0, SampleFile(vault: 1));
        service.Save(1, SampleFile(vault: 2));
        service.Save(2, SampleFile(vault: 3));

        service.Delete(1);

        Assert.AreEqual(1, service.Load(0).file.meta.vaultCoins);
        Assert.AreEqual(LoadStatus.Empty, service.Load(1).status);
        Assert.AreEqual(3, service.Load(2).file.meta.vaultCoins);
    }

    [Test]
    public void NewerSaveVersionIsReportedAndNeverOverwritten()
    {
        SaveFile future = SampleFile();
        future.version = SaveFile.CurrentVersion + 5;
        string json = JsonUtility.ToJson(future);
        store.files[SaveService.NameOf(0)] = json;

        Assert.AreEqual(LoadStatus.TooNew, service.Load(0).status);
        Assert.IsFalse(service.Save(0, SampleFile()), "refuses to overwrite a newer save");
        Assert.AreEqual(json, store.files[SaveService.NameOf(0)]);
    }

    private sealed class FillStep : ISaveMigration
    {
        public int From => 0;
        public int calls;
        public void Apply(SaveFile file) { calls++; file.meta.runsStarted = 99; }
    }

    [Test]
    public void MigrationsRunInOrderAndBumpTheVersion()
    {
        SaveFile old = new SaveFile { version = 0 };
        FillStep step = new FillStep();

        SaveMigrations.Apply(old, new List<ISaveMigration> { step });

        Assert.AreEqual(1, step.calls);
        Assert.AreEqual(99, old.meta.runsStarted);
        Assert.AreEqual(SaveFile.CurrentVersion, old.version);
    }

    [Test]
    public void PeekSummarisesASlotWithoutTheScene()
    {
        Assert.AreEqual(LoadStatus.Empty, service.Peek(0).status);
        service.Save(0, SampleFile(vault: 14));

        SlotSummary s = service.Peek(0);
        Assert.AreEqual(LoadStatus.Ok, s.status);
        Assert.IsTrue(s.hasRun);
        Assert.AreEqual(14, s.vaultCoins);
        Assert.AreEqual(1, s.unlockCount);
    }

    [Test]
    public void SanitizeDropsUnknownIdsClampsValuesAndRejectsBrokenMaps()
    {
        SaveFile f = SampleFile();
        f.meta.unlockedIds.Add("not_a_real_unlock");
        f.meta.vaultCoins = 99999;
        f.run.modifierIds.Add("ghost");
        f.run.hp = 50; f.run.maxHp = 3; f.run.coins = -4;
        f.run.inventory[0] = new SlotSave("ghost_item", 5);
        f.run.shopStock.Add(new ShopStockEntry { kind = OfferKind.Seed, id = "ghost_seed", stock = 3 });
        int shelfBefore = f.run.shopStock.Count;

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown item"));
        SaveService.Sanitize(f, db);

        CollectionAssert.DoesNotContain(f.meta.unlockedIds, "not_a_real_unlock");
        CollectionAssert.Contains(f.meta.unlockedIds, "hardy_paws");
        Assert.AreEqual(db.bank.vaultCap, f.meta.vaultCoins);
        CollectionAssert.DoesNotContain(f.run.modifierIds, "ghost");
        Assert.AreEqual(3, f.run.hp);
        Assert.AreEqual(0, f.run.coins);
        Assert.AreEqual(0, f.run.inventory[0].count);
        Assert.AreEqual(shelfBefore - 1, f.run.shopStock.Count, "unknown shelf entries are dropped");
        Assert.IsFalse(f.run.shopStock.Exists(e => e.id == "ghost_seed"));
        Assert.IsTrue(f.hasRun);

        f.run.map.bossId = -1;
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("failed validation"));
        SaveService.Sanitize(f, db);
        Assert.IsFalse(f.hasRun, "a broken map discards the run");
    }

    [Test]
    public void FileStoreWritesAtomicallyAndKeepsABackup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "mofarmsand_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            FileSaveStore fs = new FileSaveStore(dir);
            fs.WriteAtomic("slot0.json", "one");
            fs.WriteAtomic("slot0.json", "two");

            Assert.IsTrue(fs.TryRead("slot0.json", out string main));
            Assert.AreEqual("two", main);
            Assert.IsTrue(fs.TryRead("slot0.json.bak", out string bak));
            Assert.AreEqual("one", bak);
            Assert.IsFalse(File.Exists(Path.Combine(dir, "slot0.json.tmp")), "no temp file left behind");
            Assert.IsFalse(fs.TryRead("missing.json", out _));

            fs.Delete("slot0.json");
            Assert.IsFalse(fs.Exists("slot0.json"));
            Assert.IsFalse(fs.Exists("slot0.json.bak"));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
