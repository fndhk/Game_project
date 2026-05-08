using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGamePauseMenu : MonoBehaviour
{
    public string mainMenuSceneName = "LobbyScene 1";
    public string roomLobbySceneName = "CreateRoomLobbyScene";
    public KeyCode toggleKey = KeyCode.Escape;

    private static InGamePauseMenu instance;

    private Canvas canvas;
    private GameObject root;
    private GameObject settingsRoot;
    private GameObject confirmDialog;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text roomCodeText;
    private readonly Dictionary<Behaviour, bool> lockedBehaviours = new Dictionary<Behaviour, bool>();
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool isOpen;
    private System.Action pendingConfirmAction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForLoadedScene()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreateForActiveScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForActiveScene();
    }

    private static void TryCreateForActiveScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "LobbyScene" ||
            sceneName == "LobbyScene 1" ||
            sceneName == "CreateRoomLobbyScene" ||
            sceneName == "PublicRoomListScene")
        {
            if (instance != null)
            {
                Destroy(instance.gameObject);
                instance = null;
            }

            return;
        }

        if (instance != null)
        {
            return;
        }

        GameObject menuObject = new GameObject("InGamePauseMenu");
        instance = menuObject.AddComponent<InGamePauseMenu>();
        DontDestroyOnLoad(menuObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureEventSystem();
        BuildUi();
        SetOpen(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
        }

        if (isOpen)
        {
            RefreshPlayersPanel();
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;

        if (root != null)
        {
            root.SetActive(open);
        }

        if (open)
        {
            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            LockPlayerInput();
            ShowHomePanel();
        }
        else
        {
            CloseConfirmDialog();
            RestorePlayerInput();
            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
        }
    }

    private void LockPlayerInput()
    {
        lockedBehaviours.Clear();
        AddLockTargets(FindObjectsOfType<MouseLook>(true));
        AddLockTargets(FindObjectsOfType<PlayerMotor>(true));
        AddLockTargets(FindObjectsOfType<PlayerObjectiveInteractor>(true));
        AddLockTargets(FindObjectsOfType<PlayerInventory>(true));
        AddLockTargets(FindObjectsOfType<PlayerItemUser>(true));
        AddLockTargets(FindObjectsOfType<LidarSpotScanner>(true));
    }

    private void AddLockTargets<T>(T[] targets) where T : Behaviour
    {
        for (int i = 0; i < targets.Length; i++)
        {
            T target = targets[i];
            if (target == null || ReferenceEquals(target, this))
            {
                continue;
            }

            lockedBehaviours[target] = target.enabled;
            target.enabled = false;
        }
    }

    private void RestorePlayerInput()
    {
        foreach (KeyValuePair<Behaviour, bool> pair in lockedBehaviours)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        lockedBehaviours.Clear();
    }

    private void ShowHomePanel()
    {
        SetSettingsVisible(false);
        titleText.text = "PAUSED";
        roomCodeText.text = GetRoomCodeLine();
        SetBody(
            "SESSION\n" +
            GetRoomCodeLine() + "\n\n" +
            "STATUS\n" +
            GetNetworkStatusLine() + "\n\n" +
            "ESC closes this menu."
        );
    }

    private void ShowSettingsPanel()
    {
        SetSettingsVisible(true);
        titleText.text = "SETTINGS";
        roomCodeText.text = "IN-GAME SETTINGS";
    }

    private void ShowControlsPanel()
    {
        SetSettingsVisible(false);
        titleText.text = "CONTROLS";
        SetBody(
            "WASD        MOVE\n" +
            "MOUSE       LOOK\n" +
            "SHIFT       SPRINT\n" +
            "CTRL        CROUCH\n" +
            "E           INTERACT\n" +
            "F           PICK UP\n" +
            "1 / 2       SELECT ITEM\n" +
            "LMB         USE ITEM\n" +
            "G           DROP ITEM\n" +
            "V           VOICE\n" +
            "ESC         PAUSE"
        );
    }

    private void ShowPlayersPanel()
    {
        SetSettingsVisible(false);
        titleText.text = "PLAYERS";
        RefreshPlayersPanel();
    }

    private void RefreshPlayersPanel()
    {
        if (titleText == null || titleText.text != "PLAYERS")
        {
            return;
        }

        if (!PhotonNetwork.InRoom)
        {
            SetBody("Not connected to a Photon room.");
            return;
        }

        string body = "CREW\n";
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            string role = player.IsMasterClient ? "HOST" : "PLAYER";
            string local = player.IsLocal ? "YOU" : "REMOTE";
            body += "> " + local + "    " + role + "    ID " + player.ActorNumber + "\n";
        }

        SetBody(body);
    }

    private void ConfirmReturnToLobby()
    {
        ShowConfirm("RETURN TO LOBBY", "Return everyone to the room lobby?", ReturnToLobby);
    }

    private void ConfirmQuitToMainMenu()
    {
        ShowConfirm("QUIT TO MAIN MENU", "Leave the current room and return to main menu?", QuitToMainMenu);
    }

    private void ConfirmQuitGame()
    {
        ShowConfirm("QUIT GAME", "Close the game?", QuitGame);
    }

    private void ReturnToLobby()
    {
        SetOpen(false);

        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(roomLobbySceneName);
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        SceneManager.LoadScene(roomLobbySceneName);
    }

    private void QuitToMainMenu()
    {
        SetOpen(false);

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void QuitGame()
    {
        if (PhotonNetwork.InRoom || PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private string GetRoomCodeLine()
    {
        if (PhotonNetwork.InRoom)
        {
            return "ROOM CODE " + PhotonNetwork.CurrentRoom.Name;
        }

        return "ROOM CODE ----";
    }

    private string GetNetworkStatusLine()
    {
        if (PhotonNetwork.InRoom)
        {
            return "CONNECTED  " + PhotonNetwork.CurrentRoom.PlayerCount + " / " + PhotonNetwork.CurrentRoom.MaxPlayers;
        }

        return PhotonNetwork.NetworkClientState.ToString().ToUpperInvariant();
    }

    private void SetBody(string body)
    {
        if (bodyText != null)
        {
            bodyText.text = body;
        }
    }

    private void SetSettingsVisible(bool visible)
    {
        if (settingsRoot != null)
        {
            settingsRoot.SetActive(visible);
        }

        if (bodyText != null)
        {
            bodyText.gameObject.SetActive(!visible);
        }
    }

    private void ShowConfirm(string title, string message, System.Action confirmAction)
    {
        pendingConfirmAction = confirmAction;
        confirmDialog.SetActive(true);
        TMP_Text[] texts = confirmDialog.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length >= 2)
        {
            texts[0].text = title;
            texts[1].text = message;
        }
    }

    private void CloseConfirmDialog()
    {
        pendingConfirmAction = null;
        if (confirmDialog != null)
        {
            confirmDialog.SetActive(false);
        }
    }

    private void ConfirmDialogAction()
    {
        System.Action action = pendingConfirmAction;
        CloseConfirmDialog();
        action?.Invoke();
    }

    private void BuildUi()
    {
        canvas = CreateCanvas();
        root = new GameObject("PauseMenuRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image overlay = root.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.68f);

        GameObject leftPanel = CreatePanel(root.transform, "MenuPanel", new Vector2(0f, 0f), new Vector2(430f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        RectTransform leftRect = leftPanel.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0f, 1f);
        leftRect.offsetMin = Vector2.zero;
        leftRect.offsetMax = new Vector2(430f, 0f);
        VerticalLayoutGroup menuLayout = leftPanel.AddComponent<VerticalLayoutGroup>();
        menuLayout.padding = new RectOffset(52, 52, 86, 34);
        menuLayout.spacing = 10f;
        menuLayout.childControlWidth = true;
        menuLayout.childControlHeight = false;
        menuLayout.childForceExpandWidth = true;
        menuLayout.childForceExpandHeight = false;

        TMP_Text logo = CreateText(leftPanel.transform, "MenuTitle", "dark Us", 46f, FontStyles.Normal);
        logo.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        SetLayoutSize(logo.gameObject, 0f, 60f);

        CreateButton(leftPanel.transform, "ResumeButton", "Resume", SetOpenFalse);
        CreateButton(leftPanel.transform, "SettingsButton", "Settings", ShowSettingsPanel);
        CreateButton(leftPanel.transform, "ControlsButton", "Controls", ShowControlsPanel);
        CreateButton(leftPanel.transform, "PlayersButton", "Players", ShowPlayersPanel);
        CreateSpacer(leftPanel.transform, 18f);
        CreateButton(leftPanel.transform, "ReturnLobbyButton", "Return to Lobby", ConfirmReturnToLobby);
        CreateButton(leftPanel.transform, "MainMenuButton", "Quit to Main Menu", ConfirmQuitToMainMenu);
        CreateButton(leftPanel.transform, "QuitGameButton", "Quit Game", ConfirmQuitGame);

        GameObject infoPanel = CreatePanel(root.transform, "InfoPanel", new Vector2(-120f, 0f), new Vector2(940f, 780f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

        titleText = CreateText(infoPanel.transform, "TitleText", "PAUSED", 62f, FontStyles.Normal);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -80f);
        titleRect.sizeDelta = new Vector2(-100f, 80f);
        titleText.color = new Color(1f, 0.8f, 0.42f, 1f);

        roomCodeText = CreateText(infoPanel.transform, "RoomCodeText", "ROOM CODE ----", 28f, FontStyles.Normal);
        RectTransform roomRect = roomCodeText.GetComponent<RectTransform>();
        roomRect.anchorMin = new Vector2(0f, 1f);
        roomRect.anchorMax = new Vector2(1f, 1f);
        roomRect.anchoredPosition = new Vector2(0f, -145f);
        roomRect.sizeDelta = new Vector2(-100f, 44f);
        roomCodeText.color = new Color(0.76f, 0.82f, 0.84f, 1f);

        bodyText = CreateText(infoPanel.transform, "BodyText", string.Empty, 28f, FontStyles.Normal);
        RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(70f, 70f);
        bodyRect.offsetMax = new Vector2(-70f, -210f);
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.lineSpacing = 18f;

        settingsRoot = CreateSettingsContent(infoPanel.transform);
        confirmDialog = CreateConfirmDialog(root.transform);
    }

    private void SetOpenFalse()
    {
        SetOpen(false);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("InGamePauseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas newCanvas = canvasObject.GetComponent<Canvas>();
        newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        newCanvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return newCanvas;
    }

    private GameObject CreatePanel(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Vector2 pivot)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.015f, 0.022f, 0.024f, 0.78f);

        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.28f);
        outline.effectDistance = new Vector2(2f, -2f);

        return panel;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string text, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        label.raycastTarget = false;
        LocalizedTmpFontProvider.Apply(label);

        return label;
    }

    private Button CreateButton(Transform parent, string objectName, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(MenuButtonHoverEffect));
        buttonObject.transform.SetParent(parent, false);
        SetLayoutSize(buttonObject, 0f, 50f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.015f, 0.018f, 0.02f, 0.52f);

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.24f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text labelText = CreateText(buttonObject.transform, "Text (TMP)", label, 23f, FontStyles.Normal);
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
        button.onClick.AddListener(action);
        return button;
    }

    private void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(parent, false);
        SetLayoutSize(spacer, 0f, height);
    }

    private void SetLayoutSize(GameObject target, float width, float height)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = target.AddComponent<LayoutElement>();
        }

        layout.preferredWidth = width;
        layout.preferredHeight = height;
    }

    private GameObject CreateSettingsContent(Transform parent)
    {
        GameObject content = new GameObject("SettingsContent", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(parent, false);

        RectTransform rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(70f, 90f);
        rect.offsetMax = new Vector2(-70f, -190f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateSettingsSliderRow(content.transform, "Master Volume", "setting_master_volume", 0f, 1f, AudioListener.volume, false);
        CreateSettingsSliderRow(content.transform, "Voice Volume", "setting_voice_volume", 0f, 1f, PlayerPrefs.GetFloat("setting_voice_volume", 1f), false);
        CreateSettingsSliderRow(content.transform, "Mouse Sens X", "setting_mouse_x", 0.1f, 5f, PlayerPrefs.GetFloat("setting_mouse_x", 1f), false);
        CreateSettingsSliderRow(content.transform, "Mouse Sens Y", "setting_mouse_y", 0.1f, 5f, PlayerPrefs.GetFloat("setting_mouse_y", 1f), false);
        CreateSettingsSliderRow(content.transform, "Field of View", "setting_fov", 60f, 100f, PlayerPrefs.GetFloat("setting_fov", 75f), true);
        GameObject buttonRow = new GameObject("SettingsButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        buttonRow.transform.SetParent(content.transform, false);
        SetLayoutSize(buttonRow, 0f, 46f);
        HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 16f;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = true;

        CreateButton(buttonRow.transform, "ApplySettingsButton", "Apply", ApplyInGameSettings);
        CreateButton(buttonRow.transform, "ResetSettingsButton", "Reset", ResetInGameSettings);
        CreateButton(buttonRow.transform, "BackSettingsButton", "Back", ShowHomePanel);

        content.SetActive(false);
        return content;
    }

    private void CreateSettingsSliderRow(Transform parent, string label, string prefsKey, float min, float max, float defaultValue, bool wholeNumbers)
    {
        GameObject row = CreateSettingsRow(parent, label);

        TMP_Text valueText = CreateText(row.transform, "ValueText", string.Empty, 22f, FontStyles.Normal);
        RectTransform valueRect = valueText.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(1f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.anchoredPosition = new Vector2(-8f, 0f);
        valueRect.sizeDelta = new Vector2(96f, 0f);
        valueText.alignment = TextAlignmentOptions.MidlineRight;

        Slider slider = CreateSlider(row.transform, min, max, PlayerPrefs.GetFloat(prefsKey, defaultValue), wholeNumbers);
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(1f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(110f, 0f);
        sliderRect.sizeDelta = new Vector2(-420f, 28f);

        UnityEngine.Events.UnityAction<float> refresh = value =>
        {
            float finalValue = wholeNumbers ? Mathf.Round(value) : value;
            PlayerPrefs.SetFloat(prefsKey, finalValue);
            valueText.text = wholeNumbers ? finalValue.ToString("0") : finalValue.ToString("0.00");
            ApplyInGameSettings();
        };

        slider.onValueChanged.AddListener(refresh);
        refresh(slider.value);
    }

    private void CreateSettingsToggleRow(Transform parent, string label, string prefsKey, bool defaultValue)
    {
        GameObject row = CreateSettingsRow(parent, label);

        Button toggleButton = null;
        toggleButton = CreateButton(row.transform, "ToggleButton", string.Empty, () =>
        {
            bool current = PlayerPrefs.GetInt(prefsKey, defaultValue ? 1 : 0) == 1;
            PlayerPrefs.SetInt(prefsKey, current ? 0 : 1);
            ApplyInGameSettings();
            RefreshToggleLabel(toggleButton, prefsKey, defaultValue);
        });

        RectTransform buttonRect = toggleButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(180f, 46f);
        RefreshToggleLabel(toggleButton, prefsKey, defaultValue);
    }

    private GameObject CreateSettingsRow(Transform parent, string label)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        SetLayoutSize(row, 0f, 46f);

        TMP_Text labelText = CreateText(row.transform, "LabelText", label, 22f, FontStyles.Normal);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(260f, 0f);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;

        return row;
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value, bool wholeNumbers)
    {
        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        slider.wholeNumbers = wholeNumbers;

        Image background = CreateSliderImage(sliderObject.transform, "Background", new Color(0.18f, 0.25f, 0.27f, 0.9f));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 8f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        Image fill = CreateSliderImage(fillArea.transform, "Fill", new Color(0.86f, 0.66f, 0.34f, 1f));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.sizeDelta = new Vector2(0f, 8f);

        Image handle = CreateSliderImage(sliderObject.transform, "Handle", new Color(0.78f, 0.86f, 0.88f, 1f));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(24f, 24f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
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

    private void RefreshToggleLabel(Button button, string prefsKey, bool defaultValue)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = PlayerPrefs.GetInt(prefsKey, defaultValue ? 1 : 0) == 1 ? "ON" : "OFF";
        }
    }

    private void ApplyInGameSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("setting_master_volume", 1f);
        PlayerVoiceChat.ApplySavedVoiceVolumeToAll();

        float fov = PlayerPrefs.GetFloat("setting_fov", 75f);
        Camera[] cameras = FindObjectsOfType<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].fieldOfView = fov;
        }

        float mouseX = PlayerPrefs.GetFloat("setting_mouse_x", 1f);
        float mouseY = PlayerPrefs.GetFloat("setting_mouse_y", 1f);
        MouseLook[] mouseLooks = FindObjectsOfType<MouseLook>(true);
        for (int i = 0; i < mouseLooks.Length; i++)
        {
            mouseLooks[i].mouseSensitivityX = 100f * mouseX;
            mouseLooks[i].mouseSensitivityY = 95f * mouseY;
        }

        PlayerPrefs.Save();
    }

    private void ResetInGameSettings()
    {
        PlayerPrefs.SetFloat("setting_master_volume", 1f);
        PlayerPrefs.SetFloat("setting_voice_volume", 1f);
        PlayerPrefs.SetFloat("setting_mouse_x", 1f);
        PlayerPrefs.SetFloat("setting_mouse_y", 1f);
        PlayerPrefs.SetFloat("setting_fov", 75f);
        PlayerPrefs.SetInt("setting_subtitle", 1);
        ApplyInGameSettings();

        if (settingsRoot != null)
        {
            Destroy(settingsRoot);
            settingsRoot = CreateSettingsContent(bodyText.transform.parent);
            ShowSettingsPanel();
        }
    }

    private GameObject CreateConfirmDialog(Transform parent)
    {
        GameObject dialog = CreatePanel(parent, "ConfirmDialog", Vector2.zero, new Vector2(620f, 300f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        TMP_Text confirmTitle = CreateText(dialog.transform, "ConfirmTitle", "CONFIRM", 38f, FontStyles.Normal);
        RectTransform titleRect = confirmTitle.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -54f);
        titleRect.sizeDelta = new Vector2(-70f, 58f);
        confirmTitle.color = new Color(1f, 0.8f, 0.42f, 1f);

        TMP_Text message = CreateText(dialog.transform, "ConfirmMessage", string.Empty, 25f, FontStyles.Normal);
        RectTransform messageRect = message.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0f, 0.5f);
        messageRect.anchorMax = new Vector2(1f, 0.5f);
        messageRect.anchoredPosition = new Vector2(0f, 20f);
        messageRect.sizeDelta = new Vector2(-80f, 70f);

        Button confirm = CreateButton(dialog.transform, "ConfirmButton", "Confirm", ConfirmDialogAction);
        RectTransform confirmRect = confirm.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.5f, 0f);
        confirmRect.anchorMax = new Vector2(0.5f, 0f);
        confirmRect.anchoredPosition = new Vector2(-130f, 54f);
        confirmRect.sizeDelta = new Vector2(210f, 56f);

        Button cancel = CreateButton(dialog.transform, "CancelButton", "Cancel", CloseConfirmDialog);
        RectTransform cancelRect = cancel.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.5f, 0f);
        cancelRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRect.anchoredPosition = new Vector2(130f, 54f);
        cancelRect.sizeDelta = new Vector2(210f, 56f);

        dialog.SetActive(false);
        return dialog;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }
}
