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
        ApplyFixedLowGraphicsSettings();
        BuildUi();
        SetOpen(false);
    }

    private void Update()
    {
        if (!SettingsPanelLauncher.IsCapturingKey &&
            (Input.GetKeyDown(KeyCode.Escape) || GameInputBindings.GetKeyDown(GameInputBindings.PauseKey, KeyCode.Escape)))
        {
            if (SettingsPanelLauncher.ClosedByEscapeThisFrame)
            {
                return;
            }

            if (SettingsPanelLauncher.IsOpen)
            {
                SettingsPanelLauncher.MarkEscapeCloseFrame();
                SettingsPanelLauncher.Hide();
                return;
            }

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

        if (!open)
        {
            SettingsPanelLauncher.Hide();
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
        currentPanelKey = "PAUSED";
        titleText.text = T("PAUSED");
        roomCodeText.text = GetRoomCodeLine();
        bodyText.gameObject.SetActive(true);
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
        SettingsPanelLauncher.Show();
        currentPanelKey = "SETTINGS";
        titleText.text = T("SETTINGS");
        roomCodeText.text = T("IN-GAME SETTINGS");
        bodyText.gameObject.SetActive(true);
    }

    private void ShowControlsPanel()
    {
        currentPanelKey = "CONTROLS";
        titleText.text = T("CONTROLS");
        bodyText.gameObject.SetActive(true);
        SetBody(
            GameInputBindings.GetLabel(GameInputBindings.MoveForwardKey, KeyCode.W) + "/" +
            GameInputBindings.GetLabel(GameInputBindings.MoveLeftKey, KeyCode.A) + "/" +
            GameInputBindings.GetLabel(GameInputBindings.MoveBackwardKey, KeyCode.S) + "/" +
            GameInputBindings.GetLabel(GameInputBindings.MoveRightKey, KeyCode.D) + "        " + T("MOVE") + "\n" +
            T("MOUSE       LOOK") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.SprintKey, KeyCode.LeftShift) + "       " + T("SPRINT") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.CrouchKey, KeyCode.LeftControl) + "       " + T("CROUCH") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.InteractKey, KeyCode.E) + "           " + T("INTERACT") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.PickupKey, KeyCode.F) + "           " + T("PICK UP") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.Slot1Key, KeyCode.Alpha1) + " / " +
            GameInputBindings.GetLabel(GameInputBindings.Slot2Key, KeyCode.Alpha2) + "       " + T("SELECT ITEM") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.UseItemKey, KeyCode.Mouse0) + "         " + T("USE ITEM") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.DropItemKey, KeyCode.G) + "           " + T("DROP ITEM") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.MicMuteKey, KeyCode.B) + "           " + T("MIC MUTE") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.KillKey, KeyCode.Q) + "           " + T("KILL") + "\n" +
            GameInputBindings.GetLabel(GameInputBindings.PauseKey, KeyCode.Escape) + "         " + T("PAUSE")
        );
    }

    private void ShowPlayersPanel()
    {
        currentPanelKey = "PLAYERS";
        titleText.text = T("PLAYERS");
        bodyText.gameObject.SetActive(true);
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

    private void ApplyFixedLowGraphicsSettings()
    {
        QualitySettings.globalTextureMipmapLimit = 3;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowResolution = ShadowResolution.Low;
        QualitySettings.antiAliasing = 0;
        QualitySettings.vSyncCount = 0;
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
