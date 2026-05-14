using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoomLobbySceneController : MonoBehaviourPunCallbacks
{
    public string mainMenuSceneName = "LobbyScene 1";
    public string publicRoomListSceneName = "PublicRoomListScene";
    public string gameSceneName = "labor";
    public Sprite lobbyBackgroundSprite;

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
    private readonly List<RectTransform> animatedPanels = new List<RectTransform>();
    private readonly List<Vector2> animatedPanelBasePositions = new List<Vector2>();
    private readonly List<CanvasGroup> animatedPanelGroups = new List<CanvasGroup>();
    private TMP_Text networkStatusText;
    private TMP_Text roomTitleText;
    private TMP_Text roomCodeText;
    private RectTransform backgroundSweepLine;
    private Sprite micOpenIconSprite;
    private Sprite micMutedIconSprite;
    private PlayerVoiceChat lobbyVoiceChat;
    private Button readyButton;
    private Button startButton;
    private string pendingRoomCode;
    private bool pendingCreateRoom;
    private int createRetryCount;
    private int languageIndex;
    private bool isStartingGame;
    private float uiStartedAt;
    private float nextVoicePanelRefreshTime;

    private void Start()
    {
        PhotonConnectionDefaults.Apply();
        MenuCursorState.UnlockCursor();
        languageIndex = PlayerPrefs.GetInt("setting_language", 0);
        uiStartedAt = Time.unscaledTime;
        EnsureEventSystem();
        EnsureLobbyVoiceChat();
        BuildRoomLobbyUi();
        StartPhotonRoomFlow();
    }

    private void Update()
    {
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

        StartCoroutine(StartGameWithLoading());
    }

    private System.Collections.IEnumerator StartGameWithLoading()
    {
        isStartingGame = true;
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        EnsureMapSeedProperty();
        RoleAssignmentManager.EnsurePhotonImposterActor();
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { StartSignalPropertyKey, true }
        });
        PhotonNetwork.SendAllOutgoingCommands();
        SetNetworkStatus("STARTING");

        DarkScanLoadingScreen.ShowImmediate("MATCH LOCKED...");
        yield return null;

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
        UpdateRoomCodeText();
        SetNetworkStatus(PhotonNetwork.IsMasterClient ? "HOST READY" : "CONNECTED");
        RefreshPlayerSlots();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerSlots();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerSlots();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        RefreshPlayerSlots();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps != null && changedProps.ContainsKey(ReadyPropertyKey))
        {
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

            Image cardImage = slotTexts[i].transform.parent != null ? slotTexts[i].transform.parent.GetComponent<Image>() : null;
            if (cardImage != null)
            {
                cardImage.color = cardColor;
            }
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(PhotonNetwork.IsMasterClient || pendingCreateRoom);
            startButton.interactable = PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && AreAllPlayersReady();
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
            slotRect.offsetMin = new Vector2(22f, 44f);
            slotRect.offsetMax = new Vector2(-54f, -18f);
            slotText.alignment = TextAlignmentOptions.TopLeft;
            slotText.color = new Color(0.78f, 0.86f, 0.88f, 1f);
            slotTexts.Add(slotText);

            GameObject micObject = new GameObject("MicStatusIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            micObject.layer = card.layer;
            micObject.transform.SetParent(card.transform, false);
            RectTransform micRect = micObject.GetComponent<RectTransform>();
            micRect.anchorMin = new Vector2(1f, 1f);
            micRect.anchorMax = new Vector2(1f, 1f);
            micRect.anchoredPosition = new Vector2(-26f, -24f);
            micRect.sizeDelta = new Vector2(24f, 24f);
            Image micImage = micObject.GetComponent<Image>();
            micImage.sprite = GetMicOpenIconSprite();
            micImage.color = new Color(0.70f, 0.82f, 0.86f, 0.88f);
            micImage.enabled = false;
            slotMicIcons.Add(micImage);

            Slider voiceSlider = CreateSlider(card.transform, 0f, 2f, 1f);
            voiceSlider.name = "PlayerVoiceVolumeSlider";
            RectTransform sliderRect = voiceSlider.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0f);
            sliderRect.anchoredPosition = new Vector2(0f, 18f);
            sliderRect.sizeDelta = new Vector2(-44f, 22f);
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
        Sprite backgroundSprite = lobbyBackgroundSprite != null ? lobbyBackgroundSprite : Resources.Load<Sprite>("Lobby/lobby_waiting_room_bg");
        image.sprite = backgroundSprite;
        image.color = backgroundSprite != null ? new Color(0.72f, 0.82f, 0.86f, 0.72f) : new Color(0.005f, 0.007f, 0.008f, 1f);
        image.preserveAspect = false;

        GameObject overlay = new GameObject("BackgroundDarkOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(parent, false);

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.58f);

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
        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.layer = parent.gameObject.layer;
        sliderObject.transform.SetParent(parent, false);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);

        Image background = CreateSliderImage(sliderObject.transform, "Background", new Color(0.18f, 0.25f, 0.27f, 0.9f));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 8f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.layer = parent.gameObject.layer;
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
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

        FillRect(texture, 18, 7, 12, 24, color);
        FillCircle(texture, 24, 8, 6, color);
        FillCircle(texture, 24, 30, 6, color);
        FillRect(texture, 14, 25, 4, 8, color);
        FillRect(texture, 30, 25, 4, 8, color);
        FillRect(texture, 21, 33, 6, 7, color);
        FillRect(texture, 16, 40, 16, 4, color);

        if (muted)
        {
            DrawThickLine(texture, new Vector2(10f, 8f), new Vector2(38f, 40f), 6f, clear);
            DrawThickLine(texture, new Vector2(10f, 8f), new Vector2(38f, 40f), 3f, color);
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
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
}
