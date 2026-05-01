using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 이 스크립트는 현재 확정된 HUD 최종안을 관리한다.
// 체력 12칸, 스태미나 14칸, 중앙 스캔 쿨타임,
// 그리고 1/2 슬롯 선택 표시 v의 페이드아웃을 담당한다.
public class PlayerHUDController : MonoBehaviour
{
    [Header("플레이어 참조")]
    // 값을 읽어올 플레이어 스탯이다.
    public PlayerStats targetStats;

    // 중앙 쿨타임 표시를 위해 읽어올 스캐너이다.
    public LidarSpotScanner targetScanner;

    [Header("체력 / 스태미나 블록")]
    // 체력 블록들이다.
    // 반드시 왼쪽부터 오른쪽 순서로 12개 넣어야 한다.
    public Image[] vitalBlocks;

    // 스태미나 블록들이다.
    // 반드시 왼쪽부터 오른쪽 순서로 14개 넣어야 한다.
    public Image[] staminaBlocks;

    [Header("인벤토리 슬롯 표시")]
    // 선택 슬롯 위에 잠깐 나타날 v의 RectTransform이다.
    public RectTransform slotSelectMarkerRect;

    // 선택 슬롯 위에 잠깐 나타날 v의 그래픽이다.
    // TMP_Text 또는 Image를 넣으면 된다.
    public Graphic slotSelectMarkerGraphic;

    // 1번 슬롯의 위치 기준 RectTransform이다.
    public RectTransform slot1Rect;

    // 2번 슬롯의 위치 기준 RectTransform이다.
    public RectTransform slot2Rect;

    // 슬롯 기준으로 v를 얼마나 위에 띄울지 정하는 값이다.
    public float markerOffsetY = 18f;

    // 입력 후 v가 완전히 선명하게 유지될 시간이다.
    public float markerVisibleDuration = 1.1f;

    // 유지 시간이 끝난 뒤 서서히 사라지는 시간이다.
    public float markerFadeDuration = 0.9f;

    [Header("중앙 스캔 쿨타임")]
    // 화면 중앙에 둘 쿨타임 표시 텍스트이다.
    public TMP_Text centerScanCooldownText;

    // 스캔 준비 완료 상태에서의 알파값이다.
    public float cooldownReadyAlpha = 0.22f;

    // 쿨타임 진행 중 상태에서의 알파값이다.
    public float cooldownActiveAlpha = 0.9f;

    [Header("우상단 목표 텍스트")]
    // 목표 텍스트이다.
    public TMP_Text objectiveText;

    // 시작 시 넣어둘 기본 목표 문구이다.
    [TextArea]
    public string defaultObjectiveText = "Find 4 Relics";

    // 현재 선택된 슬롯 번호이다.
    // 0이면 1번 슬롯, 1이면 2번 슬롯이다.
    private int currentSelectedSlotIndex = 0;

    // 마지막으로 1 또는 2 입력을 받은 시간이다.
    private float lastSlotInputTime = -999f;

    // 한 번이라도 슬롯 표시를 보여준 적이 있는지 저장한다.
    private bool hasShownSlotMarkerOnce = false;

    // 마지막으로 표시했던 체력 칸 수이다.
    private int lastVitalVisibleCount = -1;

    // 마지막으로 표시했던 스태미나 칸 수이다.
    private int lastStaminaVisibleCount = -1;

    // 시작 시 필요한 참조를 자동으로 보정한다.
    private void Awake()
    {
        AutoFindReferences();
        ApplyDefaultObjectiveText();
        HideSlotMarkerImmediately();
        RefreshAllHudImmediately();
    }

    // 매 프레임 HUD 상태를 갱신한다.
    private void Update()
    {
        UpdateBars();
        HandleSlotInput();
        UpdateSlotMarkerFade();
        UpdateCenterScanCooldown();
        UpdateObjectiveText();
    }

