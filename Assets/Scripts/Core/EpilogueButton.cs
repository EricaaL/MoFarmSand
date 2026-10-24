using UnityEngine;

/// <summary>Capture-only: a corner button that plays the trailer epilogue, for the trailer scene. Hides itself for good once it has been pressed.</summary>
public class EpilogueButton : MonoBehaviour
{
    [SerializeField] private TrailerEpilogue epilogue;
    [SerializeField] private string label = "Play Cutscene";
    [SerializeField] private Vector2 size = new Vector2(220f, 56f);

    private void OnGUI()
    {
        if (epilogue == null || epilogue.HasPlayed) return;
        GUI.depth = -100;
        float s = Mathf.Max(0.5f, Screen.height / 1080f);
        Rect r = new Rect(24f * s, 24f * s, size.x * s, size.y * s);
        GUIStyle style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(22f * s), fontStyle = FontStyle.Bold };
        if (GUI.Button(r, label, style)) epilogue.Play();
    }
}
