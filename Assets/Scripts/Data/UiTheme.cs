using UnityEngine;

/// <summary>Single owner of the UI's fonts, palette and shared textures (the Canva design). Every IMGUI screen reads it through <see cref="ImGuiKit.Theme"/>.</summary>
[CreateAssetMenu(fileName = "UiTheme", menuName = "MoFarmSand/UI Theme")]
public class UiTheme : ScriptableObject
{
    [Header("Fonts (a missing font falls back to Unity's default)")]
    public Font body;
    public Font bold;
    public Font italic;
    [Tooltip("Big headings and the DOGSEND logo. Beautiful Magic is personal-use only and git-ignored, so this can be empty on a fresh clone: it then falls back to Bold.")]
    public Font display;
    [Tooltip("Tick when the display font has real digits and '?'. The free trial of Beautiful Magic puts a watermark logo there, so headings draw those characters in Bold instead.")]
    public bool displayHasDigits;

    [Header("Palette")]
    public Color orange = new Color(0.933f, 0.675f, 0.345f);
    public Color cream = new Color(0.996f, 0.843f, 0.647f);
    public Color panel = new Color(0.925f, 0.922f, 0.914f);
    public Color slate = new Color(0.282f, 0.271f, 0.302f);
    public Color card = new Color(0.671f, 0.671f, 0.671f);
    public Color brown = new Color(0.482f, 0.337f, 0.204f);
    public Color ink = new Color(0.067f, 0.067f, 0.067f);

    [Header("Icons")]
    public Texture2D coin;
    public Texture2D piggyBank;
    public Texture2D bread;
    [Tooltip("The sun swirl next to grow times in the item cards.")]
    public Texture2D growIcon;
    public Texture2D heartFull;
    public Texture2D heartGone;
    [Tooltip("Drawn over a heart while a Bark Shield charge covers it.")]
    public Texture2D barkShield;
    public Texture2D dayNightDial;

    [Header("Full-screen art")]
    [Tooltip("Slate merchant panel with the notch at the top (1920x1080).")]
    public Texture2D merchantPanel;
    [Tooltip("Parchment scroll + inkwell for the route map legend (1920x1080, art in the bottom-right).")]
    public Texture2D legend;
    [Tooltip("Merchant silhouette for the random event screen (1920x1080, art on the right).")]
    public Texture2D eventSilhouette;
}
