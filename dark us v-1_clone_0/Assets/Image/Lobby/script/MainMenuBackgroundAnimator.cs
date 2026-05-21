using UnityEngine;
using UnityEngine.UI;

// 메인 메뉴 배경의 분위기 연출을 담당하는 스크립트이다.
// 배경을 아주 천천히 확대/축소하고, 어두운 오버레이와 로고를 살짝 깜빡이게 만든다.
public class MainMenuBackgroundAnimator : MonoBehaviour
{
    [Header("Background Zoom")]
    // 천천히 확대/축소할 배경 RectTransform이다.
    public RectTransform backgroundRect;

    // 배경 기본 크기 배율이다.
    public float baseScale = 1.0f;

    // 배경이 최대로 커지는 배율이다.
    public float zoomAmount = 0.025f;

    // 배경 확대/축소 속도이다.
    public float zoomSpeed = 0.08f;

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

    // 시작 시 배경 기본 크기를 저장한다.
    private Vector3 initialBackgroundScale;

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
        }
    }

    private void Update()
    {
        AnimateBackgroundZoom();
        AnimateDarkOverlay();
        AnimateLogo();
        AnimateScanDotOverlay();
    }

    // 배경을 아주 천천히 확대/축소한다.
    private void AnimateBackgroundZoom()
    {
        if (backgroundRect == null)
        {
            return;
        }

        float wave = Mathf.Sin(Time.unscaledTime * zoomSpeed);
        float normalizedWave = (wave + 1f) * 0.5f;
        float targetScale = baseScale + normalizedWave * zoomAmount;

        backgroundRect.localScale = initialBackgroundScale * targetScale;
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
        float normalizedWave = (wave + 1f) * 0.5f;
        float targetAlpha = Mathf.Lerp(scanDotMinAlpha, scanDotMaxAlpha, normalizedWave);

        SetImageAlpha(scanDotOverlayImage, targetAlpha);
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
}