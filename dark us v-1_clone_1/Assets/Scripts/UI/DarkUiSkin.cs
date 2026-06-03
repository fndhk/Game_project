using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DarkUiSkin
{
    public enum PanelStyle
    {
        Standard,
        Modal,
        Hud,
        Subtle
    }

    private const string ResourcePath = "UI/DarkSkin/";

    private static Sprite buttonSprite;
    private static Sprite buttonHoverSprite;
    private static Sprite buttonPressedSprite;
    private static Sprite panelSprite;
    private static Sprite modalPanelSprite;
    private static Sprite hudPanelSprite;
    private static Sprite slotFrameSprite;
    private static Sprite slotSelectedSprite;
    private static Sprite inputSprite;
    private static Sprite inputFocusedSprite;
    private static Sprite sliderTrackSprite;
    private static Sprite sliderFillSprite;
    private static Sprite sliderHandleSprite;
    private static Sprite toggleBoxSprite;
    private static Sprite toggleCheckSprite;

    public static Color NormalTextColor => new Color(0.78f, 0.86f, 0.88f, 1f);
    public static Color HoverTextColor => new Color(1f, 0.78f, 0.38f, 1f);
    public static Color MutedTextColor => new Color(0.58f, 0.70f, 0.72f, 0.92f);
    public static Color AccentColor => new Color(1f, 0.74f, 0.18f, 1f);

    public static Sprite ButtonSprite => LoadSprite(ref buttonSprite, "Button");
    public static Sprite ButtonHoverSprite => LoadSprite(ref buttonHoverSprite, "ButtonHover");
    public static Sprite ButtonPressedSprite => LoadSprite(ref buttonPressedSprite, "ButtonPressed");
    public static Sprite PanelSprite => LoadSprite(ref panelSprite, "Panel");
    public static Sprite ModalPanelSprite => LoadSprite(ref modalPanelSprite, "ModalPanel");
    public static Sprite HudPanelSprite => LoadSprite(ref hudPanelSprite, "HudPanel");
    public static Sprite SlotFrameSprite => LoadSprite(ref slotFrameSprite, "SlotFrame");
    public static Sprite SlotSelectedSprite => LoadSprite(ref slotSelectedSprite, "SlotSelected");
    public static Sprite InputSprite => LoadSprite(ref inputSprite, "InputField");
    public static Sprite InputFocusedSprite => LoadSprite(ref inputFocusedSprite, "InputFieldFocused");
    public static Sprite SliderTrackSprite => LoadSprite(ref sliderTrackSprite, "SliderTrack");
    public static Sprite SliderFillSprite => LoadSprite(ref sliderFillSprite, "SliderFill");
    public static Sprite SliderHandleSprite => LoadSprite(ref sliderHandleSprite, "SliderHandle");
    public static Sprite ToggleBoxSprite => LoadSprite(ref toggleBoxSprite, "ToggleBox");
    public static Sprite ToggleCheckSprite => LoadSprite(ref toggleCheckSprite, "ToggleCheck");

    public static void ApplyToHierarchy(Transform root)
    {
        if (root == null)
        {
            return;
        }

        TMP_InputField[] inputFields = root.GetComponentsInChildren<TMP_InputField>(true);
        for (int i = 0; i < inputFields.Length; i++)
        {
            ApplyInputField(inputFields[i]);
        }

        TMP_Dropdown[] dropdowns = root.GetComponentsInChildren<TMP_Dropdown>(true);
        for (int i = 0; i < dropdowns.Length; i++)
        {
            ApplyDropdown(dropdowns[i]);
        }

        Toggle[] toggles = root.GetComponentsInChildren<Toggle>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            ApplyToggle(toggles[i]);
        }

        Slider[] sliders = root.GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            ApplySlider(sliders[i]);
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (ShouldStyleButton(buttons[i]))
            {
                ApplyButton(buttons[i]);
            }
            else if (ShouldStyleIconButton(buttons[i]))
            {
                ApplyIconButton(buttons[i]);
            }
        }

        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (ShouldStylePanelImage(image))
            {
                ApplyPanel(image, GuessPanelStyle(image.gameObject.name));
            }
        }
    }

    public static void ApplyButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.targetGraphic as Image;
        if (image == null)
        {
            image = button.GetComponent<Image>();
        }

        if (image == null)
        {
            return;
        }

        Sprite sprite = ButtonSprite;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.015f, 0.018f, 0.02f, 0.62f);
        }

        image.raycastTarget = true;
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Outline outline = button.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = sprite == null;
            outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.22f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = NormalTextColor;
            label.raycastTarget = false;
        }

        MenuButtonHoverEffect hover = MenuButtonHoverEffect.EnsureOn(button.gameObject);
        ApplyButtonHover(hover);
    }

    public static void ApplyIconButton(Button button)
    {
        if (button == null || button.image == null)
        {
            return;
        }

        Image frameImage = button.image;
        Sprite icon = GetPreservedIcon(button);
        ApplySlotFrame(frameImage, false);
        frameImage.raycastTarget = true;
        button.targetGraphic = frameImage;

        if (icon == null)
        {
            return;
        }

        Image iconImage = GetOrCreateChildImage(button.transform, "DarkUiIcon");
        RectTransform iconRect = iconImage.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(12f, 12f);
        iconRect.offsetMax = new Vector2(-12f, -12f);
        iconImage.sprite = icon;
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    public static void ApplyButtonHover(MenuButtonHoverEffect hover)
    {
        if (hover == null)
        {
            return;
        }

        hover.normalSprite = ButtonSprite;
        hover.hoverSprite = ButtonHoverSprite;
        hover.pressedSprite = ButtonPressedSprite;
        hover.normalBackgroundColor = hover.normalSprite != null ? Color.white : new Color(0.015f, 0.018f, 0.02f, 0.58f);
        hover.hoverBackgroundColor = hover.hoverSprite != null ? Color.white : new Color(0.09f, 0.12f, 0.13f, 0.82f);
        hover.pressedBackgroundColor = hover.pressedSprite != null ? Color.white : new Color(0.16f, 0.18f, 0.17f, 0.86f);
        hover.normalTextColor = NormalTextColor;
        hover.hoverTextColor = HoverTextColor;

        if (hover.labelText != null)
        {
            hover.labelText.color = NormalTextColor;
        }

        hover.ApplyDefaultState();
    }

    public static void ApplyPanel(Image image, PanelStyle style = PanelStyle.Standard)
    {
        if (image == null)
        {
            return;
        }

        Sprite sprite = GetPanelSprite(style);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = style == PanelStyle.Hud
                ? new Color(0f, 0f, 0f, 0.74f)
                : new Color(0.015f, 0.022f, 0.024f, 0.82f);
        }

        Outline outline = image.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = sprite == null;
            outline.effectColor = new Color(0.62f, 0.78f, 0.86f, style == PanelStyle.Hud ? 0.16f : 0.26f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    public static void ApplySlotFrame(Image image, bool selected)
    {
        if (image == null)
        {
            return;
        }

        Sprite sprite = selected ? SlotSelectedSprite : SlotFrameSprite;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
    }

    public static void ApplyInputField(TMP_InputField input)
    {
        if (input == null)
        {
            return;
        }

        Image image = input.targetGraphic as Image;
        if (image == null)
        {
            image = input.GetComponent<Image>();
        }

        if (image != null)
        {
            Sprite sprite = InputSprite;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = new Color(0.015f, 0.018f, 0.02f, 0.78f);
            }

            input.targetGraphic = image;
        }

        Outline outline = input.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = InputSprite == null;
        }

        if (input.textComponent != null)
        {
            input.textComponent.color = NormalTextColor;
        }

        TMP_Text placeholder = input.placeholder as TMP_Text;
        if (placeholder != null)
        {
            placeholder.color = new Color(0.58f, 0.70f, 0.72f, 0.56f);
        }
    }

    public static void ApplySlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        Image track = slider.GetComponent<Image>();
        if (track != null)
        {
            Sprite sprite = SliderTrackSprite;
            if (sprite != null)
            {
                track.sprite = sprite;
                track.type = Image.Type.Sliced;
                track.color = Color.white;
            }
            else
            {
                track.color = new Color(0.055f, 0.075f, 0.078f, 0.92f);
            }
        }

        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
        {
            Sprite sprite = SliderFillSprite;
            if (sprite != null)
            {
                fill.sprite = sprite;
                fill.type = Image.Type.Sliced;
                fill.color = Color.white;
            }
            else
            {
                fill.color = new Color(0.93f, 0.68f, 0.30f, 0.88f);
            }
        }

        Image handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
        if (handle != null)
        {
            Sprite sprite = SliderHandleSprite;
            if (sprite != null)
            {
                handle.sprite = sprite;
                handle.type = Image.Type.Simple;
                handle.color = Color.white;
                handle.preserveAspect = true;
            }
        }
    }

    public static void ApplyToggle(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        Image background = toggle.targetGraphic as Image;
        if (background == null)
        {
            background = toggle.GetComponentInChildren<Image>(true);
        }

        if (background != null && ToggleBoxSprite != null)
        {
            background.sprite = ToggleBoxSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }

        Image checkmark = toggle.graphic as Image;
        if (checkmark != null && ToggleCheckSprite != null)
        {
            checkmark.sprite = ToggleCheckSprite;
            checkmark.type = Image.Type.Simple;
            checkmark.color = Color.white;
            checkmark.preserveAspect = true;
        }

        TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = NormalTextColor;
            label.raycastTarget = false;
        }
    }

    public static void ApplyDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        Image image = dropdown.targetGraphic as Image;
        if (image == null)
        {
            image = dropdown.GetComponent<Image>();
        }

        if (image != null)
        {
            Sprite sprite = InputSprite;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
        }

        if (dropdown.captionText != null)
        {
            dropdown.captionText.color = NormalTextColor;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.color = NormalTextColor;
        }
    }

    private static Sprite LoadSprite(ref Sprite cache, string name)
    {
        if (cache == null)
        {
            cache = Resources.Load<Sprite>(ResourcePath + name);
        }

        return cache;
    }

    private static Sprite GetPanelSprite(PanelStyle style)
    {
        switch (style)
        {
            case PanelStyle.Modal:
                return ModalPanelSprite;

            case PanelStyle.Hud:
                return HudPanelSprite;

            default:
                return PanelSprite;
        }
    }

    private static bool ShouldStyleButton(Button button)
    {
        if (button == null)
        {
            return false;
        }

        if (button.GetComponentInChildren<TMP_Text>(true) != null)
        {
            return true;
        }

        return button.GetComponent<MenuButtonHoverEffect>() != null;
    }

    private static bool ShouldStyleIconButton(Button button)
    {
        if (button == null || button.image == null)
        {
            return false;
        }

        string path = GetTransformPath(button.transform).ToLowerInvariant();
        if (!path.Contains("skill") && button.GetComponentInParent<SkillChoiceUIController>() == null)
        {
            return false;
        }

        Sprite icon = GetPreservedIcon(button);
        return icon != null;
    }

    private static Sprite GetPreservedIcon(Button button)
    {
        if (button == null)
        {
            return null;
        }

        Transform existingIcon = button.transform.Find("DarkUiIcon");
        Image existingIconImage = existingIcon != null ? existingIcon.GetComponent<Image>() : null;
        if (existingIconImage != null && existingIconImage.sprite != null)
        {
            return existingIconImage.sprite;
        }

        Image image = button.image;
        if (image == null || image.sprite == null)
        {
            return null;
        }

        Sprite sprite = image.sprite;
        if (sprite == SlotFrameSprite ||
            sprite == SlotSelectedSprite ||
            sprite == ButtonSprite ||
            sprite == ButtonHoverSprite ||
            sprite == ButtonPressedSprite)
        {
            return null;
        }

        return sprite;
    }

    private static Image GetOrCreateChildImage(Transform parent, string objectName)
    {
        Transform child = parent.Find(objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            childObject.transform.SetParent(parent, false);
            child = childObject.transform;
        }

        Image image = child.GetComponent<Image>();
        if (image == null)
        {
            image = child.gameObject.AddComponent<Image>();
        }

        return image;
    }

    private static string GetTransformPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static bool ShouldStylePanelImage(Image image)
    {
        if (image == null)
        {
            return false;
        }

        GameObject go = image.gameObject;
        if (go.GetComponent<Button>() != null ||
            go.GetComponent<Slider>() != null ||
            go.GetComponent<TMP_InputField>() != null ||
            go.GetComponent<TMP_Dropdown>() != null ||
            go.GetComponent<Toggle>() != null ||
            go.GetComponent<Scrollbar>() != null)
        {
            return false;
        }

        string name = go.name.ToLowerInvariant();
        if (name.Contains("background") ||
            name.Contains("gradient") ||
            name.Contains("overlay") ||
            name.Contains("logo") ||
            name.Contains("icon") ||
            name.Contains("fill") ||
            name.Contains("handle") ||
            name.Contains("swatch") ||
            name.Contains("check") ||
            name.Contains("line") ||
            name.Contains("dot") ||
            name.Contains("seg") ||
            name.Contains("sweep") ||
            name.Contains("cooldown") ||
            name.Contains("color"))
        {
            return false;
        }

        return name.Contains("panel") ||
               name.Contains("dialog") ||
               name.Contains("card") ||
               name.Contains("viewport") ||
               name.Contains("window") ||
               name.Contains("frame");
    }

    private static PanelStyle GuessPanelStyle(string objectName)
    {
        string name = objectName.ToLowerInvariant();
        if (name.Contains("dialog") || name.Contains("modal") || name.Contains("confirm"))
        {
            return PanelStyle.Modal;
        }

        if (name.Contains("hud") || name.Contains("vital") || name.Contains("mic") || name.Contains("notice") || name.Contains("slot"))
        {
            return PanelStyle.Hud;
        }

        if (name.Contains("viewport") || name.Contains("row"))
        {
            return PanelStyle.Subtle;
        }

        return PanelStyle.Standard;
    }
}
