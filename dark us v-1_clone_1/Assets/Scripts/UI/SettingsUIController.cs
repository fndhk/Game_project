using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class SettingsUIController : MonoBehaviour
{
    private readonly List<TextBinding> localizedTexts = new List<TextBinding>();
    private readonly List<KeyBindingRow> keyRows = new List<KeyBindingRow>();
    private TMP_Text screenModeValueText;
    private TMP_Text fpsValueText;
    private TMP_Text languageValueText;
    private TMP_Text pendingKeyText;
    private string pendingKeyPrefsKey;
    private KeyCode pendingDefaultKey;
    private float pendingKeyStartedAt;
    private bool built;

    [SerializeField] private bool buildInEditMode;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsCapturingKey => !string.IsNullOrEmpty(pendingKeyPrefsKey);
    public System.Action Closed { get; set; }

    private struct KeyBindingRow
    {
        public TMP_Text Text;
        public string PrefsKey;
        public KeyCode DefaultKey;
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
        if (Application.isPlaying)
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

        if (IsOpen && !IsCapturingKey && Input.GetKeyDown(KeyCode.Escape))
        {
            SettingsPanelLauncher.MarkEscapeCloseFrame();
            Hide();
            return;
        }

        CapturePendingKey();
    }

    public void Show()
    {
        BuildIfNeeded();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        MenuCursorState.UnlockCursor();
        pendingKeyPrefsKey = null;
        pendingKeyText = null;
        RefreshAll();
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

        if (transform.Find("SettingsDialog") != null)
        {
            built = true;
            BindExistingHierarchy();
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
        dialogRect.sizeDelta = new Vector2(1040f, 820f);
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

        Button closeTop = CreateButton(dialog.transform, "TopCloseButton", "Close", 160f, 48f, 22f);
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
        viewportRect.offsetMax = new Vector2(-58f, -122f);
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

        BuildContent(content.transform);

        Button apply = CreateButton(dialog.transform, "ApplyButton", "Apply", 180f, 54f, 22f);
        RectTransform applyRect = apply.GetComponent<RectTransform>();
        applyRect.anchorMin = new Vector2(1f, 0f);
        applyRect.anchorMax = new Vector2(1f, 0f);
        applyRect.anchoredPosition = new Vector2(-264f, 48f);
        apply.onClick.AddListener(SettingsManager.Apply);

        Button reset = CreateButton(dialog.transform, "ResetButton", "Reset", 180f, 54f, 22f);
        RectTransform resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(1f, 0f);
        resetRect.anchorMax = new Vector2(1f, 0f);
        resetRect.anchoredPosition = new Vector2(-72f, 48f);
        reset.onClick.AddListener(() =>
        {
            SettingsManager.ResetAll();
            RefreshAll();
        });

        RefreshAll();
    }

    public void EnableEditModeBuild()
    {
        buildInEditMode = true;
        BuildIfNeeded();
    }

    private void BindExistingHierarchy()
    {
        localizedTexts.Clear();
        keyRows.Clear();

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

        BindSliderRow("Master Volume", SettingsManager.MasterVolumeKey, 0f, 1f, SettingsManager.MasterVolume);
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
        });
        valueText.text = slider.value.ToString("0.00");
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
        Button button = CreateButton(row.transform, "ChangeButton", "Change", 140f, 42f, 19f);
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
        });
        valueText.text = slider.value.ToString("0.00");
    }

    private void CreateKeyBindRow(Transform parent, string label, string prefsKey, KeyCode defaultKey)
    {
        GameObject row = CreateRow(parent, label);
        TMP_Text valueText = CreateValueText(row.transform);
        Button button = CreateButton(row.transform, "BindButton", "Bind", 140f, 42f, 19f);
        button.onClick.AddListener(() => StartKeyBinding(prefsKey, defaultKey, valueText));
        keyRows.Add(new KeyBindingRow { Text = valueText, PrefsKey = prefsKey, DefaultKey = defaultKey });
    }

    private GameObject CreateRow(Transform parent, string label)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        SetLayoutSize(row, 0f, 50f);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        TMP_Text labelText = CreateText(row.transform, "LabelText", label, 20f, FontStyles.Normal);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        SetLayoutSize(labelText.gameObject, 390f, 42f);
        return row;
    }

    private TMP_Text CreateValueText(Transform parent)
    {
        GameObject textObject = new GameObject("ValueText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.fontSize = 20f;
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        text.raycastTarget = false;
        LocalizedTmpFontProvider.Apply(text);
        SetLayoutSize(text.gameObject, 220f, 42f);
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
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value)
    {
        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        sliderObject.transform.SetParent(parent, false);
        SetLayoutSize(sliderObject, 250f, 14f);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        slider.targetGraphic = slider.GetComponent<Image>();
        slider.GetComponent<Image>().color = new Color(0.62f, 0.78f, 0.86f, 0.28f);
        Image fill = CreateSliderImage(sliderObject.transform, "Fill", new Color(1f, 0.8f, 0.42f, 0.82f));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
        Image handle = CreateSliderImage(sliderObject.transform, "Handle", new Color(0.78f, 0.86f, 0.88f, 1f));
        handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(20f, 22f);
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        return slider;
    }

    private Image CreateSliderImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
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
