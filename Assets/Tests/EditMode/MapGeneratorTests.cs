using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class MapGeneratorTests
{
    private GameDatabase db;

    [SetUp]
    public void SetUp() { db = TestUtil.LoadDb(); }

    private MapGraph Gen(int seed) => MapGenerator.Generate(db.mapGen, db.Calendar.MapLayerCount, db.nodeTypes, seed, db.Calendar.SeasonBossDays());

    [Test]
    public void FiveHundredSeedsAllValidate()
    {
        for (int seed = 0; seed < 500; seed++)
        {
            MapGraph g = Gen(seed);
            List<string> errors = MapValidator.Validate(g, db.mapGen, db.nodeTypes);
            Assert.IsEmpty(errors, $"seed {seed}: {string.Join("; ", errors)}");
        }
    }

    [Test]
    public void NewMapsNeverPlaceRestStopsOrElderCaterpillars()
    {
        for (int seed = 0; seed < 300; seed++)
            foreach (MapNode n in Gen(seed).nodes)
                Assert.IsFalse(n.nodeTypeId == "rest" || n.nodeTypeId == "elite", $"seed {seed}: node {n.id} is '{n.nodeTypeId}'");
    }

    [Test]
    public void RestAndEliteStayRegisteredSoOldSavesStillValidate()
    {
        Assert.IsTrue(db.nodeTypes.TryGet("rest", out _));
        Assert.IsTrue(db.nodeTypes.TryGet("elite", out _));
    }

    [Test]
    public void LayerCountMatchesCalendarPlusBossAndStructureIsSane()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            MapGraph g = Gen(seed);
            Assert.AreEqual(db.Calendar.MapLayerCount, g.layerCount);
            MapNode boss = g.Get(g.bossId);
            Assert.AreEqual(g.layerCount, boss.layer);
            Assert.AreEqual(1, g.NodesInLayer(g.layerCount).Count, "exactly one boss node");
            Assert.AreEqual(db.mapGen.bossType.Id, boss.nodeTypeId);

            foreach (MapNode n in g.NodesInLayer(0))
            {
                Assert.AreEqual("farm", n.nodeTypeId, "layer 0 is all farm days");
                Assert.IsTrue(g.IsReachable(n.id, g.bossId), "every start reaches the boss");
            }
            foreach (MapNode n in g.NodesInLayer(g.layerCount - 1))
                Assert.AreEqual("farm", n.nodeTypeId, "the day before the boss is a farm day");
        }
    }

    [Test]
    public void EverySeasonEndsInAMandatoryBossNightAndTheFinalBossIsOnTheLastDay()
    {
        List<int> bossDays = db.Calendar.SeasonBossDays();
        Assert.AreEqual(3, bossDays.Count);
        for (int seed = 0; seed < 100; seed++)
        {
            MapGraph g = Gen(seed);
            foreach (int layer in bossDays)
                foreach (MapNode n in g.NodesInLayer(layer))
                    Assert.AreEqual("season_boss", n.nodeTypeId, $"seed {seed}: layer {layer} must be a boss night");
            Assert.IsTrue(db.Calendar.DateOf(g.layerCount).IsLastDayOfSeason, "the final boss closes winter");
        }
    }

    [Test]
    public void NodeTypeRulesAreRespected()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            MapGraph g = Gen(seed);
            foreach (MapNode n in g.nodes)
            {
                db.nodeTypes.TryGet(n.nodeTypeId, out NodeTypeData t);
                Assert.IsNotNull(t, $"seed {seed}: node {n.id} has unknown type");
                if (n.layer < g.layerCount) Assert.GreaterOrEqual(n.layer, t.minLayer, $"seed {seed}: {t.Id} too early");
                if (t.noConsecutive)
                    foreach (int c in n.next) Assert.AreNotEqual(t.Id, g.Get(c).nodeTypeId, $"seed {seed}: consecutive {t.Id}");
            }
        }
    }

    [Test]
    public void SameSeedGivesIdenticalMapAndJsonRoundTrips()
    {
        MapGraph a = Gen(1234), b = Gen(1234), other = Gen(1235);
        string ja = JsonUtility.ToJson(a);
        Assert.AreEqual(ja, JsonUtility.ToJson(b));
        Assert.AreNotEqual(ja, JsonUtility.ToJson(other));

        MapGraph back = JsonUtility.FromJson<MapGraph>(ja);
        Assert.AreEqual(ja, JsonUtility.ToJson(back));
        Assert.IsEmpty(MapValidator.Validate(back, db.mapGen, db.nodeTypes));
    }

    [Test]
    public void ValidatorCatchesBrokenMaps()
    {
        MapGraph g = Gen(7);
        MapNode victim = g.NodesInLayer(1)[0];
        victim.next.Clear();
        Assert.IsNotEmpty(MapValidator.Validate(g, db.mapGen, db.nodeTypes), "dead end");

        MapGraph g2 = Gen(7);
        g2.bossId = -1;
        Assert.IsNotEmpty(MapValidator.Validate(g2, db.mapGen, db.nodeTypes), "missing boss");
    }

    [Test]
    public void GenerateValidatedReturnsAValidMap()
    {
        MapGraph g = MapGenerator.GenerateValidated(db.mapGen, db.Calendar.MapLayerCount, db.nodeTypes, 42, db.Calendar.SeasonBossDays());
        Assert.IsEmpty(MapValidator.Validate(g, db.mapGen, db.nodeTypes));
    }
}
