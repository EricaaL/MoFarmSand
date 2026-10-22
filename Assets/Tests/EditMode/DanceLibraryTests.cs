using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DanceLibraryTests
{
    private DanceLibrary library;
    private readonly List<DanceSlot> playable = new List<DanceSlot>();

    [SetUp]
    public void SetUp() => library = ScriptableObject.CreateInstance<DanceLibrary>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(library);

    private static DanceSlot Slot(bool withClip) => new DanceSlot { clip = withClip ? new AnimationClip() : null };

    [Test]
    public void SlotsWithoutAClipAreSkipped()
    {
        library.slots.Add(Slot(true));
        library.slots.Add(Slot(false));
        library.slots.Add(null);
        library.slots.Add(Slot(true));

        library.CollectPlayable(playable);

        Assert.AreEqual(2, playable.Count);
        Assert.AreSame(library.slots[0], playable[0]);
        Assert.AreSame(library.slots[3], playable[1]);
    }

    [Test]
    public void PlayableSlotsNeverExceedTheMaximum()
    {
        for (int i = 0; i < DanceLibrary.MaxSlots + 4; i++) library.slots.Add(Slot(true));

        library.CollectPlayable(playable);

        Assert.AreEqual(DanceLibrary.MaxSlots, playable.Count);
    }

    [Test]
    public void CollectingReplacesThePreviousResult()
    {
        library.slots.Add(Slot(true));
        playable.Add(Slot(true));
        playable.Add(Slot(true));

        library.CollectPlayable(playable);

        Assert.AreEqual(1, playable.Count);
    }
}