    // 비어 있는 참조를 자동으로 찾아 넣는 함수이다.
    private void AutoFindReferences()
    {
        // 스탯 참조가 비어 있으면 자신 또는 부모에서 찾는다.
        if (targetStats == null)
        {
            targetStats = GetComponent<PlayerStats>();

            if (targetStats == null)
            {
                targetStats = GetComponentInParent<PlayerStats>();
            }
        }

        // 스캐너 참조가 비어 있으면 자신 또는 자식에서 찾는다.
        if (targetScanner == null)
        {
            targetScanner = GetComponent<LidarSpotScanner>();

            if (targetScanner == null)
            {
                targetScanner = GetComponentInChildren<LidarSpotScanner>(true);
            }

            if (targetScanner == null)
            {
                targetScanner = GetComponentInParent<LidarSpotScanner>();
            }
        }
    }

    // 시작 시 기본 목표 텍스트를 넣는 함수이다.
    private void ApplyDefaultObjectiveText()
    {
        // 목표 텍스트가 없으면 종료한다.
        if (objectiveText == null)
        {
            return;
        }

        // 현재 텍스트가 비어 있을 때만 기본 문구를 넣는다.
        if (string.IsNullOrWhiteSpace(objectiveText.text))
        {
            objectiveText.text = defaultObjectiveText;
        }
    }

    // 시작 시 HUD를 현재 값에 맞춰 강제로 한 번 갱신한다.
    private void RefreshAllHudImmediately()
    {
        UpdateBars(true);
        UpdateCenterScanCooldown();
        UpdateObjectiveText();
    }

    // 목표 텍스트를 현재 진행도에 맞게 갱신한다.
    private void UpdateObjectiveText()
    {
        if (objectiveText == null)
        {
            return;
        }

        if (LabObjectiveManager.Instance == null)
        {
            return;
        }

        objectiveText.text = LabObjectiveManager.Instance.GetHudObjectiveText();
    }

    // 체력과 스태미나 블록을 갱신하는 함수이다.
   private void UpdateBars(bool forceRefresh = false)
{
    // 스탯 참조가 없으면 종료한다.
    if (targetStats == null)
    {
        return;
    }

    // 체력 비율을 현재 블록 개수에 맞는 칸 수로 바꾼다.
    int vitalVisibleCount = GetVisibleBlockCount(
        targetStats.GetHealthNormalized(),
        vitalBlocks != null ? vitalBlocks.Length : 0
    );

    // 스태미나 비율을 현재 블록 개수에 맞는 칸 수로 바꾼다.
    int staminaVisibleCount = GetStaminaVisibleBlockCount(
        targetStats.GetStaminaNormalized(),
        staminaBlocks != null ? staminaBlocks.Length : 0
    );

    // 체력 칸 수가 바뀌었을 때만 실제 표시를 갱신한다.
    if (forceRefresh || vitalVisibleCount != lastVitalVisibleCount)
    {
        SetBlockVisibility(vitalBlocks, vitalVisibleCount);
        lastVitalVisibleCount = vitalVisibleCount;
    }

    // 스태미나 칸 수가 바뀌었을 때만 실제 표시를 갱신한다.
    if (forceRefresh || staminaVisibleCount != lastStaminaVisibleCount)
    {
        SetBlockVisibility(staminaBlocks, staminaVisibleCount);
        lastStaminaVisibleCount = staminaVisibleCount;
    }
}

    // 0~1 비율을 실제 보일 칸 수로 바꾸는 함수이다.
    private int GetVisibleBlockCount(float normalizedValue, int totalBlockCount)
    {
        // 블록 수가 0 이하면 0을 반환한다.
        if (totalBlockCount <= 0)
        {
            return 0;
        }

        // 비율을 0~1 사이로 고정한다.
        float clampedValue = Mathf.Clamp01(normalizedValue);

        // 비율이 0이면 0칸을 반환한다.
        if (clampedValue <= 0f)
        {
            return 0;
        }

        // 비율이 1이면 전체 칸 수를 반환한다.
        if (clampedValue >= 1f)
        {
            return totalBlockCount;
        }

        // 중간값은 올림 처리해서 마지막 한 칸이 너무 빨리 사라지지 않게 한다.
        return Mathf.Clamp(Mathf.CeilToInt(clampedValue * totalBlockCount), 0, totalBlockCount);
    }
    
