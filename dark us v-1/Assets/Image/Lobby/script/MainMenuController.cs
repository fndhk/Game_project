using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 메인 메뉴 버튼 동작을 관리하는 스크립트이다.
// 방 만들기, 방 찾기, 설정 버튼을 눌렀을 때의 기본 흐름을 담당한다.
[ExecuteAlways]
public class MainMenuController : MonoBehaviour
{
    private struct LocalizedTextBinding
    {
        public TMP_Text Text;
        public string Key;
    }

    private readonly List<LocalizedTextBinding> localizedTexts = new List<LocalizedTextBinding>();

    private int selectedLanguageIndex;
    private TMP_InputField findRoomCodeInput;
    private const string RoomCodePrefsKey = "dark_us_room_code";
    private const string RoomHostPrefsKey = "dark_us_room_is_host";
    private const string RoomVisiblePrefsKey = "dark_us_room_is_visible";
    private const string RoomTitlePrefsKey = "dark_us_room_title";

    [Header("Scene Names")]
    // 방 만들기를 눌렀을 때 이동할 씬 이름이다.
    public string createRoomSceneName = "CreateRoomLobbyScene";

    // 방 찾기를 눌렀을 때 이동할 씬 이름이다.
    public string findRoomSceneName = "PublicRoomListScene";

    // 설정 버튼을 눌렀을 때 이동할 씬 이름이다.
    public string settingsSceneName = "SettingsScene";

    [Header("Panels")]
    // 친구 참가 버튼을 눌렀을 때 띄울 안내 패널이다.
    public GameObject joinFriendPanel;

    // 게임 종료를 다시 확인하는 패널이다.
    public GameObject quitConfirmPanel;

    [Header("Audio Optional")]
    // 버튼 클릭 사운드이다.
    public AudioSource clickAudioSource;

    // 시작 시 설정 패널은 꺼둔다.
    private void Start()
    {
        PhotonConnectionDefaults.Apply();
        MenuCursorState.UnlockCursor();
        SettingsPanelLauncher.DestroyInstance();
        EnsureEventSystem();

        if (!Application.isPlaying)
        {
            EnsureMainMenuLayout();
            return;
        }

        ApplyFixedLowGraphicsSettings();
        EnsureQuitUi();
        EnsureMainMenuLayout();
        EnsureMenuPanels();
        EnsureMainMenuBindings();
        DisableSeparatedScenePanels();
        MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);

