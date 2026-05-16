using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 메인 메뉴 버튼 동작을 관리하는 스크립트이다.
// 방 만들기, 방 찾기, 설정 버튼을 눌렀을 때의 기본 흐름을 담당한다.
[ExecuteAlways]
public class MainMenuController : MonoBehaviourPunCallbacks
{
    private struct LocalizedTextBinding
    {
        public TMP_Text Text;
        public string Key;
    }

    private readonly List<TMP_Text> dynamicSettingLabels = new List<TMP_Text>();
    private readonly List<Slider> settingsSliders = new List<Slider>();
    private readonly List<LocalizedTextBinding> localizedTexts = new List<LocalizedTextBinding>();
    private readonly List<KeyBindingTextBinding> keyBindingTexts = new List<KeyBindingTextBinding>();

    private FullScreenMode selectedScreenMode = FullScreenMode.FullScreenWindow;
    private int selectedFpsLimitIndex = 2;
    private int selectedLanguageIndex;
    private TMP_FontAsset localizedFontAsset;
    private Sprite settingSliderHandleSprite;
    private TMP_InputField findRoomCodeInput;
    private Transform publicRoomListContent;
    private TMP_Text publicRoomEmptyText;
    private TMP_Text publicRoomStatusText;
    private ScrollRect settingsScrollRect;
    private readonly Dictionary<string, RoomInfo> publicRooms = new Dictionary<string, RoomInfo>();
    private string pendingKeyBindingPrefsKey;
    private TMP_Text pendingKeyBindingText;
    private float pendingKeyBindingStartTime;
    private readonly int[] fpsLimits = { 30, 60, 120, 144, -1 };
    private const string RoomCodePrefsKey = "dark_us_room_code";
    private const string RoomHostPrefsKey = "dark_us_room_is_host";
    private const string RoomVisiblePrefsKey = "dark_us_room_is_visible";
    private const string RoomTitlePrefsKey = "dark_us_room_title";

    private struct KeyBindingTextBinding
    {
        public TMP_Text Text;
        public string PrefsKey;
        public KeyCode DefaultKey;
    }

    [Header("Scene Names")]
    // 방 만들기를 눌렀을 때 이동할 씬 이름이다.
    public string createRoomSceneName = "CreateRoomLobbyScene";

    // 방 찾기를 눌렀을 때 이동할 씬 이름이다.
    public string findRoomSceneName = "PublicRoomListScene";

    [Header("Panels")]
    // 방 만들기 버튼을 눌렀을 때 띄울 창이다.
    public GameObject createRoomPanel;

    // 방 찾기 버튼을 눌렀을 때 띄울 창이다.
    public GameObject findRoomPanel;

    // 설정 버튼을 눌렀을 때 켜고 끌 설정 패널이다.
    public GameObject settingsPanel;

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

        if (!Application.isPlaying)
        {
            EnsureMainMenuLayout();
            return;
        }

        PrepareSettingsState();
        ApplyFixedLowGraphicsSettings();
        EnsureMainMenuBindings();
        EnsureQuitUi();
        EnsureMainMenuLayout();
        EnsureMenuPanels();

        if (createRoomPanel != null)
        {
            createRoomPanel.SetActive(false);
        }

        if (findRoomPanel != null)
        {
            findRoomPanel.SetActive(false);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

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

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        CapturePendingKeyBinding();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
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

        if (createRoomPanel == null)
        {
            Debug.LogWarning("Create room panel is not assigned.");
            return;
        }

        ShowPanel(createRoomPanel);
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

        if (settingsPanel == null)
        {
            Debug.LogWarning("Settings panel is not assigned.");
            return;
        }

        ShowPanel(settingsPanel);
    }

    public void OnClickCreateRoomConfirm()
    {
        PlayClickSound();
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

    public void OnClickCreatePublicRoom()
    {
        PlayClickSound();
        PlayerPrefs.SetString(RoomCodePrefsKey, Random.Range(0, 10000).ToString("0000"));
        PlayerPrefs.SetString(RoomTitlePrefsKey, "Public Room");
        PlayerPrefs.SetInt(RoomHostPrefsKey, 1);
        PlayerPrefs.SetInt(RoomVisiblePrefsKey, 1);
        PlayerPrefs.Save();
        LoadMenuScene(createRoomSceneName, "Create room scene name is empty.");
    }

    public override void OnConnectedToMaster()
    {
        if (findRoomPanel != null && findRoomPanel.activeInHierarchy)
        {
            SetPublicRoomStatus("JOINING LOBBY");
            PhotonNetwork.JoinLobby(TypedLobby.Default);
        }
    }

    public override void OnJoinedLobby()
    {
        SetPublicRoomStatus("PUBLIC ROOMS");
        RebuildPublicRoomList();
    }

    public override void OnLeftRoom()
    {
        if (findRoomPanel != null && findRoomPanel.activeInHierarchy)
        {
            StartPublicRoomListFlow();
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (findRoomPanel != null && findRoomPanel.activeInHierarchy)
        {
            SetPublicRoomStatus("DISCONNECTED");
        }
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList || !room.IsOpen || !room.IsVisible || !IsValidRoomCode(room.Name))
            {
                publicRooms.Remove(room.Name);
            }
            else
            {
                publicRooms[room.Name] = room;
            }
        }

        RebuildPublicRoomList();
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

        SceneManager.LoadScene(sceneName);
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
        if (createRoomPanel != null && createRoomPanel != panel)
        {
            createRoomPanel.SetActive(false);
        }

        if (findRoomPanel != null && findRoomPanel != panel)
        {
            findRoomPanel.SetActive(false);
        }

        if (settingsPanel != null && settingsPanel != panel)
        {
            settingsPanel.SetActive(false);
        }

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

        if (panel == settingsPanel)
        {
            ResetSettingsScroll();
        }
    }

