using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 메인 메뉴 배경의 분위기 연출을 담당하는 스크립트이다.
// 배경을 아주 천천히 확대/축소하고, 어두운 오버레이와 로고를 살짝 깜빡이게 만든다.
public class MainMenuBackgroundAnimator : MonoBehaviour
{
    [Header("Background Zoom")]
    // 천천히 확대/축소할 배경 RectTransform이다.
    public RectTransform backgroundRect;

    // 배경 기본 크기 배율이다.
    public float baseScale = 1.04f;

    // 배경이 최대로 커지는 배율이다.
    public float zoomAmount = 0.025f;

    // 배경 확대/축소 속도이다.
    public float zoomSpeed = 0.055f;

    [Header("Background Drift")]
    // 배경을 살짝 좌우/상하로 움직이는 범위이다.
    public Vector2 panAmount = new Vector2(22f, 10f);

    // 배경 드리프트 속도이다.
    public float panSpeed = 0.045f;

    // 배경이 아주 미세하게 기울어지는 각도이다.
    public float rotationAmount = 0.12f;

    // 배경 기울기 변화 속도이다.
    public float rotationSpeed = 0.033f;

    [Header("Intro Reveal")]
    // 시작할 때 검은 화면에서 천장 조명이 깜빡이며 배경을 드러낼지 여부이다.
    public bool playIntroReveal = false;

    // 처음 완전히 어둡게 유지하는 시간이다.
    public float introBlackHoldDuration = 1.0f;

    // 깜빡임 후 배경이 완전히 드러나는 시간이다.
    public float introRevealDuration = 3.8f;

    // 천장 조명 깜빡임 속도이다.
    public float introLightFlickerSpeed = 8.5f;

    // 깜빡임이 배경을 순간적으로 드러내는 강도이다.
    [Range(0f, 1f)]
    public float introRevealFlickerStrength = 0.9f;

    // 천장 조명 글로우의 최대 알파값이다.
    [Range(0f, 1f)]
    public float introLightMaxAlpha = 0.86f;

    // 화면 기준 천장 조명 위치이다.
    public Vector2 introLightAnchor = new Vector2(0.62f, 0.92f);

    // 천장 조명 글로우 크기이다.
    public Vector2 introLightSize = new Vector2(900f, 420f);

    [Header("Dark Overlay Flicker")]
    // 화면 전체를 어둡게 덮는 오버레이 이미지이다.
    public Image darkOverlayImage;

    // 오버레이 최소 알파값이다.
    [Range(0f, 1f)]
    public float darkOverlayMinAlpha = 0.32f;

    // 오버레이 최대 알파값이다.
    [Range(0f, 1f)]
    public float darkOverlayMaxAlpha = 0.45f;

    // 오버레이 깜빡임 속도이다.
    public float darkOverlayFlickerSpeed = 0.7f;

    [Header("Logo Flicker")]
    // 살짝 깜빡이게 할 로고 이미지이다.
    public Image logoImage;

    // 로고 최소 알파값이다.
    [Range(0f, 1f)]
    public float logoMinAlpha = 0.72f;

    // 로고 최대 알파값이다.
    [Range(0f, 1f)]
    public float logoMaxAlpha = 1.0f;

    // 로고 깜빡임 속도이다.
    public float logoFlickerSpeed = 1.2f;

    [Header("Optional Scan Dot Overlay")]
    // 점/노이즈 오버레이 이미지이다. 없으면 비워둬도 된다.
    public Image scanDotOverlayImage;

    // 점 오버레이 최소 알파값이다.
    [Range(0f, 1f)]
    public float scanDotMinAlpha = 0.0f;

    // 점 오버레이 최대 알파값이다.
    [Range(0f, 1f)]
    public float scanDotMaxAlpha = 0.12f;

    // 점 오버레이 깜빡임 속도이다.
    public float scanDotFlickerSpeed = 0.45f;

    // 점 오버레이가 없을 때 런타임에 자동으로 만든다.
    public bool createScanDotOverlayIfMissing = true;

    // 점 오버레이가 천천히 흐르는 속도이다.
    public Vector2 scanDotScrollSpeed = new Vector2(7f, -3f);

