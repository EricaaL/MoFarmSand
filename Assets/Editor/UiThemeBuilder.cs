using UnityEditor;
using UnityEngine;

/// <summary>
/// Wires the Canva UI design into the game: texture import settings for <c>Assets/Art/UI</c>, the <see cref="UiTheme"/> asset,
/// season and node-type icons, and <c>GameDatabase.uiTheme</c>. Additive and idempotent: it only fills references that are
/// still empty, so anything tuned by hand survives a re-run. Entry point for -executeMethod UiThemeBuilder.AddUiTheme.
/// </summary>
public static class UiThemeBuilder
{
    private const string Art = "Assets/Art/UI";
    private const string Fonts = Art + "/Fonts";
    private const string ThemePath = "Assets/Data/UI/UiTheme.asset";

    [MenuItem("MoFarmSand/Add UI Theme")]
    public static void AddUiTheme()
    {
        AssetDatabase.SaveAssets();
        ImportTextures();

        if (!AssetDatabase.IsValidFolder("Assets/Data/UI")) AssetDatabase.CreateFolder("Assets/Data", "UI");
        UiTheme theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UiTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
        }

        theme.body = Fill(theme.body, Load<Font>(Fonts + "/OpenSans-Regular.ttf"));
        theme.bold = Fill(theme.bold, Load<Font>(Fonts + "/OpenSans-Bold.ttf"));
        theme.italic = Fill(theme.italic, Load<Font>(Fonts + "/OpenSans-Italic.ttf"));
        // Personal-use font, git-ignored: absent on a fresh clone, in which case the theme falls back to Bold.
        theme.display = Fill(theme.display, Load<Font>(Fonts + "/BeautifulMagic.ttf"));

        theme.coin = Fill(theme.coin, Tex("coin"));
        theme.piggyBank = Fill(theme.piggyBank, Tex("piggybank"));
        theme.bread = Fill(theme.bread, Tex("Bread"));
        theme.growIcon = Fill(theme.growIcon, Tex("UI_icon_summer"));
        theme.heartFull = Fill(theme.heartFull, Tex("HPFull"));
        theme.heartGone = Fill(theme.heartGone, Tex("HPGone"));
        theme.dayNightDial = Fill(theme.dayNightDial, Tex("DayNightPanel"));
        theme.merchantPanel = Fill(theme.merchantPanel, Tex("MerchantShope"));
        theme.legend = Fill(theme.legend, Tex("Legend"));
        theme.eventSilhouette = Fill(theme.eventSilhouette, Tex("randomevent"));
        EditorUtility.SetDirty(theme);

        SetSeasonIcon("Spring", "UI_icon_spring", new Color(0.80f, 0.90f, 0.66f));
        SetSeasonIcon("Summer", "UI_icon_summer", new Color(0.92f, 0.73f, 0.49f));
        SetSeasonIcon("Autumn", "UI_icon_fall", new Color(0.88f, 0.62f, 0.44f));
        SetSeasonIcon("Winter", "UI_icon_winter", new Color(0.72f, 0.87f, 0.89f));

        SetNodeIcon("farm", "RouteMap_FarmDay", "Farm Day");
        SetNodeIcon("merchant", "RouteMap_Merchant", "Merchant Day");
        SetNodeIcon("disaster", "RouteMap_Disater", "Disaster");
        SetNodeIcon("event", "RouteMap_QuestionMark", "Random");
        SetNodeIcon("boss", "RouteMap_Boss", "???");
        SetNodeIcon("season_boss", "RouteMap_Boss", "???");

        GameDatabase db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Data/GameDatabase.asset");
        if (db != null)
        {
            if (db.uiTheme == null) db.uiTheme = theme;
            EditorUtility.SetDirty(db);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UiThemeBuilder] UI theme ready" + (theme.display == null ? " (Beautiful Magic font missing: headings fall back to Open Sans Bold)." : "."));
    }

    private static T Fill<T>(T current, T found) where T : Object => current != null ? current : found;
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    private static Texture2D Tex(string name) => Load<Texture2D>($"{Art}/{name}.png");

    /// <summary>UI art is drawn through IMGUI: no mips, clamped, uncompressed, alpha kept.</summary>
    private static void ImportTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Art }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(Fonts)) continue;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
            if (ti.mipmapEnabled) { ti.mipmapEnabled = false; dirty = true; }
            if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            if (ti.filterMode != FilterMode.Bilinear) { ti.filterMode = FilterMode.Bilinear; dirty = true; }
            if (ti.textureCompression != TextureImporterCompression.Uncompressed) { ti.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
    }

    private static void SetSeasonIcon(string season, string texture, Color mapColor)
    {
        SeasonData s = Load<SeasonData>($"Assets/Data/Seasons/Season_{season}.asset");
        if (s == null) return;
        if (s.icon == null) s.icon = Tex(texture);
        if (s.mapColor.a < 0.01f) s.mapColor = mapColor;
        EditorUtility.SetDirty(s);
    }

    private static void SetNodeIcon(string node, string texture, string legend)
    {
        NodeTypeData n = Load<NodeTypeData>($"Assets/Data/Map/Node_{node}.asset");
        if (n == null) return;
        if (n.icon == null) n.icon = Tex(texture);
        if (string.IsNullOrEmpty(n.legendLabel)) n.legendLabel = legend;
        EditorUtility.SetDirty(n);
    }
}