    // 스태미나 블록은 너무 조금 찼을 때 첫 칸이 바로 켜지지 않게 계산한다.
    private int GetStaminaVisibleBlockCount(float normalizedValue, int totalBlockCount)
    {
        // 블록 수가 0 이하면 0을 반환한다.
        if (totalBlockCount <= 0)
        {
            return 0;
        }

        // 비율을 0~1 사이로 고정한다.
        float clampedValue = Mathf.Clamp01(normalizedValue);

        // 비율이 0이면 0칸이다.
        if (clampedValue <= 0f)
        {
            return 0;
        }

        // 블록 1칸당 차지하는 비율이다.
        float oneBlockRatio = 1f / totalBlockCount;

        // 첫 칸의 절반 정도보다 적게 찼으면 아직 0칸으로 보이게 한다.
        if (clampedValue < oneBlockRatio * 0.5f)
        {
            return 0;
        }

        // 스태미나는 올림 대신 반올림을 써서 너무 빨리 1칸이 켜지지 않게 한다.
        return Mathf.Clamp(Mathf.RoundToInt(clampedValue * totalBlockCount), 0, totalBlockCount);
    }
    
    // 실제 블록 오브젝트를 켜고 끄는 함수이다.
    private void SetBlockVisibility(Image[] blocks, int visibleCount)
    {
        // 블록 배열이 비어 있으면 종료한다.
        if (blocks == null)
        {
            return;
        }

        // 왼쪽부터 visibleCount개만 남기고 나머지는 끈다.
        for (int i = 0; i < blocks.Length; i++)
        {
            // 비어 있는 원소는 건너뛴다.
            if (blocks[i] == null)
            {
                continue;
            }

            bool shouldBeVisible = i < visibleCount;

            // 상태가 다를 때만 실제 오브젝트를 켜고 끈다.
            if (blocks[i].gameObject.activeSelf != shouldBeVisible)
            {
                blocks[i].gameObject.SetActive(shouldBeVisible);
            }
        }
    }

    // 1번과 2번 입력을 받아 선택 표시를 갱신하는 함수이다.
    private void HandleSlotInput()
    {
        // 1번을 누르면 1번 슬롯을 선택한다.
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentSelectedSlotIndex = 0;
            ShowSlotMarkerAtCurrentSlot();
            return;
        }