    // 점 오버레이가 호흡하듯 흔들리는 범위이다.
    public Vector2 scanDotDriftAmount = new Vector2(34f, 18f);

    // 타일 반복 거리이다.
    public float scanDotTileLoopDistance = 256f;

    // 시작 시 배경 기본 크기를 저장한다.
    private Vector3 initialBackgroundScale;
    private Vector2 initialBackgroundAnchoredPosition;
    private Vector3 initialBackgroundEulerAngles;
    private RectTransform scanDotOverlayRect;
    private Vector2 initialScanDotOverlayPosition;
    private Sprite generatedScanDotSprite;
    private Image introBlackoutImage;
    private Image introLightImage;
    private Canvas introOverlayCanvas;
    private GameObject introOverlayRoot;
    private Sprite generatedIntroLightSprite;
    private float introStartTime;
    private bool introRevealActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartLobbyIntroRevealAfterSceneLoad()
    {
        if (SceneManager.GetActiveScene().name != "LobbyScene")
        {
            return;
        }

        MainMenuBackgroundAnimator[] animators = Object.FindObjectsByType<MainMenuBackgroundAnimator>(FindObjectsInactive.Include);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i].isActiveAndEnabled)
            {
                animators[i].StartIntroReveal();
            }
        }
    }

    private void Awake()
    {
        // 배경 RectTransform을 직접 넣지 않았으면 자기 자신을 배경으로 사용한다.
        if (backgroundRect == null)
        {
            backgroundRect = GetComponent<RectTransform>();
        }

        if (backgroundRect != null)
        {
            initialBackgroundScale = backgroundRect.localScale;
            initialBackgroundAnchoredPosition = backgroundRect.anchoredPosition;
            initialBackgroundEulerAngles = backgroundRect.localEulerAngles;
        }

        if (scanDotOverlayImage == null && createScanDotOverlayIfMissing)
        {
            scanDotOverlayImage = CreateGeneratedScanDotOverlay();
        }

        if (scanDotOverlayImage != null)
        {
            scanDotOverlayRect = scanDotOverlayImage.rectTransform;
            initialScanDotOverlayPosition = scanDotOverlayRect.anchoredPosition;
        }

        bool shouldPlayIntroReveal = playIntroReveal || gameObject.scene.name == "LobbyScene";
        if (shouldPlayIntroReveal)
        {
            StartIntroReveal();
        }
    }

    private void Update()
    {
        AnimateBackgroundMotion();
        AnimateIntroReveal();
        AnimateDarkOverlay();
        AnimateLogo();
        AnimateScanDotOverlay();
    }

    private void OnDestroy()
    {
        if (introOverlayRoot != null)
        {
            Destroy(introOverlayRoot);
            introOverlayRoot = null;
        }

        if (generatedScanDotSprite != null)
        {
            Texture2D generatedTexture = generatedScanDotSprite.texture;
            Destroy(generatedScanDotSprite);
            if (generatedTexture != null)
            {
                Destroy(generatedTexture);
            }

            generatedScanDotSprite = null;
        }

        if (generatedIntroLightSprite != null)
        {
            Texture2D generatedLightTexture = generatedIntroLightSprite.texture;
            Destroy(generatedIntroLightSprite);
            if (generatedLightTexture != null)
            {
                Destroy(generatedLightTexture);
            }

            generatedIntroLightSprite = null;
        }
    }

    // 배경을 아주 천천히 확대/축소하고 위치를 흔든다.
    private void AnimateBackgroundMotion()
    {
        if (backgroundRect == null)
        {
            return;
        }

        float wave = Mathf.Sin(Time.unscaledTime * zoomSpeed);
        float normalizedWave = (wave + 1f) * 0.5f;
        float targetScale = baseScale + normalizedWave * zoomAmount;

        backgroundRect.localScale = initialBackgroundScale * targetScale;

        float panX = Mathf.Sin(Time.unscaledTime * panSpeed) * panAmount.x;
        float panY = Mathf.Sin(Time.unscaledTime * panSpeed * 0.73f + 1.37f) * panAmount.y;
        backgroundRect.anchoredPosition = initialBackgroundAnchoredPosition + new Vector2(panX, panY);

        float rotationZ = Mathf.Sin(Time.unscaledTime * rotationSpeed + 0.84f) * rotationAmount;
        backgroundRect.localEulerAngles = initialBackgroundEulerAngles + new Vector3(0f, 0f, rotationZ);
    }

    // 화면 어두움을 미세하게 흔들어서 불안정한 분위기를 만든다.
    private void AnimateDarkOverlay()
    {
        if (darkOverlayImage == null)
        {
            return;
        }

        float wave = Mathf.Sin(Time.unscaledTime * darkOverlayFlickerSpeed);
        float normalizedWave = (wave + 1f) * 0.5f;
        float targetAlpha = Mathf.Lerp(darkOverlayMinAlpha, darkOverlayMaxAlpha, normalizedWave);

        SetImageAlpha(darkOverlayImage, targetAlpha);
    }

    // 초반에는 검은 화면을 유지하고, 천장 조명 깜빡임에 맞춰 배경을 드러낸다.
    private void AnimateIntroReveal()
    {
        if (!introRevealActive)
        {
            return;
        }

        KeepIntroRevealOnTop();

        float elapsed = Time.unscaledTime - introStartTime;
        float revealTime = Mathf.Max(0f, elapsed - introBlackHoldDuration);
        float revealProgress = introRevealDuration > 0f ? Mathf.Clamp01(revealTime / introRevealDuration) : 1f;
        float easedReveal = SmoothStep01(revealProgress);
        float flickerPulse = EvaluateIntroLightPulse(elapsed);

        float blackoutAlpha = 1f;
        if (elapsed >= introBlackHoldDuration)
        {
            float baseBlackoutAlpha = 1f - easedReveal;
            float flickerWindow = 1f - Mathf.Clamp01(revealProgress * 1.18f);
            float flickerReveal = flickerPulse * introRevealFlickerStrength * flickerWindow;
            blackoutAlpha = Mathf.Clamp01(baseBlackoutAlpha - flickerReveal);
        }

        SetImageAlpha(introBlackoutImage, blackoutAlpha);

        if (introLightImage != null)
        {
            float lightFade = 1f - Mathf.Clamp01(revealProgress * 0.82f);
            float lightAlpha = Mathf.Clamp01((0.12f + flickerPulse * 0.88f) * introLightMaxAlpha * lightFade);
            SetImageAlpha(introLightImage, lightAlpha);
        }

        if (revealProgress >= 1f)
        {
            SetImageAlpha(introBlackoutImage, 0f);
            if (introLightImage != null)
            {
                SetImageAlpha(introLightImage, 0f);
            }

            introRevealActive = false;
            if (introOverlayRoot != null)
            {
                Destroy(introOverlayRoot);
                introOverlayRoot = null;
            }
        }
    }

    // 로고를 약하게 깜빡이게 만든다.
    private void AnimateLogo()
    {
        if (logoImage == null)
        {
            return;
        }

        float waveA = Mathf.Sin(Time.unscaledTime * logoFlickerSpeed);
        float waveB = Mathf.Sin(Time.unscaledTime * logoFlickerSpeed * 2.7f) * 0.25f;
        float mixedWave = Mathf.Clamp01(((waveA + waveB) + 1f) * 0.5f);

        float targetAlpha = Mathf.Lerp(logoMinAlpha, logoMaxAlpha, mixedWave);

        SetImageAlpha(logoImage, targetAlpha);
    }

    // 선택 사항인 점 오버레이를 약하게 깜빡이게 만든다.
    private void AnimateScanDotOverlay()
    {
        if (scanDotOverlayImage == null)
        {
            return;
        }

        float wave = Mathf.Sin(Time.unscaledTime * scanDotFlickerSpeed);
        float noiseWave = Mathf.Sin(Time.unscaledTime * scanDotFlickerSpeed * 2.83f + 0.31f) * 0.18f;
        float normalizedWave = Mathf.Clamp01(((wave + noiseWave) + 1f) * 0.5f);
        float targetAlpha = Mathf.Lerp(scanDotMinAlpha, scanDotMaxAlpha, normalizedWave);

        SetImageAlpha(scanDotOverlayImage, targetAlpha);

        if (scanDotOverlayRect == null)
        {
            return;
        }

        float loopDistance = Mathf.Max(32f, scanDotTileLoopDistance);
        float scrollX = Mathf.Repeat(Time.unscaledTime * scanDotScrollSpeed.x, loopDistance);
        float scrollY = Mathf.Repeat(Time.unscaledTime * scanDotScrollSpeed.y, loopDistance);
        float driftX = Mathf.Sin(Time.unscaledTime * 0.12f + 0.65f) * scanDotDriftAmount.x;
        float driftY = Mathf.Sin(Time.unscaledTime * 0.095f + 2.1f) * scanDotDriftAmount.y;
        scanDotOverlayRect.anchoredPosition = initialScanDotOverlayPosition + new Vector2(scrollX + driftX, scrollY + driftY);
    }

    // 이미지의 알파값만 바꾼다.
    private void SetImageAlpha(Image targetImage, float alpha)
    {
        if (targetImage == null)
        {
            return;
        }

        Color color = targetImage.color;
        color.a = alpha;
        targetImage.color = color;
    }

    private void CreateIntroRevealOverlays()
    {
        if (backgroundRect == null)
        {
            return;
        }

        Canvas sourceCanvas = backgroundRect.GetComponentInParent<Canvas>();
        introOverlayRoot = new GameObject("MainMenuIntroRevealCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        introOverlayRoot.layer = backgroundRect.gameObject.layer;

        RectTransform rootRect = introOverlayRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        introOverlayCanvas = introOverlayRoot.GetComponent<Canvas>();
        introOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        introOverlayCanvas.sortingOrder = 32760;
        introOverlayCanvas.overrideSorting = true;

        if (sourceCanvas != null)
        {
            introOverlayCanvas.sortingOrder = Mathf.Min(32760, sourceCanvas.sortingOrder + 100);
        }

        CanvasScaler sourceScaler = sourceCanvas != null ? sourceCanvas.GetComponent<CanvasScaler>() : null;
        CanvasScaler overlayScaler = introOverlayRoot.GetComponent<CanvasScaler>();
        if (sourceScaler != null)
        {
            overlayScaler.uiScaleMode = sourceScaler.uiScaleMode;
            overlayScaler.referenceResolution = sourceScaler.referenceResolution;
            overlayScaler.screenMatchMode = sourceScaler.screenMatchMode;
            overlayScaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
        }
        else
        {
            overlayScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            overlayScaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayScaler.matchWidthOrHeight = 0.5f;
        }

        introBlackoutImage = CreateOverlayImage(introOverlayRoot.transform, "IntroBlackoutOverlay");
        introBlackoutImage.color = Color.black;

        introLightImage = CreateOverlayImage(introOverlayRoot.transform, "IntroCeilingLightFlicker");
        introLightImage.sprite = CreateIntroLightSprite();
        introLightImage.color = new Color(0.62f, 0.94f, 1f, 0f);

        RectTransform lightRect = introLightImage.rectTransform;
        lightRect.anchorMin = introLightAnchor;
        lightRect.anchorMax = introLightAnchor;
        lightRect.pivot = new Vector2(0.5f, 0.5f);
        lightRect.anchoredPosition = Vector2.zero;
        lightRect.sizeDelta = introLightSize;
        KeepIntroRevealOnTop();
    }

    private void StartIntroReveal()
    {
        if (introRevealActive || introOverlayRoot != null)
        {
            return;
        }

        CreateIntroRevealOverlays();
        introStartTime = Time.unscaledTime;
        introRevealActive = introBlackoutImage != null;

        if (introRevealActive)
        {
            SetImageAlpha(introBlackoutImage, 1f);
            SetImageAlpha(introLightImage, 0f);
            KeepIntroRevealOnTop();
        }
    }

    private Image CreateOverlayImage(Transform parent, string objectName)
    {
        GameObject overlayObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.layer = backgroundRect.gameObject.layer;
        overlayObject.transform.SetParent(parent, false);
        overlayObject.transform.SetAsLastSibling();

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = overlayObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void KeepIntroRevealOnTop()
    {
        if (introOverlayCanvas != null)
        {
            introOverlayCanvas.sortingOrder = 32760;
        }

        if (introBlackoutImage != null)
        {
            introBlackoutImage.transform.SetAsLastSibling();
        }

        if (introLightImage != null)
        {
            introLightImage.transform.SetAsLastSibling();
        }
    }

    private float EvaluateIntroLightPulse(float elapsed)
    {
        float noise = Mathf.PerlinNoise(2.17f, elapsed * introLightFlickerSpeed);
        float fastPulse = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(elapsed * introLightFlickerSpeed * 6.4f) * 0.5f + 0.5f), 5f);
        float slowPulse = Mathf.Pow(Mathf.Clamp01(Mathf.Sin(elapsed * introLightFlickerSpeed * 1.7f + 1.2f) * 0.5f + 0.5f), 2f);
        return Mathf.Clamp01(noise * 0.42f + fastPulse * 0.46f + slowPulse * 0.12f);
    }

    private float SmoothStep01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private Image CreateGeneratedScanDotOverlay()
    {
        if (backgroundRect == null || backgroundRect.parent == null)
        {
            return null;
        }

        GameObject overlayObject = new GameObject("GeneratedScanDotOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayObject.layer = backgroundRect.gameObject.layer;
        overlayObject.transform.SetParent(backgroundRect.parent, false);
        overlayObject.transform.SetSiblingIndex(backgroundRect.GetSiblingIndex() + 1);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-scanDotTileLoopDistance, -scanDotTileLoopDistance);
        rect.offsetMax = new Vector2(scanDotTileLoopDistance, scanDotTileLoopDistance);

        Image image = overlayObject.GetComponent<Image>();
        image.sprite = CreateScanDotSprite();
        image.type = Image.Type.Tiled;
        image.color = new Color(0.44f, 0.92f, 1f, 0f);
        image.raycastTarget = false;

        return image;
    }

    private Sprite CreateScanDotSprite()
    {
        if (generatedScanDotSprite != null)
        {
            return generatedScanDotSprite;
        }

        const int textureSize = 256;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        for (int y = 8; y < textureSize; y += 16)
        {
            for (int x = 8; x < textureSize; x += 16)
            {
                int hash = HashGridCoordinate(x, y);
                if (hash % 10 > 6)
                {
                    continue;
                }

                float alpha = 0.08f + (hash % 5) * 0.018f;
                SetPixelAlpha(pixels, textureSize, x, y, alpha);
                SetPixelAlpha(pixels, textureSize, x + 1, y, alpha * 0.65f);
                SetPixelAlpha(pixels, textureSize, x, y + 1, alpha * 0.65f);
            }
        }

        for (int x = 0; x < textureSize; x++)
        {
            int y = (x * 3 + 52) % textureSize;
            SetPixelAlpha(pixels, textureSize, x, y, 0.035f);
            SetPixelAlpha(pixels, textureSize, x, y + 1, 0.018f);
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedScanDotSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f);
        return generatedScanDotSprite;
    }

    private Sprite CreateIntroLightSprite()
    {
        if (generatedIntroLightSprite != null)
        {
            return generatedIntroLightSprite;
        }

        const int width = 512;
        const int height = 256;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float normalizedY = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float normalizedX = x / (float)(width - 1);
                float centeredX = (normalizedX - 0.5f) / 0.48f;
                float centeredY = (normalizedY - 0.62f) / 0.38f;
                float radial = Mathf.Clamp01(1f - Mathf.Sqrt(centeredX * centeredX + centeredY * centeredY));
                float beam = Mathf.Clamp01(1f - Mathf.Abs(normalizedX - 0.5f) / 0.18f) * Mathf.Clamp01(1f - Mathf.Abs(normalizedY - 0.44f) / 0.55f);
                float alpha = Mathf.Clamp01(radial * radial * 0.76f + beam * 0.18f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        generatedIntroLightSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        return generatedIntroLightSprite;
    }

    private int HashGridCoordinate(int x, int y)
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663;
            hash = (hash << 13) ^ hash;
            return hash & 0x7fffffff;
        }
    }

    private void SetPixelAlpha(Color[] pixels, int textureSize, int x, int y, float alpha)
    {
        if (x < 0 || x >= textureSize || y < 0 || y >= textureSize)
        {
            return;
        }

        int index = y * textureSize + x;
        if (alpha > pixels[index].a)
        {
            pixels[index] = new Color(1f, 1f, 1f, alpha);
        }
    }
}
