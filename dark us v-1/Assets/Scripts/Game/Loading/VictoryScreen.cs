using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VictoryScreen : MonoBehaviour
{
    private const string VictorySceneName = "VictoryScreen";
    private const int VictorySortingOrder = 66000;

    private static VictoryScreen activeInstance;
    private static bool sceneLoadRequested;
    private static bool pendingCitizensWon;
    private static string pendingReason = "Round Complete";
    private static float pendingReturnDelay = 8f;

    private CanvasGroup canvasGroup;
    private TMP_Text titleText;
    private TMP_Text reasonText;
    private TMP_Text timerText;
    private TMP_Text subtitleText;
    private Image blackout;
    private Image accentBar;
    private Image pulseRing;
    private bool citizensWon;
    private string reason;
    private float returnDelay;
    private float shownAt;

    public static bool IsShowing => activeInstance != null;

    public static void Show(bool didCitizensWin, string gameOverReason, float secondsUntilReturn)
    {
        DarkScanLoadingScreen.ForceHideImmediate();
        RoleRevealIntro.CancelPending();
        GameplayStartupGate.ResetAll();
        GameplayStartupGate.SetVictoryScreenBlocked(true);

        pendingCitizensWon = didCitizensWin;
        pendingReason = string.IsNullOrWhiteSpace(gameOverReason) ? "Round Complete" : gameOverReason;
        pendingReturnDelay = Mathf.Max(0.1f, secondsUntilReturn);

        if (activeInstance != null)
        {
            activeInstance.Configure(pendingCitizensWon, pendingReason, pendingReturnDelay);
            return;
        }

        if (!sceneLoadRequested && Application.CanStreamedLevelBeLoaded(VictorySceneName))
        {
            sceneLoadRequested = true;
            SceneManager.LoadSceneAsync(VictorySceneName, LoadSceneMode.Additive);
            return;
        }

        GameObject root = new GameObject("Victory Screen");
        activeInstance = root.AddComponent<VictoryScreen>();
    }

    private void Awake()
    {
        activeInstance = this;
        sceneLoadRequested = false;

        if (!TryBindExistingUi())
        {
            BuildUi();
        }

        PrepareExclusiveLayer();
        Configure(pendingCitizensWon, pendingReason, pendingReturnDelay);
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }

        GameplayStartupGate.SetVictoryScreenBlocked(false);
    }

    private void Configure(bool didCitizensWin, string gameOverReason, float secondsUntilReturn)
    {
        citizensWon = didCitizensWin;
        reason = string.IsNullOrWhiteSpace(gameOverReason) ? "Round Complete" : gameOverReason;
        returnDelay = Mathf.Max(0.1f, secondsUntilReturn);
        shownAt = Time.unscaledTime;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        Color mainColor = citizensWon ? new Color(0.54f, 0.95f, 1f, 1f) : new Color(1f, 0.28f, 0.20f, 1f);
        Color softColor = citizensWon ? new Color(0.30f, 0.90f, 1f, 0.18f) : new Color(1f, 0.16f, 0.10f, 0.18f);

        if (titleText != null)
        {
            titleText.text = citizensWon ? InGameLocalization.Text("Citizens Win") : InGameLocalization.Text("Killer Wins");
            titleText.color = mainColor;
        }

        if (reasonText != null)
        {
            reasonText.text = InGameLocalization.Text(reason);
        }

        if (subtitleText != null)
        {
            subtitleText.text = citizensWon ? "EXTRACTION CONFIRMED" : "SIGNAL LOST";
            subtitleText.color = mainColor;
        }

        if (accentBar != null)
        {
            accentBar.color = mainColor;
        }

        if (pulseRing != null)
        {
            pulseRing.color = softColor;
        }
    }

    private void Update()
    {
        PrepareExclusiveLayer();

        float elapsed = Time.unscaledTime - shownAt;
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.6f));

        if (canvasGroup != null)
        {
            canvasGroup.alpha = reveal;
        }

        if (timerText != null)
        {
            float remaining = Mathf.Max(0f, returnDelay - elapsed);
            timerText.text = "RETURNING TO LOBBY IN " + Mathf.CeilToInt(remaining).ToString("00");
        }

        float pulse = Mathf.PingPong(Time.unscaledTime * 0.75f, 1f);
        if (pulseRing != null)
        {
            pulseRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.12f, pulse);
            Color color = pulseRing.color;
            color.a = Mathf.Lerp(0.08f, 0.26f, pulse);
            pulseRing.color = color;
        }

    }

    private bool TryBindExistingUi()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        titleText = FindText("Title");
        reasonText = FindText("Reason");
        timerText = FindText("Return Timer");
        subtitleText = FindText("Subtitle");
        blackout = FindImage("Blackout");
        accentBar = FindImage("Accent Bar");
        pulseRing = FindImage("Pulse Ring");

        return canvasGroup != null &&
               titleText != null &&
               reasonText != null &&
               timerText != null &&
               subtitleText != null &&
               blackout != null &&
               accentBar != null &&
               pulseRing != null;
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = VictorySortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        RectTransform root = canvas.GetComponent<RectTransform>();
        Stretch(root);

        blackout = CreateImage("Blackout", root, Color.black);
        Stretch(blackout.rectTransform);

        pulseRing = CreateImage("Pulse Ring", root, new Color(0.54f, 0.95f, 1f, 0.16f));
        pulseRing.sprite = CreateRingSprite();
        pulseRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        pulseRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        pulseRing.rectTransform.sizeDelta = new Vector2(720f, 720f);

        subtitleText = CreateText("Subtitle", root, "EXTRACTION CONFIRMED", 20f, TextAlignmentOptions.Center);
        subtitleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        subtitleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        subtitleText.rectTransform.sizeDelta = new Vector2(780f, 36f);
        subtitleText.rectTransform.anchoredPosition = new Vector2(0f, 122f);

        titleText = CreateText("Title", root, string.Empty, 78f, TextAlignmentOptions.Center);
        titleText.fontStyle = FontStyles.Bold;
        titleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        titleText.rectTransform.sizeDelta = new Vector2(980f, 118f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 28f);

        accentBar = CreateImage("Accent Bar", root, new Color(0.54f, 0.95f, 1f, 1f));
        accentBar.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        accentBar.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        accentBar.rectTransform.sizeDelta = new Vector2(460f, 4f);
        accentBar.rectTransform.anchoredPosition = new Vector2(0f, -46f);

        reasonText = CreateText("Reason", root, string.Empty, 28f, TextAlignmentOptions.Center);
        reasonText.color = new Color(0.86f, 0.92f, 0.92f, 0.90f);
        reasonText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        reasonText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        reasonText.rectTransform.sizeDelta = new Vector2(820f, 46f);
        reasonText.rectTransform.anchoredPosition = new Vector2(0f, -92f);

        timerText = CreateText("Return Timer", root, "RETURNING TO LOBBY IN 00", 20f, TextAlignmentOptions.Center);
        timerText.color = new Color(0.95f, 0.76f, 0.30f, 0.86f);
        timerText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        timerText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        timerText.rectTransform.sizeDelta = new Vector2(620f, 36f);
        timerText.rectTransform.anchoredPosition = new Vector2(0f, 82f);
    }

    private void PrepareExclusiveLayer()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = VictorySortingOrder;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        if (blackout != null)
        {
            blackout.color = Color.black;
        }

        GameplayStartupGate.SetVictoryScreenBlocked(true);
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

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Shadow));
        textObject.transform.SetParent(parent, false);

        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = new Color(0.86f, 0.98f, 1f, 0.96f);
        LocalizedTmpFontProvider.Apply(tmp);

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return tmp;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
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
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
