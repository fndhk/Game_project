using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class RoomLobbySceneController : MonoBehaviourPunCallbacks
{
    public string mainMenuSceneName = "LobbyScene";
    public string publicRoomListSceneName = "PublicRoomListScene";
    public string gameSceneName = "labor";
    public Sprite lobbyBackgroundSprite;
    public bool buildInEditMode = true;

    private const string RoomCodePrefsKey = "dark_us_room_code";
    private const string RoomHostPrefsKey = "dark_us_room_is_host";
    private const string RoomVisiblePrefsKey = "dark_us_room_is_visible";
    private const string RoomTitlePrefsKey = "dark_us_room_title";
    private const string RoomTitlePropertyKey = "title";
    private const string MapSeedPropertyKey = "mapSeed";
    private const string ReadyPropertyKey = "ready";
    private const string StartSignalPropertyKey = "gameStarting";
    private const byte MaxPlayers = 12;

    private readonly List<TMP_Text> slotTexts = new List<TMP_Text>();
    private readonly List<Image> slotMicIcons = new List<Image>();
    private readonly List<Slider> slotVoiceSliders = new List<Slider>();
    private readonly List<Image> slotColorSwatches = new List<Image>();
    private readonly List<Button> slotColorButtons = new List<Button>();
    private readonly List<Button> colorButtons = new List<Button>();
    private readonly List<Image> colorSwatches = new List<Image>();
    private readonly List<TMP_Text> colorLabels = new List<TMP_Text>();
    private readonly List<RectTransform> animatedPanels = new List<RectTransform>();
    private readonly List<Vector2> animatedPanelBasePositions = new List<Vector2>();
    private readonly List<CanvasGroup> animatedPanelGroups = new List<CanvasGroup>();
    private TMP_Text networkStatusText;
    private TMP_Text roomTitleText;
    private TMP_Text roomCodeText;
    private RectTransform backgroundSweepLine;
    private GameObject colorPickerPanelObject;
    private Button colorConfirmButton;
    private Button colorCancelButton;
    private Sprite micOpenIconSprite;
    private Sprite micMutedIconSprite;
    private Sprite settingSliderHandleSprite;
    private PlayerVoiceChat lobbyVoiceChat;
    private Button readyButton;
    private Button startButton;
    private string pendingRoomCode;
    private bool pendingCreateRoom;
    private int createRetryCount;
    private int languageIndex;
    private int pendingColorSelection = -1;
    private bool isStartingGame;
    private float uiStartedAt;
    private float nextVoicePanelRefreshTime;

    public override void OnEnable()
    {
        base.OnEnable();

        if (Application.isPlaying || !buildInEditMode)
        {
            return;
        }

        if (FindUiTransform("Canvas") == null)
        {
            languageIndex = PlayerPrefs.GetInt("setting_language", 0);
            BuildRoomLobbyUi();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PhotonConnectionDefaults.Apply();
        MenuCursorState.UnlockCursor();
        languageIndex = PlayerPrefs.GetInt("setting_language", 0);
        AudioListener.volume = PlayerPrefs.GetFloat("setting_master_volume", 1f);
        uiStartedAt = Time.unscaledTime;
        EnsureEventSystem();
        EnsureLobbyVoiceChat();
        EnsureRoomLobbyUi();
        StartPhotonRoomFlow();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

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

            OpenSettingsPanel();
        }

        AnimateLobbyUi();
        RefreshVoicePanelOnInterval();
    }

    private void AnimateLobbyUi()
    {
        float elapsed = Time.unscaledTime - uiStartedAt;

        if (backgroundSweepLine != null)
        {
            float x = Mathf.Lerp(-960f, 960f, Mathf.PingPong(elapsed * 0.055f, 1f));
            backgroundSweepLine.anchoredPosition = new Vector2(x, 0f);
        }

        for (int i = 0; i < animatedPanels.Count; i++)
        {
            RectTransform panel = animatedPanels[i];
            if (panel == null)
            {
                continue;
            }

            float delay = i * 0.09f;
            float appear = Mathf.Clamp01((elapsed - delay) * 3.2f);
            appear = appear * appear * (3f - 2f * appear);
            Vector2 basePosition = i < animatedPanelBasePositions.Count ? animatedPanelBasePositions[i] : panel.anchoredPosition;
            float slide = Mathf.Lerp(42f, 0f, appear);
            panel.anchoredPosition = basePosition + new Vector2(0f, -slide);

            float pulse = 1f + Mathf.Sin(elapsed * 1.35f + i * 0.8f) * 0.0045f;
            float scale = Mathf.Lerp(0.965f, pulse, appear);
            panel.localScale = new Vector3(scale, scale, 1f);

            if (i < animatedPanelGroups.Count && animatedPanelGroups[i] != null)
            {
                animatedPanelGroups[i].alpha = appear;
            }
        }
    }

    private void RefreshVoicePanelOnInterval()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextVoicePanelRefreshTime)
        {
            return;
        }

        nextVoicePanelRefreshTime = Time.unscaledTime + 0.18f;
        RefreshIntegratedVoiceControls();
    }

    private void EnsureLobbyVoiceChat()
    {
        lobbyVoiceChat = GetComponent<PlayerVoiceChat>();
        if (lobbyVoiceChat == null)
        {
            lobbyVoiceChat = gameObject.AddComponent<PlayerVoiceChat>();
        }

        lobbyVoiceChat.voiceEnabled = true;
        lobbyVoiceChat.muteToggleKey = KeyCode.B;
        lobbyVoiceChat.spatialBlend = 0f;
        lobbyVoiceChat.showLocalMicHud = false;
    }

    public void OnClickStartGame()
    {
        SetColorPickerVisible(false);

        if (isStartingGame)
        {
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            SetNetworkStatus("HOST ONLY");
            return;
        }

        if (!AreAllPlayersReady())
        {
            SetNetworkStatus("WAITING READY");
            return;
        }

        EnsureLocalColorSelection();
        if (!ArePlayerColorsReadyAndUnique())
        {
            SetNetworkStatus("SELECT COLOR");
            return;
        }

        StartCoroutine(StartGameWithLoading());
    }

    private System.Collections.IEnumerator StartGameWithLoading()
    {
        isStartingGame = true;
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        EnsureMapSeedProperty();
        int imposterActor = RoleAssignmentManager.SelectNewPhotonImposterActor();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { StartSignalPropertyKey, true }
        });
        PhotonNetwork.SendAllOutgoingCommands();
        SetNetworkStatus("STARTING");

        DarkScanLoadingScreen.ShowImmediate("MATCH LOCKED...");

        float waitUntil = Time.time + 1.5f;
        while (Time.time < waitUntil && RoleAssignmentManager.GetPhotonImposterActor() != imposterActor)
        {
            yield return null;
        }

        PhotonNetwork.SendAllOutgoingCommands();
        yield return new WaitForSeconds(0.2f);

        PhotonNetwork.LoadLevel(gameSceneName);
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged != null &&
            propertiesThatChanged.ContainsKey(StartSignalPropertyKey) &&
            propertiesThatChanged[StartSignalPropertyKey] is bool starting &&
            starting)
        {
            SetNetworkStatus("STARTING");
            DarkScanLoadingScreen.ShowImmediate("MATCH LOCKED...");
        }
    }

    public void OnClickBack()
    {
        SetColorPickerVisible(false);

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        LoadScene(mainMenuSceneName);
    }

    public override void OnConnectedToMaster()
    {
        SetNetworkStatus("PHOTON CONNECTED");
        ExecutePendingRoomFlow();
    }

    public override void OnCreatedRoom()
    {
        EnsureMapSeedProperty();
        SetNetworkStatus("ROOM CREATED");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (!pendingCreateRoom || createRetryCount >= 10)
        {
            SetNetworkStatus("CREATE FAILED: " + message);
            return;
        }

        createRetryCount++;
        pendingRoomCode = Random.Range(0, 10000).ToString("0000");
        PlayerPrefs.SetString(RoomCodePrefsKey, pendingRoomCode);
        PlayerPrefs.Save();
        UpdateRoomTitleText();
        UpdateRoomCodeText();
        CreatePhotonRoom();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        SetNetworkStatus("ROOM NOT FOUND: " + pendingRoomCode);
    }

    public override void OnJoinedRoom()
    {
        pendingRoomCode = PhotonNetwork.CurrentRoom.Name;
        PlayerPrefs.SetString(RoomCodePrefsKey, pendingRoomCode);
        SaveJoinedRoomTitle();
        PlayerPrefs.Save();
        SetLocalReady(false);
        EnsureLocalColorSelection();
        UpdateRoomCodeText();
        SetNetworkStatus(PhotonNetwork.IsMasterClient ? "HOST READY" : "CONNECTED");
        RefreshPlayerSlots();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        EnsureLocalColorSelection();
        RefreshPlayerSlots();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        EnsureLocalColorSelection();
        RefreshPlayerSlots();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshPlayerSlots();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps != null &&
            (changedProps.ContainsKey(ReadyPropertyKey) || changedProps.ContainsKey(PlayerColorPalette.PlayerColorPropertyKey)))
        {
            EnsureLocalColorSelection();
            RefreshPlayerSlots();
        }
    }

    public override void OnLeftRoom()
    {
        LoadScene(GetBackSceneName());
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetNetworkStatus("DISCONNECTED: " + cause);
    }

    private void StartPhotonRoomFlow()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        pendingRoomCode = GetRoomCode();
        pendingCreateRoom = PlayerPrefs.GetInt(RoomHostPrefsKey, 1) == 1;
        createRetryCount = 0;
        UpdateRoomCodeText();
        RefreshPlayerSlots();

        if (PhotonNetwork.InRoom)
        {
            pendingRoomCode = PhotonNetwork.CurrentRoom.Name;
            pendingCreateRoom = PhotonNetwork.IsMasterClient;
            SaveJoinedRoomTitle();
            PlayerPrefs.SetString(RoomCodePrefsKey, pendingRoomCode);
            PlayerPrefs.SetInt(RoomHostPrefsKey, PhotonNetwork.IsMasterClient ? 1 : 0);
            PlayerPrefs.Save();
            UpdateRoomCodeText();
            SetNetworkStatus(PhotonNetwork.IsMasterClient ? "HOST READY" : "CONNECTED");
            EnsureLocalColorSelection();
            RefreshPlayerSlots();
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            ExecutePendingRoomFlow();
            return;
        }

        SetNetworkStatus("CONNECTING PHOTON");
        PhotonNetwork.ConnectUsingSettings();
    }

    private void ExecutePendingRoomFlow()
    {
        if (pendingCreateRoom)
        {
            CreatePhotonRoom();
        }
        else
        {
            SetNetworkStatus("JOINING ROOM " + pendingRoomCode);
            PhotonNetwork.JoinRoom(pendingRoomCode);
        }
    }

    private void CreatePhotonRoom()
    {
        bool isVisible = PlayerPrefs.GetInt(RoomVisiblePrefsKey, 0) == 1;
        string roomTitle = PlayerPrefs.GetString(RoomTitlePrefsKey, isVisible ? "Public Room" : "Private Room");

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = MaxPlayers,
            IsOpen = true,
            IsVisible = isVisible,
            CleanupCacheOnLeave = true,
            CustomRoomProperties = new Hashtable
            {
                { RoomTitlePropertyKey, roomTitle },
                { MapSeedPropertyKey, Random.Range(1, int.MaxValue) }
            },
            CustomRoomPropertiesForLobby = new[] { RoomTitlePropertyKey, MapSeedPropertyKey }
        };

        SetNetworkStatus("CREATING ROOM " + pendingRoomCode);
        PhotonNetwork.CreateRoom(pendingRoomCode, options, TypedLobby.Default);
    }

    private void RefreshPlayerSlots()
    {
        for (int i = 0; i < slotTexts.Count; i++)
        {
            string nameLabel = T("EMPTY");
            string stateLabel = T("WAITING");
            Color textColor = new Color(0.78f, 0.86f, 0.88f, 1f);
            Color cardColor = new Color(0.02f, 0.03f, 0.034f, 0.78f);

            if (PhotonNetwork.InRoom && i < PhotonNetwork.PlayerList.Length)
            {
                Player player = PhotonNetwork.PlayerList[i];
                string role = player.IsMasterClient ? T("HOST") : T("PLAYER");
                nameLabel = player.IsLocal ? T("YOU") : role;
                stateLabel = IsPlayerReady(player) ? T("READY") : T("WAITING");
                textColor = player.IsLocal ? new Color(1f, 0.8f, 0.42f, 1f) : new Color(0.84f, 0.92f, 0.94f, 1f);
                cardColor = IsPlayerReady(player) ? new Color(0.03f, 0.12f, 0.10f, 0.82f) : new Color(0.035f, 0.045f, 0.052f, 0.82f);
            }
            else if (!PhotonNetwork.InRoom && i == 0 && pendingCreateRoom)
            {
                nameLabel = T("HOST");
                stateLabel = T("READY");
                cardColor = new Color(0.03f, 0.12f, 0.10f, 0.82f);
            }
            else if (!PhotonNetwork.InRoom && i == 1 && !pendingCreateRoom)
            {
                nameLabel = T("YOU");
                stateLabel = T("READY");
                textColor = new Color(1f, 0.8f, 0.42f, 1f);
                cardColor = new Color(0.03f, 0.12f, 0.10f, 0.82f);
            }

            slotTexts[i].text = nameLabel + "\n<size=70%>" + stateLabel + "</size>";
            slotTexts[i].color = textColor;
            PositionSlotMicIcon(i, nameLabel);
            RefreshSlotColorSwatch(i);

            Image cardImage = slotTexts[i].transform.parent != null ? slotTexts[i].transform.parent.GetComponent<Image>() : null;
            if (cardImage != null)
            {
                cardImage.color = cardColor;
            }
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(PhotonNetwork.IsMasterClient || pendingCreateRoom);
            startButton.interactable = PhotonNetwork.InRoom &&
                                       PhotonNetwork.IsMasterClient &&
                                       AreAllPlayersReady() &&
                                       ArePlayerColorsReadyAndUnique();
        }

        if (readyButton != null)
        {
            readyButton.interactable = PhotonNetwork.InRoom;
            TMP_Text readyLabel = readyButton.GetComponentInChildren<TMP_Text>(true);
            if (readyLabel != null)
            {
                readyLabel.text = IsPlayerReady(PhotonNetwork.LocalPlayer) ? T("Cancel Ready") : T("Ready");
            }
        }

        RefreshIntegratedVoiceControls();
        RefreshColorPicker();
    }

    private void RefreshSlotColorSwatch(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotColorSwatches.Count)
        {
            return;
        }

        Image swatch = slotColorSwatches[slotIndex];
        if (swatch == null)
        {
            return;
        }

        Button swatchButton = slotIndex < slotColorButtons.Count ? slotColorButtons[slotIndex] : null;

        if (!PhotonNetwork.InRoom || slotIndex >= PhotonNetwork.PlayerList.Length)
        {
            swatch.enabled = false;
            if (swatchButton != null)
            {
                swatchButton.interactable = false;
            }
            return;
        }

        Player player = PhotonNetwork.PlayerList[slotIndex];
        int colorIndex = PlayerColorPalette.GetPlayerColorIndex(player, -1);
        if (colorIndex < 0)
        {
            swatch.enabled = false;
            if (swatchButton != null)
            {
                swatchButton.interactable = player != null && player.IsLocal;
            }
            return;
        }

        swatch.enabled = true;
        swatch.color = PlayerColorPalette.GetColor(colorIndex);
        if (swatchButton != null)
        {
            swatchButton.interactable = player != null && player.IsLocal;
        }
    }

    private void PositionSlotMicIcon(int slotIndex, string nameLabel)
    {
        if (slotIndex < 0 || slotIndex >= slotTexts.Count || slotIndex >= slotMicIcons.Count)
        {
            return;
        }

        TMP_Text slotText = slotTexts[slotIndex];
        Image micIcon = slotMicIcons[slotIndex];
        if (slotText == null || micIcon == null)
        {
            return;
        }

        RectTransform micRect = micIcon.GetComponent<RectTransform>();
        RectTransform cardRect = slotText.transform.parent as RectTransform;
        if (micRect == null || cardRect == null)
        {
            return;
        }

        float nameWidth = slotText.GetPreferredValues(nameLabel, 170f, 30f).x;
        float cardWidth = cardRect.rect.width > 1f ? cardRect.rect.width : 260f;
        float maxX = Mathf.Max(120f, cardWidth - 42f);
        float iconX = Mathf.Clamp(22f + nameWidth + 10f, 82f, maxX);
        micRect.anchoredPosition = new Vector2(iconX, -25f);
    }

    private void RefreshIntegratedVoiceControls()
    {
        for (int i = 0; i < slotMicIcons.Count; i++)
        {
            bool isPreviewLocalSlot = !PhotonNetwork.InRoom && ((pendingCreateRoom && i == 0) || (!pendingCreateRoom && i == 1));
            bool hasPlayer = (PhotonNetwork.InRoom && i < PhotonNetwork.PlayerList.Length) || isPreviewLocalSlot;
            Player player = PhotonNetwork.InRoom && i < PhotonNetwork.PlayerList.Length ? PhotonNetwork.PlayerList[i] : null;
            bool isSpeaking = player != null && PlayerVoiceChat.IsActorSpeaking(player.ActorNumber);
            bool isMuted = hasPlayer && (player == null || player.IsLocal) && PlayerVoiceChat.IsLocalMuted();

            Image icon = slotMicIcons[i];

            if (icon != null)
            {
                icon.enabled = hasPlayer;
                icon.sprite = isMuted ? GetMicMutedIconSprite() : GetMicOpenIconSprite();
                icon.color = isMuted
                    ? new Color(1f, 0.34f, 0.28f, 1f)
                    : (isSpeaking ? new Color(0.48f, 1f, 0.68f, 1f) : new Color(0.70f, 0.82f, 0.86f, 0.88f));
            }

            Slider slider = i < slotVoiceSliders.Count ? slotVoiceSliders[i] : null;

            if (slider != null)
            {
                slider.gameObject.SetActive(hasPlayer);

                if (hasPlayer && !slider.interactable)
                {
                    slider.interactable = true;
                }

                if (hasPlayer)
                {
                    string volumeKey = player != null ? GetPlayerVoiceVolumeKey(player) : "setting_voice_volume";
                    slider.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetFloat(volumeKey, 1f), 0f, 2f));
                }
            }
        }
    }

    public void OnClickReady()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        SetLocalReady(!IsPlayerReady(PhotonNetwork.LocalPlayer));
    }

    private void SetLocalReady(bool isReady)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { ReadyPropertyKey, isReady }
        });

        RefreshPlayerSlots();
    }

    private bool IsPlayerReady(Player player)
    {
        if (player == null || player.CustomProperties == null)
        {
            return false;
        }

        return player.CustomProperties.TryGetValue(ReadyPropertyKey, out object value) &&
               value is bool isReady &&
               isReady;
    }

    private bool AreAllPlayersReady()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            if (!IsPlayerReady(PhotonNetwork.PlayerList[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureLocalColorSelection()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            RefreshColorPicker();
            return;
        }

        int currentColor = PlayerColorPalette.GetPlayerColorIndex(PhotonNetwork.LocalPlayer, -1);
        if (currentColor >= 0 && IsColorAvailableForLocal(currentColor))
        {
            RefreshColorPicker();
            return;
        }

        int preferredColor = Mathf.Clamp(PlayerPrefs.GetInt("dark_us_player_color_index", 0), 0, PlayerColorPalette.ColorCount - 1);
        if (IsColorAvailableForLocal(preferredColor))
        {
            SetLocalColor(preferredColor);
            return;
        }

        for (int i = 0; i < PlayerColorPalette.ColorCount; i++)
        {
            if (IsColorAvailableForLocal(i))
            {
                SetLocalColor(i);
                return;
            }
        }

        RefreshColorPicker();
    }

    private void SetLocalColor(int colorIndex)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        colorIndex = Mathf.Clamp(colorIndex, 0, PlayerColorPalette.ColorCount - 1);
        if (!IsColorAvailableForLocal(colorIndex))
        {
            SetNetworkStatus("COLOR TAKEN");
            RefreshColorPicker();
            return;
        }

        PlayerPrefs.SetInt("dark_us_player_color_index", colorIndex);
        PlayerPrefs.Save();

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PlayerColorPalette.PlayerColorPropertyKey, colorIndex }
        });

        RefreshColorPicker();
        SetColorPickerVisible(false);
    }

    private bool ArePlayerColorsReadyAndUnique()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList.Length <= 0)
        {
            return false;
        }

        HashSet<int> usedColors = new HashSet<int>();
        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            int colorIndex = PlayerColorPalette.GetPlayerColorIndex(PhotonNetwork.PlayerList[i], -1);
            if (colorIndex < 0 || colorIndex >= PlayerColorPalette.ColorCount || !usedColors.Add(colorIndex))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsColorAvailableForLocal(int colorIndex)
    {
        return !IsColorTakenByOther(colorIndex);
    }

    private bool IsColorTakenByOther(int colorIndex)
    {
        if (!PhotonNetwork.InRoom)
        {
            return false;
        }

        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            Player player = PhotonNetwork.PlayerList[i];
            if (player == null || player.IsLocal)
            {
                continue;
            }

            if (PlayerColorPalette.GetPlayerColorIndex(player, -1) == colorIndex)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshColorPicker()
    {
        int localColor = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null
            ? PlayerColorPalette.GetPlayerColorIndex(PhotonNetwork.LocalPlayer, -1)
            : PlayerPrefs.GetInt("dark_us_player_color_index", 0);
        bool pickerOpen = colorPickerPanelObject != null && colorPickerPanelObject.activeSelf;
        int previewColor = pickerOpen && pendingColorSelection >= 0 ? pendingColorSelection : localColor;

        for (int i = 0; i < colorButtons.Count; i++)
        {
            bool taken = IsColorTakenByOther(i);
            bool selected = previewColor == i;
            bool canClick = PhotonNetwork.InRoom && !taken;

            Button button = colorButtons[i];
            if (button != null)
            {
                button.interactable = canClick;
            }

            Image swatch = i < colorSwatches.Count ? colorSwatches[i] : null;
            if (swatch != null)
            {
                Color color = PlayerColorPalette.GetColor(i);
                color.a = taken ? 0.24f : 1f;
                swatch.color = color;
            }

            TMP_Text label = i < colorLabels.Count ? colorLabels[i] : null;
            if (label != null)
            {
                label.text = selected ? T("SELECTED") : (taken ? T("TAKEN") : string.Empty);
                label.color = selected
                    ? new Color(1f, 0.8f, 0.42f, 1f)
                    : new Color(1f, 0.34f, 0.28f, 0.95f);
            }
        }

        if (colorConfirmButton != null)
        {
            colorConfirmButton.interactable = PhotonNetwork.InRoom &&
                                              pendingColorSelection >= 0 &&
                                              !IsColorTakenByOther(pendingColorSelection);
        }
    }

    private void SelectPendingColor(int colorIndex)
    {
        colorIndex = Mathf.Clamp(colorIndex, 0, PlayerColorPalette.ColorCount - 1);
        if (IsColorTakenByOther(colorIndex))
        {
            SetNetworkStatus("COLOR TAKEN");
            RefreshColorPicker();
            return;
        }

        pendingColorSelection = colorIndex;
        RefreshColorPicker();
    }

    private void ConfirmPendingColorSelection()
    {
        if (pendingColorSelection < 0)
        {
            return;
        }

        SetLocalColor(pendingColorSelection);
    }

    private void ToggleColorPickerFromSlot(int slotIndex)
    {
        if (!PhotonNetwork.InRoom || slotIndex < 0 || slotIndex >= PhotonNetwork.PlayerList.Length)
        {
            return;
        }

        Player player = PhotonNetwork.PlayerList[slotIndex];
        if (player == null || !player.IsLocal)
        {
            return;
        }

        SetColorPickerVisible(colorPickerPanelObject == null || !colorPickerPanelObject.activeSelf);
    }

    private void SetColorPickerVisible(bool visible)
    {
        if (colorPickerPanelObject == null)
        {
            return;
        }

        colorPickerPanelObject.SetActive(visible);
        if (visible)
        {
            pendingColorSelection = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null
                ? PlayerColorPalette.GetPlayerColorIndex(PhotonNetwork.LocalPlayer, PlayerPrefs.GetInt("dark_us_player_color_index", 0))
                : PlayerPrefs.GetInt("dark_us_player_color_index", 0);
            pendingColorSelection = Mathf.Clamp(pendingColorSelection, 0, PlayerColorPalette.ColorCount - 1);
            RefreshColorPicker();
            return;
        }

        pendingColorSelection = -1;
    }

    private void OpenSettingsPanel()
    {
        MenuCursorState.UnlockCursor();
        SettingsPanelLauncher.Show();
    }

    private string GetRoomCode()
    {
        string roomCode = PlayerPrefs.GetString(RoomCodePrefsKey, string.Empty);
        if (IsValidRoomCode(roomCode))
        {
            return roomCode;
        }

        roomCode = Random.Range(0, 10000).ToString("0000");
        PlayerPrefs.SetString(RoomCodePrefsKey, roomCode);
        PlayerPrefs.SetInt(RoomHostPrefsKey, 1);
        PlayerPrefs.Save();
        return roomCode;
    }

    private void SaveJoinedRoomTitle()
    {
        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
        {
            return;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomTitlePropertyKey, out object value) &&
            value is string title &&
            !string.IsNullOrWhiteSpace(title))
        {
            PlayerPrefs.SetString(RoomTitlePrefsKey, title);
        }
    }

    private void EnsureMapSeedProperty()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(MapSeedPropertyKey))
        {
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { MapSeedPropertyKey, Random.Range(1, int.MaxValue) }
        });
    }

    private string GetBackSceneName()
    {
        return PlayerPrefs.GetInt(RoomVisiblePrefsKey, 0) == 1 ? publicRoomListSceneName : mainMenuSceneName;
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

    private void UpdateRoomCodeText()
    {
        if (roomCodeText != null)
        {
            string roomCode = !string.IsNullOrWhiteSpace(pendingRoomCode) ? pendingRoomCode : GetRoomCode();
            roomCodeText.text = T("ROOM CODE") + "  " + roomCode;
        }
    }

    private void UpdateRoomTitleText()
    {
        if (roomTitleText != null)
        {
            roomTitleText.text = TranslateRoomTitle(PlayerPrefs.GetString(RoomTitlePrefsKey, "ROOM LOBBY"));
        }
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

    private void EnsureRoomLobbyUi()
    {
        if (FindUiTransform("Canvas") != null && FindUiTransform("ReadyButton") != null)
        {
            BindExistingRoomLobbyUi();
            return;
        }

        BuildRoomLobbyUi();
    }

    private void BindExistingRoomLobbyUi()
    {
        slotTexts.Clear();
        slotMicIcons.Clear();
        slotVoiceSliders.Clear();
        slotColorSwatches.Clear();
        slotColorButtons.Clear();
        colorButtons.Clear();
        colorSwatches.Clear();
        colorLabels.Clear();
        animatedPanels.Clear();
        animatedPanelBasePositions.Clear();
        animatedPanelGroups.Clear();

        roomTitleText = FindText("RoomTitleText");
        roomCodeText = FindText("RoomCodeText");
        networkStatusText = FindText("NetworkStatusText");
        backgroundSweepLine = FindRectTransform("BackgroundSweepLine");

        AddAnimatedPanelIfFound("LobbyHeaderPanel");
        AddAnimatedPanelIfFound("CrewColorPanel");

        for (int i = 0; i < MaxPlayers; i++)
        {
            Transform card = FindUiTransform("CrewSlotCard_" + i);
            if (card == null)
            {
                continue;
            }

            TMP_Text slotText = FindText(card, "SlotText");
            if (slotText != null)
            {
                slotTexts.Add(slotText);
            }

            Image micIcon = FindImage(card, "MicStatusIcon");
            if (micIcon != null)
            {
                slotMicIcons.Add(micIcon);
            }

            Image colorSwatch = FindImage(card, "CrewColorSwatch");
            Button colorButton = FindButton(card, "CrewColorSwatch");
            if (colorSwatch != null)
            {
                slotColorSwatches.Add(colorSwatch);
            }

            if (colorButton != null)
            {
                int slotIndex = i;
                colorButton.onClick.RemoveAllListeners();
                colorButton.onClick.AddListener(() => ToggleColorPickerFromSlot(slotIndex));
                slotColorButtons.Add(colorButton);
            }

            Slider voiceSlider = FindSlider(card, "PlayerVoiceVolumeSlider");
            if (voiceSlider != null)
            {
                int slotIndex = i;
                voiceSlider.onValueChanged.RemoveAllListeners();
                voiceSlider.onValueChanged.AddListener(value => ApplySlotVoiceVolume(slotIndex, value));
                slotVoiceSliders.Add(voiceSlider);
            }
        }

        colorPickerPanelObject = FindUiTransform("CrewColorPanel")?.gameObject;
        for (int i = 0; i < PlayerColorPalette.ColorCount; i++)
        {
            Transform buttonTransform = FindUiTransform("ColorButton_" + i);
            if (buttonTransform == null)
            {
                continue;
            }

            Button button = buttonTransform.GetComponent<Button>();
            Image swatch = FindImage(buttonTransform, "Swatch");
            TMP_Text label = FindText("ColorLabel_" + i);

            if (button != null)
            {
                int colorIndex = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectPendingColor(colorIndex));
                colorButtons.Add(button);
            }

            if (swatch != null)
            {
                colorSwatches.Add(swatch);
            }

            if (label != null)
            {
                colorLabels.Add(label);
            }
        }

        colorConfirmButton = BindButton("ColorConfirmButton", ConfirmPendingColorSelection);
        colorCancelButton = BindButton("ColorCancelButton", () => SetColorPickerVisible(false));
        readyButton = BindButton("ReadyButton", OnClickReady);
        startButton = BindButton("StartGameButton", OnClickStartGame);
        BindButton("BackButton", OnClickBack);

        RefreshIntegratedVoiceControls();
        RefreshColorPicker();
        SetColorPickerVisible(false);
        UpdateRoomTitleText();
        UpdateRoomCodeText();
    }

    private void BuildRoomLobbyUi()
    {
        Canvas canvas = CreateCanvas();
        CreateBackground(canvas.transform);

        Transform headerPanel = CreateInfoPanel(canvas.transform, "LobbyHeaderPanel", new Vector2(520f, -122f), new Vector2(900f, 150f));
        TMP_Text title = CreateText(headerPanel, "TitleText", T("ROOM LOBBY"), 58f, FontStyles.UpperCase);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(263f, -42f);
        titleRect.sizeDelta = new Vector2(430f, 76f);
        title.alignment = TextAlignmentOptions.Left;
        title.color = new Color(1f, 0.8f, 0.42f, 1f);

        roomTitleText = CreateText(headerPanel, "RoomTitleText", string.Empty, 22f, FontStyles.Normal);
        RectTransform roomTitleRect = roomTitleText.GetComponent<RectTransform>();
        roomTitleRect.anchorMin = new Vector2(0f, 1f);
        roomTitleRect.anchorMax = new Vector2(0f, 1f);
        roomTitleRect.anchoredPosition = new Vector2(234f, -108f);
        roomTitleRect.sizeDelta = new Vector2(360f, 34f);
        roomTitleText.alignment = TextAlignmentOptions.Left;
        roomTitleText.color = new Color(0.76f, 0.82f, 0.84f, 1f);

        roomCodeText = CreateText(headerPanel, "RoomCodeText", T("ROOM CODE") + "  " + GetRoomCode(), 30f, FontStyles.Normal);
        RectTransform codeRect = roomCodeText.GetComponent<RectTransform>();
        codeRect.anchorMin = new Vector2(1f, 1f);
        codeRect.anchorMax = new Vector2(1f, 1f);
        codeRect.anchoredPosition = new Vector2(-210f, -50f);
        codeRect.sizeDelta = new Vector2(360f, 48f);
        roomCodeText.alignment = TextAlignmentOptions.Right;
        roomCodeText.color = new Color(0.88f, 0.96f, 0.98f, 1f);

        networkStatusText = CreateText(headerPanel, "NetworkStatusText", T("PHOTON READY"), 20f, FontStyles.UpperCase);
        RectTransform statusRect = networkStatusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(1f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.anchoredPosition = new Vector2(-210f, -104f);
        statusRect.sizeDelta = new Vector2(360f, 34f);
        networkStatusText.alignment = TextAlignmentOptions.Right;

        GameObject panel = new GameObject("CrewGridPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(GridLayoutGroup));
        panel.layer = canvas.gameObject.layer;
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.anchoredPosition = new Vector2(520f, -42f);
        panelRect.sizeDelta = new Vector2(900f, 600f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.01f, 0.014f, 0.016f, 0.66f);

        Outline panelOutline = panel.GetComponent<Outline>();
        panelOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.42f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        GridLayoutGroup layout = panel.GetComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(34, 34, 34, 34);
        layout.cellSize = new Vector2(260f, 116f);
        layout.spacing = new Vector2(26f, 22f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;

        slotTexts.Clear();
        slotMicIcons.Clear();
        slotVoiceSliders.Clear();
        slotColorSwatches.Clear();
        slotColorButtons.Clear();
        for (int i = 0; i < MaxPlayers; i++)
        {
            GameObject card = new GameObject("CrewSlotCard_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            card.layer = panel.layer;
            card.transform.SetParent(panel.transform, false);

            Image cardImage = card.GetComponent<Image>();
            cardImage.color = new Color(0.02f, 0.03f, 0.034f, 0.78f);
            Outline cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color(0.42f, 0.60f, 0.66f, 0.32f);
            cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

            TMP_Text slotText = CreateText(card.transform, "SlotText", T("EMPTY") + "\n" + T("WAITING"), 24f, FontStyles.UpperCase);
            RectTransform slotRect = slotText.GetComponent<RectTransform>();
            slotRect.anchorMin = Vector2.zero;
            slotRect.anchorMax = Vector2.one;
            slotRect.offsetMin = new Vector2(22f, 52f);
            slotRect.offsetMax = new Vector2(-52f, -14f);
            slotText.alignment = TextAlignmentOptions.TopLeft;
            slotText.color = new Color(0.78f, 0.86f, 0.88f, 1f);
            slotTexts.Add(slotText);

            GameObject micObject = new GameObject("MicStatusIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            micObject.layer = card.layer;
            micObject.transform.SetParent(card.transform, false);
            RectTransform micRect = micObject.GetComponent<RectTransform>();
            micRect.anchorMin = new Vector2(0f, 1f);
            micRect.anchorMax = new Vector2(0f, 1f);
            micRect.pivot = new Vector2(0f, 0.5f);
            micRect.anchoredPosition = new Vector2(116f, -25f);
            micRect.sizeDelta = new Vector2(18f, 18f);
            Image micImage = micObject.GetComponent<Image>();
            micImage.sprite = GetMicOpenIconSprite();
            micImage.color = new Color(0.70f, 0.82f, 0.86f, 0.88f);
            micImage.enabled = false;
            slotMicIcons.Add(micImage);

            GameObject colorObject = new GameObject("CrewColorSwatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            colorObject.layer = card.layer;
            colorObject.transform.SetParent(card.transform, false);
            RectTransform colorRect = colorObject.GetComponent<RectTransform>();
            colorRect.anchorMin = new Vector2(1f, 1f);
            colorRect.anchorMax = new Vector2(1f, 1f);
            colorRect.anchoredPosition = new Vector2(-30f, -28f);
            colorRect.sizeDelta = new Vector2(28f, 28f);
            Image colorImage = colorObject.GetComponent<Image>();
            colorImage.color = new Color(0.18f, 0.22f, 0.24f, 0.4f);
            colorImage.enabled = false;
            Outline colorOutline = colorObject.GetComponent<Outline>();
            colorOutline.effectColor = new Color(0.86f, 0.96f, 0.98f, 0.5f);
            colorOutline.effectDistance = new Vector2(1f, -1f);
            slotColorSwatches.Add(colorImage);

            Button colorButton = colorObject.GetComponent<Button>();
            colorButton.targetGraphic = colorImage;
            ColorBlock colorButtonColors = colorButton.colors;
            colorButtonColors.normalColor = Color.white;
            colorButtonColors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colorButtonColors.pressedColor = new Color(0.78f, 0.86f, 0.88f, 1f);
            colorButtonColors.disabledColor = new Color(0.52f, 0.52f, 0.52f, 0.78f);
            colorButton.colors = colorButtonColors;
            int colorSlotIndex = i;
            colorButton.onClick.AddListener(() => ToggleColorPickerFromSlot(colorSlotIndex));
            colorButton.interactable = false;
            slotColorButtons.Add(colorButton);

            Slider voiceSlider = CreateSlider(card.transform, 0f, 2f, 1f);
            voiceSlider.name = "PlayerVoiceVolumeSlider";
            RectTransform sliderRect = voiceSlider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0f);
            sliderRect.anchorMax = new Vector2(0.5f, 0f);
            sliderRect.anchoredPosition = new Vector2(0f, 24f);
            sliderRect.sizeDelta = new Vector2(220f, 12f);
            voiceSlider.gameObject.SetActive(false);

            int slotIndex = i;
            voiceSlider.onValueChanged.AddListener(value => ApplySlotVoiceVolume(slotIndex, value));
            slotVoiceSliders.Add(voiceSlider);
        }

        TMP_Text rosterTitle = CreateText(canvas.transform, "CrewRosterTitle", T("CREW ROSTER"), 24f, FontStyles.UpperCase);
        RectTransform rosterTitleRect = rosterTitle.GetComponent<RectTransform>();
        rosterTitleRect.anchorMin = new Vector2(0f, 0.5f);
        rosterTitleRect.anchorMax = new Vector2(0f, 0.5f);
        rosterTitleRect.anchoredPosition = new Vector2(314f, 280f);
        rosterTitleRect.sizeDelta = new Vector2(420f, 34f);
        rosterTitle.alignment = TextAlignmentOptions.Left;
        rosterTitle.color = new Color(0.58f, 0.84f, 0.92f, 0.95f);

        BuildColorPicker(canvas.transform);
        RefreshIntegratedVoiceControls();

        readyButton = CreateButton(canvas.transform, "ReadyButton", T("Ready"), 260f, 70f, 30f);
        RectTransform readyRect = readyButton.GetComponent<RectTransform>();
        readyRect.anchorMin = new Vector2(0.5f, 0f);
        readyRect.anchorMax = new Vector2(0.5f, 0f);
        readyRect.anchoredPosition = new Vector2(-300f, 76f);
        readyButton.onClick.AddListener(OnClickReady);

        startButton = CreateButton(canvas.transform, "StartGameButton", T("Start Game"), 260f, 70f, 30f);
        RectTransform startRect = startButton.GetComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.5f, 0f);
        startRect.anchorMax = new Vector2(0.5f, 0f);
        startRect.anchoredPosition = new Vector2(0f, 76f);
        startButton.onClick.AddListener(OnClickStartGame);

        Button backButton = CreateButton(canvas.transform, "BackButton", T("Back"), 200f, 70f, 30f);
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.anchoredPosition = new Vector2(270f, 76f);
        backButton.onClick.AddListener(OnClickBack);

        UpdateRoomTitleText();
        UpdateRoomCodeText();
    }

    private void BuildColorPicker(Transform parent)
    {
        Transform colorPanel = CreateInfoPanel(parent, "CrewColorPanel", new Vector2(1140f, -300f), new Vector2(270f, 320f));
        colorPickerPanelObject = colorPanel.gameObject;

        colorButtons.Clear();
        colorSwatches.Clear();
        colorLabels.Clear();

        for (int i = 0; i < PlayerColorPalette.ColorCount; i++)
        {
            int colorIndex = i;
            int column = i % 4;
            int row = i / 4;

            GameObject buttonObject = new GameObject("ColorButton_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            buttonObject.layer = parent.gameObject.layer;
            buttonObject.transform.SetParent(colorPanel, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.anchoredPosition = new Vector2(34f + column * 66f, -32f - row * 78f);
            buttonRect.sizeDelta = new Vector2(50f, 50f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.03f, 0.04f, 0.046f, 0.92f);

            Outline buttonOutline = buttonObject.GetComponent<Outline>();
            buttonOutline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.42f);
            buttonOutline.effectDistance = new Vector2(1.4f, -1.4f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.78f, 0.86f, 0.88f, 1f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.62f);
            button.colors = colors;
            button.onClick.AddListener(() => SelectPendingColor(colorIndex));

            GameObject swatchObject = new GameObject("Swatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            swatchObject.layer = buttonObject.layer;
            swatchObject.transform.SetParent(buttonObject.transform, false);
            RectTransform swatchRect = swatchObject.GetComponent<RectTransform>();
            swatchRect.anchorMin = Vector2.zero;
            swatchRect.anchorMax = Vector2.one;
            swatchRect.offsetMin = new Vector2(7f, 7f);
            swatchRect.offsetMax = new Vector2(-7f, -7f);
            Image swatchImage = swatchObject.GetComponent<Image>();
            swatchImage.color = PlayerColorPalette.GetColor(i);
            swatchImage.raycastTarget = false;

            TMP_Text label = CreateText(colorPanel, "ColorLabel_" + i, string.Empty, 13f, FontStyles.UpperCase);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(34f + column * 66f, -65f - row * 78f);
            labelRect.sizeDelta = new Vector2(54f, 18f);
            label.alignment = TextAlignmentOptions.Center;

            colorButtons.Add(button);
            colorSwatches.Add(swatchImage);
            colorLabels.Add(label);
        }

        colorConfirmButton = CreateButton(colorPanel, "ColorConfirmButton", T("Select"), 108f, 36f, 18f);
        RectTransform confirmRect = colorConfirmButton.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0f, 1f);
        confirmRect.anchorMax = new Vector2(0f, 1f);
        confirmRect.anchoredPosition = new Vector2(78f, -286f);
        colorConfirmButton.onClick.AddListener(ConfirmPendingColorSelection);

        colorCancelButton = CreateButton(colorPanel, "ColorCancelButton", T("Exit"), 108f, 36f, 18f);
        RectTransform cancelRect = colorCancelButton.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0f, 1f);
        cancelRect.anchorMax = new Vector2(0f, 1f);
        cancelRect.anchoredPosition = new Vector2(194f, -286f);
        colorCancelButton.onClick.AddListener(() => SetColorPickerVisible(false));

        RefreshColorPicker();
        SetColorPickerVisible(false);
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
        GameObject background = new GameObject("BackgroundImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        background.transform.SetParent(parent, false);

        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = background.GetComponent<Image>();
        Sprite backgroundSprite = lobbyBackgroundSprite != null ? lobbyBackgroundSprite : Resources.Load<Sprite>("Lobby/MainMenuBackground");
        if (backgroundSprite == null)
        {
            backgroundSprite = Resources.Load<Sprite>("Lobby/lobby_waiting_room_bg");
        }
        image.sprite = backgroundSprite;
        image.color = backgroundSprite != null ? Color.white : new Color(0.005f, 0.007f, 0.008f, 1f);
        image.preserveAspect = false;

        GameObject overlay = new GameObject("BackgroundDarkOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(parent, false);

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.26f);

        MainMenuBackgroundAnimator animator = background.AddComponent<MainMenuBackgroundAnimator>();
        animator.backgroundRect = rect;
        animator.darkOverlayImage = overlayImage;
        animator.darkOverlayMinAlpha = 0.2f;
        animator.darkOverlayMaxAlpha = 0.32f;

        GameObject sweep = new GameObject("BackgroundSweepLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sweep.transform.SetParent(parent, false);
        backgroundSweepLine = sweep.GetComponent<RectTransform>();
        backgroundSweepLine.anchorMin = new Vector2(0.5f, 0f);
        backgroundSweepLine.anchorMax = new Vector2(0.5f, 1f);
        backgroundSweepLine.sizeDelta = new Vector2(3f, 0f);
        backgroundSweepLine.anchoredPosition = new Vector2(-960f, 0f);

        Image sweepImage = sweep.GetComponent<Image>();
        sweepImage.color = new Color(0.44f, 0.86f, 1f, 0.18f);
    }

    private string GetPlayerVoiceVolumeKey(Player player)
    {
        if (player == null)
        {
            return "setting_voice_volume";
        }

        return "setting_voice_volume_actor_" + player.ActorNumber;
    }

    private void ApplySlotVoiceVolume(int slotIndex, float value)
    {
        if (!PhotonNetwork.InRoom || slotIndex < 0 || slotIndex >= PhotonNetwork.PlayerList.Length)
        {
            PlayerPrefs.SetFloat("setting_voice_volume", value);
            PlayerPrefs.Save();
            PlayerVoiceChat.ApplySavedVoiceVolumeToAll();
            return;
        }

        Player player = PhotonNetwork.PlayerList[slotIndex];

        if (player == null)
        {
            return;
        }

        PlayerPrefs.SetFloat(GetPlayerVoiceVolumeKey(player), value);
        PlayerPrefs.SetFloat("setting_voice_volume_client_" + Mathf.Max(0, player.ActorNumber - 1), value);

        if (player.IsLocal)
        {
            PlayerPrefs.SetFloat("setting_voice_volume", value);
        }

        PlayerPrefs.Save();
        PlayerVoiceChat.ApplySavedVoiceVolumeToAll();
    }

    private Transform CreateInfoPanel(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        panel.layer = parent.gameObject.layer;
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.01f, 0.014f, 0.016f, 0.62f);

        Outline outline = panel.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.38f);
        outline.effectDistance = new Vector2(2f, -2f);

        animatedPanels.Add(rect);
        animatedPanelBasePositions.Add(anchoredPosition);
        animatedPanelGroups.Add(panel.GetComponent<CanvasGroup>());
        return panel.transform;
    }

    private void ConfigurePanelTitle(TMP_Text title)
    {
        RectTransform rect = title.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(0f, -42f);
        rect.sizeDelta = new Vector2(-64f, 54f);
        title.alignment = TextAlignmentOptions.Left;
        title.color = new Color(1f, 0.8f, 0.42f, 1f);
    }

    private void SetNetworkStatus(string status)
    {
        if (networkStatusText != null)
        {
            networkStatusText.text = T(status);
        }

        Debug.Log(status);
    }

    private string T(string key)
    {
        if (key.StartsWith("JOINING ROOM "))
        {
            return T("JOINING ROOM") + " " + key.Substring("JOINING ROOM ".Length);
        }

        if (key.StartsWith("CREATING ROOM "))
        {
            return T("CREATING ROOM") + " " + key.Substring("CREATING ROOM ".Length);
        }

        if (key.StartsWith("CREATE FAILED: "))
        {
            return T("CREATE FAILED") + ": " + key.Substring("CREATE FAILED: ".Length);
        }

        if (key.StartsWith("DISCONNECTED: "))
        {
            return T("DISCONNECTED") + ": " + key.Substring("DISCONNECTED: ".Length);
        }

        if (key.StartsWith("ROOM NOT FOUND: "))
        {
            return T("ROOM NOT FOUND") + " " + key.Substring("ROOM NOT FOUND: ".Length);
        }

        switch (languageIndex)
        {
            case 1:
                return key;
            case 2:
                return TranslateJapanese(key);
            default:
                return TranslateKorean(key);
        }
    }

    private string TranslateRoomTitle(string title)
    {
        if (title == "Private Room" || title == "Public Room" || title == "ROOM LOBBY")
        {
            return T(title);
        }

        return title;
    }

    private string TranslateKorean(string key)
    {
        switch (key)
        {
            case "ROOM LOBBY": return "방 로비";
            case "ROOM CODE": return "방 코드";
            case "CREW ROSTER": return "대원 목록";
            case "CREW COLOR": return "대원 색상";
            case "SELECT COLOR": return "색상을 선택하세요";
            case "SELECTED": return "선택됨";
            case "TAKEN": return "사용 중";
            case "COLOR TAKEN": return "이미 선택된 색상";
            case "Private Room": return "비공개 방";
            case "Public Room": return "공개 방";
            case "PHOTON READY": return "포톤 준비됨";
            case "HOST ONLY": return "호스트만 가능";
            case "WAITING READY": return "준비 대기 중";
            case "PHOTON CONNECTED": return "포톤 연결됨";
            case "ROOM CREATED": return "방 생성됨";
            case "CREATE FAILED": return "방 생성 실패";
            case "ROOM NOT FOUND": return "방을 찾을 수 없음";
            case "HOST READY": return "호스트 준비됨";
            case "CONNECTED": return "연결됨";
            case "CONNECTING PHOTON": return "포톤 연결 중";
            case "JOINING ROOM": return "방 참가 중";
            case "CREATING ROOM": return "방 생성 중";
            case "DISCONNECTED": return "연결 끊김";
            case "YOU": return "나";
            case "HOST": return "호스트";
            case "PLAYER": return "플레이어";
            case "READY": return "준비";
            case "WAITING": return "대기";
            case "EMPTY": return "비어 있음";
            case "Ready": return "준비";
            case "Cancel Ready": return "준비 취소";
            case "Start Game": return "게임 시작";
            case "Back": return "뒤로";
            case "Select": return "선택";
            case "Exit": return "나가기";
            case "MISSION BRIEFING": return "임무 브리핑";
            case "FACILITY": return "시설";
            case "OBJECTIVE": return "목표";
            case "THREAT LEVEL": return "위험도";
            case "TEAM SIZE": return "팀 인원";
            case "STATUS": return "상태";
            case "INVESTIGATE SIGNAL": return "신호 조사";
            case "UNKNOWN": return "알 수 없음";
            case "WAITING FOR CREW": return "대원 대기 중";
            case "CONNECTING": return "연결 중";
            case "SYSTEM LOG": return "시스템 로그";
            case "VOICE SETTINGS": return "음성 설정";
            case "CREW VOICE": return "대원 음성";
            case "ADJUST EACH PLAYER": return "플레이어별 수신 음량을 조절합니다";
            case "Voice Volume": return "마이크 볼륨";
            case "TALKING": return "말함";
            case "MUTED": return "음소거";
            case "IDLE": return "대기";
            case "MIC MUTED": return "마이크 꺼짐";
            case "MIC OPEN": return "마이크 켜짐";
            case "SETTINGS": return "설정";
            case "Master Volume": return "전체 볼륨";
            case "Mouse Sensitivity X": return "마우스 감도 X";
            case "Mouse Sensitivity Y": return "마우스 감도 Y";
            case "Close": return "닫기";
            case "Room initialized": return "방 초기화됨";
            case "Voice channel standby": return "음성 채널 대기";
            case "Waiting for players": return "플레이어 대기 중";
            default: return key;
        }
    }

    private string TranslateJapanese(string key)
    {
        switch (key)
        {
            case "ROOM LOBBY": return "ルームロビー";
            case "ROOM CODE": return "ルームコード";
            case "CREW ROSTER": return "隊員リスト";
            case "CREW COLOR": return "隊員カラー";
            case "SELECT COLOR": return "色を選択";
            case "SELECTED": return "選択中";
            case "TAKEN": return "使用中";
            case "COLOR TAKEN": return "使用中の色";
            case "Private Room": return "プライベートルーム";
            case "Public Room": return "公開ルーム";
            case "PHOTON READY": return "Photon準備完了";
            case "HOST ONLY": return "ホストのみ";
            case "WAITING READY": return "準備待ち";
            case "PHOTON CONNECTED": return "Photon接続済み";
            case "ROOM CREATED": return "ルーム作成済み";
            case "ROOM NOT FOUND": return "ルームなし";
            case "HOST READY": return "ホスト準備完了";
            case "CONNECTED": return "接続済み";
            case "CONNECTING PHOTON": return "Photon接続中";
            case "JOINING ROOM": return "ルーム参加中";
            case "CREATING ROOM": return "ルーム作成中";
            case "CREATE FAILED": return "作成失敗";
            case "DISCONNECTED": return "切断";
            case "YOU": return "自分";
            case "HOST": return "ホスト";
            case "PLAYER": return "プレイヤー";
            case "READY": return "準備完了";
            case "WAITING": return "待機中";
            case "EMPTY": return "空き";
            case "Ready": return "準備";
            case "Cancel Ready": return "準備取消";
            case "Start Game": return "ゲーム開始";
            case "Back": return "戻る";
            case "Select": return "選択";
            case "Exit": return "閉じる";
            case "MISSION BRIEFING": return "任務ブリーフィング";
            case "FACILITY": return "施設";
            case "OBJECTIVE": return "目標";
            case "THREAT LEVEL": return "脅威度";
            case "TEAM SIZE": return "チーム人数";
            case "STATUS": return "状態";
            case "INVESTIGATE SIGNAL": return "信号を調査";
            case "UNKNOWN": return "不明";
            case "WAITING FOR CREW": return "クルー待機中";
            case "CONNECTING": return "接続中";
            case "SYSTEM LOG": return "システムログ";
            case "VOICE SETTINGS": return "ボイス設定";
            case "CREW VOICE": return "クルーボイス";
            case "ADJUST EACH PLAYER": return "プレイヤーごとの受信音量を調整";
            case "Voice Volume": return "マイク音量";
            case "TALKING": return "発話中";
            case "MUTED": return "ミュート";
            case "IDLE": return "待機";
            case "MIC MUTED": return "マイクOFF";
            case "MIC OPEN": return "マイクON";
            case "SETTINGS": return "設定";
            case "Master Volume": return "全体音量";
            case "Mouse Sensitivity X": return "マウス感度 X";
            case "Mouse Sensitivity Y": return "マウス感度 Y";
            case "Close": return "閉じる";
            case "Room initialized": return "ルーム初期化";
            case "Voice channel standby": return "ボイスチャンネル待機";
            case "Waiting for players": return "プレイヤー待機中";
            default: return key;
        }
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
        LocalizedTmpFontProvider.Apply(label);

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
        image.color = new Color(0.015f, 0.018f, 0.02f, 0.66f);
        image.type = Image.Type.Sliced;

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.42f);
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
        hover.normalBackgroundColor = new Color(0.015f, 0.018f, 0.02f, 0.66f);
        hover.hoverBackgroundColor = new Color(0.09f, 0.12f, 0.13f, 0.84f);
        hover.pressedBackgroundColor = new Color(0.16f, 0.18f, 0.17f, 0.86f);
        hover.normalTextColor = new Color(0.76f, 0.82f, 0.84f, 1f);
        hover.hoverTextColor = new Color(1f, 0.8f, 0.42f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        return button;
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value)
    {
        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(Image));
        sliderObject.layer = parent.gameObject.layer;
        sliderObject.transform.SetParent(parent, false);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        slider.direction = Slider.Direction.LeftToRight;

        Image background = sliderObject.GetComponent<Image>();
        background.color = new Color(0.62f, 0.78f, 0.86f, 0.28f);
        slider.targetGraphic = background;

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(220f, 12f);

        Image fill = CreateSliderImage(sliderObject.transform, "Fill", new Color(1f, 0.8f, 0.42f, 0.82f));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Image handle = CreateSliderImage(sliderObject.transform, "Handle", new Color(0.76f, 0.82f, 0.84f, 1f));
        handle.sprite = GetSettingSliderHandleSprite();
        handle.type = Image.Type.Simple;
        handle.preserveAspect = true;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(20f, 20f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        return slider;
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

    private Sprite GetMicOpenIconSprite()
    {
        if (micOpenIconSprite == null)
        {
            micOpenIconSprite = CreateMicIconSprite(false);
        }

        return micOpenIconSprite;
    }

    private Sprite GetMicMutedIconSprite()
    {
        if (micMutedIconSprite == null)
        {
            micMutedIconSprite = CreateMicIconSprite(true);
        }

        return micMutedIconSprite;
    }

    private Sprite CreateMicIconSprite(bool muted)
    {
        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color color = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        FillRect(texture, 18, 17, 12, 20, color);
        FillCircle(texture, 24, 17, 6, color);
        FillCircle(texture, 24, 37, 6, color);
        FillRect(texture, 13, 16, 4, 9, color);
        FillRect(texture, 31, 16, 4, 9, color);
        FillRect(texture, 21, 8, 6, 9, color);
        FillRect(texture, 16, 4, 16, 4, color);

        if (muted)
        {
            DrawThickLine(texture, new Vector2(10f, 40f), new Vector2(38f, 8f), 6f, clear);
            DrawThickLine(texture, new Vector2(10f, 40f), new Vector2(38f, 8f), 3f, color);
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateCircleSprite(int size, float radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color color = Color.white;
        float center = (size - 1) * 0.5f;
        float radiusSqr = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                texture.SetPixel(x, y, dx * dx + dy * dy <= radiusSqr ? color : clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
            {
                if (xx >= 0 && xx < texture.width && yy >= 0 && yy < texture.height)
                {
                    texture.SetPixel(xx, yy, color);
                }
            }
        }
    }

    private void FillCircle(Texture2D texture, int cx, int cy, int radius, Color color)
    {
        int radiusSqr = radius * radius;

        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int dx = x - cx;
                int dy = y - cy;

                if (dx * dx + dy * dy <= radiusSqr && x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private void DrawThickLine(Texture2D texture, Vector2 from, Vector2 to, float thickness, Color color)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * 2f);
        int radius = Mathf.CeilToInt(thickness * 0.5f);

        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
            FillCircle(texture, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), radius, color);
        }
    }

    private Image CreateSliderImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void AddAnimatedPanelIfFound(string objectName)
    {
        Transform target = FindUiTransform(objectName);
        if (target == null)
        {
            return;
        }

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
        {
            return;
        }

        animatedPanels.Add(rect);
        animatedPanelBasePositions.Add(rect.anchoredPosition);
        animatedPanelGroups.Add(target.GetComponent<CanvasGroup>());
    }

    private Button BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButton(objectName);
        if (button == null)
        {
            return null;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        return button;
    }

    private Transform FindUiTransform(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform target in transforms)
        {
            if (target.name == objectName && target.gameObject.scene == gameObject.scene)
            {
                return target;
            }
        }

        return null;
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

    private TMP_Text FindText(string objectName)
    {
        return FindUiTransform(objectName)?.GetComponent<TMP_Text>();
    }

    private TMP_Text FindText(Transform root, string objectName)
    {
        return FindDescendant(root, objectName)?.GetComponent<TMP_Text>();
    }

    private Image FindImage(Transform root, string objectName)
    {
        return FindDescendant(root, objectName)?.GetComponent<Image>();
    }

    private Button FindButton(string objectName)
    {
        return FindUiTransform(objectName)?.GetComponent<Button>();
    }

    private Button FindButton(Transform root, string objectName)
    {
        return FindDescendant(root, objectName)?.GetComponent<Button>();
    }

    private Slider FindSlider(Transform root, string objectName)
    {
        return FindDescendant(root, objectName)?.GetComponent<Slider>();
    }

    private RectTransform FindRectTransform(string objectName)
    {
        return FindUiTransform(objectName)?.GetComponent<RectTransform>();
    }
}
