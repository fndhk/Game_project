using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인게임 HUD를 런타임과 에디터에서 생성하고 갱신한다.
// 기존 씬에 배치된 HUD 요소는 숨기고, 작은 LIDAR 스타일 HUD를 새로 만든다.
[ExecuteAlways]
public class PlayerHUDController : MonoBehaviour
{
    [Header("플레이어 참조")]
    public PlayerStats targetStats;
    public LidarSpotScanner targetScanner;
    public PlayerInventory targetInventory;
    public InstancedScanDotRenderer targetDotRenderer;
    public PlayerCombatTarget targetCombatTarget;

    [Header("Legacy HUD References")]
    public Image[] vitalBlocks;
    public Image[] staminaBlocks;
    public RectTransform slotSelectMarkerRect;
    public Graphic slotSelectMarkerGraphic;
    public RectTransform slot1Rect;
    public RectTransform slot2Rect;
    public float markerOffsetY = 18f;
    public float markerVisibleDuration = 1.1f;
    public float markerFadeDuration = 0.9f;
    public TMP_Text centerScanCooldownText;
    public float cooldownReadyAlpha = 0.22f;
    public float cooldownActiveAlpha = 0.9f;
    public TMP_Text objectiveText;

    [Header("Objective")]
    [TextArea]
    public string defaultObjectiveText = "Find Target Computers 0/4";

    [Header("Runtime HUD")]
    public bool buildRuntimeHud = true;
    public int vitalSegmentCount = 12;
    public int staminaSegmentCount = 14;
    public int dotMeterSegmentCount = 28;
    public float roleRevealDuration = 4f;

    private Canvas hudCanvas;
    private RectTransform hudRoot;
    private CanvasGroup hudCanvasGroup;
    private Image[] runtimeVitalBlocks;
    private Image[] runtimeStaminaBlocks;
    private Image[] dotMeterBlocks;
    private TMP_Text dotCounterText;
    private TMP_Text objectiveRuntimeText;
    private TMP_Text vitalLabelText;
    private TMP_Text staminaLabelText;
    private TMP_Text dotMemoryLabelText;
    private TMP_Text roundTimerText;
    private TMP_Text killTimeWarningText;
    private RectTransform micStatusRoot;
    private Image micStatusIcon;
    private TMP_Text micStatusText;
    private RectTransform roleRevealRoot;
    private TMP_Text roleRevealTitleText;
    private TMP_Text roleRevealRoleText;
    private TMP_Text scanCooldownText;
    private RectTransform scanSweepRect;
    private Image scanCooldownFill;
    private RectTransform[] slotRects;
    private Image[] slotFrames;
    private Image[] slotIcons;
    private TMP_Text[] slotCountTexts;
    private TMP_Text[] slotKeyTexts;
    private Image[] slotHighlights;
    private RectTransform[] slotHighlightRects;

    private Sprite whiteSprite;
    private Sprite cameraIconSprite;
    private Sprite knifeIconSprite;
    private Sprite medkitIconSprite;
    private Sprite micOpenIconSprite;
    private Sprite micMutedIconSprite;
    private Sprite emptyIconSprite;

    private readonly Color panelColor = new Color(0f, 0f, 0f, 0.82f);
    private readonly Color lineColor = new Color(0.92f, 0.96f, 0.92f, 0.96f);
    private readonly Color dimLineColor = new Color(0.56f, 0.62f, 0.60f, 0.38f);
    private readonly Color vitalColor = new Color(0.96f, 0.96f, 0.86f, 1f);
    private readonly Color staminaColor = new Color(0.62f, 0.92f, 1f, 0.96f);
    private readonly Color amberColor = new Color(1f, 0.74f, 0.18f, 1f);
    private readonly Color cyanColor = new Color(0.54f, 0.88f, 1f, 0.96f);
    private bool hasBuiltHud;
    private PlayerRole lastDisplayedRole;
    private bool hasDisplayedRole;
    private float roleRevealEndTime;
    private int lastLanguageIndex = -1;
    private bool lastMicMutedState;
    private bool hasMicState;
    private float micStatusVisibleUntil;
    private int lastKillTimeWindowIndex = -2;
    private float killTimeMessageVisibleUntil;

    private void Awake()
    {
        EnsureHudReady();
    }

    private void OnEnable()
    {
        EnsureHudReady();
    }

    private void Update()
    {
        EnsureHudReady();

        if (ApplyOverlayVisibility())
        {
            return;
        }

        AutoFindReferences();
        UpdateVitals();
        UpdateInventory();
        UpdateScanCooldown();
        UpdateDotMemory();
        UpdateObjectiveText();
        UpdateLocalizedStaticText();
        UpdateHudOpacity();
        UpdateRoundTimer();
        UpdateVoiceStatus();
        UpdateHudAnimation();
    }

