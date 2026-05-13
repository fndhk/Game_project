using System.Collections;
using ArtNotes.UndergroundLaboratoryGenerator;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DarkScanLoadingScreen : MonoBehaviour
{
    private const float MinimumVisibleTime = 1.25f;

    private static bool sceneHooked;
    private static DarkScanLoadingScreen activeInstance;

    private CanvasGroup canvasGroup;
    private TMP_Text statusText;
    private TMP_Text logText;
    private TMP_Text percentText;
    private TMP_Text phaseText;
    private RectTransform progressFill;
    private RectTransform sweepLine;
    private RectTransform horizontalSweepLine;
    private Image scanRing;
    private Image innerRing;
    private GraphicRaycaster graphicRaycaster;
    private ScanDotsGraphic scanDots;

    private float targetProgress = 0.08f;
    private float displayProgress = 0.08f;
    private float visibleStartedAt;
    private bool finishing;
    private bool destroyRequested;
    private bool waitForGameSceneBeforeFallback;
    private string initialMessage = "SCANNING AREA...";

    public static void ShowImmediate(string message = "MATCH LOCKED...")
    {
        EnsureSceneHooked();

        if (activeInstance != null)
        {
            activeInstance.Configure(message, true);
            return;
        }

        GameObject root = new GameObject("Dark Scan Loading Screen");
        DontDestroyOnLoad(root);
        DarkScanLoadingScreen screen = root.AddComponent<DarkScanLoadingScreen>();
        screen.Configure(message, true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureSceneHooked();
        TryCreateForScene(SceneManager.GetActiveScene());
    }

    private static void EnsureSceneHooked()
    {
        if (sceneHooked)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        sceneHooked = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForScene(scene);
    }

    private static void TryCreateForScene(Scene scene)
    {
        if (activeInstance != null || FindObjectOfType<DarkScanLoadingScreen>() != null)
        {
            return;
        }

        if (!ShouldShowForScene(scene))
        {
            return;
        }

        GameObject root = new GameObject("Dark Scan Loading Screen");
        DontDestroyOnLoad(root);
        root.AddComponent<DarkScanLoadingScreen>();
    }

    private static bool ShouldShowForScene(Scene scene)
    {
        string sceneName = scene.name;

        if (sceneName == "LobbyScene" ||
            sceneName == "LobbyScene 1" ||
            sceneName == "CreateRoomLobbyScene" ||
            sceneName == "PublicRoomListScene")
        {
            return false;
        }

        if (sceneName == "labor" || sceneName == "GameScene")
        {
            return true;
        }

        return FindObjectOfType<LaboratoryGenerator>(true) != null;
    }

    private void Awake()
    {
        activeInstance = this;
        BuildUi();
        visibleStartedAt = Time.unscaledTime;
        LaboratoryGenerator.LoadingPhaseChanged += HandleLoadingPhaseChanged;
        LaboratoryGenerator.GenerationFinished += HandleGenerationFinished;
    }

    private void Start()
    {
        StartCoroutine(WatchGenerationState());
    }

    private void OnDestroy()
    {
        StopAllCoroutines();

        if (activeInstance == this)
        {
            activeInstance = null;
        }

        LaboratoryGenerator.LoadingPhaseChanged -= HandleLoadingPhaseChanged;
        LaboratoryGenerator.GenerationFinished -= HandleGenerationFinished;
    }

    private void Configure(string message, bool waitForGameScene)
    {
        waitForGameSceneBeforeFallback = waitForGameScene;
        initialMessage = string.IsNullOrEmpty(message) ? initialMessage : message;

        if (statusText != null)
        {
            statusText.text = initialMessage;
            AppendLog(initialMessage);
        }
    }

    private void Update()
    {
        if (destroyRequested ||
            canvasGroup == null ||
            progressFill == null ||
            sweepLine == null ||
            horizontalSweepLine == null ||
            scanRing == null ||
            innerRing == null)
        {
            return;
        }

        displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.unscaledDeltaTime * 0.75f);
        progressFill.anchorMax = new Vector2(Mathf.Clamp01(displayProgress), 1f);
        if (percentText != null)
        {
            percentText.text = Mathf.RoundToInt(displayProgress * 100f).ToString("000") + "%";
        }

        float pulse = Mathf.PingPong(Time.unscaledTime * 0.75f, 1f);
        scanRing.color = new Color(0.55f, 0.95f, 1f, Mathf.Lerp(0.08f, 0.24f, pulse));
        scanRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.08f, pulse);
        innerRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * -18f);

        float sweepX = Mathf.Lerp(-430f, 430f, Mathf.PingPong(Time.unscaledTime * 0.32f, 1f));
        sweepLine.anchoredPosition = new Vector2(sweepX, 0f);
        float sweepY = Mathf.Lerp(-260f, 260f, Mathf.PingPong(Time.unscaledTime * 0.24f, 1f));
        horizontalSweepLine.anchoredPosition = new Vector2(0f, sweepY);

        if (scanDots != null && scanDots.isActiveAndEnabled)
        {
            scanDots.AnimationTime = Time.unscaledTime;
            scanDots.Progress = displayProgress;
            scanDots.MarkDirtySafely();
        }
    }

    private IEnumerator WatchGenerationState()
    {
        yield return null;

        while (waitForGameSceneBeforeFallback &&
               !ShouldShowForScene(SceneManager.GetActiveScene()) &&
               FindObjectOfType<LaboratoryGenerator>(true) == null)
        {
            yield return null;
        }

        LaboratoryGenerator generator = FindObjectOfType<LaboratoryGenerator>(true);

        if (generator == null)
        {
            HandleLoadingPhaseChanged("ENTERING DARKNESS...", 1f);
            yield return new WaitForSecondsRealtime(MinimumVisibleTime);
            StartCoroutine(FadeOutAndDestroy());
            yield break;
        }

        while (!generator.IsGenerationComplete && LaboratoryGenerator.IsAnyGenerationRunning)
        {
            yield return null;
        }

        if (!finishing && generator.IsGenerationComplete)
        {
            HandleGenerationFinished();
        }
    }

    private void HandleLoadingPhaseChanged(string message, float progress)
    {
        if (destroyRequested)
        {
            return;
        }

        targetProgress = Mathf.Max(targetProgress, progress);
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (phaseText != null)
        {
            phaseText.text = "PHASE " + Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f).ToString("000");
        }

        AppendLog(message);
    }

    private void HandleGenerationFinished()
    {
        if (finishing)
        {
            return;
        }

        finishing = true;
        targetProgress = 1f;
        ReleaseInputBlocking();

        if (statusText != null)
        {
            statusText.text = "ENTERING DARKNESS...";
        }

        AppendLog("SCAN READY");
        StartCoroutine(FadeOutWhenReady());
    }

    private IEnumerator FadeOutWhenReady()
    {
        float elapsed = Time.unscaledTime - visibleStartedAt;
        if (elapsed < MinimumVisibleTime)
        {
            yield return new WaitForSecondsRealtime(MinimumVisibleTime - elapsed);
        }

        yield return new WaitForSecondsRealtime(0.35f);
        yield return FadeOutAndDestroy();
    }

    private IEnumerator FadeOutAndDestroy()
    {
        ReleaseInputBlocking();

        float duration = 0.7f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
            }

            yield return null;
        }

        destroyRequested = true;
        ReleaseInputBlocking();
        DisableGraphicsBeforeDestroy();
        RoleRevealIntro.ShowWhenReady();
        Destroy(gameObject);
    }

    private void AppendLog(string message)
    {
        if (logText == null || destroyRequested)
        {
            return;
        }

        string line = "> " + message;
        string current = logText.text;

        if (string.IsNullOrEmpty(current))
        {
            logText.text = line;
            return;
        }

        string[] lines = current.Split('\n');
        if (lines.Length >= 4)
        {
            logText.text = lines[1] + "\n" + lines[2] + "\n" + lines[3] + "\n" + line;
        }
        else
        {
            logText.text = current + "\n" + line;
        }
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
        graphicRaycaster.enabled = false;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        RectTransform root = canvas.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image background = CreateImage("Blackout", root, new Color(0.003f, 0.004f, 0.005f, 1f));
        Stretch(background.rectTransform);

        scanDots = CreateGraphic<ScanDotsGraphic>("Scan Dots", root);
        Stretch(scanDots.rectTransform);
        scanDots.color = Color.white;

        scanRing = CreateImage("Scan Ring", root, new Color(0.55f, 0.95f, 1f, 0.08f));
        scanRing.sprite = CreateRingSprite();
        scanRing.type = Image.Type.Simple;
        scanRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scanRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scanRing.rectTransform.sizeDelta = new Vector2(520f, 520f);
        scanRing.rectTransform.anchoredPosition = Vector2.zero;

        innerRing = CreateImage("Inner Ring", root, new Color(1f, 0.76f, 0.24f, 0.10f));
        innerRing.sprite = CreateRingSprite();
        innerRing.type = Image.Type.Simple;
        innerRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        innerRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        innerRing.rectTransform.sizeDelta = new Vector2(330f, 330f);
        innerRing.rectTransform.anchoredPosition = Vector2.zero;

        sweepLine = CreateImage("Sweep Line", root, new Color(0.45f, 0.95f, 1f, 0.16f)).rectTransform;
        sweepLine.anchorMin = new Vector2(0.5f, 0.5f);
        sweepLine.anchorMax = new Vector2(0.5f, 0.5f);
        sweepLine.sizeDelta = new Vector2(2f, 620f);
        sweepLine.anchoredPosition = Vector2.zero;

        horizontalSweepLine = CreateImage("Horizontal Sweep Line", root, new Color(1f, 0.76f, 0.24f, 0.10f)).rectTransform;
        horizontalSweepLine.anchorMin = new Vector2(0.5f, 0.5f);
        horizontalSweepLine.anchorMax = new Vector2(0.5f, 0.5f);
        horizontalSweepLine.sizeDelta = new Vector2(720f, 2f);
        horizontalSweepLine.anchoredPosition = Vector2.zero;

        RectTransform centerPanel = CreateImage("Center Panel", root, new Color(0.006f, 0.010f, 0.012f, 0.46f)).rectTransform;
        centerPanel.anchorMin = new Vector2(0.5f, 0.5f);
        centerPanel.anchorMax = new Vector2(0.5f, 0.5f);
        centerPanel.sizeDelta = new Vector2(760f, 190f);
        centerPanel.anchoredPosition = new Vector2(0f, -255f);

        statusText = CreateText("Status", root, "SCANNING AREA...", 28, TextAlignmentOptions.Center);
        statusText.color = new Color(0.78f, 0.92f, 0.94f, 0.92f);
        statusText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        statusText.rectTransform.sizeDelta = new Vector2(720f, 44f);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, 158f);

        RectTransform progressRoot = CreateImage("Progress Track", root, new Color(0.35f, 0.42f, 0.42f, 0.25f)).rectTransform;
        progressRoot.anchorMin = new Vector2(0.5f, 0f);
        progressRoot.anchorMax = new Vector2(0.5f, 0f);
        progressRoot.sizeDelta = new Vector2(640f, 5f);
        progressRoot.anchoredPosition = new Vector2(0f, 124f);

        progressFill = CreateImage("Progress Fill", progressRoot, new Color(0.54f, 0.92f, 1f, 0.75f)).rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(displayProgress, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;

        percentText = CreateText("Progress Percent", root, "000%", 18, TextAlignmentOptions.Right);
        percentText.color = new Color(0.95f, 0.76f, 0.30f, 0.86f);
        percentText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        percentText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        percentText.rectTransform.sizeDelta = new Vector2(110f, 28f);
        percentText.rectTransform.anchoredPosition = new Vector2(380f, 118f);

        phaseText = CreateText("Phase", root, "PHASE 000", 18, TextAlignmentOptions.Left);
        phaseText.color = new Color(0.52f, 0.95f, 1f, 0.72f);
        phaseText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        phaseText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        phaseText.rectTransform.sizeDelta = new Vector2(180f, 28f);
        phaseText.rectTransform.anchoredPosition = new Vector2(-406f, 118f);

        logText = CreateText("System Log", root, string.Empty, 18, TextAlignmentOptions.BottomLeft);
        logText.color = new Color(0.66f, 0.74f, 0.70f, 0.58f);
        logText.rectTransform.anchorMin = new Vector2(0f, 0f);
        logText.rectTransform.anchorMax = new Vector2(0f, 0f);
        logText.rectTransform.sizeDelta = new Vector2(520f, 130f);
        logText.rectTransform.anchoredPosition = new Vector2(42f, 48f);

        TMP_Text titleText = CreateText("Title", root, "DARK SCAN", 20, TextAlignmentOptions.TopRight);
        titleText.color = new Color(0.95f, 0.76f, 0.30f, 0.62f);
        titleText.rectTransform.anchorMin = new Vector2(1f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.sizeDelta = new Vector2(240f, 38f);
        titleText.rectTransform.anchoredPosition = new Vector2(-38f, -34f);
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = CreateGraphic<Image>(name, parent);
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.fontStyle = FontStyles.UpperCase;
        return tmp;
    }

    private static T CreateGraphic<T>(string name, Transform parent) where T : Graphic
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.AddComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        const float innerRadius = 50f;
        const float outerRadius = 54f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.InverseLerp(innerRadius, innerRadius + 1.5f, distance) *
                              (1f - Mathf.InverseLerp(outerRadius - 1.5f, outerRadius, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void ReleaseInputBlocking()
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (graphicRaycaster != null)
        {
            graphicRaycaster.enabled = false;
        }
    }

    private void DisableGraphicsBeforeDestroy()
    {
        if (scanDots != null)
        {
            scanDots.enabled = false;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
                graphics[i].enabled = false;
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private class ScanDotsGraphic : Graphic
    {
        private const int DotCount = 140;
        private readonly Vector2[] dotPositions = new Vector2[DotCount];
        private readonly float[] dotSeeds = new float[DotCount];

        public float AnimationTime { get; set; }
        public float Progress { get; set; }

        public void MarkDirtySafely()
        {
            if (!isActiveAndEnabled || canvasRenderer == null)
            {
                return;
            }

            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < DotCount; i++)
            {
                dotPositions[i] = new Vector2(Random.value, Random.value);
                dotSeeds[i] = Random.Range(0f, 100f);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float reveal = Mathf.Clamp01(Progress + 0.18f);

            for (int i = 0; i < DotCount; i++)
            {
                Vector2 normalized = dotPositions[i];

                if (normalized.x > reveal)
                {
                    continue;
                }

                float flicker = Mathf.PerlinNoise(dotSeeds[i], AnimationTime * 1.7f);
                float alpha = Mathf.Lerp(0.03f, 0.42f, flicker);

                if (flicker < 0.18f)
                {
                    continue;
                }

                float size = Mathf.Lerp(1.2f, 3.8f, Mathf.PerlinNoise(dotSeeds[i], dotSeeds[i] * 0.37f));
                Vector2 point = new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, normalized.x),
                    Mathf.Lerp(rect.yMin, rect.yMax, normalized.y));

                Color32 dotColor = new Color(0.62f, 0.94f, 1f, alpha);

                if (i % 19 == 0)
                {
                    dotColor = new Color(1f, 0.72f, 0.24f, alpha * 0.8f);
                }

                AddQuad(vh, point, size, dotColor);
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 center, float size, Color32 color)
        {
            int startIndex = vh.currentVertCount;
            float half = size * 0.5f;

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector3(center.x - half, center.y - half);
            vh.AddVert(vertex);
            vertex.position = new Vector3(center.x - half, center.y + half);
            vh.AddVert(vertex);
            vertex.position = new Vector3(center.x + half, center.y + half);
            vh.AddVert(vertex);
            vertex.position = new Vector3(center.x + half, center.y - half);
            vh.AddVert(vertex);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}
