using System.Collections;
using ArtNotes.UndergroundLaboratoryGenerator;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DarkScanLoadingScreen : MonoBehaviour
{
    private const float MinimumVisibleTime = 1.25f;
    private const string LoadingScreenSceneName = "LoadingScreen";

    private static bool sceneHooked;
    private static DarkScanLoadingScreen activeInstance;
    private static bool sceneLoadRequested;
    private static string pendingMessage;
    private static bool pendingWaitForGameScene;

    private CanvasGroup canvasGroup;
    private TMP_Text statusText;
    private TMP_Text logText;
    private TMP_Text percentText;
    private TMP_Text phaseText;
    private TMP_Text titleText;
    private TMP_Text telemetryText;
    private TMP_Text tipText;
    private RectTransform progressFill;
    private RectTransform progressGlow;
    private Image scanRing;
    private Image innerRing;
    private GraphicRaycaster graphicRaycaster;
    private LoadingBackdropGraphic backdrop;
    private ScanDotsGraphic scanDots;
    private ProgressSegmentsGraphic progressSegments;

    private float targetProgress = 0.08f;
    private float displayProgress = 0.08f;
    private float visibleStartedAt;
    private bool finishing;
    private bool generationFailed;
    private bool destroyRequested;
    private bool waitForGameSceneBeforeFallback;
    private string initialMessage = "SCANNING AREA...";

    public static bool IsShowing => activeInstance != null;

    public static void ShowImmediate(string message = "MATCH LOCKED...", bool waitForGameScene = true)
    {
        EnsureSceneHooked();
        GameplayStartupGate.SetLoadingScreenBlocked(true);
        pendingMessage = string.IsNullOrEmpty(message) ? "MATCH LOCKED..." : message;
        pendingWaitForGameScene = waitForGameScene;

        if (activeInstance != null)
        {
            activeInstance.Configure(pendingMessage, waitForGameScene);
            return;
        }

        if (!sceneLoadRequested && Application.CanStreamedLevelBeLoaded(LoadingScreenSceneName))
        {
            sceneLoadRequested = true;
            SceneManager.LoadSceneAsync(LoadingScreenSceneName, LoadSceneMode.Additive);
            return;
        }

        GameObject root = new GameObject("Dark Scan Loading Screen");
        DontDestroyOnLoad(root);
        DarkScanLoadingScreen screen = root.AddComponent<DarkScanLoadingScreen>();
        screen.Configure(pendingMessage, waitForGameScene);
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
        if (GameplayStartupGate.IsMenuScene(scene.name))
        {
            ForceHideImmediate();
            return;
        }

        if (activeInstance != null || Object.FindAnyObjectByType<DarkScanLoadingScreen>() != null)
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

    public static void ForceHideImmediate()
    {
        GameplayStartupGate.SetLoadingScreenBlocked(false);
        sceneLoadRequested = false;
        pendingWaitForGameScene = false;

        if (activeInstance == null)
        {
            return;
        }

        activeInstance.destroyRequested = true;
        activeInstance.ReleaseInputBlocking();
        activeInstance.DisableGraphicsBeforeDestroy();
        UnityEngine.Object.Destroy(activeInstance.gameObject);
    }

    private static bool ShouldShowForScene(Scene scene)
    {
        string sceneName = scene.name;

        if (sceneName == "LobbyScene" ||
            sceneName == "LobbyScene 1" ||
            sceneName == "CreateRoomLobbyScene" ||
            sceneName == "PublicRoomListScene" ||
            sceneName == LoadingScreenSceneName ||
            sceneName == "RoleRevealScreen" ||
            sceneName == "VictoryScreen")
        {
            return false;
        }

        if (sceneName == "labor" || sceneName == "GameScene")
        {
            return true;
        }

        return Object.FindAnyObjectByType<LaboratoryGenerator>(FindObjectsInactive.Include) != null;
    }

    private void Awake()
    {
        activeInstance = this;
        sceneLoadRequested = false;
        DontDestroyOnLoad(gameObject);
        GameplayStartupGate.SetLoadingScreenBlocked(true);

        if (string.IsNullOrEmpty(pendingMessage))
        {
            pendingMessage = initialMessage;
        }

        if (!TryBindExistingUi())
        {
            BuildUi();
        }

        Configure(pendingMessage, pendingWaitForGameScene);
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
        GameplayStartupGate.SetLoadingScreenBlocked(false);
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
            scanRing == null ||
            innerRing == null)
        {
            return;
        }

        float animationTime = Time.unscaledTime;
        displayProgress = Mathf.MoveTowards(displayProgress, targetProgress, Time.unscaledDeltaTime * 0.75f);
        progressFill.anchorMax = new Vector2(Mathf.Clamp01(displayProgress), 1f);
        if (progressGlow != null)
        {
            progressGlow.anchorMax = new Vector2(Mathf.Clamp01(displayProgress), 1f);
        }

        if (percentText != null)
        {
            percentText.text = Mathf.RoundToInt(displayProgress * 100f).ToString("000") + "%";
        }

        float pulse = Mathf.PingPong(animationTime * 0.75f, 1f);
        scanRing.color = new Color(0.55f, 0.95f, 1f, Mathf.Lerp(0.08f, 0.24f, pulse));
        scanRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.08f, pulse);
        scanRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, animationTime * 8f);
        innerRing.rectTransform.localRotation = Quaternion.Euler(0f, 0f, animationTime * -26f);

        if (titleText != null)
        {
            titleText.color = new Color(0.86f, 0.98f, 1f, Mathf.Lerp(0.72f, 1f, pulse));
        }

        if (tipText != null)
        {
            tipText.color = new Color(0.86f, 0.92f, 0.88f, Mathf.Lerp(0.42f, 0.76f, pulse));
        }

        if (telemetryText != null)
        {
            int syncPercent = Mathf.RoundToInt(Mathf.Lerp(37f, 99f, displayProgress));
            int routePercent = Mathf.RoundToInt(Mathf.Lerp(12f, 100f, Mathf.PingPong(animationTime * 0.18f, 1f)));
            telemetryText.text =
                "UPLINK        " + syncPercent.ToString("000") + "%\n" +
                "ROUTE TRACE   " + routePercent.ToString("000") + "%\n" +
                "SIGNAL        ENCRYPTED\n" +
                "AREA MODEL    STREAMING\n" +
                "SQUAD SYNC    ACTIVE";
        }

        if (backdrop != null && backdrop.isActiveAndEnabled)
        {
            backdrop.AnimationTime = animationTime;
            backdrop.Progress = displayProgress;
            backdrop.MarkDirtySafely();
        }

        if (scanDots != null && scanDots.isActiveAndEnabled)
        {
            scanDots.AnimationTime = animationTime;
            scanDots.Progress = displayProgress;
            scanDots.MarkDirtySafely();
        }

        if (progressSegments != null && progressSegments.isActiveAndEnabled)
        {
            progressSegments.AnimationTime = animationTime;
            progressSegments.Progress = displayProgress;
            progressSegments.MarkDirtySafely();
        }
    }

    private IEnumerator WatchGenerationState()
    {
        yield return null;

        while (waitForGameSceneBeforeFallback &&
               !ShouldShowForScene(SceneManager.GetActiveScene()) &&
               Object.FindAnyObjectByType<LaboratoryGenerator>(FindObjectsInactive.Include) == null)
        {
            yield return null;
        }

        LaboratoryGenerator generator = Object.FindAnyObjectByType<LaboratoryGenerator>(FindObjectsInactive.Include);

        if (generator == null)
        {
            if (waitForGameSceneBeforeFallback)
            {
                HandleLoadingPhaseChanged("ENTERING DARKNESS...", 1f);
            }
            else
            {
                HandleLoadingPhaseChanged(initialMessage, 1f);
            }

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
        else if (!finishing)
        {
            generationFailed = true;
            HandleLoadingPhaseChanged("SCAN FAILED", 1f);
            yield return new WaitForSecondsRealtime(MinimumVisibleTime);
            StartCoroutine(FadeOutAndDestroy());
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

        if (!generationFailed && ShouldShowRoleRevealForScene(SceneManager.GetActiveScene()))
        {
            RoleRevealIntro.ShowWhenReady();
        }

        Destroy(gameObject);
    }

    private static bool ShouldShowRoleRevealForScene(Scene scene)
    {
        string sceneName = scene.name;
        return sceneName == "labor" || sceneName == "GameScene";
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

        backdrop = CreateGraphic<LoadingBackdropGraphic>("Cinematic Backdrop", root);
        Stretch(backdrop.rectTransform);
        backdrop.color = Color.white;

        Image topShade = CreateImage("Top Cinematic Shade", root, new Color(0f, 0f, 0f, 0.50f));
        topShade.rectTransform.anchorMin = new Vector2(0f, 1f);
        topShade.rectTransform.anchorMax = Vector2.one;
        topShade.rectTransform.offsetMin = new Vector2(0f, -132f);
        topShade.rectTransform.offsetMax = Vector2.zero;

        Image bottomShade = CreateImage("Bottom Cinematic Shade", root, new Color(0f, 0f, 0f, 0.54f));
        bottomShade.rectTransform.anchorMin = Vector2.zero;
        bottomShade.rectTransform.anchorMax = new Vector2(1f, 0f);
        bottomShade.rectTransform.offsetMin = Vector2.zero;
        bottomShade.rectTransform.offsetMax = new Vector2(0f, 162f);

        scanDots = CreateGraphic<ScanDotsGraphic>("Scan Dots", root);
        Stretch(scanDots.rectTransform);
        scanDots.color = Color.white;

        titleText = CreateText("Mission Title", root, "DARK US", 44, TextAlignmentOptions.Left);
        titleText.color = new Color(0.86f, 0.98f, 1f, 0.88f);
        titleText.fontStyle = FontStyles.Bold;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0f, 1f);
        titleText.rectTransform.sizeDelta = new Vector2(420f, 58f);
        titleText.rectTransform.anchoredPosition = new Vector2(72f, -64f);

        TMP_Text operationText = CreateText("Operation Text", root, "OPERATION // UNDERGROUND SIGNAL INTERCEPT", 18, TextAlignmentOptions.Left);
        operationText.color = new Color(0.95f, 0.76f, 0.30f, 0.78f);
        operationText.rectTransform.anchorMin = new Vector2(0f, 1f);
        operationText.rectTransform.anchorMax = new Vector2(0f, 1f);
        operationText.rectTransform.sizeDelta = new Vector2(560f, 30f);
        operationText.rectTransform.anchoredPosition = new Vector2(76f, -104f);

        TMP_Text buildText = CreateText("Build Text", root, "DARK SCAN / LIVE MAP", 18, TextAlignmentOptions.TopRight);
        buildText.color = new Color(0.95f, 0.76f, 0.30f, 0.62f);
        buildText.rectTransform.anchorMin = new Vector2(1f, 1f);
        buildText.rectTransform.anchorMax = new Vector2(1f, 1f);
        buildText.rectTransform.sizeDelta = new Vector2(360f, 38f);
        buildText.rectTransform.anchoredPosition = new Vector2(-60f, -44f);

        scanRing = CreateImage("Scan Ring", root, new Color(0.55f, 0.95f, 1f, 0.08f));
        scanRing.sprite = CreateRingSprite();
        scanRing.type = Image.Type.Simple;
        scanRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        scanRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scanRing.rectTransform.sizeDelta = new Vector2(690f, 690f);
        scanRing.rectTransform.anchoredPosition = new Vector2(0f, 34f);

        Image middleRing = CreateImage("Middle Scan Ring", root, new Color(0.18f, 0.55f, 0.68f, 0.16f));
        middleRing.sprite = CreateRingSprite();
        middleRing.type = Image.Type.Simple;
        middleRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        middleRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        middleRing.rectTransform.sizeDelta = new Vector2(500f, 500f);
        middleRing.rectTransform.anchoredPosition = new Vector2(0f, 34f);

        innerRing = CreateImage("Inner Ring", root, new Color(1f, 0.76f, 0.24f, 0.10f));
        innerRing.sprite = CreateRingSprite();
        innerRing.type = Image.Type.Simple;
        innerRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        innerRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        innerRing.rectTransform.sizeDelta = new Vector2(290f, 290f);
        innerRing.rectTransform.anchoredPosition = new Vector2(0f, 34f);

        Image centerCore = CreateImage("Scan Core", root, new Color(0.45f, 0.95f, 1f, 0.08f));
        centerCore.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        centerCore.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        centerCore.rectTransform.sizeDelta = new Vector2(42f, 42f);
        centerCore.rectTransform.anchoredPosition = new Vector2(0f, 34f);

        RectTransform leftPanel = CreatePanel("System Panel", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(72f, -76f), new Vector2(430f, 230f));
        RectTransform rightPanel = CreatePanel("Telemetry Panel", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-72f, -44f), new Vector2(410f, 210f));

        TMP_Text leftHeader = CreateText("System Header", leftPanel, "SYSTEM EVENTS", 16, TextAlignmentOptions.Left);
        leftHeader.color = new Color(0.95f, 0.76f, 0.30f, 0.80f);
        leftHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
        leftHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
        leftHeader.rectTransform.offsetMin = new Vector2(24f, -42f);
        leftHeader.rectTransform.offsetMax = new Vector2(-24f, -14f);

        TMP_Text telemetryHeader = CreateText("Telemetry Header", rightPanel, "DEPLOYMENT TELEMETRY", 16, TextAlignmentOptions.Left);
        telemetryHeader.color = new Color(0.95f, 0.76f, 0.30f, 0.80f);
        telemetryHeader.rectTransform.anchorMin = new Vector2(0f, 1f);
        telemetryHeader.rectTransform.anchorMax = new Vector2(1f, 1f);
        telemetryHeader.rectTransform.offsetMin = new Vector2(24f, -42f);
        telemetryHeader.rectTransform.offsetMax = new Vector2(-24f, -14f);

        logText = CreateText("System Log", leftPanel, string.Empty, 18, TextAlignmentOptions.BottomLeft);
        logText.color = new Color(0.70f, 0.88f, 0.86f, 0.72f);
        logText.rectTransform.anchorMin = Vector2.zero;
        logText.rectTransform.anchorMax = Vector2.one;
        logText.rectTransform.offsetMin = new Vector2(24f, 24f);
        logText.rectTransform.offsetMax = new Vector2(-24f, -52f);

        telemetryText = CreateText("Telemetry", rightPanel, string.Empty, 18, TextAlignmentOptions.TopLeft);
        telemetryText.color = new Color(0.72f, 0.94f, 0.98f, 0.78f);
        telemetryText.rectTransform.anchorMin = Vector2.zero;
        telemetryText.rectTransform.anchorMax = Vector2.one;
        telemetryText.rectTransform.offsetMin = new Vector2(24f, 28f);
        telemetryText.rectTransform.offsetMax = new Vector2(-24f, -58f);

        statusText = CreateText("Status", root, "SCANNING AREA...", 30, TextAlignmentOptions.Center);
        statusText.color = new Color(0.82f, 0.97f, 1f, 0.94f);
        statusText.fontStyle = FontStyles.Bold;
        statusText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        statusText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        statusText.rectTransform.sizeDelta = new Vector2(820f, 46f);
        statusText.rectTransform.anchoredPosition = new Vector2(0f, 116f);

        RectTransform progressRoot = CreateImage("Progress Track", root, new Color(0.27f, 0.36f, 0.37f, 0.34f)).rectTransform;
        progressRoot.anchorMin = new Vector2(0.5f, 0f);
        progressRoot.anchorMax = new Vector2(0.5f, 0f);
        progressRoot.sizeDelta = new Vector2(1040f, 12f);
        progressRoot.anchoredPosition = new Vector2(0f, 86f);

        progressGlow = CreateImage("Progress Glow", progressRoot, new Color(0.42f, 0.95f, 1f, 0.20f)).rectTransform;
        progressGlow.anchorMin = Vector2.zero;
        progressGlow.anchorMax = new Vector2(displayProgress, 1f);
        progressGlow.offsetMin = new Vector2(0f, -6f);
        progressGlow.offsetMax = new Vector2(0f, 6f);

        progressFill = CreateImage("Progress Fill", progressRoot, new Color(0.54f, 0.92f, 1f, 0.82f)).rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(displayProgress, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;

        progressSegments = CreateGraphic<ProgressSegmentsGraphic>("Progress Segments", progressRoot);
        Stretch(progressSegments.rectTransform);
        progressSegments.color = Color.white;

        percentText = CreateText("Progress Percent", root, "000%", 22, TextAlignmentOptions.Right);
        percentText.color = new Color(0.95f, 0.76f, 0.30f, 0.92f);
        percentText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        percentText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        percentText.rectTransform.sizeDelta = new Vector2(130f, 32f);
        percentText.rectTransform.anchoredPosition = new Vector2(600f, 76f);

        phaseText = CreateText("Phase", root, "PHASE 000", 22, TextAlignmentOptions.Left);
        phaseText.color = new Color(0.52f, 0.95f, 1f, 0.84f);
        phaseText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        phaseText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        phaseText.rectTransform.sizeDelta = new Vector2(210f, 32f);
        phaseText.rectTransform.anchoredPosition = new Vector2(-618f, 76f);

        tipText = CreateText("Tip Text", root, "KEEP YOUR LIGHT LOW. TRUST THE DOTS.", 18, TextAlignmentOptions.Center);
        tipText.color = new Color(0.86f, 0.92f, 0.88f, 0.58f);
        tipText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        tipText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        tipText.rectTransform.sizeDelta = new Vector2(860f, 30f);
        tipText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
    }

    private bool TryBindExistingUi()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        graphicRaycaster = GetComponent<GraphicRaycaster>();

        statusText = FindText("Status");
        logText = FindText("System Log");
        percentText = FindText("Progress Percent");
        phaseText = FindText("Phase");
        titleText = FindText("Mission Title");
        telemetryText = FindText("Telemetry");
        tipText = FindText("Tip Text");
        progressFill = FindRect("Progress Fill");
        progressGlow = FindRect("Progress Glow");
        scanRing = FindImage("Scan Ring");
        innerRing = FindImage("Inner Ring");
        backdrop = GetComponentInChildren<LoadingBackdropGraphic>(true);
        scanDots = GetComponentInChildren<ScanDotsGraphic>(true);
        progressSegments = GetComponentInChildren<ProgressSegmentsGraphic>(true);

        return canvasGroup != null &&
               statusText != null &&
               percentText != null &&
               phaseText != null &&
               progressFill != null &&
               scanRing != null &&
               innerRing != null;
    }

    private TMP_Text FindText(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<TMP_Text>() : null;
    }

    private Image FindImage(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private RectTransform FindRect(string objectName)
    {
        Transform target = FindDeepChild(transform, objectName);
        return target != null ? target as RectTransform : null;
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.fontStyle = FontStyles.UpperCase;
        return tmp;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        RectTransform panel = CreateImage(name, parent, new Color(0.006f, 0.012f, 0.014f, 0.56f)).rectTransform;
        panel.anchorMin = anchorMin;
        panel.anchorMax = anchorMax;
        panel.pivot = new Vector2(anchorMin.x <= 0.5f ? 0f : 1f, 0.5f);
        panel.sizeDelta = size;
        panel.anchoredPosition = anchoredPosition;

        Color lineColor = new Color(0.44f, 0.92f, 1f, 0.18f);
        RectTransform top = CreateImage(name + " Top", panel, lineColor).rectTransform;
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.offsetMin = new Vector2(0f, -2f);
        top.offsetMax = Vector2.zero;

        RectTransform bottom = CreateImage(name + " Bottom", panel, lineColor).rectTransform;
        bottom.anchorMin = Vector2.zero;
        bottom.anchorMax = new Vector2(1f, 0f);
        bottom.offsetMin = Vector2.zero;
        bottom.offsetMax = new Vector2(0f, 2f);

        RectTransform left = CreateImage(name + " Left", panel, lineColor).rectTransform;
        left.anchorMin = Vector2.zero;
        left.anchorMax = new Vector2(0f, 1f);
        left.offsetMin = Vector2.zero;
        left.offsetMax = new Vector2(2f, 0f);

        RectTransform right = CreateImage(name + " Right", panel, lineColor).rectTransform;
        right.anchorMin = new Vector2(1f, 0f);
        right.anchorMax = Vector2.one;
        right.offsetMin = new Vector2(-2f, 0f);
        right.offsetMax = Vector2.zero;

        return panel;
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

    private class LoadingBackdropGraphic : Graphic
    {
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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            AddQuad(vh, rect.xMin, rect.yMin, rect.width, rect.height, new Color(0.002f, 0.004f, 0.006f, 1f));

            float gridSpacing = 92f;
            float xOffset = Mathf.Repeat(AnimationTime * 12f, gridSpacing);
            for (float x = rect.xMin - gridSpacing + xOffset; x < rect.xMax + gridSpacing; x += gridSpacing)
            {
                AddQuad(vh, x, rect.yMin, 1.2f, rect.height, new Color(0.22f, 0.78f, 0.92f, 0.055f));
            }

            float yOffset = Mathf.Repeat(AnimationTime * 8f, gridSpacing);
            for (float y = rect.yMin - gridSpacing + yOffset; y < rect.yMax + gridSpacing; y += gridSpacing)
            {
                AddQuad(vh, rect.xMin, y, rect.width, 1.2f, new Color(0.22f, 0.78f, 0.92f, 0.045f));
            }

            float edge = Mathf.Min(rect.width, rect.height) * 0.14f;
            AddQuad(vh, rect.xMin, rect.yMin, edge, rect.height, new Color(0f, 0f, 0f, 0.36f));
            AddQuad(vh, rect.xMax - edge, rect.yMin, edge, rect.height, new Color(0f, 0f, 0f, 0.36f));
            AddQuad(vh, rect.xMin, rect.yMin, rect.width, edge, new Color(0f, 0f, 0f, 0.42f));
            AddQuad(vh, rect.xMin, rect.yMax - edge, rect.width, edge, new Color(0f, 0f, 0f, 0.42f));
        }

        private static void AddQuad(VertexHelper vh, float x, float y, float width, float height, Color32 color)
        {
            int startIndex = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector3(x, y);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x, y + height);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x + width, y + height);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x + width, y);
            vh.AddVert(vertex);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }

    private class ProgressSegmentsGraphic : Graphic
    {
        private const int SegmentCount = 42;

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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            float gap = 4f;
            float segmentWidth = (rect.width - gap * (SegmentCount - 1)) / SegmentCount;
            int activeCount = Mathf.Clamp(Mathf.CeilToInt(Progress * SegmentCount), 0, SegmentCount);

            for (int i = 0; i < SegmentCount; i++)
            {
                float x = rect.xMin + i * (segmentWidth + gap);
                bool active = i < activeCount;
                float scan = Mathf.PingPong(AnimationTime * 1.6f + i * 0.035f, 1f);
                Color color = active
                    ? new Color(0.52f, 0.93f, 1f, Mathf.Lerp(0.46f, 0.88f, scan))
                    : new Color(0.16f, 0.28f, 0.30f, 0.50f);

                if (active && i % 7 == 0)
                {
                    color = new Color(0.95f, 0.74f, 0.25f, Mathf.Lerp(0.48f, 0.86f, scan));
                }

                AddQuad(vh, x, rect.yMin, segmentWidth, rect.height, color);
            }
        }

        private static void AddQuad(VertexHelper vh, float x, float y, float width, float height, Color32 color)
        {
            int startIndex = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector3(x, y);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x, y + height);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x + width, y + height);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x + width, y);
            vh.AddVert(vertex);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
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
