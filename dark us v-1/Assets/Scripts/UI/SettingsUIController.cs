using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class SettingsUIController : MonoBehaviour
{
    private readonly List<TextBinding> localizedTexts = new List<TextBinding>();
    private readonly List<KeyBindingRow> keyRows = new List<KeyBindingRow>();
    private readonly List<SliderBinding> sliderRows = new List<SliderBinding>();
    private TMP_Text screenModeValueText;
    private TMP_Text fpsValueText;
    private TMP_Text languageValueText;
    private TMP_Text pendingKeyText;
    private string pendingKeyPrefsKey;
    private KeyCode pendingDefaultKey;
    private float pendingKeyStartedAt;
    private bool built;
    private bool opening;
    private Sprite sliderTrackSprite;
    private Sprite sliderHandleSprite;

    private const float DialogWidth = 1040f;
    private const float DialogHeight = 820f;
    private const float FooterButtonWidth = 160f;
    private const float FooterButtonHeight = 52f;
    private const float RowHeight = 46f;
    private const float RowSpacing = 10f;
    private const float RowLabelWidth = 300f;
    private const float RowSliderWidth = 238f;
    private const float SliderTrackHeight = 12f;
    private const float SliderHandleSize = 14f;
    private const float RowValueWidth = 150f;
    private const float RowButtonWidth = 126f;
    private const float RowButtonHeight = 38f;

    [SerializeField] private bool buildInEditMode;
    [SerializeField] private bool embeddedMode;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsCapturingKey => !string.IsNullOrEmpty(pendingKeyPrefsKey);
    public System.Action Closed { get; set; }

    private struct KeyBindingRow
    {
        public TMP_Text Text;
        public string PrefsKey;
        public KeyCode DefaultKey;
    }

    private struct SliderBinding
    {
        public Slider Slider;
        public TMP_Text ValueText;
        public string PrefsKey;
        public float Min;
        public float Max;
    }

    private struct TextBinding
    {
        public TMP_Text Text;
        public string Key;
    }

    private void Awake()
    {
        if (!Application.isPlaying && !buildInEditMode)
        {
            return;
        }

        BuildIfNeeded();
        if (Application.isPlaying && !opening)
        {
            HideWithoutNotify();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying && buildInEditMode)
        {
            BuildIfNeeded();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SettingsPanelLauncher.TickEscapeCloseFrame();

        if (!embeddedMode && IsOpen && !IsCapturingKey && Input.GetKeyDown(KeyCode.Escape))
        {
            SettingsPanelLauncher.MarkEscapeCloseFrame();
            Hide();
            return;
        }

        CapturePendingKey();
    }

    public void Show()
    {
        opening = true;
        try
        {
            gameObject.SetActive(true);
            BuildIfNeeded();
            transform.SetAsLastSibling();
            NormalizeLayout();
            if (!embeddedMode)
            {
                MenuCursorState.UnlockCursor();
            }

            pendingKeyPrefsKey = null;
            pendingKeyText = null;
            RefreshAll();
        }
        finally
        {
            opening = false;
        }
    }

    public void Hide()
    {
        Hide(true);
    }

    public void HideWithoutNotify()
    {
        Hide(false);
    }

    private void Hide(bool notifyClosed)
    {
        bool wasOpen = gameObject.activeSelf;
        gameObject.SetActive(false);

        if (wasOpen && notifyClosed)
        {
            Closed?.Invoke();
        }
    }

    private void BuildIfNeeded()
    {
        if (built)
        {
            return;
        }

        if (embeddedMode)
        {
            built = true;
            BuildEmbeddedLayout();
            NormalizeEmbeddedLayout();
            DarkUiSkin.ApplyToHierarchy(transform);
            RefreshAll();
            return;
        }

        if (transform.Find("SettingsDialog") != null)
        {
            built = true;
            BindExistingHierarchy();
            NormalizeLayout();
            DarkUiSkin.ApplyToHierarchy(transform);
            RefreshAll();
            return;
        }

        built = true;
        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImage = GetComponent<Image>();
        if (rootImage == null)
        {
            rootImage = gameObject.AddComponent<Image>();
        }

        rootImage.color = new Color(0f, 0f, 0f, 0.68f);
        rootImage.raycastTarget = true;

        GameObject dialog = new GameObject("SettingsDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        dialog.transform.SetParent(transform, false);
        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(DialogWidth, DialogHeight);
        dialog.GetComponent<Image>().color = new Color(0.01f, 0.014f, 0.016f, 0.94f);
        dialog.GetComponent<Outline>().effectColor = new Color(0.62f, 0.78f, 0.86f, 0.38f);
        dialog.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);

        TMP_Text title = CreateText(dialog.transform, "TitleText", "SETTINGS", 42f, FontStyles.UpperCase);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -52f);
        titleRect.sizeDelta = new Vector2(-88f, 56f);
        title.alignment = TextAlignmentOptions.Left;
        title.color = new Color(1f, 0.8f, 0.42f, 1f);

        Button closeTop = CreateButton(dialog.transform, "TopCloseButton", "Close", 150f, 46f, 22f);
        RectTransform closeTopRect = closeTop.GetComponent<RectTransform>();
        closeTopRect.anchorMin = new Vector2(1f, 1f);
        closeTopRect.anchorMax = new Vector2(1f, 1f);
        closeTopRect.anchoredPosition = new Vector2(-118f, -54f);
        closeTop.onClick.AddListener(Hide);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewport.transform.SetParent(dialog.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(58f, 92f);
        viewportRect.offsetMax = new Vector2(-78f, -122f);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.08f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = viewport.GetComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 36f;
        EnsureVerticalScrollbar(dialog.transform, viewportRect, scrollRect, false);

        BuildContent(content.transform);

        Button apply = CreateButton(dialog.transform, "ApplyButton", "Apply", FooterButtonWidth, FooterButtonHeight, 22f);
        RectTransform applyRect = apply.GetComponent<RectTransform>();
        applyRect.anchorMin = new Vector2(1f, 0f);
        applyRect.anchorMax = new Vector2(1f, 0f);
        applyRect.anchoredPosition = new Vector2(-310f, 48f);
        apply.onClick.AddListener(SettingsManager.Apply);

        Button reset = CreateButton(dialog.transform, "ResetButton", "Reset", FooterButtonWidth, FooterButtonHeight, 22f);
        RectTransform resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(1f, 0f);
        resetRect.anchorMax = new Vector2(1f, 0f);
        resetRect.anchoredPosition = new Vector2(-132f, 48f);
        reset.onClick.AddListener(() =>
        {
            SettingsManager.ResetAll();
            RefreshAll();
        });

        NormalizeLayout();
        DarkUiSkin.ApplyToHierarchy(transform);
        RefreshAll();
    }

    public void EnableEditModeBuild()
    {
        buildInEditMode = true;
        BuildIfNeeded();
    }

    public void SetEmbeddedMode(bool value)
    {
        if (built && embeddedMode != value)
        {
            Debug.LogWarning("SettingsUIController embedded mode must be set before building.");
            return;
        }

        embeddedMode = value;
    }

    private void BuildEmbeddedLayout()
    {
        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        ScrollRect scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = gameObject.AddComponent<ScrollRect>();
        }

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 34f;

        GameObject viewport = new GameObject("EmbeddedViewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = new Vector2(-20f, 0f);
        viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        EnsureVerticalScrollbar(transform, viewportRect, scrollRect, true);

        BuildContent(content.transform);
        CreateEmbeddedActionRow(content.transform);
    }

    private void CreateEmbeddedActionRow(Transform parent)
    {
        GameObject row = new GameObject("ActionRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        SetLayoutSize(row, 0f, 56f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button apply = CreateButton(row.transform, "ApplyButton", "Apply", 140f, 42f, 18f);
        apply.onClick.AddListener(SettingsManager.Apply);

        Button reset = CreateButton(row.transform, "ResetButton", "Reset", 140f, 42f, 18f);
        reset.onClick.AddListener(() =>
        {
            SettingsManager.ResetAll();
            RefreshAll();
        });
    }

    private void BindExistingHierarchy()
    {
        localizedTexts.Clear();
        keyRows.Clear();
        sliderRows.Clear();

        BindButton("TopCloseButton", Hide);
        BindButton("ApplyButton", SettingsManager.Apply);
        BindButton("ResetButton", () =>
        {
            SettingsManager.ResetAll();
            RefreshAll();
        });

        screenModeValueText = BindCycleRow("Screen Mode", CycleScreenMode);
        fpsValueText = BindCycleRow("FPS Limit", CycleFps);
        languageValueText = BindCycleRow("Language", CycleLanguage);

        EnsureAudioRowsExist();
        BindSliderRow("Master Volume", SettingsManager.MasterVolumeKey, 0f, 1f, SettingsManager.MasterVolume);
        BindSliderRow("BGM Volume", SettingsManager.BgmVolumeKey, 0f, 1f, SettingsManager.BgmVolume);
        BindSliderRow("SFX Volume", SettingsManager.SfxVolumeKey, 0f, 1f, SettingsManager.SfxVolume);
        BindSliderRow("Voice Volume", SettingsManager.VoiceVolumeKey, 0f, 1f, SettingsManager.VoiceVolume);
        BindSliderRow("Mouse Sensitivity X", SettingsManager.MouseXKey, 0.1f, 5f, SettingsManager.MouseSensitivityX);
        BindSliderRow("Mouse Sensitivity Y", SettingsManager.MouseYKey, 0.1f, 5f, SettingsManager.MouseSensitivityY);
        BindSliderRow("HUD Opacity", SettingsManager.HudOpacityKey, 0.45f, 1f, SettingsManager.HudOpacity);

        BindKeyBindRow("Move Forward", GameInputBindings.MoveForwardKey, KeyCode.W);
        BindKeyBindRow("Move Back", GameInputBindings.MoveBackwardKey, KeyCode.S);
        BindKeyBindRow("Move Left", GameInputBindings.MoveLeftKey, KeyCode.A);
        BindKeyBindRow("Move Right", GameInputBindings.MoveRightKey, KeyCode.D);
        BindKeyBindRow("Sprint", GameInputBindings.SprintKey, KeyCode.LeftShift);
        BindKeyBindRow("Crouch", GameInputBindings.CrouchKey, KeyCode.LeftControl);
        BindKeyBindRow("Interact", GameInputBindings.InteractKey, KeyCode.E);
        BindKeyBindRow("Pick Up", GameInputBindings.PickupKey, KeyCode.F);
        BindKeyBindRow("Scan", GameInputBindings.ScanKey, KeyCode.Mouse1);
        BindKeyBindRow("Use Item", GameInputBindings.UseItemKey, KeyCode.Mouse0);
        BindKeyBindRow("Drop Item", GameInputBindings.DropItemKey, KeyCode.G);
        BindKeyBindRow("Slot 1", GameInputBindings.Slot1Key, KeyCode.Alpha1);
        BindKeyBindRow("Slot 2", GameInputBindings.Slot2Key, KeyCode.Alpha2);
        BindKeyBindRow("Mic Mute", GameInputBindings.MicMuteKey, KeyCode.B);
        BindKeyBindRow("Kill", GameInputBindings.KillKey, KeyCode.Q);
        BindKeyBindRow("Pause", GameInputBindings.PauseKey, KeyCode.Escape);
    }

    private void EnsureAudioRowsExist()
    {
        Transform content = FindDescendant(transform, "Content");
        if (content == null)
        {
            return;
        }

        bool changed = false;
        changed |= EnsureSliderRowExists(content, "BGM Volume", SettingsManager.BgmVolumeKey, 0f, 1f, SettingsManager.BgmVolume);
        changed |= EnsureSliderRowExists(content, "SFX Volume", SettingsManager.SfxVolumeKey, 0f, 1f, SettingsManager.SfxVolume);

#if UNITY_EDITOR
        if (changed && !Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private bool EnsureSliderRowExists(Transform content, string label, string prefsKey, float min, float max, float value)
    {
        if (FindDescendant(transform, label + "Row") != null)
        {
            return false;
        }

        CreateSliderRow(content, label, prefsKey, min, max, value);
        return true;
    }

    private TMP_Text BindCycleRow(string label, UnityEngine.Events.UnityAction action)
    {
        Transform row = FindDescendant(transform, label + "Row");
        if (row == null)
        {
            return null;
        }

        Button button = FindDescendant(row, "ChangeButton")?.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            MenuButtonHoverEffect.EnsureOn(button);
        }

        return FindDescendant(row, "ValueText")?.GetComponent<TMP_Text>();
    }

    private void BindSliderRow(string label, string prefsKey, float min, float max, float value)
    {
        Transform row = FindDescendant(transform, label + "Row");
        if (row == null)
        {
            return;
        }

        Slider slider = row.GetComponentInChildren<Slider>(true);
        TMP_Text valueText = FindDescendant(row, "ValueText")?.GetComponent<TMP_Text>();
        if (slider == null || valueText == null)
        {
            return;
        }

        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(prefsKey, v);
            valueText.text = v.ToString("0.00");
            ApplyAudioSettingIfNeeded(prefsKey);
        });
        valueText.text = slider.value.ToString("0.00");
        sliderRows.Add(new SliderBinding { Slider = slider, ValueText = valueText, PrefsKey = prefsKey, Min = min, Max = max });
    }

    private void BindKeyBindRow(string label, string prefsKey, KeyCode defaultKey)
    {
        Transform row = FindDescendant(transform, label + "Row");
        if (row == null)
        {
            return;
        }

        TMP_Text valueText = FindDescendant(row, "ValueText")?.GetComponent<TMP_Text>();
        Button button = FindDescendant(row, "BindButton")?.GetComponent<Button>();
        if (valueText == null || button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => StartKeyBinding(prefsKey, defaultKey, valueText));
        MenuButtonHoverEffect.EnsureOn(button);
        keyRows.Add(new KeyBindingRow { Text = valueText, PrefsKey = prefsKey, DefaultKey = defaultKey });
    }

    private void BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindDescendant(transform, objectName)?.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        MenuButtonHoverEffect.EnsureOn(button);
    }

    private Transform FindDescendant(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void BuildContent(Transform parent)
    {
        CreateSection(parent, "Display");
        screenModeValueText = CreateCycleRow(parent, "Screen Mode", CycleScreenMode);
        fpsValueText = CreateCycleRow(parent, "FPS Limit", CycleFps);
        languageValueText = CreateCycleRow(parent, "Language", CycleLanguage);

        CreateSection(parent, "Audio");
        CreateSliderRow(parent, "Master Volume", SettingsManager.MasterVolumeKey, 0f, 1f, SettingsManager.MasterVolume);
        CreateSliderRow(parent, "BGM Volume", SettingsManager.BgmVolumeKey, 0f, 1f, SettingsManager.BgmVolume);
        CreateSliderRow(parent, "SFX Volume", SettingsManager.SfxVolumeKey, 0f, 1f, SettingsManager.SfxVolume);
        CreateSliderRow(parent, "Voice Volume", SettingsManager.VoiceVolumeKey, 0f, 1f, SettingsManager.VoiceVolume);

        CreateSection(parent, "Controls & Keybindings");
        CreateSliderRow(parent, "Mouse Sensitivity X", SettingsManager.MouseXKey, 0.1f, 5f, SettingsManager.MouseSensitivityX);
        CreateSliderRow(parent, "Mouse Sensitivity Y", SettingsManager.MouseYKey, 0.1f, 5f, SettingsManager.MouseSensitivityY);
        CreateKeyBindRow(parent, "Move Forward", GameInputBindings.MoveForwardKey, KeyCode.W);
        CreateKeyBindRow(parent, "Move Back", GameInputBindings.MoveBackwardKey, KeyCode.S);
        CreateKeyBindRow(parent, "Move Left", GameInputBindings.MoveLeftKey, KeyCode.A);
        CreateKeyBindRow(parent, "Move Right", GameInputBindings.MoveRightKey, KeyCode.D);
        CreateKeyBindRow(parent, "Sprint", GameInputBindings.SprintKey, KeyCode.LeftShift);
        CreateKeyBindRow(parent, "Crouch", GameInputBindings.CrouchKey, KeyCode.LeftControl);
        CreateKeyBindRow(parent, "Interact", GameInputBindings.InteractKey, KeyCode.E);
        CreateKeyBindRow(parent, "Pick Up", GameInputBindings.PickupKey, KeyCode.F);
        CreateKeyBindRow(parent, "Scan", GameInputBindings.ScanKey, KeyCode.Mouse1);
        CreateKeyBindRow(parent, "Use Item", GameInputBindings.UseItemKey, KeyCode.Mouse0);
        CreateKeyBindRow(parent, "Drop Item", GameInputBindings.DropItemKey, KeyCode.G);
        CreateKeyBindRow(parent, "Slot 1", GameInputBindings.Slot1Key, KeyCode.Alpha1);
        CreateKeyBindRow(parent, "Slot 2", GameInputBindings.Slot2Key, KeyCode.Alpha2);
        CreateKeyBindRow(parent, "Mic Mute", GameInputBindings.MicMuteKey, KeyCode.B);
        CreateKeyBindRow(parent, "Kill", GameInputBindings.KillKey, KeyCode.Q);
        CreateKeyBindRow(parent, "Pause", GameInputBindings.PauseKey, KeyCode.Escape);

        CreateSection(parent, "Gameplay");
        CreateSliderRow(parent, "HUD Opacity", SettingsManager.HudOpacityKey, 0.45f, 1f, SettingsManager.HudOpacity);
    }

    private void CreateSection(Transform parent, string title)
    {
        TMP_Text text = CreateText(parent, title + "Title", title, 26f, FontStyles.UpperCase);
        SetLayoutSize(text.gameObject, 0f, 42f);
        text.alignment = TextAlignmentOptions.Left;
        text.color = new Color(1f, 0.8f, 0.42f, 1f);
    }

    private TMP_Text CreateCycleRow(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject row = CreateRow(parent, label);
        TMP_Text valueText = CreateValueText(row.transform);
        Button button = CreateButton(row.transform, "ChangeButton", "Change", RowButtonWidth, RowButtonHeight, 18f);
        button.onClick.AddListener(action);
        return valueText;
    }

    private void CreateSliderRow(Transform parent, string label, string prefsKey, float min, float max, float value)
    {
        GameObject row = CreateRow(parent, label);
        Slider slider = CreateSlider(row.transform, min, max, value);
        TMP_Text valueText = CreateValueText(row.transform);
        slider.transform.SetSiblingIndex(1);
        slider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(prefsKey, v);
            valueText.text = v.ToString("0.00");
            ApplyAudioSettingIfNeeded(prefsKey);
        });
        valueText.text = slider.value.ToString("0.00");
        sliderRows.Add(new SliderBinding { Slider = slider, ValueText = valueText, PrefsKey = prefsKey, Min = min, Max = max });
    }

    private void ApplyAudioSettingIfNeeded(string prefsKey)
    {
        if (prefsKey == SettingsManager.MasterVolumeKey ||
            prefsKey == SettingsManager.BgmVolumeKey ||
            prefsKey == SettingsManager.SfxVolumeKey ||
            prefsKey == SettingsManager.VoiceVolumeKey)
        {
            SettingsManager.ApplyAudio();
        }
    }

    private void CreateKeyBindRow(Transform parent, string label, string prefsKey, KeyCode defaultKey)
    {
        GameObject row = CreateRow(parent, label);
        TMP_Text valueText = CreateValueText(row.transform);
        Button button = CreateButton(row.transform, "BindButton", "Bind", RowButtonWidth, RowButtonHeight, 18f);
        button.onClick.AddListener(() => StartKeyBinding(prefsKey, defaultKey, valueText));
        keyRows.Add(new KeyBindingRow { Text = valueText, PrefsKey = prefsKey, DefaultKey = defaultKey });
    }

    private GameObject CreateRow(Transform parent, string label)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        SetLayoutSize(row, 0f, RowHeight);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = RowSpacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        TMP_Text labelText = CreateText(row.transform, "LabelText", label, 18f, FontStyles.Normal);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutSize(labelText.gameObject, RowLabelWidth, 40f);
        return row;
    }

    private TMP_Text CreateValueText(Transform parent)
    {
        GameObject textObject = new GameObject("ValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.fontSize = 18f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        text.raycastTarget = false;
        LocalizedTmpFontProvider.Apply(text);
        SetLayoutSize(text.gameObject, RowValueWidth, 40f);
        return text;
    }

    private void StartKeyBinding(string prefsKey, KeyCode defaultKey, TMP_Text valueText)
    {
        pendingKeyPrefsKey = prefsKey;
        pendingDefaultKey = defaultKey;
        pendingKeyText = valueText;
        pendingKeyStartedAt = Time.unscaledTime;
        valueText.text = T("Press a key");
        valueText.color = new Color(1f, 0.8f, 0.42f, 1f);
    }

    private void CapturePendingKey()
    {
        if (string.IsNullOrEmpty(pendingKeyPrefsKey) || Time.unscaledTime - pendingKeyStartedAt < 0.12f)
        {
            return;
        }

        if (!GameInputBindings.TryGetPressedBindableKey(out KeyCode key))
        {
            return;
        }

        GameInputBindings.SetKey(pendingKeyPrefsKey, key);
        pendingKeyPrefsKey = null;
        pendingKeyText = null;
        RefreshAll();
    }

    private void CycleScreenMode()
    {
        SettingsManager.ScreenMode = SettingsManager.ScreenMode == FullScreenMode.FullScreenWindow
            ? FullScreenMode.Windowed
            : FullScreenMode.FullScreenWindow;
        RefreshAll();
    }

    private void CycleFps()
    {
        int current = SettingsManager.FpsLimit;
        int nextIndex = 0;
        for (int i = 0; i < SettingsManager.FpsLimits.Length; i++)
        {
            if (SettingsManager.FpsLimits[i] == current)
            {
                nextIndex = (i + 1) % SettingsManager.FpsLimits.Length;
                break;
            }
        }

        SettingsManager.FpsLimit = SettingsManager.FpsLimits[nextIndex];
        RefreshAll();
    }

    private void CycleLanguage()
    {
        SettingsManager.Language = (SettingsManager.Language + 1) % 3;
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshLocalizedTexts();
        if (screenModeValueText != null)
        {
            screenModeValueText.text = SettingsManager.ScreenMode == FullScreenMode.Windowed ? T("WINDOWED") : T("BORDERLESS");
        }

        if (fpsValueText != null)
        {
            int fps = SettingsManager.FpsLimit;
            fpsValueText.text = fps < 0 ? T("UNLIMITED") : fps + " FPS";
        }

        if (languageValueText != null)
        {
            languageValueText.text = SettingsManager.Language == 1 ? T("ENGLISH") : SettingsManager.Language == 2 ? T("JAPANESE") : T("KOREAN");
        }

        for (int i = 0; i < keyRows.Count; i++)
        {
            keyRows[i].Text.text = GameInputBindings.GetLabel(keyRows[i].PrefsKey, keyRows[i].DefaultKey);
            keyRows[i].Text.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        }

        RefreshSliderRows();
    }

    private void RefreshSliderRows()
    {
        for (int i = 0; i < sliderRows.Count; i++)
        {
            SliderBinding row = sliderRows[i];
            if (row.Slider == null || row.ValueText == null)
            {
                continue;
            }

            float value = Mathf.Clamp(GetSliderValue(row.PrefsKey), row.Min, row.Max);
            row.Slider.SetValueWithoutNotify(value);
            row.ValueText.text = value.ToString("0.00");
        }
    }

    private float GetSliderValue(string prefsKey)
    {
        if (prefsKey == SettingsManager.MasterVolumeKey)
        {
            return SettingsManager.MasterVolume;
        }

        if (prefsKey == SettingsManager.BgmVolumeKey)
        {
            return SettingsManager.BgmVolume;
        }

        if (prefsKey == SettingsManager.SfxVolumeKey)
        {
            return SettingsManager.SfxVolume;
        }

        if (prefsKey == SettingsManager.VoiceVolumeKey)
        {
            return SettingsManager.VoiceVolume;
        }

        if (prefsKey == SettingsManager.MouseXKey)
        {
            return SettingsManager.MouseSensitivityX;
        }

        if (prefsKey == SettingsManager.MouseYKey)
        {
            return SettingsManager.MouseSensitivityY;
        }

        if (prefsKey == SettingsManager.HudOpacityKey)
        {
            return SettingsManager.HudOpacity;
        }

        return PlayerPrefs.GetFloat(prefsKey, 0f);
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value)
    {
        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image), typeof(Outline));
        sliderObject.transform.SetParent(parent, false);
        SetLayoutSize(sliderObject, RowSliderWidth, SliderTrackHeight);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        slider.direction = Slider.Direction.LeftToRight;

        Image track = slider.GetComponent<Image>();
        track.sprite = GetSliderTrackSprite();
        track.type = Image.Type.Sliced;
        track.color = new Color(0.055f, 0.075f, 0.078f, 0.92f);

        Outline trackOutline = slider.GetComponent<Outline>();
        trackOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.14f);
        trackOutline.effectDistance = new Vector2(1f, -1f);

        GameObject fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        Image fill = CreateSliderImage(fillArea.transform, "Fill", new Color(0.93f, 0.68f, 0.30f, 0.88f));
        fill.sprite = GetSliderTrackSprite();
        fill.type = Image.Type.Sliced;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
        handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
        handleAreaRect.pivot = new Vector2(0.5f, 0.5f);
        handleAreaRect.anchoredPosition = Vector2.zero;
        handleAreaRect.sizeDelta = Vector2.zero;

        Image handle = CreateSliderImage(handleArea.transform, "Handle", new Color(0.80f, 0.85f, 0.84f, 1f));
        handle.sprite = GetSliderHandleSprite();
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(SliderHandleSize, SliderHandleSize);
        handle.rectTransform.anchoredPosition = Vector2.zero;

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        NormalizeSliderVisuals(slider);
        DarkUiSkin.ApplySlider(slider);
        return slider;
    }

    private Image CreateSliderImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void NormalizeSliderVisuals(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        SetLayoutSize(slider.gameObject, RowSliderWidth, SliderTrackHeight);
        slider.direction = Slider.Direction.LeftToRight;

        Image track = slider.GetComponent<Image>();
        if (track == null)
        {
            track = slider.gameObject.AddComponent<Image>();
        }

        track.sprite = GetSliderTrackSprite();
        track.type = Image.Type.Sliced;
        track.color = new Color(0.055f, 0.075f, 0.078f, 0.92f);
        track.raycastTarget = true;

        Outline trackOutline = slider.GetComponent<Outline>();
        if (trackOutline == null)
        {
            trackOutline = slider.gameObject.AddComponent<Outline>();
        }

        trackOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.14f);
        trackOutline.effectDistance = new Vector2(1f, -1f);

        RectTransform fillAreaRect = GetOrCreateRectChild(slider.transform, "FillArea");
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(3f, 3f);
        fillAreaRect.offsetMax = new Vector2(-3f, -3f);

        Transform oldFill = slider.transform.Find("Fill");
        if (oldFill != null && oldFill.parent != fillAreaRect)
        {
            oldFill.SetParent(fillAreaRect, false);
        }

        Image fill = GetOrCreateImage(fillAreaRect, "Fill");
        fill.transform.SetParent(fillAreaRect, false);
        fill.color = new Color(0.93f, 0.68f, 0.30f, 0.88f);
        fill.sprite = GetSliderTrackSprite();
        fill.type = Image.Type.Sliced;
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        RectTransform handleAreaRect = GetOrCreateRectChild(slider.transform, "HandleArea");
        handleAreaRect.anchorMin = new Vector2(0f, 0.5f);
        handleAreaRect.anchorMax = new Vector2(1f, 0.5f);
        handleAreaRect.pivot = new Vector2(0.5f, 0.5f);
        handleAreaRect.anchoredPosition = Vector2.zero;
        handleAreaRect.sizeDelta = Vector2.zero;

        Transform oldHandle = slider.transform.Find("Handle");
        if (oldHandle != null && oldHandle.parent != handleAreaRect)
        {
            oldHandle.SetParent(handleAreaRect, false);
        }

        Image handle = GetOrCreateImage(handleAreaRect, "Handle");
        handle.transform.SetParent(handleAreaRect, false);
        handle.color = new Color(0.80f, 0.85f, 0.84f, 1f);
        handle.sprite = GetSliderHandleSprite();
        handle.type = Image.Type.Simple;
        handle.raycastTarget = false;
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(SliderHandleSize, SliderHandleSize);

        Transform glow = handle.transform.Find("Glow");
        if (glow != null)
        {
            glow.gameObject.SetActive(false);
        }

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        DarkUiSkin.ApplySlider(slider);
    }

    private RectTransform GetOrCreateRectChild(Transform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(objectName, typeof(RectTransform));
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        RectTransform rect = child.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = child.gameObject.AddComponent<RectTransform>();
        }

        return rect;
    }

    private Image GetOrCreateImage(Transform parent, string objectName)
    {
        Transform child = FindDescendant(parent, objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.transform.SetParent(parent, false);
            return childObject.GetComponent<Image>();
        }

        Image image = child.GetComponent<Image>();
        if (image == null)
        {
            image = child.gameObject.AddComponent<Image>();
        }

        return image;
    }

    private Sprite GetSliderTrackSprite()
    {
        if (sliderTrackSprite == null)
        {
            sliderTrackSprite = CreateRoundedSprite("Settings Slider Track", 64, 14, 7f, new Vector4(7f, 7f, 7f, 7f));
        }

        return sliderTrackSprite;
    }

    private Sprite GetSliderHandleSprite()
    {
        if (sliderHandleSprite == null)
        {
            sliderHandleSprite = CreateRoundedSprite("Settings Slider Handle", 20, 20, 10f, Vector4.zero);
        }

        return sliderHandleSprite;
    }

    private Sprite CreateRoundedSprite(string spriteName, int width, int height, float radius, Vector4 border)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = spriteName + " Texture";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[width * height];
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 inner = new Vector2(width * 0.5f - radius, height * 0.5f - radius);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 delta = new Vector2(Mathf.Abs(point.x - center.x), Mathf.Abs(point.y - center.y));
                Vector2 corner = new Vector2(Mathf.Max(delta.x - inner.x, 0f), Mathf.Max(delta.y - inner.y, 0f));
                float distance = corner.magnitude;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.75f - distance) * 255f);
                pixels[y * width + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
        sprite.name = spriteName;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private Button CreateButton(Transform parent, string objectName, string label, float width, float height, float fontSize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(MenuButtonHoverEffect));
        buttonObject.transform.SetParent(parent, false);
        SetLayoutSize(buttonObject, width, height);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.015f, 0.018f, 0.02f, 0.62f);
        buttonObject.GetComponent<Outline>().effectColor = new Color(0.62f, 0.78f, 0.86f, 0.34f);
        buttonObject.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
        TMP_Text labelText = CreateText(buttonObject.transform, "Text (TMP)", label, fontSize, FontStyles.UpperCase);
        MenuButtonHoverEffect hover = buttonObject.GetComponent<MenuButtonHoverEffect>();
        hover.buttonImage = image;
        hover.labelText = labelText;
        hover.normalBackgroundColor = new Color(0.015f, 0.018f, 0.02f, 0.52f);
        hover.hoverBackgroundColor = new Color(0.09f, 0.12f, 0.13f, 0.76f);
        hover.pressedBackgroundColor = new Color(0.16f, 0.18f, 0.17f, 0.86f);
        hover.normalTextColor = new Color(0.76f, 0.82f, 0.84f, 1f);
        hover.hoverTextColor = new Color(1f, 0.8f, 0.42f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        DarkUiSkin.ApplyButton(button);
        return button;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = T(text);
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        label.raycastTarget = false;
        LocalizedTmpFontProvider.Apply(label);
        localizedTexts.Add(new TextBinding { Text = label, Key = text });
        return label;
    }

    private void RefreshLocalizedTexts()
    {
        for (int i = 0; i < localizedTexts.Count; i++)
        {
            TextBinding binding = localizedTexts[i];
            if (binding.Text != null)
            {
                binding.Text.text = T(binding.Key);
            }
        }
    }

    private void NormalizeLayout()
    {
        if (embeddedMode)
        {
            NormalizeEmbeddedLayout();
            return;
        }

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect != null)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }

        RectTransform dialogRect = FindDescendant(transform, "SettingsDialog")?.GetComponent<RectTransform>();
        if (dialogRect != null)
        {
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.anchoredPosition = Vector2.zero;
            dialogRect.sizeDelta = new Vector2(DialogWidth, DialogHeight);
        }

        RectTransform viewportRect = FindDescendant(transform, "Viewport")?.GetComponent<RectTransform>();
        if (viewportRect != null)
        {
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(58f, 98f);
            viewportRect.offsetMax = new Vector2(-78f, -124f);

            ScrollRect scrollRect = viewportRect.GetComponent<ScrollRect>();
            EnsureVerticalScrollbar(viewportRect.parent, viewportRect, scrollRect, false);
        }

        Transform content = FindDescendant(transform, "Content");
        if (content != null)
        {
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.spacing = 8f;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = false;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;
            }

            NormalizeRows(content);
        }

        NormalizeFloatingButton("TopCloseButton", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-116f, -54f), 150f, 46f, 18f);
        NormalizeFloatingButton("ApplyButton", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-310f, 48f), FooterButtonWidth, FooterButtonHeight, 20f);
        NormalizeFloatingButton("ResetButton", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-132f, 48f), FooterButtonWidth, FooterButtonHeight, 20f);

        if (Application.isPlaying)
        {
            MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);
        }

        DarkUiSkin.ApplyToHierarchy(transform);
    }

    private void NormalizeEmbeddedLayout()
    {
        RectTransform viewportRect = FindDescendant(transform, "EmbeddedViewport")?.GetComponent<RectTransform>();
        if (viewportRect != null)
        {
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-20f, 0f);

            ScrollRect scrollRect = GetComponent<ScrollRect>();
            EnsureVerticalScrollbar(transform, viewportRect, scrollRect, true);
        }

        Transform content = FindDescendant(transform, "Content");
        if (content != null)
        {
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.spacing = 8f;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = false;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;
            }

            NormalizeRows(content);
        }

        if (Application.isPlaying)
        {
            MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);
        }

        DarkUiSkin.ApplyToHierarchy(transform);
    }

    private void EnsureVerticalScrollbar(Transform parent, RectTransform viewportRect, ScrollRect scrollRect, bool embedded)
    {
        if (parent == null || viewportRect == null || scrollRect == null)
        {
            return;
        }

        string objectName = embedded ? "EmbeddedVerticalScrollbar" : "VerticalScrollbar";
        Transform existing = parent.Find(objectName);
        GameObject scrollbarObject;
        if (existing == null)
        {
            scrollbarObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            scrollbarObject.transform.SetParent(parent, false);
        }
        else
        {
            scrollbarObject = existing.gameObject;
        }

        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        if (embedded)
        {
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.offsetMin = new Vector2(-12f, 0f);
            scrollbarRect.offsetMax = new Vector2(-2f, 0f);
        }
        else
        {
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.offsetMin = new Vector2(-52f, 98f);
            scrollbarRect.offsetMax = new Vector2(-40f, -124f);
        }

        Image track = scrollbarObject.GetComponent<Image>();
        track.color = new Color(0.35f, 0.38f, 0.39f, 0.24f);
        track.raycastTarget = true;

        RectTransform slidingArea = GetOrCreateRectChild(scrollbarObject.transform, "Sliding Area");
        slidingArea.anchorMin = Vector2.zero;
        slidingArea.anchorMax = Vector2.one;
        slidingArea.offsetMin = new Vector2(2f, 3f);
        slidingArea.offsetMax = new Vector2(-2f, -3f);

        Image handle = GetOrCreateImage(slidingArea, "Handle");
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        handle.color = new Color(0.62f, 0.66f, 0.67f, 0.78f);
        handle.raycastTarget = true;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.ColorTint;
        scrollbar.targetGraphic = handle;
        scrollbar.handleRect = handleRect;
        scrollbar.size = Mathf.Clamp(scrollbar.size, 0.16f, 1f);

        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scrollRect.verticalScrollbarSpacing = 4f;
    }

    private void NormalizeRows(Transform content)
    {
        Transform[] rows = content.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < rows.Length; i++)
        {
            Transform row = rows[i];
            if (row == null || !row.name.EndsWith("Row"))
            {
                continue;
            }

            SetLayoutSize(row.gameObject, 0f, RowHeight);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = RowSpacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            TMP_Text label = FindDescendant(row, "LabelText")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                SetLayoutSize(label.gameObject, RowLabelWidth, 40f);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                FitText(label, 18f, false);
            }

            Slider slider = row.GetComponentInChildren<Slider>(true);
            if (slider != null)
            {
                NormalizeSliderVisuals(slider);
            }

            TMP_Text valueText = FindDescendant(row, "ValueText")?.GetComponent<TMP_Text>();
            if (valueText != null)
            {
                SetLayoutSize(valueText.gameObject, RowValueWidth, 40f);
                valueText.alignment = TextAlignmentOptions.Center;
                FitText(valueText, 18f, false);
            }

            Button[] buttons = row.GetComponentsInChildren<Button>(true);
            for (int buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
            {
                Button button = buttons[buttonIndex];
                if (button == null)
                {
                    continue;
                }

                SetLayoutSize(button.gameObject, RowButtonWidth, RowButtonHeight);
                TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
                FitText(buttonText, 17f, false);
                MenuButtonHoverEffect.EnsureOn(button);
            }
        }
    }

    private void NormalizeFloatingButton(string objectName, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, float width, float height, float maxFontSize)
    {
        Transform buttonTransform = FindDescendant(transform, objectName);
        if (buttonTransform == null)
        {
            return;
        }

        RectTransform rect = buttonTransform.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
        }

        SetLayoutSize(buttonTransform.gameObject, width, height);
        Button button = buttonTransform.GetComponent<Button>();
        if (button != null)
        {
            MenuButtonHoverEffect.EnsureOn(button);
        }

        FitText(buttonTransform.GetComponentInChildren<TMP_Text>(true), maxFontSize, false);
    }

    private void FitText(TMP_Text text, float maxFontSize, bool wrap)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = 11f;
        text.fontSizeMax = maxFontSize;
        text.fontSize = Mathf.Min(text.fontSize, maxFontSize);
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
    }

    private void SetLayoutSize(GameObject target, float width, float height)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = width;
            layout.preferredHeight = height;
        }
    }

    private string T(string key)
    {
        return InGameLocalization.Text(key);
    }
}