    private void EnsureMainMenuBindings()
    {
        if (settingsPanel == null)
        {
            Transform settingsPanelTransform = FindUiTransform("SettingsPanel");
            if (settingsPanelTransform != null)
            {
                settingsPanel = settingsPanelTransform.gameObject;
            }
        }

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

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
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
        Canvas canvas = FindObjectOfType<Canvas>();
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

        MenuButtonHoverEffect hover = buttonTransform.GetComponent<MenuButtonHoverEffect>();
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

    private void EnsureMenuPanels()
    {
        if (createRoomPanel == null)
        {
            Transform existingPanel = FindUiTransform("CreateRoomPanel");
            createRoomPanel = existingPanel != null ? existingPanel.gameObject : CreateMenuPanel(
                "CreateRoomPanel",
                "Create Room",
                "Open a private operation room and wait for the crew.",
                "Create",
                OnClickCreateRoomConfirm
            );
        }

        PrepareExistingPanel(createRoomPanel, "Create Room", "Open a private operation room and wait for the crew.", "Create", OnClickCreateRoomConfirm);

        if (findRoomPanel == null)
        {
            Transform existingPanel = FindUiTransform("FindRoomPanel");
            findRoomPanel = existingPanel != null ? existingPanel.gameObject : CreatePublicRoomListPanel();
        }

        PreparePublicRoomListPanel(findRoomPanel);

        if (settingsPanel == null)
        {
            Transform existingPanel = FindUiTransform("SettingsPanel");
            settingsPanel = existingPanel != null ? existingPanel.gameObject : CreateSettingsPanel();
        }

        PrepareExistingSettingsPanel(settingsPanel);

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

    private GameObject CreateMenuPanel(string objectName, string title, string body, string primaryLabel, UnityEngine.Events.UnityAction primaryAction)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
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

    private GameObject CreatePublicRoomListPanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas is not found. Public room panel was not created.");
            return null;
        }

        GameObject panelObject = new GameObject("FindRoomPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.layer = canvas.gameObject.layer;
        panelObject.transform.SetParent(canvas.transform, false);
        PrepareOverlayRect(panelObject.GetComponent<RectTransform>());
        BuildPublicRoomListPanelContents(panelObject);
        panelObject.SetActive(false);
        return panelObject;
    }

    private void PreparePublicRoomListPanel(GameObject panelObject)
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

        if (panelObject.transform.Find("PublicRoomDialog") == null)
        {
            ClearChildren(panelObject.transform);
            BuildPublicRoomListPanelContents(panelObject);
        }
    }

    private void BuildPublicRoomListPanelContents(GameObject panelObject)
    {
        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.68f);

