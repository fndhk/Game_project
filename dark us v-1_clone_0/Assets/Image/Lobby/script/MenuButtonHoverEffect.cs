using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 메인 메뉴 버튼의 마우스 오버 효과를 담당한다.
// 버튼이 선택되면 테두리와 글자가 조금 더 밝아지고,
// 마우스를 빼면 다시 어둡게 돌아간다.
[RequireComponent(typeof(Image))]
public class MenuButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Target")]
    // 버튼 배경 이미지이다.
    public Image buttonImage;

    // 버튼 안의 TMP 텍스트이다.
    public TMP_Text labelText;

    [Header("Base Colors")]
    // 기본 버튼 배경 색이다.
    public Color normalBackgroundColor = new Color(0f, 0f, 0f, 0.25f);

    // 마우스를 올렸을 때 버튼 배경 색이다.
    public Color hoverBackgroundColor = new Color(1f, 1f, 1f, 0.08f);

    // 버튼을 누르고 있을 때 배경 색이다.
    public Color pressedBackgroundColor = new Color(1f, 1f, 1f, 0.14f);

    // 기본 글자 색이다.
    public Color normalTextColor = new Color(0.72f, 0.72f, 0.72f, 1f);

    // 마우스를 올렸을 때 글자 색이다.
    public Color hoverTextColor = new Color(1f, 1f, 1f, 1f);

    [Header("Animation")]
    // 색이 바뀌는 속도이다.
    public float colorLerpSpeed = 12f;

    // 마우스 오버 시 살짝 커지는 크기이다.
    public float hoverScale = 1.025f;

    // 눌렀을 때 살짝 줄어드는 크기이다.
    public float pressedScale = 0.985f;

    // 크기 변경 속도이다.
    public float scaleLerpSpeed = 14f;

    // 현재 목표 배경 색이다.
    private Color targetBackgroundColor;

    // 현재 목표 글자 색이다.
    private Color targetTextColor;

    // 현재 목표 크기이다.
    private Vector3 targetScale;

    // 마우스가 올라가 있는지 저장한다.
    private bool isHovered = false;

    // 버튼을 누르고 있는지 저장한다.
    private bool isPressed = false;

    private void Awake()
    {
        // 참조가 비어 있으면 자동으로 찾는다.
        if (buttonImage == null)
        {
            buttonImage = GetComponent<Image>();
        }

        if (labelText == null)
        {
            labelText = GetComponentInChildren<TMP_Text>(true);
        }

        targetBackgroundColor = normalBackgroundColor;
        targetTextColor = normalTextColor;
        targetScale = Vector3.one;

        ApplyImmediateState();
    }

    private void Update()
    {
        // 배경 색을 부드럽게 바꾼다.
        if (buttonImage != null)
        {
            buttonImage.color = Color.Lerp(
                buttonImage.color,
                targetBackgroundColor,
                colorLerpSpeed * Time.unscaledDeltaTime
            );
        }

        // 글자 색을 부드럽게 바꾼다.
        if (labelText != null)
        {
            labelText.color = Color.Lerp(
                labelText.color,
                targetTextColor,
                colorLerpSpeed * Time.unscaledDeltaTime
            );
        }

        // 버튼 크기를 부드럽게 바꾼다.
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            scaleLerpSpeed * Time.unscaledDeltaTime
        );
    }

    // 마우스를 올렸을 때 호출된다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshTargetState();
    }

    // 마우스를 뺐을 때 호출된다.
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        isPressed = false;
        RefreshTargetState();
    }

    // 버튼을 눌렀을 때 호출된다.
    public void OnPointerDown(PointerEventData eventData)
    {
        isPressed = true;
        RefreshTargetState();
    }

    // 버튼에서 손을 뗐을 때 호출된다.
    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        RefreshTargetState();
    }

    // 현재 상태에 맞게 목표 값을 갱신한다.
    private void RefreshTargetState()
    {
        if (isPressed)
        {
            targetBackgroundColor = pressedBackgroundColor;
            targetTextColor = hoverTextColor;
            targetScale = Vector3.one * pressedScale;
            return;
        }

        if (isHovered)
        {
            targetBackgroundColor = hoverBackgroundColor;
            targetTextColor = hoverTextColor;
            targetScale = Vector3.one * hoverScale;
            return;
        }

        targetBackgroundColor = normalBackgroundColor;
        targetTextColor = normalTextColor;
        targetScale = Vector3.one;
    }

    // 시작 시 즉시 기본 상태를 적용한다.
    private void ApplyImmediateState()
    {
        if (buttonImage != null)
        {
            buttonImage.color = normalBackgroundColor;
        }

        if (labelText != null)
        {
            labelText.color = normalTextColor;
        }

        transform.localScale = Vector3.one;
    }
}