    private void EnsureHudReady()
    {
        AutoFindReferences();
        PrepareSprites();
        HideLegacyHud();

        if (buildRuntimeHud && (!hasBuiltHud || hudRoot == null))
        {
            BuildRuntimeHud();
            hasBuiltHud = true;
        }

        RefreshAll();
    }

    private bool ApplyOverlayVisibility()
    {
        if (hudRoot == null)
        {
            return false;
        }

        bool hideHud = Application.isPlaying && (InGamePauseMenu.IsOpen || RoleRevealIntro.IsShowing);

        if (hudRoot.gameObject.activeSelf == hideHud)
        {
            hudRoot.gameObject.SetActive(!hideHud);
        }

        return hideHud;
    }

    private void AutoFindReferences()
    {
        if (targetStats == null)
        {
            targetStats = GetComponent<PlayerStats>();
        }

        if (targetInventory == null)
        {
            targetInventory = GetComponent<PlayerInventory>();
        }

        if (targetCombatTarget == null)
        {
            targetCombatTarget = GetComponent<PlayerCombatTarget>();
        }

        if (targetScanner == null)
        {
            targetScanner = GetComponentInChildren<LidarSpotScanner>(true);
        }

        if (targetScanner == null)
        {
            targetScanner = GetComponentInParent<LidarSpotScanner>();
        }

        if (targetDotRenderer == null)
        {
            targetDotRenderer = GetComponentInChildren<InstancedScanDotRenderer>(true);
        }

        if (targetDotRenderer == null && targetScanner != null)
        {
            targetDotRenderer = targetScanner.GetComponent<InstancedScanDotRenderer>();
        }

        if (targetDotRenderer == null)
        {
            targetDotRenderer = Object.FindFirstObjectByType<InstancedScanDotRenderer>();
        }
    }

    private void HideLegacyHud()
    {
        SetGraphicsEnabled(vitalBlocks, false);
        SetGraphicsEnabled(staminaBlocks, false);

        if (slotSelectMarkerRect != null)
        {
            slotSelectMarkerRect.gameObject.SetActive(false);
        }

        if (slot1Rect != null)
        {
            slot1Rect.gameObject.SetActive(false);
        }

        if (slot2Rect != null)
        {
            slot2Rect.gameObject.SetActive(false);
        }

        if (centerScanCooldownText != null)
        {
            centerScanCooldownText.gameObject.SetActive(false);
        }

        if (objectiveText != null)
        {
            objectiveText.gameObject.SetActive(false);
        }
    }

