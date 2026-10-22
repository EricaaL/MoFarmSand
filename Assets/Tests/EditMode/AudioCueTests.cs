using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AudioCueTests
{
    [Test]
    public void ACueWithNoClipsIsSilent()
    {
        AudioCue cue = ScriptableObject.CreateInstance<AudioCue>();
        Assert.IsTrue(cue.IsEmpty);
        Assert.IsNull(cue.Pick());

        cue.clips = new AudioClip[] { null, null };
        Assert.IsTrue(cue.IsEmpty, "null entries don't count as clips");
        Object.DestroyImmediate(cue);
    }

    [Test]
    public void PitchStaysInsideTheRangeEvenWhenItIsEnteredBackwards()
    {
        AudioCue cue = ScriptableObject.CreateInstance<AudioCue>();
        cue.pitchRange = new Vector2(1.2f, 0.8f);
        for (int i = 0; i < 50; i++) Assert.That(cue.RandomPitch(), Is.InRange(0.8f, 1.2f));
        Object.DestroyImmediate(cue);
    }

    [Test]
    public void EveryCueInTheAudioLibraryIsAssigned()
    {
        AudioLibrary library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/AudioLibrary.asset");
        Assert.IsNotNull(library, "run MoFarmSand > Add Demo Polish to create the audio library");
        foreach (System.Reflection.FieldInfo field in typeof(AudioLibrary).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (field.FieldType == typeof(AudioCue)) Assert.IsNotNull(field.GetValue(library), $"AudioLibrary.{field.Name} has no cue");
        }
    }
}
