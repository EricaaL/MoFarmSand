using UnityEditor;
using UnityEngine;

/// <summary>Creates the Credits asset (once) and links it to the GameDatabase. Additive and idempotent: an existing asset keeps its Inspector edits.</summary>
public static class CreditsBuilder
{
    private const string Path = "Assets/Data/UI/Credits.asset";

    [MenuItem("MoFarmSand/Add Credits")]
    public static void AddCredits()
    {
        CreditsData credits = AssetDatabase.LoadAssetAtPath<CreditsData>(Path);
        if (credits == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/UI")) AssetDatabase.CreateFolder("Assets/Data", "UI");
            credits = ScriptableObject.CreateInstance<CreditsData>();
            Fill(credits);
            AssetDatabase.CreateAsset(credits, Path);
        }

        GameDatabase db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
        if (db != null)
        {
            if (db.credits != credits) { db.credits = credits; EditorUtility.SetDirty(db); }
        }
        else Debug.LogError("[CreditsBuilder] GameDatabase.asset not found; credits are not linked.");

        AssetDatabase.SaveAssets();
    }

    private static void Fill(CreditsData c)
    {
        c.sections.Add(Section("MoFarmSand",
            ("Producer", "Alley"),
            ("Art Director", "Erica"),
            ("Technical Artist", "Erica"),
            ("Concept Artist", "Elisha"),
            ("Storyboard Artist", "Elisha"),
            ("Post Production Editor", "Alley")));

        c.sections.Add(Section("3D Art and Environment",
            ("Poly Universal Pack", "Polyperfect, Unity Asset Store"),
            ("Low Poly Trees Pack", "LMHPOLY, Unity Asset Store"),
            ("Low Poly Fantasy Bundle", "JustCreate, Unity Asset Store")));

        c.sections.Add(Section("Sky and Weather",
            ("COZY: Stylized Weather 3", "Distant Lands")));

        c.sections.Add(Section("Animation",
            ("Mixamo", "Adobe. Character, dance and breakdance animations")));

        c.sections.Add(Section("Rigging",
            ("Mixamo", "Adobe. Character rigging")));

        c.sections.Add(Section("Music",
            ("Ukulele de Chocobo", "SE Music"),
            ("Enigmatic", "Benjamin Tissot"),
            ("Evolution", "Benjamin Tissot")));

        c.sections.Add(Section("Fonts",
            ("Open Sans", "Steve Matteson, SIL Open Font License"),
            ("Beautiful Magic", "Headings and the DOGSEND logo (personal-use license)")));
    }

    private static CreditsData.Section Section(string heading, params (string name, string detail)[] lines)
    {
        var section = new CreditsData.Section { heading = heading };
        foreach ((string name, string detail) in lines) section.lines.Add(new CreditsData.Line { name = name, detail = detail });
        return section;
    }
}
