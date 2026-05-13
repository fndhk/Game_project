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
    private GameObject settingsActionRoot;
    private GameObject confirmDialog;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text roomCodeText;
    private readonly Dictionary<Behaviour, bool> lockedBehaviours = new Dictionary<Behaviour, bool>();
    private CursorLockMode previousLockState;
    private bool previousCursorVisible;
    private bool isOpen;
    private string currentPanelKey;
    private System.Action pendingConfirmAction;
    private Sprite roundSliderHandleSprite;

    public static bool IsOpen
    {
        get { return instance != null && instance.isOpen; }
    }

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
        currentPanelKey = "PAUSED";
        titleText.text = T("PAUSED");
        roomCodeText.text = GetRoomCodeLine();
        SetBody(
            T("SESSION") + "\n" +
            GetRoomCodeLine() + "\n\n" +
            T("STATUS") + "\n" +
            GetNetworkStatusLine() + "\n\n" +
            T("ESC closes this menu.")
        );
    }

    private void ShowSettingsPanel()
    {
        SetSettingsVisible(true);
        currentPanelKey = "SETTINGS";
        titleText.text = T("SETTINGS");
        roomCodeText.text = T("IN-GAME SETTINGS");
    }

    private void ShowControlsPanel()
    {
        SetSettingsVisible(false);
        currentPanelKey = "CONTROLS";
        titleText.text = T("CONTROLS");
        SetBody(
            T("WASD        MOVE") + "\n" +
            T("MOUSE       LOOK") + "\n" +
            T("SHIFT       SPRINT") + "\n" +
            T("CTRL        CROUCH") + "\n" +
            T("E           INTERACT") + "\n" +
            T("F           PICK UP") + "\n" +
            T("1 / 2       SELECT ITEM") + "\n" +
            T("LMB         USE ITEM") + "\n" +
            T("G           DROP ITEM") + "\n" +
            T("V           VOICE") + "\n" +
            T("ESC         PAUSE")
        );
    }

    private void ShowPlayersPanel()
    {
        SetSettingsVisible(false);
        currentPanelKey = "PLAYERS";
        titleText.text = T("PLAYERS");
        RefreshPlayersPanel();
    }

    private void RefreshPlayersPanel()
    {
        if (titleText == null || currentPanelKey != "PLAYERS")
        {
            return;
        }

        if (!PhotonNetwork.InRoom)
        {
            SetBody(T("Not connected to a Photon room."));
            return;
        }

        string body = T("CREW") + "\n";
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            string role = player.IsMasterClient ? T("HOST") : T("PLAYER");
            string local = player.IsLocal ? T("YOU") : T("REMOTE");
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
        MenuCursorState.UnlockCursor();

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
        MenuCursorState.UnlockCursor();

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
            return T("ROOM CODE") + " " + PhotonNetwork.CurrentRoom.Name;
        }

        return T("ROOM CODE") + " ----";
    }

    private string GetNetworkStatusLine()
    {
        if (PhotonNetwork.InRoom)
        {
            return T("CONNECTED") + "  " + PhotonNetwork.CurrentRoom.PlayerCount + " / " + PhotonNetwork.CurrentRoom.MaxPlayers;
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

        if (settingsActionRoot != null)
        {
            settingsActionRoot.SetActive(visible);
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
            texts[0].text = T(title);
            texts[1].text = T(message);
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

        TMP_Text logo = CreateText(leftPanel.transform, "MenuTitle", T("dark Us"), 46f, FontStyles.Normal);
        logo.color = new Color(0.76f, 0.82f, 0.84f, 1f);
        SetLayoutSize(logo.gameObject, 0f, 60f);

        CreateButton(leftPanel.transform, "ResumeButton", T("Resume"), SetOpenFalse);
        CreateButton(leftPanel.transform, "SettingsButton", T("Settings"), ShowSettingsPanel);
        CreateButton(leftPanel.transform, "ControlsButton", T("Controls"), ShowControlsPanel);
        CreateButton(leftPanel.transform, "PlayersButton", T("Players"), ShowPlayersPanel);
        CreateSpacer(leftPanel.transform, 18f);
        CreateButton(leftPanel.transform, "MainMenuButton", T("Quit to Main Menu"), ConfirmQuitToMainMenu);
        CreateButton(leftPanel.transform, "QuitGameButton", T("Quit Game"), ConfirmQuitGame);

        GameObject infoPanel = CreatePanel(root.transform, "InfoPanel", new Vector2(-120f, 0f), new Vector2(940f, 780f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

        titleText = CreateText(infoPanel.transform, "TitleText", T("PAUSED"), 62f, FontStyles.Normal);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -80f);
        titleRect.sizeDelta = new Vector2(-100f, 80f);
        titleText.color = new Color(1f, 0.8f, 0.42f, 1f);

        roomCodeText = CreateText(infoPanel.transform, "RoomCodeText", T("ROOM CODE") + " ----", 28f, FontStyles.Normal);
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
        rect.offsetMin = new Vector2(48f, 154f);
        rect.offsetMax = new Vector2(-48f, -190f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateSettingsSliderRow(content.transform, "Master Volume", "setting_master_volume", 0f, 1f, AudioListener.volume, false);
        CreateSettingsSliderRow(content.transform, "Voice Volume", "setting_voice_volume", 0f, 1f, PlayerPrefs.GetFloat("setting_voice_volume", 1f), false);
        CreateSettingsSliderRow(content.transform, "Mouse Sens X", "setting_mouse_x", 0.1f, 5f, PlayerPrefs.GetFloat("setting_mouse_x", 1f), false);
        CreateSettingsSliderRow(content.transform, "Mouse Sens Y", "setting_mouse_y", 0.1f, 5f, PlayerPrefs.GetFloat("setting_mouse_y", 1f), false);
        CreateSettingsSliderRow(content.transform, "HUD Opacity", "setting_hud_opacity", 0.45f, 1f, PlayerPrefs.GetFloat("setting_hud_opacity", 1f), false);

        if (settingsActionRoot != null)
        {
            Destroy(settingsActionRoot);
        }

        GameObject buttonRow = new GameObject("SettingsButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRow.transform.SetParent(parent, false);
        settingsActionRoot = buttonRow;

        RectTransform buttonRowRect = buttonRow.GetComponent<RectTransform>();
        buttonRowRect.anchorMin = new Vector2(0f, 0f);
        buttonRowRect.anchorMax = new Vector2(1f, 0f);
        buttonRowRect.pivot = new Vector2(0.5f, 0f);
        buttonRowRect.anchoredPosition = new Vector2(0f, 54f);
        buttonRowRect.sizeDelta = new Vector2(-140f, 56f);

        HorizontalLayoutGroup buttonLayout = buttonRow.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 16f;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = true;

        CreateButton(buttonRow.transform, "ApplySettingsButton", T("Apply"), ApplyInGameSettings);
        CreateButton(buttonRow.transform, "ResetSettingsButton", T("Reset"), ResetInGameSettings);
        CreateButton(buttonRow.transform, "BackSettingsButton", T("Back"), ShowHomePanel);

        content.SetActive(false);
        buttonRow.SetActive(false);
        return content;
    }

    private void CreateSettingsSliderRow(Transform parent, string label, string prefsKey, float min, float max, float defaultValue, bool wholeNumbers)
    {
        GameObject row = CreateSettingsRow(parent, label);

        TMP_Text valueText = CreateText(row.transform, "ValueText", string.Empty, 22f, FontStyles.Normal);
        RectTransform valueRect = valueText.GetComponent<RectTransform>();
        valueRect.sizeDelta = new Vector2(210f, 36f);
        valueText.alignment = TextAlignmentOptions.Center;

        Slider slider = CreateSlider(row.transform, min, max, PlayerPrefs.GetFloat(prefsKey, defaultValue), wholeNumbers);
        RectTransform sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(220f, 12f);
        slider.transform.SetSiblingIndex(1);

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
        GameObject row = new GameObject(label + "Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        SetLayoutSize(row, 0f, 54f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_Text labelText = CreateText(row.transform, "LabelText", T(label), 22f, FontStyles.Normal);
        RectTransform labelRect = labelText.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(360f, 40f);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;

        return row;
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value, bool wholeNumbers)
    {
        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        sliderObject.transform.SetParent(parent, false);
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        slider.wholeNumbers = wholeNumbers;
        slider.targetGraphic = slider.GetComponent<Image>();
        slider.GetComponent<Image>().color = new Color(0.62f, 0.78f, 0.86f, 0.28f);

        Image fill = CreateSliderImage(sliderObject.transform, "Fill", new Color(1f, 0.8f, 0.42f, 0.82f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image handle = CreateSliderImage(sliderObject.transform, "Handle", new Color(0.78f, 0.86f, 0.88f, 1f));
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handle.sprite = GetRoundSliderHandleSprite();
        handle.type = Image.Type.Simple;
        handle.preserveAspect = true;
        handleRect.sizeDelta = new Vector2(20f, 20f);

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

    private Sprite GetRoundSliderHandleSprite()
    {
        if (roundSliderHandleSprite != null)
        {
            return roundSliderHandleSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "In Game Slider Round Handle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.43f;
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
        roundSliderHandleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return roundSliderHandleSprite;
    }

    private void RefreshToggleLabel(Button button, string prefsKey, bool defaultValue)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = PlayerPrefs.GetInt(prefsKey, defaultValue ? 1 : 0) == 1 ? T("ON") : T("OFF");
        }
    }

    private void ApplyInGameSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("setting_master_volume", 1f);
        PlayerVoiceChat.ApplySavedVoiceVolumeToAll();

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
        PlayerPrefs.SetFloat("setting_hud_opacity", 1f);
        ApplyInGameSettings();

        if (settingsRoot != null)
        {
            Destroy(settingsRoot);
            if (settingsActionRoot != null)
            {
                Destroy(settingsActionRoot);
                settingsActionRoot = null;
            }

            settingsRoot = CreateSettingsContent(bodyText.transform.parent);
            ShowSettingsPanel();
        }
    }

    private GameObject CreateConfirmDialog(Transform parent)
    {
        GameObject dialog = CreatePanel(parent, "ConfirmDialog", Vector2.zero, new Vector2(620f, 300f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        TMP_Text confirmTitle = CreateText(dialog.transform, "ConfirmTitle", T("CONFIRM"), 38f, FontStyles.Normal);
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

        Button confirm = CreateButton(dialog.transform, "ConfirmButton", T("Confirm"), ConfirmDialogAction);
        RectTransform confirmRect = confirm.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.5f, 0f);
        confirmRect.anchorMax = new Vector2(0.5f, 0f);
        confirmRect.anchoredPosition = new Vector2(-130f, 54f);
        confirmRect.sizeDelta = new Vector2(210f, 56f);

        Button cancel = CreateButton(dialog.transform, "CancelButton", T("Cancel"), CloseConfirmDialog);
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

    private string T(string key)
    {
        return InGameLocalization.Text(key);
    }
}