        // 2번을 누르면 2번 슬롯을 선택한다.
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            currentSelectedSlotIndex = 1;
            ShowSlotMarkerAtCurrentSlot();
            return;
        }
    }

    // 현재 선택된 슬롯 위치에 v를 즉시 보여주는 함수이다.
        // 현재 선택된 슬롯 위치에 v를 즉시 보여주는 함수이다.
        // 현재 선택된 슬롯 위치에 v를 즉시 보여주는 함수이다.
    private void ShowSlotMarkerAtCurrentSlot()
    {
        // 표시용 참조가 없으면 종료한다.
        if (slotSelectMarkerRect == null || slotSelectMarkerGraphic == null)
        {
            return;
        }

        // 현재 선택된 슬롯에 맞는 목표 RectTransform을 가져온다.
        RectTransform targetSlotRect = currentSelectedSlotIndex == 0 ? slot1Rect : slot2Rect;

        // 목표가 없으면 종료한다.
        if (targetSlotRect == null)
        {
            return;
        }

        // 마커 오브젝트를 켠다.
        if (!slotSelectMarkerRect.gameObject.activeSelf)
        {
            slotSelectMarkerRect.gameObject.SetActive(true);
        }

        // 목표 위치와 똑같이 맞춘다.
        slotSelectMarkerRect.anchoredPosition = targetSlotRect.anchoredPosition;

        // 입력 직후에는 완전히 선명하게 보이게 만든다.
        SetSlotMarkerAlpha(1f);

        // 마지막 입력 시간을 기록한다.
        lastSlotInputTime = Time.time;

        // 한 번이라도 보여준 상태로 기록한다.
        hasShownSlotMarkerOnce = true;
    }

    // 입력이 없을 때 v를 서서히 사라지게 만드는 함수이다.
    private void UpdateSlotMarkerFade()
    {
        // 아직 한 번도 보여준 적이 없으면 종료한다.
        if (!hasShownSlotMarkerOnce)
        {
            return;
        }

        // 필수 참조가 없으면 종료한다.
        if (slotSelectMarkerRect == null || slotSelectMarkerGraphic == null)
        {
            return;
        }

        // 표시 오브젝트가 꺼져 있으면 더 처리할 필요가 없다.
        if (!slotSelectMarkerRect.gameObject.activeSelf)
        {
            return;
        }

        // 마지막 입력 후 지난 시간을 계산한다.
        float elapsedSinceLastInput = Time.time - lastSlotInputTime;

        // 아직 유지 시간 안이면 완전히 보이게 둔다.
        if (elapsedSinceLastInput <= markerVisibleDuration)
        {
            SetSlotMarkerAlpha(1f);
            return;
        }

        // 유지 시간이 끝난 뒤부터 페이드 진행 시간을 계산한다.
        float fadeElapsed = elapsedSinceLastInput - markerVisibleDuration;

        // 페이드 시간이 0 이하면 즉시 숨긴다.
        if (markerFadeDuration <= 0f)
        {
            HideSlotMarkerImmediately();
            return;
        }

        // 1에서 0으로 줄어드는 알파를 계산한다.
        float alpha = 1f - Mathf.Clamp01(fadeElapsed / markerFadeDuration);

        // 알파를 반영한다.
        SetSlotMarkerAlpha(alpha);

        // 완전히 사라졌으면 오브젝트를 꺼둔다.
        if (alpha <= 0f)
        {
            slotSelectMarkerRect.gameObject.SetActive(false);
        }
    }

    // v의 알파를 바꾸는 함수이다.
    private void SetSlotMarkerAlpha(float alpha)
    {
        // 그래픽이 없으면 종료한다.
        if (slotSelectMarkerGraphic == null)
        {
            return;
        }

        // 현재 색을 가져온다.
        Color color = slotSelectMarkerGraphic.color;

        // 알파만 바꿔서 다시 넣는다.
        color.a = Mathf.Clamp01(alpha);
        slotSelectMarkerGraphic.color = color;
    }

    // v를 즉시 숨기는 함수이다.
    private void HideSlotMarkerImmediately()
    {
        // 표시용 RectTransform이 있으면 오브젝트를 꺼둔다.
        if (slotSelectMarkerRect != null)
        {
            slotSelectMarkerRect.gameObject.SetActive(false);
        }

        // 그래픽이 있으면 알파를 0으로 만든다.
        if (slotSelectMarkerGraphic != null)
        {
            SetSlotMarkerAlpha(0f);
        }
    }

    // 중앙 스캔 쿨타임 아이콘을 갱신하는 함수이다.
    private void UpdateCenterScanCooldown()
    {
        // 중앙 텍스트가 없으면 종료한다.
        if (centerScanCooldownText == null)
        {
            return;
        }

        // 기본값은 준비 완료 상태로 둔다.
        bool isReady = true;
        float cooldownNormalized = 1f;

        // 스캐너가 있으면 실제 쿨타임 값을 읽어온다.
        if (targetScanner != null)
        {
            isReady = targetScanner.IsPulseReady;
            cooldownNormalized = targetScanner.GetCooldownNormalized();
        }

        // 현재 상태에 맞는 심볼을 넣는다.
        centerScanCooldownText.text = GetCooldownSymbol(isReady, cooldownNormalized);

        // 준비 완료일 때는 희미하게, 진행 중일 때는 더 또렷하게 보이게 한다.
        Color color = centerScanCooldownText.color;
        color.a = isReady ? cooldownReadyAlpha : cooldownActiveAlpha;
        centerScanCooldownText.color = color;
    }

    // 현재 쿨타임 상태에 맞는 심볼을 반환하는 함수이다.
        // 현재 쿨타임 상태에 맞는 심볼을 반환하는 함수이다.
    private string GetCooldownSymbol(bool isReady, float cooldownNormalized)
    {
        // 준비 완료면 가장 또렷한 문자로 표시한다.
        if (isReady)
        {
            return "O";
        }

        // 진행률을 0~1로 고정한다.
        float clampedValue = Mathf.Clamp01(cooldownNormalized);

        // 폰트 깨짐을 피하려고 ASCII 문자만 쓴다.
        if (clampedValue < 0.34f)
        {
            return ".";
        }

        if (clampedValue < 0.67f)
        {
            return ":";
        }

        return "o";
    }
}