    private void SetGraphicsEnabled(Image[] images, bool enabled)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
            {
                images[i].gameObject.SetActive(enabled);
            }
        }
    }

    private void BuildRuntimeHud()
    {
        hudCanvas = GetComponentInChildren<Canvas>(true);

        if (!IsValidHudCanvas(hudCanvas))
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                if (IsValidHudCanvas(canvases[i]))
                {
                    hudCanvas = canvases[i];
                    break;
                }
            }
        }

        if (!IsValidHudCanvas(hudCanvas))
        {
            GameObject canvasObject = new GameObject("Canvas_HUD");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        hudCanvas.overrideSorting = true;
        hudCanvas.sortingOrder = 100;

        HideExistingCanvasHudChildren();

        Transform existing = hudCanvas.transform.Find("Runtime_LidarHud");

        if (existing != null)
        {
            DestroyHudObject(existing.gameObject);
        }

        hudRoot = CreateRect("Runtime_LidarHud", hudCanvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;
        hudCanvasGroup = hudRoot.gameObject.AddComponent<CanvasGroup>();

        BuildVitalModule();
        BuildInventoryModule();
        BuildCenterScanModule();
        BuildDotMemoryModule();
        BuildObjectiveModule();
        BuildRoundTimerModule();
        BuildVoiceStatusModule();
    }

    private bool IsValidHudCanvas(Canvas candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string canvasName = candidate.gameObject.name;
        return canvasName != "InGamePauseCanvas" &&
               canvasName != "Dark Scan Loading Screen";
    }

    private void DestroyHudObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private void HideExistingCanvasHudChildren()
    {
        if (hudCanvas == null)
        {
            return;
        }

        for (int i = 0; i < hudCanvas.transform.childCount; i++)
        {
            Transform child = hudCanvas.transform.GetChild(i);

            if (child == null || child.name == "Runtime_LidarHud")
            {
                continue;
            }

            if (child.GetComponent<InGamePauseMenu>() != null || child.name.Contains("Pause"))
            {
                continue;
            }

            child.gameObject.SetActive(false);
        }
    }

    private void BuildVitalModule()
    {
        RectTransform root = CreateRect("Vitals", hudRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(70f, 66f));
        root.sizeDelta = new Vector2(315f, 80f);
        AddPanel(root, new Color(0f, 0f, 0f, 0.82f));

        vitalLabelText = CreateLabel(T("VITAL"), root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -4f), 15, lineColor, TextAlignmentOptions.Left);
        vitalLabelText.rectTransform.sizeDelta = new Vector2(78f, 24f);
        staminaLabelText = CreateLabel(T("STAM"), root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -36f), 13, new Color(0.82f, 0.90f, 0.90f, 0.92f), TextAlignmentOptions.Left);
        staminaLabelText.rectTransform.sizeDelta = new Vector2(78f, 24f);

        runtimeVitalBlocks = CreateSegmentRow("VitalBlocks", root, vitalSegmentCount, new Vector2(84f, -4f), new Vector2(11f, 22f), 6f);
        runtimeStaminaBlocks = CreateSegmentRow("StaminaBlocks", root, staminaSegmentCount, new Vector2(84f, -38f), new Vector2(9f, 13f), 5f);
    }

    private void BuildInventoryModule()
    {
        RectTransform root = CreateRect("Inventory", hudRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 52f));
        root.sizeDelta = new Vector2(170f, 78f);

        slotRects = new RectTransform[2];
        slotFrames = new Image[2];
        slotIcons = new Image[2];
        slotCountTexts = new TMP_Text[2];
        slotKeyTexts = new TMP_Text[2];
        slotHighlights = new Image[2];
        slotHighlightRects = new RectTransform[2];

        for (int i = 0; i < 2; i++)
        {
            RectTransform slot = CreateRect("Slot_" + (i + 1), root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f + i * 74f, 0f));
            slot.sizeDelta = new Vector2(56f, 56f);
            slotRects[i] = slot;

            slotFrames[i] = AddImage(slot, panelColor);
            AddOutline(slot, dimLineColor, new Vector2(1f, -1f));

            slotHighlightRects[i] = CreateRect("Highlight", slot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            slotHighlightRects[i].offsetMin = new Vector2(-5f, -5f);
            slotHighlightRects[i].offsetMax = new Vector2(5f, 5f);
            slotHighlights[i] = AddImage(slotHighlightRects[i], new Color(1f, 0.74f, 0.18f, 0.12f));
            AddOutline(slotHighlightRects[i], amberColor, new Vector2(1.5f, -1.5f));

            slotIcons[i] = AddImage(CreateRect("Icon", slot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero), Color.white);
            slotIcons[i].rectTransform.offsetMin = new Vector2(10f, 10f);
            slotIcons[i].rectTransform.offsetMax = new Vector2(-10f, -10f);

            slotCountTexts[i] = CreateLabel("x0", slot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(6f, -2f), 15, lineColor, TextAlignmentOptions.Right);
            slotKeyTexts[i] = CreateLabel("[" + (i + 1) + "]", slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), 15, amberColor, TextAlignmentOptions.Center);
        }
    }

    private void BuildCenterScanModule()
    {
        RectTransform root = CreateRect("CenterScan", hudRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        root.sizeDelta = new Vector2(58f, 58f);

        scanCooldownFill = AddImage(CreateRect("CooldownFill", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero), new Color(0.68f, 0.90f, 1f, 0.16f));
        scanCooldownFill.sprite = CreateCircleSprite(96, 38f);
        scanCooldownFill.type = Image.Type.Filled;
        scanCooldownFill.fillMethod = Image.FillMethod.Radial360;
        scanCooldownFill.fillOrigin = 2;
        scanCooldownFill.fillClockwise = true;
        scanCooldownFill.rectTransform.sizeDelta = new Vector2(34f, 34f);

        Image inner = AddImage(CreateRect("CooldownInner", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero), new Color(0f, 0f, 0f, 0.48f));
        inner.sprite = CreateCircleSprite(64, 24f);
        inner.rectTransform.sizeDelta = new Vector2(23f, 23f);

        CreateCrosshairLine(root, new Vector2(-10f, 0f), new Vector2(5f, 1f));
        CreateCrosshairLine(root, new Vector2(10f, 0f), new Vector2(5f, 1f));
        CreateCrosshairLine(root, new Vector2(0f, -10f), new Vector2(1f, 5f));
        CreateCrosshairLine(root, new Vector2(0f, 10f), new Vector2(1f, 5f));

        Image dot = AddImage(CreateRect("CenterDot", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero), new Color(0.78f, 0.80f, 0.76f, 0.58f));
        dot.sprite = CreateCircleSprite(16, 7f);
        dot.rectTransform.sizeDelta = new Vector2(3.2f, 3.2f);

        scanSweepRect = CreateRect("SweepArc", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
        Image sweep = AddImage(scanSweepRect, new Color(0.65f, 0.88f, 1f, 0.14f));
        sweep.sprite = whiteSprite;
        scanSweepRect.sizeDelta = new Vector2(1f, 18f);
        scanSweepRect.anchoredPosition = new Vector2(0f, 9f);

        scanCooldownText = CreateLabel(T("SCAN"), root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), 10, new Color(0.82f, 0.94f, 1f, 0.9f), TextAlignmentOptions.Center);
    }

    private void BuildDotMemoryModule()
    {
        RectTransform root = CreateRect("DotMemory", hudRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -46f));
        root.sizeDelta = new Vector2(430f, 58f);

        dotMemoryLabelText = CreateLabel(T("DOT MEMORY"), root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, 13, lineColor, TextAlignmentOptions.Left);
        dotMemoryLabelText.rectTransform.sizeDelta = new Vector2(190f, 24f);
        dotCounterText = CreateLabel("0 / 150k", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, 13, lineColor, TextAlignmentOptions.Right);
        dotCounterText.rectTransform.sizeDelta = new Vector2(160f, 24f);
        dotMeterBlocks = CreateSegmentRow("DotMeter", root, dotMeterSegmentCount, new Vector2(0f, -31f), new Vector2(8f, 10f), 4f);
    }

    private void BuildObjectiveModule()
    {
        objectiveRuntimeText = CreateLabel(T("Find Target Computers") + " 0/4", hudRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -50f), 17, lineColor, TextAlignmentOptions.Right);
        objectiveRuntimeText.rectTransform.sizeDelta = new Vector2(520f, 38f);
    }

    private void BuildRoundTimerModule()
    {
        roundTimerText = CreateLabel("20:00", hudRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), 24, new Color(0.96f, 0.96f, 0.92f, 0.98f), TextAlignmentOptions.Center);
        roundTimerText.rectTransform.sizeDelta = new Vector2(220f, 42f);

        killTimeWarningText = CreateLabel("", hudRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), 15, new Color(1f, 0.18f, 0.12f, 0.98f), TextAlignmentOptions.Center);
        killTimeWarningText.rectTransform.sizeDelta = new Vector2(420f, 28f);
        killTimeWarningText.gameObject.SetActive(false);
    }

    private void BuildVoiceStatusModule()
    {
        micStatusRoot = CreateRect("MicStatus", hudRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 142f));
        micStatusRoot.sizeDelta = new Vector2(190f, 42f);
        AddPanel(micStatusRoot, new Color(0f, 0f, 0f, 0.72f));
        AddOutline(micStatusRoot, new Color(0.72f, 0.82f, 0.82f, 0.42f), new Vector2(1f, -1f));

        micStatusIcon = AddImage(CreateRect("MicIcon", micStatusRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f)), cyanColor);
        micStatusIcon.rectTransform.sizeDelta = new Vector2(30f, 30f);
        micStatusIcon.sprite = micOpenIconSprite;

        micStatusText = CreateLabel("MIC OPEN", micStatusRoot, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), 16, cyanColor, TextAlignmentOptions.Left);
        micStatusText.rectTransform.sizeDelta = new Vector2(-70f, 28f);
        micStatusRoot.gameObject.SetActive(false);
    }

    private void BuildRoleRevealModule()
    {
        roleRevealRoot = CreateRect("RoleReveal", hudRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f));
        roleRevealRoot.sizeDelta = new Vector2(560f, 190f);
        AddPanel(roleRevealRoot, new Color(0f, 0f, 0f, 0.62f));

        roleRevealTitleText = CreateLabel(T("Role"), roleRevealRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), 28, new Color(0.76f, 0.82f, 0.84f, 1f), TextAlignmentOptions.Center);
        roleRevealTitleText.rectTransform.sizeDelta = new Vector2(500f, 48f);

        roleRevealRoleText = CreateLabel(T("Citizen"), roleRevealRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), 58, vitalColor, TextAlignmentOptions.Center);
        roleRevealRoleText.rectTransform.sizeDelta = new Vector2(500f, 90f);

        LocalizedTmpFontProvider.Apply(roleRevealTitleText);
        LocalizedTmpFontProvider.Apply(roleRevealRoleText);
        roleRevealRoot.gameObject.SetActive(false);
    }

    private void RefreshAll()
    {
        UpdateVitals();
        UpdateInventory();
        UpdateScanCooldown();
        UpdateDotMemory();
        UpdateObjectiveText();
        UpdateLocalizedStaticText();
        UpdateRoundTimer();
    }

    private void UpdateVitals()
    {
        if (targetStats == null)
        {
            return;
        }

        SetSegmentFill(runtimeVitalBlocks, targetStats.GetHealthNormalized(), vitalColor, new Color(0.38f, 0.39f, 0.36f, 0.38f));
        SetSegmentFill(runtimeStaminaBlocks, targetStats.GetStaminaNormalized(), staminaColor, new Color(0.28f, 0.38f, 0.42f, 0.34f));
    }

    private void UpdateInventory()
    {
        if (targetInventory == null || slotIcons == null)
        {
            return;
        }

        int selected = targetInventory.SelectedSlotIndex;

        for (int i = 0; i < slotIcons.Length; i++)
        {
            PlayerInventory.ItemSlot slot = targetInventory.slots != null && i < targetInventory.slots.Length ? targetInventory.slots[i] : null;
            ItemType itemType = slot != null && slot.amount > 0 ? slot.itemType : ItemType.None;
            int amount = slot != null ? Mathf.Max(0, slot.amount) : 0;

            slotIcons[i].sprite = GetIconSprite(itemType);
            slotIcons[i].color = itemType == ItemType.None ? new Color(0.45f, 0.48f, 0.46f, 0.22f) : new Color(0.88f, 0.90f, 0.84f, 0.92f);
            slotCountTexts[i].text = amount > 0 ? "x" + amount : "";
            slotKeyTexts[i].text = "[" + (i == 0
                ? GameInputBindings.GetLabel(GameInputBindings.Slot1Key, KeyCode.Alpha1)
                : GameInputBindings.GetLabel(GameInputBindings.Slot2Key, KeyCode.Alpha2)) + "]";
            slotHighlights[i].gameObject.SetActive(i == selected);
            slotFrames[i].color = i == selected ? new Color(0f, 0f, 0f, 0.92f) : panelColor;
            slotKeyTexts[i].color = i == selected ? amberColor : new Color(0.76f, 0.75f, 0.66f, 0.52f);
        }
    }

    private void UpdateScanCooldown()
    {
        if (scanCooldownText == null)
        {
            return;
        }

        bool ready = targetScanner == null || targetScanner.IsPulseReady;
        float normalized = targetScanner != null ? targetScanner.GetCooldownNormalized() : 1f;
        float fill = ready ? 1f : Mathf.Clamp01(normalized);

        scanCooldownFill.fillAmount = fill;
        scanCooldownFill.color = ready ? new Color(0.70f, 0.92f, 1f, 0.14f) : new Color(1f, 0.74f, 0.18f, 0.28f);
        scanCooldownText.text = ready ? T("SCAN RDY") : T("SCAN") + " " + Mathf.RoundToInt(fill * 100f) + "%";
    }

    private void UpdateDotMemory()
    {
        if (dotCounterText == null || targetDotRenderer == null)
        {
            return;
        }

        int active = targetDotRenderer.GetActiveDotCount();
        int max = targetDotRenderer.GetMaxActiveDotCount();
        float normalized = max > 0 ? active / (float)max : 0f;

        SetSegmentFill(dotMeterBlocks, normalized, cyanColor, new Color(0.38f, 0.45f, 0.45f, 0.18f));
        dotCounterText.text = FormatDotCount(active) + " / " + FormatDotCount(max);
    }

    private void UpdateObjectiveText()
    {
        if (objectiveRuntimeText == null)
        {
            return;
        }

        if (LabObjectiveManager.Instance != null)
        {
            objectiveRuntimeText.text = LabObjectiveManager.Instance.GetHudObjectiveText();
        }
        else if (string.IsNullOrWhiteSpace(objectiveRuntimeText.text))
        {
            objectiveRuntimeText.text = T("Find Target Computers") + " 0/4";
        }
    }

    private void UpdateLocalizedStaticText()
    {
        int languageIndex = InGameLocalization.LanguageIndex;
        if (lastLanguageIndex == languageIndex)
        {
            return;
        }

        lastLanguageIndex = languageIndex;

        if (vitalLabelText != null)
        {
            vitalLabelText.text = T("VITAL");
            vitalLabelText.rectTransform.sizeDelta = new Vector2(78f, 24f);
        }

        if (staminaLabelText != null)
        {
            staminaLabelText.text = T("STAM");
            staminaLabelText.rectTransform.sizeDelta = new Vector2(78f, 24f);
        }

        if (dotMemoryLabelText != null)
        {
            dotMemoryLabelText.text = T("DOT MEMORY");
            dotMemoryLabelText.rectTransform.sizeDelta = new Vector2(190f, 24f);
        }

        if (roleRevealTitleText != null)
        {
            roleRevealTitleText.text = T("Role");
        }

        hasDisplayedRole = false;
        UpdateScanCooldown();
        UpdateObjectiveText();
    }

    private void UpdateRoundTimer()
    {
        if (roundTimerText == null)
        {
            return;
        }

        float remaining = RoundTimer.RemainingSeconds;
        int displayRemaining = Mathf.CeilToInt(remaining);
        int minutes = displayRemaining / 60;
        int seconds = displayRemaining % 60;
        roundTimerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");

        int killTimeWindowIndex = RoundTimer.CurrentKillTimeWindowIndex;
        bool isKillTime = killTimeWindowIndex >= 0;

        roundTimerText.color = isKillTime
            ? new Color(1f, 0.18f, 0.12f, 0.98f)
            : new Color(0.96f, 0.96f, 0.92f, 0.98f);

        if (killTimeWindowIndex != lastKillTimeWindowIndex)
        {
            lastKillTimeWindowIndex = killTimeWindowIndex;

            if (isKillTime)
            {
                killTimeMessageVisibleUntil = Time.unscaledTime + 4f;
            }
        }

        UpdateKillTimeWarning(isKillTime);
    }

    private void UpdateKillTimeWarning(bool isKillTime)
    {
        if (killTimeWarningText == null)
        {
            return;
        }

        bool shouldShow = isKillTime && (!Application.isPlaying || Time.unscaledTime <= killTimeMessageVisibleUntil);
        killTimeWarningText.gameObject.SetActive(shouldShow);

        if (!shouldShow)
        {
            return;
        }

        bool isKiller = targetCombatTarget != null && targetCombatTarget.role == PlayerRole.Killer;
        killTimeWarningText.text = isKiller
            ? T("Kill Time") + " / " + T("Press") + " " + GameInputBindings.GetLabel(GameInputBindings.KillKey, KeyCode.Q) + " " + T("to Kill")
            : T("Kill Time") + " / " + T("Hide From Imposter");
        killTimeWarningText.color = new Color(1f, 0.18f, 0.12f, 0.98f);
    }

    private void UpdateVoiceStatus()
    {
        if (micStatusRoot == null || micStatusIcon == null || micStatusText == null)
        {
            return;
        }

        bool muted = PlayerVoiceChat.IsLocalMuted();
        if (!hasMicState || muted != lastMicMutedState)
        {
            hasMicState = true;
            lastMicMutedState = muted;
            micStatusVisibleUntil = Time.unscaledTime + 2.2f;
        }

        if (Input.GetKeyDown(GameInputBindings.MicMute))
        {
            micStatusVisibleUntil = Time.unscaledTime + 2.2f;
        }

        bool shouldShow = muted || Time.unscaledTime <= micStatusVisibleUntil;

        micStatusRoot.gameObject.SetActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        micStatusText.text = muted ? "MIC MUTED" : "MIC OPEN";
        micStatusText.color = muted ? new Color(1f, 0.32f, 0.24f, 0.96f) : new Color(0.48f, 1f, 0.68f, 0.96f);
        micStatusIcon.sprite = muted ? micMutedIconSprite : micOpenIconSprite;
        micStatusIcon.color = micStatusText.color;
    }

    private void UpdateHudOpacity()
    {
        if (hudCanvasGroup == null)
        {
            return;
        }

        hudCanvasGroup.alpha = Mathf.Clamp(PlayerPrefs.GetFloat("setting_hud_opacity", 1f), 0.45f, 1f);
    }

    private void UpdateRoleReveal()
    {
        if (roleRevealRoot == null || targetCombatTarget == null)
        {
            return;
        }

        if (RoleAssignmentManager.IsWaitingForPhotonRole())
        {
            roleRevealRoot.gameObject.SetActive(false);
            return;
        }

        if (!hasDisplayedRole || lastDisplayedRole != targetCombatTarget.role)
        {
            hasDisplayedRole = true;
            lastDisplayedRole = targetCombatTarget.role;
            roleRevealEndTime = Application.isPlaying ? Time.unscaledTime + roleRevealDuration : 0f;

            bool isImposter = targetCombatTarget.role == PlayerRole.Killer;
            roleRevealRoleText.text = InGameLocalization.RoleName(targetCombatTarget.role);
            roleRevealRoleText.color = isImposter ? new Color(1f, 0.36f, 0.28f, 0.96f) : new Color(0.58f, 0.92f, 1f, 0.96f);
            roleRevealRoot.gameObject.SetActive(true);
        }

        if (Application.isPlaying && Time.unscaledTime >= roleRevealEndTime)
        {
            roleRevealRoot.gameObject.SetActive(false);
        }
    }

    private void UpdateHudAnimation()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (scanSweepRect != null)
        {
            scanSweepRect.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 90f);
        }

        if (slotHighlightRects != null && targetInventory != null)
        {
            int selected = Mathf.Clamp(targetInventory.SelectedSlotIndex, 0, slotHighlightRects.Length - 1);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4.8f) * 0.045f;
            slotHighlightRects[selected].localScale = Vector3.one * pulse;
        }

    }

    private void SetSegmentFill(Image[] blocks, float normalized, Color activeColor, Color inactiveColor)
    {
        if (blocks == null)
        {
            return;
        }

        int filled = Mathf.RoundToInt(Mathf.Clamp01(normalized) * blocks.Length);

        for (int i = 0; i < blocks.Length; i++)
        {
            if (blocks[i] == null)
            {
                continue;
            }

            bool active = i < filled;
            blocks[i].color = active ? activeColor : inactiveColor;
        }
    }

    private string FormatDotCount(int value)
    {
        if (value >= 1000)
        {
            float thousands = value / 1000f;
            return thousands >= 100f ? Mathf.RoundToInt(thousands) + "k" : thousands.ToString("0.0") + "k";
        }

        return value.ToString();
    }

    private Image[] CreateSegmentRow(string name, Transform parent, int count, Vector2 start, Vector2 size, float gap)
    {
        Image[] result = new Image[Mathf.Max(0, count)];
        RectTransform row = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), start);

        for (int i = 0; i < result.Length; i++)
        {
            RectTransform segment = CreateRect("Seg_" + i, row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(i * (size.x + gap), 0f));
            segment.sizeDelta = size;
            result[i] = AddImage(segment, dimLineColor);
        }

        return result;
    }

    private void CreateCrosshairLine(Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform line = CreateRect("CrosshairLine", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position);
        line.sizeDelta = size;
        AddImage(line, new Color(0.78f, 0.82f, 0.80f, 0.58f));
    }

    private TMP_Text CreateLabel(string text, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, int fontSize, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect("Text_" + text, parent, anchorMin, anchorMax, pivot, anchoredPosition);
        rect.sizeDelta = new Vector2(220f, 28f);
        TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.enableAutoSizing = true;
        label.fontSizeMax = fontSize;
        label.fontSizeMin = Mathf.Max(8f, fontSize - 5f);
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.86f);
        shadow.effectDistance = new Vector2(1.4f, -1.4f);
        LocalizedTmpFontProvider.Apply(label);
        return label;
    }

    private string T(string key)
    {
        return InGameLocalization.Text(key);
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private Image AddPanel(RectTransform rect, Color color)
    {
        Image image = AddImage(rect, color);
        AddOutline(rect, new Color(0.72f, 0.82f, 0.82f, 0.18f), new Vector2(1f, -1f));
        return image;
    }

    private Image AddImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = whiteSprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void AddOutline(RectTransform rect, Color color, Vector2 distance)
    {
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
    }

    private Sprite GetIconSprite(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Camera:
                return cameraIconSprite;

            case ItemType.Knife:
                return knifeIconSprite;

            case ItemType.Medkit:
                return medkitIconSprite;

            default:
                return emptyIconSprite;
        }
    }

    private void PrepareSprites()
    {
        if (whiteSprite == null)
        {
            whiteSprite = CreateSolidSprite(2, 2, Color.white);
        }

        if (emptyIconSprite == null)
        {
            emptyIconSprite = CreateIconSprite(IconKind.Empty);
            cameraIconSprite = CreateIconSprite(IconKind.Camera);
            knifeIconSprite = CreateIconSprite(IconKind.Knife);
            medkitIconSprite = CreateIconSprite(IconKind.Medkit);
        }

        if (micOpenIconSprite == null || micMutedIconSprite == null)
        {
            micOpenIconSprite = CreateIconSprite(IconKind.MicOpen);
            micMutedIconSprite = CreateIconSprite(IconKind.MicMuted);
        }

    }

    private enum IconKind
    {
        Empty,
        Camera,
        Knife,
        Medkit,
        MicOpen,
        MicMuted
    }

    private Sprite CreateIconSprite(IconKind kind)
    {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        Color c = Color.white;

        if (kind == IconKind.Camera)
        {
            FillRect(texture, 9, 17, 31, 19, c);
            FillRect(texture, 14, 12, 13, 6, c);
            FillCircle(texture, 25, 27, 7, clear);
            FillCircle(texture, 25, 27, 4, c);
        }
        else if (kind == IconKind.Knife)
        {
            FillPolygon(
                texture,
                new Vector2[]
                {
                    new Vector2(6f, 8f),
                    new Vector2(15f, 12f),
                    new Vector2(31f, 28f),
                    new Vector2(27f, 32f),
                    new Vector2(11f, 19f)
                },
                c
            );
            FillPolygon(
                texture,
                new Vector2[]
                {
                    new Vector2(13f, 15f),
                    new Vector2(28f, 29f),
                    new Vector2(25f, 31f),
                    new Vector2(11f, 19f)
                },
                new Color(0f, 0f, 0f, 0f)
            );
            FillPolygon(
                texture,
                new Vector2[]
                {
                    new Vector2(26f, 27f),
                    new Vector2(31f, 22f),
                    new Vector2(35f, 26f),
                    new Vector2(30f, 31f)
                },
                c
            );
            DrawThickLine(texture, new Vector2(32f, 30f), new Vector2(43f, 41f), 5f, c);
            DrawThickLine(texture, new Vector2(34f, 28f), new Vector2(45f, 39f), 2f, new Color(0f, 0f, 0f, 0f));
        }
        else if (kind == IconKind.Medkit)
        {
            FillRect(texture, 9, 15, 30, 24, c);
            FillRect(texture, 18, 9, 12, 7, c);
            FillRect(texture, 22, 20, 5, 14, clear);
            FillRect(texture, 17, 25, 15, 5, clear);
        }
        else if (kind == IconKind.MicOpen || kind == IconKind.MicMuted)
        {
            FillRect(texture, 18, 7, 12, 24, c);
            FillCircle(texture, 24, 8, 6, c);
            FillCircle(texture, 24, 30, 6, c);
            FillRect(texture, 14, 25, 4, 8, c);
            FillRect(texture, 30, 25, 4, 8, c);
            FillRect(texture, 21, 33, 6, 7, c);
            FillRect(texture, 16, 40, 16, 4, c);

            if (kind == IconKind.MicMuted)
            {
                DrawThickLine(texture, new Vector2(10f, 8f), new Vector2(38f, 40f), 6f, new Color(0f, 0f, 0f, 0f));
                DrawThickLine(texture, new Vector2(10f, 8f), new Vector2(38f, 40f), 3f, c);
            }
        }
        else
        {
            FillRect(texture, 20, 22, 8, 4, new Color(1f, 1f, 1f, 0.35f));
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateCircleSprite(int size, float radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 1f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateRingSprite(int size, float radius, float thickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float innerRadius = Mathf.Max(0f, radius - thickness);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outerAlpha = Mathf.Clamp01(radius + 1f - distance);
                float innerAlpha = Mathf.Clamp01(distance - innerRadius);
                float alpha = Mathf.Min(outerAlpha, innerAlpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateSolidSprite(int width, int height, Color color)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
            {
                if (xx >= 0 && yy >= 0 && xx < texture.width && yy < texture.height)
                {
                    texture.SetPixel(xx, yy, color);
                }
            }
        }
    }

    private void FillCircle(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        int sqrRadius = radius * radius;

        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx;
                int dy = y - cy;

                if (dx * dx + dy * dy <= sqrRadius && x >= 0 && y >= 0 && x < texture.width && y < texture.height)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private void DrawThickLine(Texture2D texture, Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 direction = to - from;
        float length = direction.magnitude;

        if (length <= 0.001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);

        FillPolygon(
            texture,
            new Vector2[]
            {
                from + normal,
                to + normal,
                to - normal,
                from - normal
            },
            color
        );
    }

    private void FillPolygon(Texture2D texture, Vector2[] points, Color color)
    {
        if (points == null || points.Length < 3)
        {
            return;
        }

        int minX = texture.width;
        int minY = texture.height;
        int maxX = 0;
        int maxY = 0;

        for (int i = 0; i < points.Length; i++)
        {
            minX = Mathf.Min(minX, Mathf.FloorToInt(points[i].x));
            minY = Mathf.Min(minY, Mathf.FloorToInt(points[i].y));
            maxX = Mathf.Max(maxX, Mathf.CeilToInt(points[i].x));
            maxY = Mathf.Max(maxY, Mathf.CeilToInt(points[i].y));
        }

        minX = Mathf.Clamp(minX, 0, texture.width - 1);
        minY = Mathf.Clamp(minY, 0, texture.height - 1);
        maxX = Mathf.Clamp(maxX, 0, texture.width - 1);
        maxY = Mathf.Clamp(maxY, 0, texture.height - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (IsPointInsidePolygon(new Vector2(x + 0.5f, y + 0.5f), points))
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private bool IsPointInsidePolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            bool intersects = ((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                              (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y + 0.0001f) + polygon[i].x);

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