        GameObject dialogObject = new GameObject("PublicRoomDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        dialogObject.layer = panelObject.layer;
        dialogObject.transform.SetParent(panelObject.transform, false);

        RectTransform dialogRect = dialogObject.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = new Vector2(0f, 0f);
        dialogRect.sizeDelta = new Vector2(1120f, 720f);

        Image dialogImage = dialogObject.GetComponent<Image>();
        dialogImage.color = new Color(0.015f, 0.018f, 0.02f, 0.94f);

        Outline dialogOutline = dialogObject.GetComponent<Outline>();
        dialogOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.4f);
        dialogOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text titleText = CreateLabel(dialogObject.transform, "TitleText", "Public Game", 46f, FontStyles.Normal);
        RegisterLocalizedText(titleText, "Public Game");
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -64f);
        titleRect.sizeDelta = new Vector2(-96f, 68f);
        titleText.color = new Color(1f, 0.8f, 0.42f, 1f);

        publicRoomStatusText = CreateLabel(dialogObject.transform, "StatusText", "CONNECTING", 22f, FontStyles.UpperCase);
        RectTransform statusRect = publicRoomStatusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -116f);
        statusRect.sizeDelta = new Vector2(-96f, 34f);
        publicRoomStatusText.color = new Color(0.62f, 0.7f, 0.72f, 1f);

        GameObject listObject = new GameObject("RoomList", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
        listObject.layer = panelObject.layer;
        listObject.transform.SetParent(dialogObject.transform, false);

        RectTransform listRect = listObject.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 0f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.offsetMin = new Vector2(72f, 150f);
        listRect.offsetMax = new Vector2(-72f, -170f);

        Image listImage = listObject.GetComponent<Image>();
        listImage.color = new Color(0.035f, 0.045f, 0.048f, 0.64f);

        Outline listOutline = listObject.GetComponent<Outline>();
        listOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.28f);
        listOutline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup listLayout = listObject.GetComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(24, 24, 24, 24);
        listLayout.spacing = 12f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        publicRoomListContent = listObject.transform;

        publicRoomEmptyText = CreateLabel(listObject.transform, "EmptyText", "No public rooms found.", 28f, FontStyles.Normal);
        RectTransform emptyRect = publicRoomEmptyText.GetComponent<RectTransform>();
        emptyRect.sizeDelta = new Vector2(0f, 72f);
        publicRoomEmptyText.color = new Color(0.62f, 0.7f, 0.72f, 1f);

        Button createButton = CreateMenuButton(dialogObject.transform, "CreatePublicRoomButton", "Create Room", 260f, 64f, 26f);
        RectTransform createRect = createButton.GetComponent<RectTransform>();
        createRect.anchorMin = new Vector2(1f, 0f);
        createRect.anchorMax = new Vector2(1f, 0f);
        createRect.anchoredPosition = new Vector2(-212f, 72f);
        createButton.onClick.AddListener(OnClickCreatePublicRoom);

        Button closeButton = CreateMenuButton(dialogObject.transform, "CloseButton", "Close", 220f, 64f, 26f);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0f, 0f);
        closeRect.anchorMax = new Vector2(0f, 0f);
        closeRect.anchoredPosition = new Vector2(182f, 72f);
        closeButton.onClick.AddListener(() => OnClickClosePanel(panelObject));
    }

    private void PrepareExistingPanel(GameObject panelObject, string title, string body, string primaryLabel, UnityEngine.Events.UnityAction primaryAction)
    {
        if (panelObject == null || panelObject.transform.childCount > 0)
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

        BuildPanelContents(panelObject, title, body, primaryLabel, primaryAction);
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
            return;
        }

        for (int i = panelObject.transform.childCount - 1; i >= 0; i--)
        {
            DestroyUiObject(panelObject.transform.GetChild(i).gameObject);
        }

        BuildPanelContents(panelObject, "Join Friend", "Enter the 4-digit room code from the host.", "Join", OnClickFindRoomConfirm);
    }

    private void StartPublicRoomListFlow()
    {
        RebuildPublicRoomList();

        if (PhotonNetwork.InRoom)
        {
            SetPublicRoomStatus("LEAVING ROOM");
            PhotonNetwork.LeaveRoom();
            return;
        }

        if (PhotonNetwork.InLobby)
        {
            SetPublicRoomStatus("PUBLIC ROOMS");
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            SetPublicRoomStatus("JOINING LOBBY");
            PhotonNetwork.JoinLobby(TypedLobby.Default);
            return;
        }

        SetPublicRoomStatus("CONNECTING PHOTON");
        PhotonNetwork.ConnectUsingSettings();
    }

    private void RebuildPublicRoomList()
    {
        if (publicRoomListContent == null)
        {
            return;
        }

        for (int i = publicRoomListContent.childCount - 1; i >= 0; i--)
        {
            Transform child = publicRoomListContent.GetChild(i);
            if (publicRoomEmptyText != null && child == publicRoomEmptyText.transform)
            {
                continue;
            }

            DestroyUiObject(child.gameObject);
        }

        bool hasRoom = false;
        foreach (RoomInfo room in publicRooms.Values)
        {
            if (!room.IsOpen || !room.IsVisible || !IsValidRoomCode(room.Name) || room.PlayerCount >= room.MaxPlayers)
            {
                continue;
            }

            hasRoom = true;
            CreatePublicRoomRow(publicRoomListContent, room);
        }

        if (publicRoomEmptyText != null)
        {
            publicRoomEmptyText.gameObject.SetActive(!hasRoom);
        }
    }

    private void CreatePublicRoomRow(Transform parent, RoomInfo room)
    {
        Button rowButton = CreateMenuButton(parent, "Room_" + room.Name, "ROOM " + room.Name + "        " + room.PlayerCount + " / " + room.MaxPlayers, 960f, 66f, 24f);
        LayoutElement layout = rowButton.GetComponent<LayoutElement>();
        layout.preferredWidth = 960f;
        layout.preferredHeight = 66f;

        TMP_Text label = rowButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Left;
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.offsetMin = new Vector2(32f, 0f);
            labelRect.offsetMax = new Vector2(-32f, 0f);
        }

        string roomName = room.Name;
        rowButton.onClick.AddListener(() => JoinPublicRoom(roomName));
    }

    private void JoinPublicRoom(string roomName)
    {
        PlayClickSound();
        PlayerPrefs.SetString(RoomCodePrefsKey, roomName);
        PlayerPrefs.SetString(RoomTitlePrefsKey, "Public Room");
        PlayerPrefs.SetInt(RoomHostPrefsKey, 0);
        PlayerPrefs.SetInt(RoomVisiblePrefsKey, 1);
        PlayerPrefs.Save();
        LoadMenuScene(createRoomSceneName, "Create room scene name is empty.");
    }

    private void SetPublicRoomStatus(string status)
    {
        if (publicRoomStatusText != null)
        {
            publicRoomStatusText.text = status;
        }
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

    private GameObject CreateSettingsPanel()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("Canvas is not found. Settings panel was not created.");
            return null;
        }

        GameObject panelObject = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.layer = canvas.gameObject.layer;
        panelObject.transform.SetParent(canvas.transform, false);
        PrepareOverlayRect(panelObject.GetComponent<RectTransform>());
        BuildSettingsPanelContents(panelObject);
        panelObject.SetActive(false);
        return panelObject;
    }

    private void PrepareExistingSettingsPanel(GameObject panelObject)
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

        ClearChildren(panelObject.transform);
        BuildSettingsPanelContents(panelObject);
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
        dialogRect.sizeDelta = new Vector2(680f, 360f);

        Image dialogImage = dialogObject.GetComponent<Image>();
        dialogImage.color = new Color(0.015f, 0.018f, 0.02f, 0.94f);

        Outline dialogOutline = dialogObject.GetComponent<Outline>();
        dialogOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.4f);
        dialogOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text titleText = CreateLabel(dialogObject.transform, "TitleText", title, 46f, FontStyles.UpperCase);
        RegisterLocalizedText(titleText, title);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -78f);
        titleRect.sizeDelta = new Vector2(-80f, 74f);
        titleText.color = new Color(1f, 0.8f, 0.42f, 1f);

        TMP_Text bodyText = CreateLabel(dialogObject.transform, "BodyText", body, 28f, FontStyles.Normal);
        RegisterLocalizedText(bodyText, body);
        RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0.5f);
        bodyRect.anchorMax = new Vector2(1f, 0.5f);
        bodyRect.anchoredPosition = new Vector2(0f, 18f);
        bodyRect.sizeDelta = new Vector2(-120f, 110f);
        bodyText.enableWordWrapping = true;
        bodyText.color = new Color(0.76f, 0.82f, 0.84f, 1f);

        if (title == "Find Room" || title == "Join Friend")
        {
            bodyRect.anchoredPosition = new Vector2(0f, 54f);
            bodyRect.sizeDelta = new Vector2(-120f, 86f);
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

    private void BuildSettingsPanelContents(GameObject panelObject)
    {
        PrepareSettingsState();
        dynamicSettingLabels.Clear();
        settingsSliders.Clear();
        keyBindingTexts.Clear();

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject dialogObject = new GameObject("SettingsDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        dialogObject.layer = panelObject.layer;
        dialogObject.transform.SetParent(panelObject.transform, false);

        RectTransform dialogRect = dialogObject.GetComponent<RectTransform>();
        dialogRect.anchorMin = Vector2.zero;
        dialogRect.anchorMax = Vector2.one;
        dialogRect.offsetMin = new Vector2(24f, 24f);
        dialogRect.offsetMax = new Vector2(-24f, -24f);

        Image dialogImage = dialogObject.GetComponent<Image>();
        dialogImage.color = new Color(0.012f, 0.015f, 0.017f, 0.96f);

        Outline outline = dialogObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.42f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text titleText = CreateLabel(dialogObject.transform, "TitleText", "SETTINGS", 48f, FontStyles.UpperCase);
        RegisterLocalizedText(titleText, "SETTINGS");
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -50f);
        titleRect.sizeDelta = new Vector2(-60f, 60f);
        titleText.color = new Color(1f, 0.8f, 0.42f, 1f);

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
        viewportObject.layer = panelObject.layer;
        viewportObject.transform.SetParent(dialogObject.transform, false);

        GameObject tabBarObject = new GameObject("SettingsTabBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabBarObject.layer = panelObject.layer;
        tabBarObject.transform.SetParent(dialogObject.transform, false);

        RectTransform tabBarRect = tabBarObject.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0f, 1f);
        tabBarRect.anchorMax = new Vector2(1f, 1f);
        tabBarRect.anchoredPosition = new Vector2(0f, -122f);
        tabBarRect.sizeDelta = new Vector2(-96f, 58f);

        HorizontalLayoutGroup tabBarLayout = tabBarObject.GetComponent<HorizontalLayoutGroup>();
        tabBarLayout.spacing = 12f;
        tabBarLayout.childAlignment = TextAnchor.MiddleCenter;
        tabBarLayout.childControlWidth = true;
        tabBarLayout.childControlHeight = true;
        tabBarLayout.childForceExpandWidth = true;
        tabBarLayout.childForceExpandHeight = true;

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);
        viewportRect.offsetMin = new Vector2(48f, 124f);
        viewportRect.offsetMax = new Vector2(-82f, -178f);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.08f);
        viewportImage.raycastTarget = true;

        Mask viewportMask = viewportObject.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.layer = panelObject.layer;
        contentObject.transform.SetParent(viewportObject.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 18f;
        contentLayout.padding = new RectOffset(6, 6, 4, 10);
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        settingsScrollRect = viewportObject.AddComponent<ScrollRect>();
        settingsScrollRect.viewport = viewportRect;
        settingsScrollRect.content = contentRect;
        settingsScrollRect.horizontal = false;
        settingsScrollRect.vertical = true;
        settingsScrollRect.movementType = ScrollRect.MovementType.Clamped;
        settingsScrollRect.inertia = true;
        settingsScrollRect.scrollSensitivity = 48f;
        settingsScrollRect.verticalScrollbar = CreateSettingsScrollbar(dialogObject.transform);
        settingsScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        settingsScrollRect.verticalScrollbarSpacing = 10f;

        Transform[] settingsPages =
        {
            BuildDisplaySettings(contentObject.transform),
            BuildAudioSettings(contentObject.transform),
            BuildControlSettings(contentObject.transform),
            BuildGameplaySettings(contentObject.transform)
        };

        Button[] tabButtons =
        {
            CreateSettingsTabButton(tabBarObject.transform, "Display"),
            CreateSettingsTabButton(tabBarObject.transform, "Audio"),
            CreateSettingsTabButton(tabBarObject.transform, "Controls Keybindings"),
            CreateSettingsTabButton(tabBarObject.transform, "Gameplay")
        };

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tabIndex = i;
            tabButtons[i].onClick.AddListener(() => ShowSettingsPage(settingsPages, tabButtons, tabIndex));
        }

        ShowSettingsPage(settingsPages, tabButtons, 0);

        Button resetButton = CreateMenuButton(dialogObject.transform, "ResetButton", "Reset", 220f, 60f, 26f);
        RectTransform resetRect = resetButton.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0.5f, 0f);
        resetRect.anchorMax = new Vector2(0.5f, 0f);
        resetRect.anchoredPosition = new Vector2(-244f, 42f);
        resetButton.onClick.AddListener(ResetSettingsToDefault);

        Button applyButton = CreateMenuButton(dialogObject.transform, "ApplyButton", "Apply", 220f, 60f, 26f);
        RectTransform applyRect = applyButton.GetComponent<RectTransform>();
        applyRect.anchorMin = new Vector2(0.5f, 0f);
        applyRect.anchorMax = new Vector2(0.5f, 0f);
        applyRect.anchoredPosition = new Vector2(0f, 42f);
        applyButton.onClick.AddListener(ApplySettings);

        Button closeButton = CreateMenuButton(dialogObject.transform, "CloseButton", "Close", 220f, 60f, 26f);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(244f, 42f);
        closeButton.onClick.AddListener(() => OnClickClosePanel(panelObject));

        RefreshDynamicSettingLabels();
        ApplyLanguage();
    }

    private Transform BuildDisplaySettings(Transform parent)
    {
        Transform section = CreateSettingsSection(parent, "Display", 340f);
        CreateCycleRow(section, "Screen Mode", () => GetScreenModeLabel(), CycleScreenMode);
        CreateCycleRow(section, "FPS Limit", () => GetFpsLimitLabel(), CycleFpsLimit);
        CreateCycleRow(section, "Language", () => GetLanguageLabel(), CycleLanguage);
        return section;
    }

    private Transform BuildAudioSettings(Transform parent)
    {
        Transform section = CreateSettingsSection(parent, "Audio", 240f);
        CreateSliderRow(section, "Master Volume", 0f, 1f, AudioListener.volume, false, "setting_master_volume");
        CreateSliderRow(section, "Voice Volume", 0f, 1f, PlayerPrefs.GetFloat("setting_voice_volume", 1f), false, "setting_voice_volume");
        return section;
    }

    private Transform BuildControlSettings(Transform parent)
    {
        Transform section = CreateSettingsSection(parent, "Controls & Keybindings", 1360f);
        CreateSliderRow(section, "Mouse Sensitivity X", 0.1f, 5f, PlayerPrefs.GetFloat("setting_mouse_x", 1f), false, "setting_mouse_x");
        CreateSliderRow(section, "Mouse Sensitivity Y", 0.1f, 5f, PlayerPrefs.GetFloat("setting_mouse_y", 1f), false, "setting_mouse_y");
        CreateKeyBindRow(section, "Move Forward", GameInputBindings.MoveForwardKey, KeyCode.W);
        CreateKeyBindRow(section, "Move Back", GameInputBindings.MoveBackwardKey, KeyCode.S);
        CreateKeyBindRow(section, "Move Left", GameInputBindings.MoveLeftKey, KeyCode.A);
        CreateKeyBindRow(section, "Move Right", GameInputBindings.MoveRightKey, KeyCode.D);
        CreateKeyBindRow(section, "Sprint", GameInputBindings.SprintKey, KeyCode.LeftShift);
        CreateKeyBindRow(section, "Crouch", GameInputBindings.CrouchKey, KeyCode.LeftControl);
        CreateKeyBindRow(section, "Interact", GameInputBindings.InteractKey, KeyCode.E);
        CreateKeyBindRow(section, "Pick Up", GameInputBindings.PickupKey, KeyCode.F);
        CreateKeyBindRow(section, "Scan", GameInputBindings.ScanKey, KeyCode.Mouse1);
        CreateKeyBindRow(section, "Use Item", GameInputBindings.UseItemKey, KeyCode.Mouse0);
        CreateKeyBindRow(section, "Drop Item", GameInputBindings.DropItemKey, KeyCode.G);
        CreateKeyBindRow(section, "Slot 1", GameInputBindings.Slot1Key, KeyCode.Alpha1);
        CreateKeyBindRow(section, "Slot 2", GameInputBindings.Slot2Key, KeyCode.Alpha2);
        CreateKeyBindRow(section, "Mic Mute", GameInputBindings.MicMuteKey, KeyCode.B);
        CreateKeyBindRow(section, "Kill", GameInputBindings.KillKey, KeyCode.Q);
        CreateKeyBindRow(section, "Pause", GameInputBindings.PauseKey, KeyCode.Escape);
        return section;
    }

    private Transform BuildGameplaySettings(Transform parent)
    {
        Transform section = CreateSettingsSection(parent, "Gameplay", 160f);
        CreateSliderRow(section, "HUD Opacity", 0.45f, 1f, PlayerPrefs.GetFloat("setting_hud_opacity", 1f), false, "setting_hud_opacity");
        return section;
    }

    private Button CreateSettingsTabButton(Transform parent, string label)
    {
        Button button = CreateMenuButton(parent, label + "TabButton", label, 220f, 52f, 18f);
        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        layoutElement.minWidth = 160f;
        layoutElement.preferredWidth = 220f;
        layoutElement.preferredHeight = 52f;
        return button;
    }

    private Scrollbar CreateSettingsScrollbar(Transform parent)
    {
        GameObject scrollbarObject = new GameObject("SettingsScrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar), typeof(Outline));
        scrollbarObject.layer = parent.gameObject.layer;
        scrollbarObject.transform.SetParent(parent, false);

        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-62f, 124f);
        scrollbarRect.offsetMax = new Vector2(-46f, -178f);

        Image background = scrollbarObject.GetComponent<Image>();
        background.color = new Color(0.015f, 0.018f, 0.02f, 0.72f);

        Outline outline = scrollbarObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.24f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject slidingAreaObject = new GameObject("Sliding Area", typeof(RectTransform));
        slidingAreaObject.layer = parent.gameObject.layer;
        slidingAreaObject.transform.SetParent(scrollbarObject.transform, false);

        RectTransform slidingAreaRect = slidingAreaObject.GetComponent<RectTransform>();
        slidingAreaRect.anchorMin = Vector2.zero;
        slidingAreaRect.anchorMax = Vector2.one;
        slidingAreaRect.offsetMin = new Vector2(2f, 2f);
        slidingAreaRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleObject.layer = parent.gameObject.layer;
        handleObject.transform.SetParent(slidingAreaObject.transform, false);

        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = new Color(1f, 0.8f, 0.42f, 0.86f);

        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;

        Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handleRect;
        scrollbar.size = 0.18f;
        scrollbar.value = 1f;
        return scrollbar;
    }

    private void ShowSettingsPage(Transform[] pages, Button[] tabButtons, int selectedIndex)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
            {
                pages[i].gameObject.SetActive(i == selectedIndex);
            }

            if (i >= tabButtons.Length || tabButtons[i] == null)
            {
                continue;
            }

            bool selected = i == selectedIndex;
            Image image = tabButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = selected ? new Color(0.16f, 0.18f, 0.17f, 0.88f) : new Color(0.015f, 0.018f, 0.02f, 0.52f);
            }

            TMP_Text text = tabButtons[i].GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.color = selected ? new Color(1f, 0.8f, 0.42f, 1f) : new Color(0.76f, 0.82f, 0.84f, 1f);
            }
        }

        if (settingsScrollRect != null)
        {
            ResetSettingsScroll();
        }
    }

    private void ResetSettingsScroll()
    {
        if (settingsScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        settingsScrollRect.StopMovement();
        settingsScrollRect.verticalNormalizedPosition = 1f;

        if (settingsScrollRect.content != null)
        {
            Vector2 position = settingsScrollRect.content.anchoredPosition;
            position.y = 0f;
            settingsScrollRect.content.anchoredPosition = position;
        }
    }

    private Transform CreateSettingsSection(Transform parent, string title, float preferredHeight)
    {
        GameObject sectionObject = new GameObject(title + "Section", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        sectionObject.layer = parent.gameObject.layer;
        sectionObject.transform.SetParent(parent, false);

        Image image = sectionObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);

        Outline outline = sectionObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layout = sectionObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LayoutElement layoutElement = sectionObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;

        TMP_Text titleText = CreateLabel(sectionObject.transform, title + "Title", title, 28f, FontStyles.UpperCase);
        RegisterLocalizedText(titleText, title);
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.color = new Color(1f, 0.8f, 0.42f, 1f);
        titleText.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);

        return sectionObject.transform;
    }

    private void CreateCycleRow(Transform parent, string label, System.Func<string> valueGetter, UnityEngine.Events.UnityAction cycleAction)
    {
        Transform row = CreateSettingsRow(parent, label);
        TMP_Text valueText = CreateValueText(row, valueGetter());
        dynamicSettingLabels.Add(valueText);

        Button button = CreateMenuButton(row, "ChangeButton", "Change", 150f, 44f, 20f);
        button.onClick.AddListener(cycleAction);
        button.onClick.AddListener(RefreshDynamicSettingLabels);
    }

    private void CreateKeyBindRow(Transform parent, string label, string prefsKey, KeyCode defaultKey)
    {
        Transform row = CreateSettingsRow(parent, label);
        TMP_Text valueText = CreateValueText(row, GameInputBindings.GetLabel(prefsKey, defaultKey));
        keyBindingTexts.Add(new KeyBindingTextBinding
        {
            Text = valueText,
            PrefsKey = prefsKey,
            DefaultKey = defaultKey
        });

        Button button = CreateKeyBindButton(row, "BindButton", "Bind", 132f, 44f, 18f);
        button.onClick.AddListener(() => StartKeyBindingCapture(prefsKey, defaultKey, valueText));
    }

    private Button CreateKeyBindButton(Transform parent, string objectName, string label, float preferredWidth, float preferredHeight, float fontSize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(MenuButtonHoverEffect));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(preferredWidth, preferredHeight);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.015f, 0.018f, 0.02f, 0.62f);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.preferredHeight = preferredHeight;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.34f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text labelText = CreateLabel(buttonObject.transform, "Text (TMP)", label, fontSize, FontStyles.UpperCase);

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

    private void StartKeyBindingCapture(string prefsKey, KeyCode defaultKey, TMP_Text valueText)
    {
        pendingKeyBindingPrefsKey = prefsKey;
        pendingKeyBindingText = valueText;
        pendingKeyBindingStartTime = Time.unscaledTime;

        if (pendingKeyBindingText != null)
        {
            pendingKeyBindingText.text = Translate("Press a key");
            pendingKeyBindingText.color = new Color(1f, 0.8f, 0.42f, 1f);
        }
    }

    private void CapturePendingKeyBinding()
    {
        if (string.IsNullOrEmpty(pendingKeyBindingPrefsKey))
        {
            return;
        }

        if (Time.unscaledTime - pendingKeyBindingStartTime < 0.12f)
        {
            return;
        }

        if (!GameInputBindings.TryGetPressedBindableKey(out KeyCode key))
        {
            return;
        }

        GameInputBindings.SetKey(pendingKeyBindingPrefsKey, key);
        pendingKeyBindingPrefsKey = null;
        pendingKeyBindingText = null;
        RefreshKeyBindingTexts();
    }

    private void RefreshKeyBindingTexts()
    {
        for (int i = keyBindingTexts.Count - 1; i >= 0; i--)
        {
            KeyBindingTextBinding binding = keyBindingTexts[i];
            TMP_Text text = binding.Text;

            if (text == null)
            {
                keyBindingTexts.RemoveAt(i);
                continue;
            }

            ApplyFontForLanguage(text);
            text.text = GameInputBindings.GetLabel(binding.PrefsKey, binding.DefaultKey);
            text.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        }
    }

    private void CreateSliderRow(Transform parent, string label, float min, float max, float value, bool wholeNumbers, string playerPrefsKey)
    {
        Transform row = CreateSettingsRow(parent, label);

        Slider slider = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider), typeof(Image)).GetComponent<Slider>();
        slider.gameObject.layer = parent.gameObject.layer;
        slider.transform.SetParent(row, false);
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = PlayerPrefs.GetFloat(playerPrefsKey, value);
        slider.wholeNumbers = wholeNumbers;
        slider.targetGraphic = slider.GetComponent<Image>();
        slider.GetComponent<Image>().color = new Color(0.62f, 0.78f, 0.86f, 0.28f);
        slider.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 12f);

        Image fillImage = CreateSettingGraphic(slider.transform, "Fill", new Color(1f, 0.8f, 0.42f, 0.82f), new Vector2(0f, 0f), new Vector2(1f, 1f));
        Image handleImage = CreateSettingGraphic(slider.transform, "Handle", new Color(0.76f, 0.82f, 0.84f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        handleImage.sprite = GetSettingSliderHandleSprite();
        handleImage.type = Image.Type.Simple;
        handleImage.preserveAspect = true;
        handleImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 20f);
        handleImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 20f);
        slider.fillRect = fillImage.rectTransform;
        slider.handleRect = handleImage.rectTransform;
        slider.direction = Slider.Direction.LeftToRight;

        slider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(playerPrefsKey, v);
        });
        settingsSliders.Add(slider);

        TMP_Text valueText = CreateValueText(row, FormatSettingValue(slider.value, wholeNumbers));
        slider.onValueChanged.AddListener(v => valueText.text = FormatSettingValue(v, wholeNumbers));
    }

    private Image CreateSettingGraphic(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject graphicObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        graphicObject.layer = parent.gameObject.layer;
        graphicObject.transform.SetParent(parent, false);

        Image image = graphicObject.GetComponent<Image>();
        image.color = color;

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        return image;
    }

    private Sprite GetSettingSliderHandleSprite()
    {
        if (settingSliderHandleSprite != null)
        {
            return settingSliderHandleSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Setting Slider Round Handle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        float softEdge = 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / softEdge);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        settingSliderHandleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return settingSliderHandleSprite;
    }

    private Transform CreateSettingsRow(Transform parent, string label)
    {
        GameObject rowObject = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.layer = parent.gameObject.layer;
        rowObject.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 54f;

        TMP_Text labelText = CreateLabel(rowObject.transform, label + "Label", label, 22f, FontStyles.Normal);
        RegisterLocalizedText(labelText, label);
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 40f);

        return rowObject.transform;
    }

    private TMP_Text CreateValueText(Transform parent, string value)
    {
        TMP_Text valueText = CreateLabel(parent, "ValueText", value, 22f, FontStyles.UpperCase);
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        valueText.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 36f);
        return valueText;
    }

    private void PrepareSettingsState()
    {
        selectedFpsLimitIndex = Mathf.Clamp(PlayerPrefs.GetInt("setting_fps_limit", selectedFpsLimitIndex), 0, fpsLimits.Length - 1);
        selectedLanguageIndex = Mathf.Clamp(PlayerPrefs.GetInt("setting_language", 0), 0, 2);

        int screenMode = PlayerPrefs.GetInt("setting_screen_mode", (int)Screen.fullScreenMode);
        selectedScreenMode = (FullScreenMode)screenMode;
    }

    private void CycleScreenMode()
    {
        if (selectedScreenMode == FullScreenMode.ExclusiveFullScreen)
        {
            selectedScreenMode = FullScreenMode.Windowed;
        }
        else if (selectedScreenMode == FullScreenMode.Windowed)
        {
            selectedScreenMode = FullScreenMode.FullScreenWindow;
        }
        else
        {
            selectedScreenMode = FullScreenMode.ExclusiveFullScreen;
        }

        PlayerPrefs.SetInt("setting_screen_mode", (int)selectedScreenMode);
    }

    private void CycleFpsLimit()
    {
        selectedFpsLimitIndex = (selectedFpsLimitIndex + 1) % fpsLimits.Length;
        PlayerPrefs.SetInt("setting_fps_limit", selectedFpsLimitIndex);
    }

    private void CycleLanguage()
    {
        selectedLanguageIndex = (selectedLanguageIndex + 1) % 3;
        PlayerPrefs.SetInt("setting_language", selectedLanguageIndex);
    }

    private void RefreshDynamicSettingLabels()
    {
        int index = 0;

        if (dynamicSettingLabels.Count > index)
        {
            SetDynamicSettingLabel(index++, GetScreenModeLabel());
        }

        if (dynamicSettingLabels.Count > index)
        {
            SetDynamicSettingLabel(index++, GetFpsLimitLabel());
        }

        if (dynamicSettingLabels.Count > index)
        {
            SetDynamicSettingLabel(index, GetLanguageLabel());
        }
    }

    private void SetDynamicSettingLabel(int index, string text)
    {
        TMP_Text label = dynamicSettingLabels[index];
        ApplyFontForLanguage(label);
        label.text = text;
    }

    private string GetScreenModeLabel()
    {
        switch (selectedScreenMode)
        {
            case FullScreenMode.ExclusiveFullScreen:
                return Translate("FULLSCREEN");
            case FullScreenMode.Windowed:
                return Translate("WINDOWED");
            default:
                return Translate("BORDERLESS");
        }
    }

    private string GetFpsLimitLabel()
    {
        int fpsLimit = fpsLimits[selectedFpsLimitIndex];
        return fpsLimit < 0 ? Translate("UNLIMITED") : fpsLimit + " FPS";
    }

    private string GetLanguageLabel()
    {
        switch (selectedLanguageIndex)
        {
            case 1:
                return Translate("ENGLISH");
            case 2:
                return Translate("JAPANESE");
            default:
                return Translate("KOREAN");
        }
    }

    private string FormatSettingValue(float value, bool wholeNumbers)
    {
        return wholeNumbers ? Mathf.RoundToInt(value).ToString() : value.ToString("0.00");
    }

    private void ApplySettings()
    {
        Resolution resolution = Screen.currentResolution;
        Screen.SetResolution(resolution.width, resolution.height, selectedScreenMode, resolution.refreshRate);
        ApplyFixedLowGraphicsSettings();

        int fpsLimit = fpsLimits[selectedFpsLimitIndex];
        Application.targetFrameRate = fpsLimit;
        AudioListener.volume = PlayerPrefs.GetFloat("setting_master_volume", 1f);
        PlayerVoiceChat.ApplySavedVoiceVolumeToAll();

        PlayerPrefs.Save();
        RefreshDynamicSettingLabels();
        ApplyLanguage();
    }

    private void ApplyFixedLowGraphicsSettings()
    {
        QualitySettings.globalTextureMipmapLimit = 3;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 0;
    }

    private void ResetSettingsToDefault()
    {
        PlayerPrefs.DeleteKey("setting_screen_mode");
        PlayerPrefs.DeleteKey("setting_resolution");
        PlayerPrefs.DeleteKey("setting_quality");
        PlayerPrefs.DeleteKey("setting_texture");
        PlayerPrefs.DeleteKey("setting_shadow");
        PlayerPrefs.DeleteKey("setting_aa");
        PlayerPrefs.DeleteKey("setting_vsync");
        PlayerPrefs.DeleteKey("setting_fps_limit");
        PlayerPrefs.DeleteKey("setting_motion_blur");
        PlayerPrefs.DeleteKey("setting_camera_shake");
        PlayerPrefs.DeleteKey("setting_master_volume");
        PlayerPrefs.DeleteKey("setting_bgm_volume");
        PlayerPrefs.DeleteKey("setting_sfx_volume");
        PlayerPrefs.DeleteKey("setting_voice_volume");
        PlayerPrefs.DeleteKey("setting_mouse_x");
        PlayerPrefs.DeleteKey("setting_mouse_y");
        PlayerPrefs.DeleteKey("setting_invert_y");
        PlayerPrefs.DeleteKey("setting_gamepad_vibration");
        PlayerPrefs.DeleteKey("setting_hud_opacity");
        PlayerPrefs.DeleteKey("setting_scan_density");
        PlayerPrefs.DeleteKey("setting_scan_dot_size");
        PlayerPrefs.DeleteKey("setting_language");
        PlayerPrefs.DeleteKey("setting_color_blind");
        PlayerPrefs.DeleteKey("setting_tutorial");
        GameInputBindings.ResetAll();
        PlayerPrefs.Save();

        PrepareSettingsState();
        RefreshDynamicSettingLabels();
        RefreshKeyBindingTexts();
        ApplySettings();
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

        RefreshDynamicSettingLabels();
        RefreshKeyBindingTexts();
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

    private TMP_FontAsset GetLocalizedFontAsset()
    {
        if (localizedFontAsset != null)
        {
            return localizedFontAsset;
        }

        string[] fontPaths =
        {
            "C:/Windows/Fonts/malgun.ttf",
            "C:/Windows/Fonts/malgunbd.ttf",
            "C:/Windows/Fonts/NotoSansCJK-Regular.ttc"
        };

        foreach (string fontPath in fontPaths)
        {
            if (!System.IO.File.Exists(fontPath))
            {
                continue;
            }

            localizedFontAsset = TMP_FontAsset.CreateFontAsset(fontPath, 0, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048);
            if (localizedFontAsset == null)
            {
                continue;
            }

            localizedFontAsset.name = "Runtime Korean Japanese TMP Font";
            localizedFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            localizedFontAsset.TryAddCharacters(GetLocalizedCharacterSet());
            return localizedFontAsset;
        }

        Debug.LogWarning("Korean/Japanese TMP font was not found. Install Malgun Gothic or add a TMP font fallback asset.");
        return null;
    }

    private string GetLocalizedCharacterSet()
    {
        return "가나다라마바사아자차카타파하"
            + "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /&-.?"
            + "방만들기찾설정나가기닫적용초기화변경확인취소게임을종료할까요"
            + "비공개친구참가"
            + "신호터미널명령시스템상태네트워크대기음성링크준비대원인증필요"
            + "그래픽및화면표시오디오조작키플레이접근성모드텍스처품질그림자수직동기제한"
            + "모션블러카메라흔마스터볼륨배경음효과음음성이동상호작용눌러서말하기마우스감도반전패드진동HUD투명도스캔밀도점크기언어색약튜토리얼전체창테두리없음한국어영어일본어"
            + "ルーム作成検索設定終了閉じる適用リセット変更確認キャンセルゲームを終了しますか"
            + "公開フレンド参加"
            + "信号端末コマンドシステム状態ネットワーク待機ボイスリンク準備クルー認証必要"
            + "プライベート作戦待信号リスト参加探グラフィック表示画面オーディオ操作キーゲームプレイアクセシビリティ"
            + "モードテクスチャ品質影垂直同期制限ブラー揺れ音量移動インタラクトプッシュトゥトーク"
            + "マウス感度反転パッド振動HUD透明度スキャン密度点サイズ言語色覚サポートチュートリアルフルスクリーンウィンドウボーダーレス無韓国語英日本";
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
        Canvas canvas = FindObjectOfType<Canvas>();
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
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform targetTransform in transforms)
        {
            if (targetTransform.name == objectName && targetTransform.gameObject.scene.IsValid())
            {
                return targetTransform;
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
