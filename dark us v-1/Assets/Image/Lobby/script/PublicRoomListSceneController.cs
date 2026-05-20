using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class PublicRoomListSceneController : MonoBehaviourPunCallbacks
{
    public string mainMenuSceneName = "LobbyScene";
    public string roomLobbySceneName = "CreateRoomLobbyScene";
    public bool buildInEditMode = true;

    private const string RoomCodePrefsKey = "dark_us_room_code";
    private const string RoomHostPrefsKey = "dark_us_room_is_host";
    private const string RoomVisiblePrefsKey = "dark_us_room_is_visible";
    private const string RoomTitlePrefsKey = "dark_us_room_title";
    private const string RoomTitlePropertyKey = "title";

    private readonly Dictionary<string, RoomInfo> publicRooms = new Dictionary<string, RoomInfo>();
    private Transform roomListContent;
    private TMP_Text emptyText;
    private TMP_Text statusText;
    private GameObject createRoomDialog;
    private TMP_InputField roomTitleInput;
    private int languageIndex;

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
            BuildUi();
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
        EnsureEventSystem();
        EnsurePublicRoomListUi();
        ConnectToPhotonLobby();
    }

    public void OnClickCreateRoom()
    {
        if (createRoomDialog != null)
        {
            createRoomDialog.SetActive(true);
        }

        if (roomTitleInput != null)
        {
            roomTitleInput.text = string.Empty;
            EventSystem.current.SetSelectedGameObject(roomTitleInput.gameObject);
        }
    }

    public void OnClickConfirmCreateRoom()
    {
        string roomCode = Random.Range(0, 10000).ToString("0000");
        string roomTitle = roomTitleInput != null ? roomTitleInput.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(roomTitle))
        {
            roomTitle = "Public Room";
        }

        PlayerPrefs.SetString(RoomCodePrefsKey, roomCode);
        PlayerPrefs.SetString(RoomTitlePrefsKey, roomTitle);
        PlayerPrefs.SetInt(RoomHostPrefsKey, 1);
        PlayerPrefs.SetInt(RoomVisiblePrefsKey, 1);
        PlayerPrefs.Save();
        LoadScene(roomLobbySceneName);
    }

    public void OnClickCancelCreateRoom()
    {
        if (createRoomDialog != null)
        {
            createRoomDialog.SetActive(false);
        }
    }

    public void OnClickBack()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.LeaveLobby();
        }

        LoadScene(mainMenuSceneName);
    }

    public override void OnConnectedToMaster()
    {
        SetStatus("JOINING LOBBY");
        PhotonNetwork.JoinLobby(TypedLobby.Default);
    }

    public override void OnJoinedLobby()
    {
        SetStatus("PUBLIC ROOMS");
        RebuildRoomList();
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

        RebuildRoomList();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SetStatus("DISCONNECTED");
    }

    public override void OnLeftRoom()
    {
        ConnectToPhotonLobby();
    }

    private void ConnectToPhotonLobby()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.InRoom)
        {
            SetStatus("LEAVING ROOM");
            PhotonNetwork.LeaveRoom();
            return;
        }

        if (PhotonNetwork.InLobby)
        {
            SetStatus("PUBLIC ROOMS");
            RebuildRoomList();
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            SetStatus("JOINING LOBBY");
            PhotonNetwork.JoinLobby(TypedLobby.Default);
            return;
        }

        SetStatus("CONNECTING PHOTON");
        PhotonNetwork.ConnectUsingSettings();
    }

    private void JoinPublicRoom(string roomName)
    {
        RoomInfo room = publicRooms.ContainsKey(roomName) ? publicRooms[roomName] : null;
        string roomTitle = GetRoomTitle(room);
        PlayerPrefs.SetString(RoomCodePrefsKey, roomName);
        PlayerPrefs.SetString(RoomTitlePrefsKey, roomTitle);
        PlayerPrefs.SetInt(RoomHostPrefsKey, 0);
        PlayerPrefs.SetInt(RoomVisiblePrefsKey, 1);
        PlayerPrefs.Save();
        LoadScene(roomLobbySceneName);
    }

    private void RebuildRoomList()
    {
        if (roomListContent == null)
        {
            return;
        }

        for (int i = roomListContent.childCount - 1; i >= 0; i--)
        {
            Transform child = roomListContent.GetChild(i);
            if (emptyText != null && child == emptyText.transform)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        bool hasRoom = false;
        int roomIndex = 1;
        foreach (RoomInfo room in publicRooms.Values)
        {
            if (!room.IsOpen || !room.IsVisible || !IsValidRoomCode(room.Name) || room.PlayerCount >= room.MaxPlayers)
            {
                continue;
            }

            hasRoom = true;
            CreateRoomRow(room, roomIndex);
            roomIndex++;
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(!hasRoom);
        }
    }

    private void CreateRoomRow(RoomInfo room, int index)
    {
        string roomTitle = GetRoomTitle(room);
        Button button = CreateButton(roomListContent, "Room_" + room.Name, string.Empty, 980f, 70f, 26f);
        LayoutElement layout = button.GetComponent<LayoutElement>();
        layout.preferredWidth = 980f;
        layout.preferredHeight = 70f;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = string.Empty;
        }

        CreateRoomRowText(button.transform, "IndexText", index.ToString(), 34f, 90f, TextAlignmentOptions.MidlineLeft);
        CreateRoomRowText(button.transform, "TitleText", roomTitle, 150f, 620f, TextAlignmentOptions.MidlineLeft);
        CreateRoomRowText(button.transform, "PlayerCountText", room.PlayerCount + " / " + room.MaxPlayers, 790f, 156f, TextAlignmentOptions.MidlineRight);

        string roomName = room.Name;
        button.onClick.AddListener(() => JoinPublicRoom(roomName));
    }

    private void CreateRoomRowText(Transform parent, string objectName, string value, float x, float width, TextAlignmentOptions alignment)
    {
        TMP_Text text = CreateText(parent, objectName, value, 26f, FontStyles.Normal);
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width, 0f);
        text.alignment = alignment;
    }

    private string GetRoomTitle(RoomInfo room)
    {
        if (room != null &&
            room.CustomProperties != null &&
            room.CustomProperties.TryGetValue(RoomTitlePropertyKey, out object value) &&
            value is string title &&
            !string.IsNullOrWhiteSpace(title))
        {
            return TranslateRoomTitle(title);
        }

        return T("Public Room");
    }

    private string TranslateRoomTitle(string title)
    {
        if (title == "Public Room" || title == "Private Room")
        {
            return T(title);
        }

        return title;
    }

    private void BuildUi()
    {
        Canvas canvas = CreateCanvas();
        CreateBackground(canvas.transform);

        TMP_Text title = CreateText(canvas.transform, "TitleText", T("PUBLIC GAME"), 64f, FontStyles.Normal);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -110f);
        titleRect.sizeDelta = new Vector2(-160f, 90f);
        title.color = new Color(1f, 0.8f, 0.42f, 1f);

        statusText = CreateText(canvas.transform, "StatusText", T("CONNECTING"), 24f, FontStyles.UpperCase);
        RectTransform statusRect = statusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -175f);
        statusRect.sizeDelta = new Vector2(-160f, 42f);
        statusText.color = new Color(0.62f, 0.7f, 0.72f, 1f);

        GameObject listPanel = new GameObject("RoomListPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
        listPanel.layer = canvas.gameObject.layer;
        listPanel.transform.SetParent(canvas.transform, false);

        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0.5f, 0.5f);
        listRect.anchorMax = new Vector2(0.5f, 0.5f);
        listRect.anchoredPosition = new Vector2(0f, -10f);
        listRect.sizeDelta = new Vector2(1180f, 610f);

        Image listImage = listPanel.GetComponent<Image>();
        listImage.color = new Color(0.015f, 0.018f, 0.02f, 0.74f);

        Outline outline = listPanel.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.34f);
        outline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup layout = listPanel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(36, 36, 36, 36);
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        roomListContent = listPanel.transform;

        emptyText = CreateText(roomListContent, "EmptyText", T("No public rooms found."), 30f, FontStyles.Normal);
        RectTransform emptyRect = emptyText.GetComponent<RectTransform>();
        emptyRect.sizeDelta = new Vector2(0f, 80f);
        emptyText.color = new Color(0.62f, 0.7f, 0.72f, 1f);

        Button backButton = CreateButton(canvas.transform, "BackButton", T("Back"), 240f, 66f, 28f);
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 0f);
        backRect.anchoredPosition = new Vector2(180f, 82f);
        backButton.onClick.AddListener(OnClickBack);

        Button createButton = CreateButton(canvas.transform, "CreateRoomButton", T("Create Room"), 280f, 66f, 28f);
        RectTransform createRect = createButton.GetComponent<RectTransform>();
        createRect.anchorMin = new Vector2(1f, 0f);
        createRect.anchorMax = new Vector2(1f, 0f);
        createRect.anchoredPosition = new Vector2(-220f, 82f);
        createButton.onClick.AddListener(OnClickCreateRoom);

        createRoomDialog = CreateCreateRoomDialog(canvas.transform);
        createRoomDialog.SetActive(false);
    }

    private void EnsurePublicRoomListUi()
    {
        if (FindUiTransform("Canvas") != null && FindUiTransform("RoomListPanel") != null)
        {
            BindExistingPublicRoomListUi();
            MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);
            return;
        }

        BuildUi();
        MenuButtonHoverEffect.EnsureOnAllSceneButtons(gameObject.scene);
    }

    private void BindExistingPublicRoomListUi()
    {
        roomListContent = FindUiTransform("RoomListPanel");
        emptyText = FindUiTransform("EmptyText")?.GetComponent<TMP_Text>();
        statusText = FindUiTransform("StatusText")?.GetComponent<TMP_Text>();
        createRoomDialog = FindUiTransform("CreateRoomDialog")?.gameObject;
        roomTitleInput = FindUiTransform("RoomTitleInput")?.GetComponent<TMP_InputField>();

        BindButton("BackButton", OnClickBack);
        BindButton("CreateRoomButton", OnClickCreateRoom);
        BindButton("ConfirmCreateRoomButton", OnClickConfirmCreateRoom);
        BindButton("CancelCreateRoomButton", OnClickCancelCreateRoom);

        if (createRoomDialog != null)
        {
            createRoomDialog.SetActive(false);
        }
    }

    private GameObject CreateCreateRoomDialog(Transform parent)
    {
        GameObject overlay = new GameObject("CreateRoomDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.layer = parent.gameObject.layer;
        overlay.transform.SetParent(parent, false);

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlay.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.58f);

        GameObject dialog = new GameObject("Dialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        dialog.layer = parent.gameObject.layer;
        dialog.transform.SetParent(overlay.transform, false);

        RectTransform dialogRect = dialog.GetComponent<RectTransform>();
        dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
        dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRect.anchoredPosition = Vector2.zero;
        dialogRect.sizeDelta = new Vector2(680f, 360f);

        Image dialogImage = dialog.GetComponent<Image>();
        dialogImage.color = new Color(0.015f, 0.018f, 0.02f, 0.96f);

        Outline outline = dialog.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.4f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text title = CreateText(dialog.transform, "TitleText", T("Create Room"), 42f, FontStyles.Normal);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -62f);
        titleRect.sizeDelta = new Vector2(-80f, 62f);
        title.color = new Color(1f, 0.8f, 0.42f, 1f);

        TMP_Text label = CreateText(dialog.transform, "RoomTitleLabel", T("Room Title"), 24f, FontStyles.Normal);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 42f);
        labelRect.sizeDelta = new Vector2(420f, 40f);
        label.color = new Color(0.62f, 0.7f, 0.72f, 1f);

        roomTitleInput = CreateTextInput(dialog.transform, "RoomTitleInput", T("Public Room"));

        Button confirmButton = CreateButton(dialog.transform, "ConfirmCreateRoomButton", T("Create"), 220f, 62f, 26f);
        RectTransform confirmRect = confirmButton.GetComponent<RectTransform>();
        confirmRect.anchorMin = new Vector2(0.5f, 0f);
        confirmRect.anchorMax = new Vector2(0.5f, 0f);
        confirmRect.anchoredPosition = new Vector2(-124f, 72f);
        confirmButton.onClick.AddListener(OnClickConfirmCreateRoom);

        Button cancelButton = CreateButton(dialog.transform, "CancelCreateRoomButton", T("Cancel"), 220f, 62f, 26f);
        RectTransform cancelRect = cancelButton.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.5f, 0f);
        cancelRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRect.anchoredPosition = new Vector2(124f, 72f);
        cancelButton.onClick.AddListener(OnClickCancelCreateRoom);

        return overlay;
    }

    private TMP_InputField CreateTextInput(Transform parent, string objectName, string placeholderText)
    {
        GameObject inputObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField), typeof(Outline));
        inputObject.layer = parent.gameObject.layer;
        inputObject.transform.SetParent(parent, false);

        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.anchoredPosition = new Vector2(0f, -18f);
        inputRect.sizeDelta = new Vector2(440f, 58f);

        Image inputImage = inputObject.GetComponent<Image>();
        inputImage.color = new Color(0.015f, 0.018f, 0.02f, 0.78f);

        Outline outline = inputObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 0.86f, 0.36f);
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text text = CreateText(inputObject.transform, "Text Area", string.Empty, 26f, FontStyles.Normal);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 6f);
        textRect.offsetMax = new Vector2(-20f, -6f);
        text.alignment = TextAlignmentOptions.MidlineLeft;

        TMP_Text placeholder = CreateText(inputObject.transform, "Placeholder", placeholderText, 26f, FontStyles.Normal);
        RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(20f, 6f);
        placeholderRect.offsetMax = new Vector2(-20f, -6f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        placeholder.color = new Color(0.62f, 0.7f, 0.72f, 0.58f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 24;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.targetGraphic = inputImage;

        return input;
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
        LocalizedTmpFontProvider.Apply(label);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(520f, 42f);

        return label;
    }

    private Button CreateButton(Transform parent, string objectName, string label, float width, float height, float fontSize)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(MenuButtonHoverEffect));
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

        TMP_Text labelText = CreateText(buttonObject.transform, "Text (TMP)", label, fontSize, FontStyles.Normal);
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

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        return button;
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

    private void SetStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = T(status);
        }

        Debug.Log(status);
    }

    private string T(string key)
    {
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

    private string TranslateKorean(string key)
    {
        switch (key)
        {
            case "PUBLIC GAME": return "공개 게임";
            case "PUBLIC ROOMS": return "공개 방 목록";
            case "CONNECTING": return "연결 중";
            case "JOINING LOBBY": return "로비 참가 중";
            case "DISCONNECTED": return "연결 끊김";
            case "LEAVING ROOM": return "방 나가는 중";
            case "CONNECTING PHOTON": return "포톤 연결 중";
            case "No public rooms found.": return "공개 방이 없습니다.";
            case "Back": return "뒤로";
            case "Create Room": return "방 만들기";
            case "Room Title": return "방 제목";
            case "Public Room": return "공개 방";
            case "Private Room": return "비공개 방";
            case "Create": return "생성";
            case "Cancel": return "취소";
            default: return key;
        }
    }

    private string TranslateJapanese(string key)
    {
        switch (key)
        {
            case "PUBLIC GAME": return "公開ゲーム";
            case "PUBLIC ROOMS": return "公開ルーム一覧";
            case "CONNECTING": return "接続中";
            case "JOINING LOBBY": return "ロビー参加中";
            case "DISCONNECTED": return "切断";
            case "LEAVING ROOM": return "ルーム退出中";
            case "CONNECTING PHOTON": return "Photon接続中";
            case "No public rooms found.": return "公開ルームがありません。";
            case "Back": return "戻る";
            case "Create Room": return "ルーム作成";
            case "Room Title": return "ルーム名";
            case "Public Room": return "公開ルーム";
            case "Private Room": return "プライベートルーム";
            case "Create": return "作成";
            case "Cancel": return "キャンセル";
            default: return key;
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
        UiEventSystemUtility.EnsureSingle(gameObject);
    }

    private Button BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindUiTransform(objectName)?.GetComponent<Button>();
        if (button == null)
        {
            return null;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
        MenuButtonHoverEffect.EnsureOn(button);
        return button;
    }

    private Transform FindUiTransform(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (Transform target in transforms)
        {
            if (target.name == objectName && target.gameObject.scene == gameObject.scene)
            {
                return target;
            }
        }

        return null;
    }
}
