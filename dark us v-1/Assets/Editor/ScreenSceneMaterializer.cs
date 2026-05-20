using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ScreenSceneMaterializer
{
    private const string SceneDirectory = "Assets/Scenes";
    private const string LoadingScenePath = SceneDirectory + "/LoadingScreen.unity";
    private const string RoleRevealScenePath = SceneDirectory + "/RoleRevealScreen.unity";
    private const string VictoryScenePath = SceneDirectory + "/VictoryScreen.unity";

    static ScreenSceneMaterializer()
    {
        EditorApplication.delayCall += MaterializeMissingScreenScenes;
    }

    [MenuItem("Tools/Dark Us/Materialize Screen Scenes")]
    public static void MaterializeScreenScenesFromMenu()
    {
        MaterializeScreenScenes(true);
    }

    private static void MaterializeMissingScreenScenes()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += MaterializeMissingScreenScenes;
            return;
        }

        if (File.Exists(LoadingScenePath) &&
            File.Exists(RoleRevealScenePath) &&
            File.Exists(VictoryScenePath))
        {
            EnsureBuildSettings();
            return;
        }

        MaterializeScreenScenes(false);
    }

    private static void MaterializeScreenScenes(bool overwrite)
    {
        Directory.CreateDirectory(SceneDirectory);

        if (overwrite || !File.Exists(LoadingScenePath))
        {
            CreateLoadingScene();
        }

        if (overwrite || !File.Exists(RoleRevealScenePath))
        {
            CreateRoleRevealScene();
        }

        if (overwrite || !File.Exists(VictoryScenePath))
        {
            CreateVictoryScene();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnsureBuildSettings();
    }

    private static void CreateLoadingScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = CreateCanvasRoot("LoadingScreen", 32000);
        root.AddComponent<DarkScanLoadingScreen>();

        RectTransform canvas = root.GetComponent<RectTransform>();
        CreateImage("Cinematic Backdrop", canvas, Stretch, new Color(0.002f, 0.004f, 0.006f, 1f));
        CreateShade("Top Cinematic Shade", canvas, true);
        CreateShade("Bottom Cinematic Shade", canvas, false);
        CreateGrid(canvas);

        TMP_Text title = CreateText("Mission Title", canvas, "DARK US", 44f, TextAlignmentOptions.Left, new Color(0.86f, 0.98f, 1f, 0.88f));
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -64f), new Vector2(420f, 58f), new Vector2(0.5f, 0.5f));
        TMP_Text operation = CreateText("Operation Text", canvas, "OPERATION // UNDERGROUND SIGNAL INTERCEPT", 18f, TextAlignmentOptions.Left, new Color(0.95f, 0.76f, 0.30f, 0.78f));
        SetRect(operation.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(76f, -104f), new Vector2(560f, 30f), new Vector2(0.5f, 0.5f));
        TMP_Text liveMap = CreateText("Build Text", canvas, "DARK SCAN / LIVE MAP", 18f, TextAlignmentOptions.TopRight, new Color(0.95f, 0.76f, 0.30f, 0.62f));
        SetRect(liveMap.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -44f), new Vector2(360f, 38f), new Vector2(0.5f, 0.5f));

        CreateRing("Scan Ring", canvas, new Vector2(690f, 690f), new Vector2(0f, 34f), new Color(0.55f, 0.95f, 1f, 0.12f));
        CreateRing("Middle Scan Ring", canvas, new Vector2(500f, 500f), new Vector2(0f, 34f), new Color(0.18f, 0.55f, 0.68f, 0.16f));
        CreateRing("Inner Ring", canvas, new Vector2(290f, 290f), new Vector2(0f, 34f), new Color(1f, 0.76f, 0.24f, 0.12f));
        CreateImage("Scan Core", canvas, r => SetRect(r, Center, Center, new Vector2(0f, 34f), new Vector2(42f, 42f), Center), new Color(0.45f, 0.95f, 1f, 0.12f));

        RectTransform leftPanel = CreatePanel("System Panel", canvas, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, -76f), new Vector2(430f, 230f), new Vector2(0f, 0.5f));
        RectTransform rightPanel = CreatePanel("Telemetry Panel", canvas, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-72f, -44f), new Vector2(410f, 210f), new Vector2(1f, 0.5f));
        CreatePanelHeader("System Header", leftPanel, "SYSTEM EVENTS");
        CreatePanelHeader("Telemetry Header", rightPanel, "DEPLOYMENT TELEMETRY");
        TMP_Text log = CreateText("System Log", leftPanel, "", 18f, TextAlignmentOptions.BottomLeft, new Color(0.70f, 0.88f, 0.86f, 0.72f));
        StretchWithOffset(log.rectTransform, new Vector2(24f, 24f), new Vector2(-24f, -52f));
        TMP_Text telemetry = CreateText("Telemetry", rightPanel, "", 18f, TextAlignmentOptions.TopLeft, new Color(0.72f, 0.94f, 0.98f, 0.78f));
        StretchWithOffset(telemetry.rectTransform, new Vector2(24f, 28f), new Vector2(-24f, -58f));

        TMP_Text status = CreateText("Status", canvas, "SCANNING AREA...", 30f, TextAlignmentOptions.Center, new Color(0.82f, 0.97f, 1f, 0.94f));
        status.fontStyle = FontStyles.Bold;
        SetRect(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 116f), new Vector2(820f, 46f), Center);
        RectTransform track = CreateImage("Progress Track", canvas, r => SetRect(r, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 86f), new Vector2(1040f, 12f), Center), new Color(0.27f, 0.36f, 0.37f, 0.34f)).rectTransform;
        CreateImage("Progress Glow", track, Stretch, new Color(0.42f, 0.95f, 1f, 0.20f));
        CreateImage("Progress Fill", track, Stretch, new Color(0.54f, 0.92f, 1f, 0.82f));
        TMP_Text percent = CreateText("Progress Percent", canvas, "000%", 22f, TextAlignmentOptions.Right, new Color(0.95f, 0.76f, 0.30f, 0.92f));
        SetRect(percent.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(600f, 76f), new Vector2(130f, 32f), Center);
        TMP_Text phase = CreateText("Phase", canvas, "PHASE 000", 22f, TextAlignmentOptions.Left, new Color(0.52f, 0.95f, 1f, 0.84f));
        SetRect(phase.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-618f, 76f), new Vector2(210f, 32f), Center);
        TMP_Text tip = CreateText("Tip Text", canvas, "KEEP YOUR LIGHT LOW. TRUST THE DOTS.", 18f, TextAlignmentOptions.Center, new Color(0.86f, 0.92f, 0.88f, 0.58f));
        SetRect(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(860f, 30f), Center);

        EditorSceneManager.SaveScene(scene, LoadingScenePath);
    }

    private static void CreateRoleRevealScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = CreateCanvasRoot("RoleRevealScreen", 32100);
        root.AddComponent<RoleRevealIntro>();
        root.AddComponent<AudioSource>().playOnAwake = false;
        RectTransform canvas = root.GetComponent<RectTransform>();

        CreateImage("Blackout", canvas, Stretch, new Color(0f, 0f, 0f, 0.96f));
        CreateRing("Pulse Ring", canvas, new Vector2(620f, 620f), Vector2.zero, new Color(0.45f, 0.95f, 1f, 0.12f));
        TMP_Text title = CreateText("Title", canvas, "ROLE", 26f, TextAlignmentOptions.Center, new Color(0.80f, 0.88f, 0.88f, 0.82f));
        SetRect(title.rectTransform, Center, Center, new Vector2(0f, 110f), new Vector2(640f, 42f), Center);
        TMP_Text role = CreateText("Role", canvas, "CITIZEN", 72f, TextAlignmentOptions.Center, new Color(0.58f, 0.95f, 1f, 1f));
        SetRect(role.rectTransform, Center, Center, Vector2.zero, new Vector2(900f, 110f), Center);
        TMP_Text hint = CreateText("Hint", canvas, "FIND TARGET COMPUTERS", 18f, TextAlignmentOptions.Center, new Color(0.95f, 0.74f, 0.28f, 0.82f));
        SetRect(hint.rectTransform, Center, Center, new Vector2(0f, -94f), new Vector2(760f, 36f), Center);

        EditorSceneManager.SaveScene(scene, RoleRevealScenePath);
    }

    private static void CreateVictoryScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = CreateCanvasRoot("VictoryScreen", 32200);
        root.AddComponent<VictoryScreen>();
        RectTransform canvas = root.GetComponent<RectTransform>();

        CreateImage("Blackout", canvas, Stretch, new Color(0f, 0f, 0f, 0.88f));
        CreateGrid(canvas);
        CreateRing("Pulse Ring", canvas, new Vector2(720f, 720f), Vector2.zero, new Color(0.54f, 0.95f, 1f, 0.16f));
        TMP_Text subtitle = CreateText("Subtitle", canvas, "EXTRACTION CONFIRMED", 20f, TextAlignmentOptions.Center, new Color(0.54f, 0.95f, 1f, 1f));
        SetRect(subtitle.rectTransform, Center, Center, new Vector2(0f, 122f), new Vector2(780f, 36f), Center);
        TMP_Text title = CreateText("Title", canvas, "CITIZENS WIN", 78f, TextAlignmentOptions.Center, new Color(0.54f, 0.95f, 1f, 1f));
        title.fontStyle = FontStyles.Bold;
        SetRect(title.rectTransform, Center, Center, new Vector2(0f, 28f), new Vector2(980f, 118f), Center);
        CreateImage("Accent Bar", canvas, r => SetRect(r, Center, Center, new Vector2(0f, -46f), new Vector2(460f, 4f), Center), new Color(0.54f, 0.95f, 1f, 1f));
        TMP_Text reason = CreateText("Reason", canvas, "CITIZENS ESCAPED", 28f, TextAlignmentOptions.Center, new Color(0.86f, 0.92f, 0.92f, 0.90f));
        SetRect(reason.rectTransform, Center, Center, new Vector2(0f, -92f), new Vector2(820f, 46f), Center);
        TMP_Text timer = CreateText("Return Timer", canvas, "RETURNING TO LOBBY IN 08", 20f, TextAlignmentOptions.Center, new Color(0.95f, 0.76f, 0.30f, 0.86f));
        SetRect(timer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 82f), new Vector2(620f, 36f), Center);

        EditorSceneManager.SaveScene(scene, VictoryScenePath);
    }

    private static GameObject CreateCanvasRoot(string name, int sortingOrder)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Stretch(root.GetComponent<RectTransform>());
        return root;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
    {
        Image panel = CreateImage(name, parent, r => SetRect(r, anchorMin, anchorMax, position, size, pivot), new Color(0.006f, 0.012f, 0.014f, 0.56f));
        RectTransform rect = panel.rectTransform;
        CreateImage(name + " Top", rect, r => SetOffsets(r, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -2f), Vector2.zero), new Color(0.44f, 0.92f, 1f, 0.18f));
        CreateImage(name + " Bottom", rect, r => SetOffsets(r, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f)), new Color(0.44f, 0.92f, 1f, 0.18f));
        return rect;
    }

    private static void CreatePanelHeader(string name, Transform parent, string text)
    {
        TMP_Text header = CreateText(name, parent, text, 16f, TextAlignmentOptions.Left, new Color(0.95f, 0.76f, 0.30f, 0.80f));
        SetOffsets(header.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -42f), new Vector2(-24f, -14f));
    }

    private static void CreateGrid(Transform parent)
    {
        for (int i = -9; i <= 9; i++)
        {
            CreateImage("Grid Vertical " + i, parent, r => SetRect(r, Center, Center, new Vector2(i * 104f, 0f), new Vector2(1f, 1080f), Center), new Color(0.22f, 0.78f, 0.92f, 0.045f));
        }

        for (int i = -5; i <= 5; i++)
        {
            CreateImage("Grid Horizontal " + i, parent, r => SetRect(r, Center, Center, new Vector2(0f, i * 104f), new Vector2(1920f, 1f), Center), new Color(0.22f, 0.78f, 0.92f, 0.035f));
        }
    }

    private static void CreateShade(string name, Transform parent, bool top)
    {
        if (top)
        {
            CreateImage(name, parent, r => SetOffsets(r, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -132f), Vector2.zero), new Color(0f, 0f, 0f, 0.50f));
        }
        else
        {
            CreateImage(name, parent, r => SetOffsets(r, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 162f)), new Color(0f, 0f, 0f, 0.54f));
        }
    }

    private static void CreateRing(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        Image ring = CreateImage(name, parent, r => SetRect(r, Center, Center, position, size, Center), color);
        ring.sprite = CreateRingSprite();
    }

    private static Image CreateImage(string name, Transform parent, System.Action<RectTransform> layout, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        layout(obj.GetComponent<RectTransform>());
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Shadow));
        obj.transform.SetParent(parent, false);
        TMP_Text tmp = obj.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        LocalizedTmpFontProvider.Apply(tmp);
        Shadow shadow = obj.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.90f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return tmp;
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.InverseLerp(49f, 51f, distance) * (1f - Mathf.InverseLerp(56f, 59f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), Center, 100f);
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        AddBuildSceneIfMissing(scenes, LoadingScenePath);
        AddBuildSceneIfMissing(scenes, RoleRevealScenePath);
        AddBuildSceneIfMissing(scenes, VictoryScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void AddBuildSceneIfMissing(List<EditorBuildSettingsScene> scenes, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == path)
            {
                scenes[i].enabled = true;
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void SetOffsets(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect)
    {
        SetOffsets(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void StretchWithOffset(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        SetOffsets(rect, Vector2.zero, Vector2.one, offsetMin, offsetMax);
    }

    private static Vector2 Center => new Vector2(0.5f, 0.5f);
}
