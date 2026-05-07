using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomLobbySceneController : MonoBehaviour
{
    public string mainMenuSceneName = "LobbyScene 1";
    public string gameSceneName = "labor";

    private void Start()
    {
        EnsureEventSystem();
        BuildRoomLobbyUi();
    }

    public void OnClickStartGame()
    {
        LoadScene(gameSceneName);
    }

    public void OnClickBack()
    {
        LoadScene(mainMenuSceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("Scene name is empty.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void BuildRoomLobbyUi()
    {
        Canvas canvas = CreateCanvas();
        CreateBackground(canvas.transform);

        TMP_Text title = CreateText(canvas.transform, "TitleText", "ROOM LOBBY", 64f, FontStyles.UpperCase);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(300f, -150f);
        titleRect.sizeDelta = new Vector2(520f, 90f);
        title.color = new Color(1f, 0.8f, 0.42f, 1f);

        TMP_Text code = CreateText(canvas.transform, "RoomCodeText", "ROOM CODE 0000", 34f, FontStyles.UpperCase);
        RectTransform codeRect = code.GetComponent<RectTransform>();
        codeRect.anchorMin = new Vector2(0f, 1f);
        codeRect.anchorMax = new Vector2(0f, 1f);
        codeRect.anchoredPosition = new Vector2(300f, -230f);
        codeRect.sizeDelta = new Vector2(520f, 56f);

        GameObject panel = new GameObject("CrewPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
        panel.layer = canvas.gameObject.layer;
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(300f, -430f);
        panelRect.sizeDelta = new Vector2(520f, 260f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.018f, 0.02f, 0.66f);

        Outline panelOutline = panel.GetComponent<Outline>();
        panelOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.34f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 26, 26);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        CreateText(panel.transform, "SlotHost", "HOST    READY", 30f, FontStyles.UpperCase);
        CreateText(panel.transform, "SlotEmpty1", "EMPTY   WAITING", 30f, FontStyles.UpperCase);
        CreateText(panel.transform, "SlotEmpty2", "EMPTY   WAITING", 30f, FontStyles.UpperCase);
        CreateText(panel.transform, "SlotEmpty3", "EMPTY   WAITING", 30f, FontStyles.UpperCase);

        Button startButton = CreateButton(canvas.transform, "StartGameButton", "Start Game", 260f, 70f, 30f);
        RectTransform startRect = startButton.GetComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0f, 0f);
        startRect.anchorMax = new Vector2(0f, 0f);
        startRect.anchoredPosition = new Vector2(300f, 160f);
        startButton.onClick.AddListener(OnClickStartGame);

        Button backButton = CreateButton(canvas.transform, "BackButton", "Back", 260f, 70f, 30f);
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 0f);
        backRect.anchoredPosition = new Vector2(300f, 72f);
        backButton.onClick.AddListener(OnClickBack);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private void CreateBackground(Transform parent)
    {
        GameObject background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        background.transform.SetParent(parent, false);

        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = background.GetComponent<Image>();
        image.color = new Color(0.005f, 0.007f, 0.008f, 1f);
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        label.raycastTarget = false;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 42f);

        return label;
    }

    private Button CreateButton(Transform parent, string objectName, string label, float width, float height, float fontSize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(MenuButtonHoverEffect));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.015f, 0.018f, 0.02f, 0.62f);
        image.type = Image.Type.Sliced;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.34f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text labelText = CreateText(buttonObject.transform, "Text (TMP)", label, fontSize, FontStyles.UpperCase);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

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
}
