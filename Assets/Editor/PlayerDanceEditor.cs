using UnityEditor;

/// <summary>Shows the dance library's slot panel on the player, so slots can be edited right from the prefab.</summary>
[CustomEditor(typeof(PlayerDance))]
public class PlayerDanceEditor : Editor
{
    private Editor libraryEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DanceLibrary library = ((PlayerDance)target).Library;
        if (library == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Emote wheel dances (edits the DanceLibrary asset)", EditorStyles.boldLabel);
        CreateCachedEditor(library, typeof(DanceLibraryEditor), ref libraryEditor);
        libraryEditor.OnInspectorGUI();
    }

    private void OnDisable()
    {
        if (libraryEditor != null) DestroyImmediate(libraryEditor);
    }
}