        if (joinFriendPanel != null)
        {
            joinFriendPanel.SetActive(false);
        }

        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }

        ApplyLanguage();
        ClearSelectedUi();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            MenuCursorState.UnlockCursor();
            EnsureEventSystem();
            EnsureQuitUi();
            EnsureMainMenuLayout();
            EnsureMenuPanels();
            EnsureMainMenuBindings();
            DisableSeparatedScenePanels();
            MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);
            ClearSelectedUi();
            return;
        }

        UnityEditor.EditorApplication.delayCall -= EnsureEditorMainMenuLayout;
        UnityEditor.EditorApplication.delayCall += EnsureEditorMainMenuLayout;
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        UnityEditor.EditorApplication.delayCall -= EnsureEditorMainMenuLayout;
        UnityEditor.EditorApplication.delayCall += EnsureEditorMainMenuLayout;
    }

    private void EnsureEditorMainMenuLayout()
    {
        if (this == null || Application.isPlaying)
        {
            return;
        }

        EnsureMainMenuLayout();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    // 방 만들기 버튼에서 호출한다.
    public void OnClickCreateRoom()
    {
        PlayClickSound();
        StartPrivateRoomLobby();
    }

    // 방 찾기 버튼에서 호출한다.
    public void OnClickFindRoom()
    {
        PlayClickSound();
        LoadMenuScene(findRoomSceneName, "Public room list scene name is empty.");
    }

    public void OnClickJoinFriend()
    {
        PlayClickSound();
        EnsureMenuPanels();

        if (joinFriendPanel == null)
        {
            Debug.LogWarning("Join friend panel is not assigned.");
            return;
        }

        ShowPanel(joinFriendPanel);
    }

    // 설정 버튼에서 호출한다.
    public void OnClickSettings()
    {
        PlayClickSound();
        LoadMenuScene(settingsSceneName, "Settings scene name is empty.");
    }

    public void OnClickCreateRoomConfirm()
    {
        PlayClickSound();
        StartPrivateRoomLobby();
    }

    private void StartPrivateRoomLobby()
    {
        PlayerPrefs.SetString(RoomCodePrefsKey, Random.Range(0, 10000).ToString("0000"));
        PlayerPrefs.SetString(RoomTitlePrefsKey, "Private Room");
        PlayerPrefs.SetInt(RoomHostPrefsKey, 1);
        PlayerPrefs.SetInt(RoomVisiblePrefsKey, 0);
        PlayerPrefs.Save();
        LoadMenuScene(createRoomSceneName, "Create room scene name is empty.");
    }

    public void OnClickFindRoomConfirm()
    {
        PlayClickSound();

        string roomCode = findRoomCodeInput != null ? findRoomCodeInput.text.Trim() : string.Empty;
        if (!IsValidRoomCode(roomCode))
        {
            Debug.LogWarning("Room code must be exactly 4 digits.");
            return;
        }

        PlayerPrefs.SetString(RoomCodePrefsKey, roomCode);
        PlayerPrefs.SetString(RoomTitlePrefsKey, "Private Room");
        PlayerPrefs.SetInt(RoomHostPrefsKey, 0);
        PlayerPrefs.SetInt(RoomVisiblePrefsKey, 0);
        PlayerPrefs.Save();
        LoadMenuScene(createRoomSceneName, "Create room scene name is empty.");
    }

    public void OnClickClosePanel(GameObject panel)
    {
        PlayClickSound();

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    // 게임 종료 버튼을 나중에 만들 경우 호출한다.
    public void OnClickQuit()
    {
        PlayClickSound();

        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(true);
            return;
        }

        QuitGame();
    }

    // 종료 확인창의 확인 버튼에서 호출한다.
    public void OnClickConfirmQuit()
    {
        PlayClickSound();
        QuitGame();
    }

    // 종료 확인창의 취소 버튼에서 호출한다.
    public void OnClickCancelQuit()
    {
        PlayClickSound();

        if (quitConfirmPanel != null)
        {
            quitConfirmPanel.SetActive(false);
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadMenuScene(string sceneName, string emptyWarning)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning(emptyWarning);
            return;
        }

        ClearSelectedUi();
        SettingsPanelLauncher.DestroyInstance();
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    private bool IsValidRoomCode(string roomCode)
    {
        if (roomCode.Length != 4)
        {
            return false;
        }

        for (int i = 0; i < roomCode.Length; i++)
        {
            if (!char.IsDigit(roomCode[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void ShowPanel(GameObject panel)
    {
        if (joinFriendPanel != null && joinFriendPanel != panel)
        {
            joinFriendPanel.SetActive(false);
        }

        if (quitConfirmPanel != null && quitConfirmPanel != panel)
        {
            quitConfirmPanel.SetActive(false);
        }

        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
        MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);

    }

    private void EnsureMainMenuBindings()
    {
        AddButtonListener("CreateRoomButton", OnClickCreateRoom);
        AddButtonListener("FindRoomButton", OnClickFindRoom);
        AddButtonListener("JoinFriendButton", OnClickJoinFriend);
        AddButtonListener("SettingsButton", OnClickSettings);
    }

    private void AddButtonListener(string buttonName, UnityEngine.Events.UnityAction action)
    {
        Transform buttonTransform = FindUiTransform(buttonName);
        if (buttonTransform == null)
        {
            return;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        MenuButtonHoverEffect.EnsureOn(button);
    }

    private void EnsureQuitUi()
    {
        Transform exitTransform = FindUiTransform("ExitButton");
        if (exitTransform == null)
        {
            Transform buttonGroup = FindUiTransform("ButtonGroup");
            if (buttonGroup != null)
            {
                if (buttonGroup.Find("ExitSpacer") == null)
                {
                    CreateExitSpacer(buttonGroup);
                }

                Button exitButton = CreateMenuButton(buttonGroup, "ExitButton", "Exit");
                exitButton.onClick.AddListener(OnClickQuit);
            }
            else
            {
                Debug.LogWarning("ButtonGroup is not found. Exit button was not created.");
            }
        }
        else
        {
            Button exitButton = exitTransform.GetComponent<Button>();
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnClickQuit);
                exitButton.onClick.AddListener(OnClickQuit);
            }
        }

        if (quitConfirmPanel == null)
        {
            Transform existingPanel = FindUiTransform("QuitConfirmPanel");
            quitConfirmPanel = existingPanel != null ? existingPanel.gameObject : CreateQuitConfirmPanel();
        }
    }

    private void EnsureMainMenuLayout()
    {
        Transform buttonGroup = FindUiTransform("ButtonGroup");
        if (buttonGroup == null)
        {
            Debug.LogWarning("ButtonGroup is not found. Main menu layout was not updated.");
            return;
        }

        Button joinFriendButton = null;
        Transform joinFriendTransform = buttonGroup.Find("JoinFriendButton");
        if (joinFriendTransform == null)
        {
            joinFriendButton = CreateMenuButton(buttonGroup, "JoinFriendButton", "Join Friend");
            joinFriendTransform = joinFriendButton.transform;
        }
        else
        {
            joinFriendButton = joinFriendTransform.GetComponent<Button>();
        }

        joinFriendTransform.SetSiblingIndex(2);
        if (joinFriendButton != null)
        {
            joinFriendButton.onClick.RemoveListener(OnClickJoinFriend);
            joinFriendButton.onClick.AddListener(OnClickJoinFriend);
        }

        SetMenuButtonLabel("CreateRoomButton", "Private Game");
        SetMenuButtonLabel("FindRoomButton", "Public Game");
        SetMenuButtonLabel("JoinFriendButton", "Join Friend");
        SetMenuButtonLabel("SettingsButton", "Settings");
        SetMenuButtonLabel("ExitButton", "Quit Game");
        DisableDecorativeRaycasts();
        ConfigureMainMenuVisualStyle("CreateRoomButton");
        ConfigureMainMenuVisualStyle("FindRoomButton");
        ConfigureMainMenuVisualStyle("JoinFriendButton");
        ConfigureMainMenuVisualStyle("SettingsButton");
        ConfigureMainMenuVisualStyle("ExitButton");
        ConfigureMenuButtonSelection("CreateRoomButton");
        ConfigureMenuButtonSelection("FindRoomButton");
        ConfigureMenuButtonSelection("JoinFriendButton");
        ConfigureMenuButtonSelection("SettingsButton");
        ConfigureMenuButtonSelection("ExitButton");
        EnsureMainMenuChrome(buttonGroup);

        RectTransform groupRect = buttonGroup.GetComponent<RectTransform>();
        if (groupRect != null)
        {
            groupRect.anchorMin = new Vector2(0f, 0.5f);
            groupRect.anchorMax = new Vector2(0f, 0.5f);
            groupRect.pivot = new Vector2(0f, 0.5f);
            groupRect.anchoredPosition = new Vector2(76f, 46f);
            groupRect.sizeDelta = new Vector2(420f, 560f);
        }

        VerticalLayoutGroup verticalLayout = buttonGroup.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null)
        {
            verticalLayout.enabled = false;
        }

        PositionMainMenuButton("CreateRoomButton", new Vector2(0f, 206f));
        PositionMainMenuButton("FindRoomButton", new Vector2(0f, 136f));
        PositionMainMenuButton("JoinFriendButton", new Vector2(0f, 66f));
        PositionMainMenuButton("SettingsButton", new Vector2(0f, -4f));
        PositionMainMenuButton("ExitButton", new Vector2(0f, -350f));
        AlignLogoToMainMenuButtons();
    }

    private void DisableDecorativeRaycasts()
    {
        DisableGraphicRaycast("DarkOverlay");
        DisableGraphicRaycast("LeftGradientPanel");
        DisableGraphicRaycast("VersionText");
    }

    private void DisableGraphicRaycast(string objectName)
    {
        Transform target = FindUiTransform(objectName);
        if (target == null)
        {
            return;
        }

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = false;
        }
    }

    private void AlignLogoToMainMenuButtons()
    {
        Transform logoTransform = FindUiTransform("LogoImage");
        Transform firstButtonTransform = FindUiTransform("CreateRoomButton");
        if (logoTransform == null || firstButtonTransform == null)
        {
            return;
        }

        RectTransform logoRect = logoTransform.GetComponent<RectTransform>();
        RectTransform buttonRect = firstButtonTransform.GetComponent<RectTransform>();
        if (logoRect == null || buttonRect == null)
        {
            return;
        }

        Vector3[] buttonCorners = new Vector3[4];
        buttonRect.GetWorldCorners(buttonCorners);
        float buttonCenterX = (buttonCorners[0].x + buttonCorners[2].x) * 0.5f;

        Vector3 logoPosition = logoRect.position;
        logoPosition.x = buttonCenterX;
        logoRect.position = logoPosition;
    }

    private void PositionMainMenuButton(string buttonName, Vector2 anchoredPosition)
    {
        Transform buttonTransform = FindUiTransform(buttonName);
        if (buttonTransform == null)
        {
            return;
        }

        RectTransform rectTransform = buttonTransform.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(0f, 0.5f);
        rectTransform.pivot = new Vector2(0f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(420f, 50f);

        LayoutElement layout = buttonTransform.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.ignoreLayout = true;
        }
    }

    private void EnsureMainMenuChrome(Transform buttonGroup)
    {
        Canvas canvas = FindSceneCanvas();
        if (canvas == null)
        {
            return;
        }

        Transform existing = canvas.transform.Find("MainMenuChrome");
        if (existing != null)
        {
            DestroyUiObject(existing.gameObject);
        }

        // 기존 배경/타이틀을 가리지 않도록 추가 장식 패널은 만들지 않는다.
        // 버튼 스타일만 정리해서 메인 메뉴 원본 구도를 유지한다.
        if (buttonGroup != null)
        {
            buttonGroup.SetAsLastSibling();
        }
    }

    private GameObject CreateMenuChromePanel(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Vector2 pivot, Color color)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        panel.layer = parent.gameObject.layer;
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.color = color;

        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);
        return panel;
    }

    private TMP_Text CreateChromeText(Transform parent, string objectName, string text, float fontSize, FontStyles fontStyle, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment, Color color)
    {
        TMP_Text label = CreateLabel(parent, objectName, text, fontSize, fontStyle);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        label.alignment = alignment;
        label.color = color;
        LocalizedTmpFontProvider.Apply(label);
        return label;
    }

    private void CreateAccentLine(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Color color)
    {
        GameObject line = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        line.layer = parent.gameObject.layer;
        line.transform.SetParent(parent, false);

        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x, anchor.y);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = line.GetComponent<Image>();
        image.color = color;
    }

    private void SetMenuButtonLabel(string buttonName, string label)
    {
        Transform buttonTransform = FindUiTransform(buttonName);
        if (buttonTransform == null)
        {
            return;
        }

        TMP_Text text = buttonTransform.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.fontStyle = FontStyles.Normal;
            text.text = label;
        }
    }

    private void ConfigureMainMenuVisualStyle(string buttonName)
    {
        Transform buttonTransform = FindUiTransform(buttonName);
        if (buttonTransform == null)
        {
            return;
        }

        Image image = buttonTransform.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.015f, 0.018f, 0.02f, 0.58f);
        }

        TMP_Text text = buttonTransform.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.fontStyle = FontStyles.Normal;
            text.color = new Color(0.76f, 0.82f, 0.84f, 1f);
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            RectTransform textRect = text.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.offsetMin = new Vector2(20f, 0f);
                textRect.offsetMax = new Vector2(-16f, 0f);
            }
        }

        RectTransform rectTransform = buttonTransform.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(420f, 50f);
        }

        LayoutElement layout = buttonTransform.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = 420f;
            layout.preferredHeight = 50f;
        }

        Outline outline = buttonTransform.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.20f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        MenuButtonHoverEffect hover = MenuButtonHoverEffect.EnsureOn(buttonTransform.gameObject);
        if (hover != null)
        {
            hover.normalBackgroundColor = new Color(0.015f, 0.018f, 0.02f, 0.58f);
            hover.hoverBackgroundColor = new Color(0.09f, 0.12f, 0.13f, 0.82f);
            hover.pressedBackgroundColor = new Color(0.16f, 0.18f, 0.17f, 0.86f);
            hover.normalTextColor = new Color(0.76f, 0.82f, 0.84f, 1f);
            hover.hoverTextColor = new Color(1f, 0.8f, 0.42f, 1f);
        }
    }

    private void ConfigureMenuButtonSelection(string buttonName)
    {
        Transform buttonTransform = FindUiTransform(buttonName);
        if (buttonTransform == null)
        {
            return;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    private void ClearSelectedUi()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void EnsureEventSystem()
    {
        UiEventSystemUtility.EnsureSingle(gameObject);
    }

    private void EnsureMenuPanels()
    {
        if (joinFriendPanel == null)
        {
            Transform existingPanel = FindUiTransform("JoinFriendPanel");
            joinFriendPanel = existingPanel != null ? existingPanel.gameObject : CreateMenuPanel(
                "JoinFriendPanel",
                "Join Friend",
                "Enter the 4-digit room code from the host.",
                "Join",
                OnClickFindRoomConfirm
            );
        }

        PrepareJoinFriendPanel(joinFriendPanel);
    }

    private void DisableSeparatedScenePanels()
    {
        DisablePanelIfFound("CreateRoomPanel");
        DisablePanelIfFound("FindRoomPanel");
        DisablePanelIfFound("SettingsPanel");
    }

    private void DisablePanelIfFound(string panelName)
    {
        Transform panel = FindUiTransform(panelName);

        if (panel != null)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private GameObject CreateMenuPanel(string objectName, string title, string body, string primaryLabel, UnityEngine.Events.UnityAction primaryAction)
    {
        Canvas canvas = FindSceneCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas is not found. Menu panel was not created.");
            return null;
        }

        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.layer = canvas.gameObject.layer;
        panelObject.transform.SetParent(canvas.transform, false);
        PrepareOverlayRect(panelObject.GetComponent<RectTransform>());
        BuildPanelContents(panelObject, title, body, primaryLabel, primaryAction);
        panelObject.SetActive(false);
        return panelObject;
    }

    private void PrepareJoinFriendPanel(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return;
        }

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            PrepareOverlayRect(rectTransform);
        }

        if (panelObject.GetComponent<Image>() == null)
        {
            panelObject.AddComponent<CanvasRenderer>();
            panelObject.AddComponent<Image>();
        }

        Transform existingInput = panelObject.transform.Find("Dialog/RoomCodeInput");
        if (existingInput != null)
        {
            findRoomCodeInput = existingInput.GetComponent<TMP_InputField>();
            NormalizeJoinFriendPanel(panelObject);
            return;
        }

        for (int i = panelObject.transform.childCount - 1; i >= 0; i--)
        {
            DestroyUiObject(panelObject.transform.GetChild(i).gameObject);
        }

        BuildPanelContents(panelObject, "Join Friend", "Enter the 4-digit room code from the host.", "Join", OnClickFindRoomConfirm);
        NormalizeJoinFriendPanel(panelObject);
    }

    private void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            DestroyUiObject(parent.GetChild(i).gameObject);
        }
    }

    private void DestroyUiObject(GameObject target)
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

    private void PrepareOverlayRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void BuildPanelContents(GameObject panelObject, string title, string body, string primaryLabel, UnityEngine.Events.UnityAction primaryAction)
    {
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.68f);

        GameObject dialogObject = new GameObject("Dialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        dialogObject.layer = panelObject.layer;
        dialogObject.transform.SetParent(panelObject.transform, false);

        RectTransform dialogRect = dialogObject.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = new Vector2(0f, 10f);
        dialogRect.sizeDelta = new Vector2(700f, 390f);

        Image dialogImage = dialogObject.GetComponent<Image>();
        dialogImage.color = new Color(0.015f, 0.018f, 0.02f, 0.94f);

        Outline dialogOutline = dialogObject.GetComponent<Outline>();
        dialogOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.4f);
        dialogOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text titleText = CreateLabel(dialogObject.transform, "TitleText", title, 42f, FontStyles.UpperCase);
        RegisterLocalizedText(titleText, title);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -62f);
        titleRect.sizeDelta = new Vector2(-80f, 64f);
        titleText.color = new Color(1f, 0.8f, 0.42f, 1f);

        TMP_Text bodyText = CreateLabel(dialogObject.transform, "BodyText", body, 26f, FontStyles.Normal);
        RegisterLocalizedText(bodyText, body);
        RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.5f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.anchoredPosition = new Vector2(0f, 8f);
        bodyRect.sizeDelta = new Vector2(-120f, 80f);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.color = new Color(0.76f, 0.82f, 0.84f, 1f);

        if (title == "Find Room" || title == "Join Friend")
        {
            bodyRect.anchoredPosition = new Vector2(0f, 22f);
            bodyRect.sizeDelta = new Vector2(-120f, 72f);
            findRoomCodeInput = CreateRoomCodeInput(dialogObject.transform);
        }

        Button primaryButton = CreateMenuButton(dialogObject.transform, primaryLabel + "Button", primaryLabel, 220f, 64f, 28f);
        RectTransform primaryRect = primaryButton.GetComponent<RectTransform>();
        primaryRect.anchorMin = new Vector2(0.5f, 0f);
        primaryRect.anchorMax = new Vector2(0.5f, 0f);
        primaryRect.anchoredPosition = new Vector2(-124f, 70f);
        primaryButton.onClick.AddListener(primaryAction);

        Button closeButton = CreateMenuButton(dialogObject.transform, "CloseButton", "Close", 220f, 64f, 28f);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(124f, 70f);
        closeButton.onClick.AddListener(() => OnClickClosePanel(panelObject));
        NormalizeJoinFriendPanel(panelObject);
    }

    private void NormalizeJoinFriendPanel(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return;
        }

        Transform dialog = panelObject.transform.Find("Dialog");
        if (dialog == null)
        {
            return;
        }

        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        if (dialogRect != null)
        {
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.anchoredPosition = new Vector2(0f, 10f);
            dialogRect.sizeDelta = new Vector2(700f, 390f);
        }

        TMP_Text titleText = dialog.Find("TitleText")?.GetComponent<TMP_Text>();
        if (titleText != null)
        {
            RectTransform titleRect = titleText.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -62f);
            titleRect.sizeDelta = new Vector2(-80f, 64f);
            FitPanelText(titleText, 42f, false);
        }

        TMP_Text bodyText = dialog.Find("BodyText")?.GetComponent<TMP_Text>();
        if (bodyText != null)
        {
            RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0.5f);
            bodyRect.anchorMax = new Vector2(1f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, 22f);
            bodyRect.sizeDelta = new Vector2(-120f, 72f);
            FitPanelText(bodyText, 25f, true);
        }

        RectTransform inputRect = dialog.Find("RoomCodeInput")?.GetComponent<RectTransform>();
        if (inputRect != null)
        {
            inputRect.anchorMin = new Vector2(0.5f, 0.5f);
            inputRect.anchorMax = new Vector2(0.5f, 0.5f);
            inputRect.anchoredPosition = new Vector2(0f, -48f);
            inputRect.sizeDelta = new Vector2(280f, 56f);
        }

        NormalizePanelButton(dialog, "JoinButton", new Vector2(-124f, 74f));
        NormalizePanelButton(dialog, "FindButton", new Vector2(-124f, 74f));
        NormalizePanelButton(dialog, "CloseButton", new Vector2(124f, 74f));

        if (Application.isPlaying)
        {
            MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);
        }
    }

    private void NormalizePanelButton(Transform dialog, string objectName, Vector2 position)
    {
        Transform buttonTransform = dialog.Find(objectName);
        if (buttonTransform == null)
        {
            return;
        }

        RectTransform rect = buttonTransform.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(210f, 60f);
        }

        LayoutElement layout = buttonTransform.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredWidth = 210f;
            layout.preferredHeight = 60f;
        }

        Button button = buttonTransform.GetComponent<Button>();
        if (button != null)
        {
            MenuButtonHoverEffect.EnsureOn(button);
        }

        FitPanelText(buttonTransform.GetComponentInChildren<TMP_Text>(true), 25f, false);
    }

    private void FitPanelText(TMP_Text text, float maxFontSize, bool wrap)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = maxFontSize;
        text.fontSize = Mathf.Min(text.fontSize, maxFontSize);
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
    }

    private TMP_InputField CreateRoomCodeInput(Transform parent)
    {
        GameObject inputObject = new GameObject("RoomCodeInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField), typeof(Outline));
        inputObject.layer = parent.gameObject.layer;
        inputObject.transform.SetParent(parent, false);

        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(0f, -42f);
        inputRect.sizeDelta = new Vector2(280f, 58f);

        Image inputImage = inputObject.GetComponent<Image>();
        inputImage.color = new Color(0.015f, 0.018f, 0.02f, 0.78f);

        Outline outline = inputObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.36f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text text = CreateLabel(inputObject.transform, "Text Area", string.Empty, 30f, FontStyles.UpperCase);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.offsetMin = new Vector2(20f, 6f);
        textRect.offsetMax = new Vector2(-20f, -6f);
        text.alignment = TextAlignmentOptions.Center;

        TMP_Text placeholder = CreateLabel(inputObject.transform, "Placeholder", "0000", 30f, FontStyles.UpperCase);
        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.offsetMin = new Vector2(20f, 6f);
        placeholderRect.offsetMax = new Vector2(-20f, -6f);
        placeholder.alignment = TextAlignmentOptions.Center;
        placeholder.color = new Color(0.76f, 0.82f, 0.84f, 0.36f);

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.textViewport = inputRect;
        inputField.characterLimit = 4;
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.caretColor = new Color(1f, 0.8f, 0.42f, 1f);
        inputField.selectionColor = new Color(1f, 0.8f, 0.42f, 0.28f);

        return inputField;
    }

    private void ApplyFixedLowGraphicsSettings()
    {
        QualitySettings.globalTextureMipmapLimit = 3;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 0;
    }

    private void RegisterLocalizedText(TMP_Text text, string key)
    {
        if (text == null || string.IsNullOrEmpty(key))
        {
            return;
        }

        localizedTexts.Add(new LocalizedTextBinding
        {
            Text = text,
            Key = key
        });
    }

    private void ApplyLanguage()
    {
        selectedLanguageIndex = Mathf.Clamp(PlayerPrefs.GetInt("setting_language", 0), 0, 2);

        ApplyLanguageToSceneButton("CreateRoomButton", "Private Game");
        ApplyLanguageToSceneButton("FindRoomButton", "Public Game");
        ApplyLanguageToSceneButton("JoinFriendButton", "Join Friend");
        ApplyLanguageToSceneButton("SettingsButton", "Settings");
        ApplyLanguageToSceneButton("ExitButton", "Quit Game");

        for (int i = localizedTexts.Count - 1; i >= 0; i--)
        {
            TMP_Text text = localizedTexts[i].Text;
            if (text == null)
            {
                localizedTexts.RemoveAt(i);
                continue;
            }

            ApplyFontForLanguage(text);
            text.text = Translate(localizedTexts[i].Key);
        }
    }

    private void ApplyLanguageToSceneButton(string objectName, string key)
    {
        Transform buttonTransform = FindUiTransform(objectName);
        if (buttonTransform == null)
        {
            return;
        }

        TMP_Text text = buttonTransform.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            ApplyFontForLanguage(text);
            text.fontStyle = FontStyles.Normal;
            text.text = Translate(key);
        }
    }

    private void ApplyFontForLanguage(TMP_Text text)
    {
        LocalizedTmpFontProvider.Apply(text);
    }

    private string Translate(string key)
    {
        switch (selectedLanguageIndex)
        {
            case 1:
                return TranslateEnglish(key);
            case 2:
                return TranslateJapanese(key);
            default:
                return TranslateKorean(key);
        }
    }

    private string TranslateEnglish(string key)
    {
        return key;
    }

    private string TranslateKorean(string key)
    {
        switch (key)
        {
            case "Create Room": return "방 만들기";
            case "Find Room": return "방 찾기";
            case "Private Game": return "비공개 게임";
            case "Public Game": return "공개 게임";
            case "Join Friend": return "친구 참가";
            case "Steam friend invites will be connected here later.": return "Steam 친구 초대는 여기에 나중에 연결됩니다.";
            case "Enter the 4-digit room code from the host.": return "호스트 화면에 보이는 4자리 방 코드를 입력하세요.";
            case "Join": return "참가";
            case "Setting": return "설정";
            case "Settings": return "설정";
            case "SETTINGS": return "설정";
            case "Exit": return "나가기";
            case "Quit Game": return "게임 종료";
            case "Close": return "닫기";
            case "Apply": return "적용";
            case "Reset": return "초기화";
            case "Change": return "변경";
            case "Confirm": return "확인";
            case "Cancel": return "취소";
            case "Quit Game?": return "게임을 종료할까요?";
            case "Create": return "생성";
            case "Search": return "검색";
            case "Open a private operation room and wait for the crew.": return "비공개 작전 방을 만들고 대원을 기다립니다.";
            case "Enter the signal list and search for an active room.": return "신호 목록으로 들어가 활성화된 방을 찾습니다.";
            case "R-03 SIGNAL TERMINAL": return "R-03 신호 터미널";
            case "COMMAND": return "명령";
            case "SYSTEM STATUS": return "시스템 상태";
            case "NETWORK STANDBY": return "네트워크 대기";
            case "VOICE LINK READY": return "음성 링크 준비";
            case "CREW AUTH REQUIRED": return "대원 인증 필요";
            case "Display": return "화면";
            case "Graphics Display": return "그래픽 표시";
            case "Graphics & Display": return "그래픽 및 화면";
            case "Audio": return "오디오";
            case "Controls Keybindings": return "조작 키 설정";
            case "Controls & Keybindings": return "조작 및 키 설정";
            case "Gameplay": return "게임플레이";
            case "Accessibility": return "접근성";
            case "Screen Mode": return "화면 모드";
            case "Texture Detail": return "텍스처 품질";
            case "Shadow Detail": return "그림자 품질";
            case "Anti-aliasing": return "안티앨리어싱";
            case "V-Sync": return "수직 동기화";
            case "FPS Limit": return "FPS 제한";
            case "Motion Blur": return "모션 블러";
            case "Camera Shake": return "카메라 흔들림";
            case "Master Volume": return "마스터 볼륨";
            case "BGM Volume": return "배경음 볼륨";
            case "SFX Volume": return "효과음 볼륨";
            case "Voice Volume": return "음성 볼륨";
            case "Move": return "이동";
            case "Move Forward": return "앞으로 이동";
            case "Move Back": return "뒤로 이동";
            case "Move Left": return "왼쪽 이동";
            case "Move Right": return "오른쪽 이동";
            case "Sprint": return "달리기";
            case "Crouch": return "웅크리기";
            case "Interact": return "상호작용";
            case "Pick Up": return "줍기";
            case "Scan": return "스캔";
            case "Use Item": return "아이템 사용";
            case "Drop Item": return "아이템 버리기";
            case "Slot 1": return "슬롯 1";
            case "Slot 2": return "슬롯 2";
            case "Push To Talk": return "눌러서 말하기";
            case "Mic Mute": return "마이크 음소거";
            case "Kill": return "킬";
            case "Pause": return "일시정지";
            case "Press a key": return "키를 누르세요";
            case "Mouse Sensitivity X": return "마우스 감도 X";
            case "Mouse Sensitivity Y": return "마우스 감도 Y";
            case "Invert Mouse Y": return "마우스 Y축 반전";
            case "Gamepad Vibration": return "게임패드 진동";
            case "HUD Opacity": return "HUD 투명도";
            case "Scan Density": return "스캔 밀도";
            case "Scan Dot Size": return "스캔 점 크기";
            case "Language": return "언어";
            case "Color Blind Mode": return "색약 모드";
            case "Tutorial": return "튜토리얼";
            case "FULLSCREEN": return "전체 화면";
            case "WINDOWED": return "창 모드";
            case "BORDERLESS": return "테두리 없음";
            case "UNLIMITED": return "제한 없음";
            case "KOREAN": return "한국어";
            case "ENGLISH": return "영어";
            case "JAPANESE": return "일본어";
            default: return key;
        }
    }

    private string TranslateJapanese(string key)
    {
        switch (key)
        {
            case "Create Room": return "ルーム作成";
            case "Find Room": return "ルーム検索";
            case "Private Game": return "プライベートゲーム";
            case "Public Game": return "公開ゲーム";
            case "Join Friend": return "フレンド参加";
            case "Steam friend invites will be connected here later.": return "Steamフレンド招待は後でここに接続されます。";
            case "Enter the 4-digit room code from the host.": return "ホスト画面の4桁ルームコードを入力してください。";
            case "Join": return "参加";
            case "Setting": return "設定";
            case "Settings": return "設定";
            case "SETTINGS": return "設定";
            case "Exit": return "終了";
            case "Quit Game": return "ゲーム終了";
            case "Close": return "閉じる";
            case "Apply": return "適用";
            case "Reset": return "リセット";
            case "Change": return "変更";
            case "Confirm": return "確認";
            case "Cancel": return "キャンセル";
            case "Quit Game?": return "ゲームを終了しますか?";
            case "Create": return "作成";
            case "Search": return "検索";
            case "Open a private operation room and wait for the crew.": return "プライベート作戦ルームを開き、クルーを待ちます。";
            case "Enter the signal list and search for an active room.": return "信号リストに入り、参加できるルームを探します。";
            case "R-03 SIGNAL TERMINAL": return "R-03信号端末";
            case "COMMAND": return "コマンド";
            case "SYSTEM STATUS": return "システム状態";
            case "NETWORK STANDBY": return "ネットワーク待機";
            case "VOICE LINK READY": return "ボイスリンク準備";
            case "CREW AUTH REQUIRED": return "クルー認証が必要";
            case "Display": return "画面";
            case "Graphics Display": return "グラフィック表示";
            case "Graphics & Display": return "グラフィックと画面";
            case "Audio": return "オーディオ";
            case "Controls Keybindings": return "操作キー設定";
            case "Controls & Keybindings": return "操作とキー設定";
            case "Gameplay": return "ゲームプレイ";
            case "Accessibility": return "アクセシビリティ";
            case "Screen Mode": return "画面モード";
            case "Texture Detail": return "テクスチャ品質";
            case "Shadow Detail": return "影の品質";
            case "Anti-aliasing": return "アンチエイリアス";
            case "V-Sync": return "垂直同期";
            case "FPS Limit": return "FPS制限";
            case "Motion Blur": return "モーションブラー";
            case "Camera Shake": return "カメラ揺れ";
            case "Master Volume": return "マスター音量";
            case "BGM Volume": return "BGM音量";
            case "SFX Volume": return "効果音音量";
            case "Voice Volume": return "ボイス音量";
            case "Move": return "移動";
            case "Move Forward": return "前進";
            case "Move Back": return "後退";
            case "Move Left": return "左移動";
            case "Move Right": return "右移動";
            case "Sprint": return "走る";
            case "Crouch": return "しゃがむ";
            case "Interact": return "インタラクト";
            case "Pick Up": return "拾う";
            case "Scan": return "スキャン";
            case "Use Item": return "アイテム使用";
            case "Drop Item": return "アイテムを捨てる";
            case "Slot 1": return "スロット 1";
            case "Slot 2": return "スロット 2";
            case "Push To Talk": return "プッシュトゥトーク";
            case "Mic Mute": return "マイクミュート";
            case "Kill": return "キル";
            case "Pause": return "一時停止";
            case "Press a key": return "キーを押してください";
            case "Mouse Sensitivity X": return "マウス感度 X";
            case "Mouse Sensitivity Y": return "マウス感度 Y";
            case "Invert Mouse Y": return "マウスY反転";
            case "Gamepad Vibration": return "ゲームパッド振動";
            case "HUD Opacity": return "HUD透明度";
            case "Scan Density": return "スキャン密度";
            case "Scan Dot Size": return "スキャン点サイズ";
            case "Language": return "言語";
            case "Color Blind Mode": return "色覚サポート";
            case "Tutorial": return "チュートリアル";
            case "FULLSCREEN": return "フルスクリーン";
            case "WINDOWED": return "ウィンドウ";
            case "BORDERLESS": return "ボーダーレス";
            case "UNLIMITED": return "無制限";
            case "KOREAN": return "韓国語";
            case "ENGLISH": return "英語";
            case "JAPANESE": return "日本語";
            default: return key;
        }
    }

    private void CreateExitSpacer(Transform parent)
    {
        GameObject spacerObject = new GameObject("ExitSpacer", typeof(RectTransform), typeof(LayoutElement));
        spacerObject.layer = parent.gameObject.layer;
        spacerObject.transform.SetParent(parent, false);

        LayoutElement layoutElement = spacerObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 78f;
        layoutElement.ignoreLayout = false;

        RectTransform spacerRect = spacerObject.GetComponent<RectTransform>();
        spacerRect.sizeDelta = new Vector2(360f, 78f);
    }

    private Button CreateMenuButton(Transform parent, string objectName, string label)
    {
        return CreateMenuButton(parent, objectName, label, 360f, 74f, 34f);
    }

    private Button CreateMenuButton(Transform parent, string objectName, string label, float preferredWidth, float preferredHeight, float fontSize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(MenuButtonHoverEffect));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(preferredWidth, preferredHeight);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.015f, 0.018f, 0.02f, 0.62f);
        image.type = Image.Type.Sliced;

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.34f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text labelText = CreateLabel(buttonObject.transform, "Text (TMP)", label, fontSize, FontStyles.UpperCase);
        RegisterLocalizedText(labelText, label);

        MenuButtonHoverEffect hoverEffect = buttonObject.GetComponent<MenuButtonHoverEffect>();
        hoverEffect.buttonImage = image;
        hoverEffect.labelText = labelText;
        hoverEffect.normalBackgroundColor = new Color(0.015f, 0.018f, 0.02f, 0.52f);
        hoverEffect.hoverBackgroundColor = new Color(0.09f, 0.12f, 0.13f, 0.76f);
        hoverEffect.pressedBackgroundColor = new Color(0.16f, 0.18f, 0.17f, 0.86f);
        hoverEffect.normalTextColor = new Color(0.76f, 0.82f, 0.84f, 1f);
        hoverEffect.hoverTextColor = new Color(1f, 0.8f, 0.42f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        return button;
    }

    private GameObject CreateQuitConfirmPanel()
    {
        Canvas canvas = FindSceneCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas is not found. Quit confirm panel was not created.");
            return null;
        }

        GameObject panelObject = new GameObject("QuitConfirmPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.layer = canvas.gameObject.layer;
        panelObject.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject dialogObject = new GameObject("Dialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        dialogObject.layer = panelObject.layer;
        dialogObject.transform.SetParent(panelObject.transform, false);

        RectTransform dialogRect = dialogObject.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(520f, 260f);

        Image dialogImage = dialogObject.GetComponent<Image>();
        dialogImage.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);

        Outline dialogOutline = dialogObject.GetComponent<Outline>();
        dialogOutline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        dialogOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text messageText = CreateLabel(dialogObject.transform, "MessageText", "Quit Game?", 42f, FontStyles.Normal);
        RegisterLocalizedText(messageText, "Quit Game?");
        RectTransform messageRect = messageText.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0f, 0.5f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.offsetMin = new Vector2(30f, -10f);
        messageRect.offsetMax = new Vector2(-30f, -20f);

        GameObject rowObject = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowObject.layer = panelObject.layer;
        rowObject.transform.SetParent(dialogObject.transform, false);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0f);
        rowRect.anchoredPosition = new Vector2(0f, 62f);
        rowRect.sizeDelta = new Vector2(-80f, 74f);

        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 24f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;

        Button confirmButton = CreateMenuButton(rowObject.transform, "ConfirmQuitButton", "Confirm");
        confirmButton.onClick.AddListener(OnClickConfirmQuit);

        Button cancelButton = CreateMenuButton(rowObject.transform, "CancelQuitButton", "Cancel");
        cancelButton.onClick.AddListener(OnClickCancelQuit);

        panelObject.SetActive(false);
        return panelObject;
    }

    private TMP_Text CreateLabel(Transform parent, string objectName, string text, float fontSize, FontStyles fontStyle)
    {
        GameObject labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.layer = parent.gameObject.layer;
        labelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = labelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        TMP_Text labelText = labelObject.GetComponent<TMP_Text>();
        labelText.text = text;
        labelText.fontSize = fontSize;
        labelText.fontStyle = fontStyle;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        labelText.raycastTarget = false;

        return labelText;
    }

    private Transform FindUiTransform(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform targetTransform in transforms)
        {
            if (targetTransform.name == objectName && targetTransform.gameObject.scene == gameObject.scene)
            {
                return targetTransform;
            }
        }

        return null;
    }

    private Canvas FindSceneCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.scene == gameObject.scene)
            {
                return canvas;
            }
        }

        return null;
    }

    // 버튼 클릭 사운드를 재생한다.
    private void PlayClickSound()
    {
        if (clickAudioSource == null)
        {
            return;
        }

        if (clickAudioSource.clip != null)
        {
            clickAudioSource.PlayOneShot(clickAudioSource.clip);
            return;
        }

        clickAudioSource.Play();
    }
}
