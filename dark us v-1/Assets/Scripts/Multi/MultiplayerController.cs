using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiplayerController : MonoBehaviourPunCallbacks
{
    public static MultiplayerController Instance;

    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private GameObject backgroundPanel;

    [Header("UI References")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    [Header("Settings")]
    [SerializeField] private int minPlayers = 4;
    [SerializeField] private int maxPlayers = 12;
    [SerializeField] private string gameSceneName = "labor";

    private string pendingRoomCode;
    private bool pendingCreateRoom;
    private bool pendingJoinRoom;

    private void Awake()
    {
        Instance = this;
        PhotonConnectionDefaults.Apply();
        PhotonNetwork.AutomaticallySyncScene = true;

        if (codeInputField != null)
        {
            codeInputField.onSubmit.AddListener(delegate { JoinRoomWithCode(); });
        }

        ShowLobby();
    }

    private void ShowLobby()
    {
        SetPanelActive(lobbyPanel, true);
        SetPanelActive(roomPanel, false);
        SetPanelActive(loadingPanel, false);
        SetPanelActive(errorPanel, false);
        SetPanelActive(backgroundPanel, true);
    }

    private void ShowRoom()
    {
        SetPanelActive(lobbyPanel, false);
        SetPanelActive(roomPanel, true);
        SetPanelActive(loadingPanel, false);
        SetPanelActive(errorPanel, false);
        SetPanelActive(backgroundPanel, false);

        if (startButton != null)
        {
            startButton.gameObject.SetActive(PhotonNetwork.IsMasterClient);
        }

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(true);
        }
    }

    public void CreateRoom()
    {
        pendingRoomCode = Random.Range(0, 10000).ToString("0000");
        pendingCreateRoom = true;
        pendingJoinRoom = false;

        if (roomCodeText != null)
        {
            roomCodeText.text = "방 코드\n" + pendingRoomCode;
        }

        SetPanelActive(loadingPanel, true);
        ExecuteOrConnect();
    }

    public void JoinRoomWithCode()
    {
        if (PhotonNetwork.InRoom)
        {
            return;
        }

        string input = codeInputField != null ? codeInputField.text.Trim() : string.Empty;
        if (!IsValidRoomCode(input))
        {
            ShowErrorPopup("4자리 방 코드를 입력하세요.");
            return;
        }

        pendingRoomCode = input;
        pendingCreateRoom = false;
        pendingJoinRoom = true;

        SetPanelActive(loadingPanel, true);
        ExecuteOrConnect();
    }

    public void CancelConnection()
    {
        pendingCreateRoom = false;
        pendingJoinRoom = false;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        SetPanelActive(loadingPanel, false);
        ShowLobby();
    }

    private void Update()
    {
        if (startButton != null && startButton.gameObject.activeSelf)
        {
            int count = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
            startButton.interactable = PhotonNetwork.IsMasterClient && count >= minPlayers;
        }
    }

    private void ExecuteOrConnect()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (PhotonNetwork.IsConnectedAndReady)
        {
            ExecutePendingRoomAction();
            return;
        }

        PhotonNetwork.ConnectUsingSettings();
    }

    private void ExecutePendingRoomAction()
    {
        if (pendingCreateRoom)
        {
            RoomOptions options = new RoomOptions
            {
                MaxPlayers = Mathf.Clamp(maxPlayers, minPlayers, 20),
                IsOpen = true,
                IsVisible = false,
                CleanupCacheOnLeave = true,
                CustomRoomProperties = new Hashtable
                {
                    { "mapSeed", Random.Range(1, int.MaxValue) }
                },
                CustomRoomPropertiesForLobby = new[] { "mapSeed" }
            };

            PhotonNetwork.CreateRoom(pendingRoomCode, options, TypedLobby.Default);
            return;
        }

        if (pendingJoinRoom)
        {
            PhotonNetwork.JoinRoom(pendingRoomCode);
        }
    }

    public override void OnConnectedToMaster()
    {
        ExecutePendingRoomAction();
    }

    public override void OnCreatedRoom()
    {
        ShowRoom();
    }

    public override void OnJoinedRoom()
    {
        if (roomCodeText != null)
        {
            roomCodeText.text = "방 코드\n" + PhotonNetwork.CurrentRoom.Name;
        }

        ShowRoom();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        ShowErrorPopup("방 생성 실패: " + message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        ShowErrorPopup("방을 찾을 수 없습니다: " + pendingRoomCode);
    }

    public override void OnLeftRoom()
    {
        if (HostDepartureManager.IsForcingRoomExit)
        {
            SceneManager.LoadScene("LobbyScene");
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ShowErrorPopup(string message)
    {
        SetPanelActive(loadingPanel, false);

        if (errorPanel != null)
        {
            errorPanel.SetActive(true);
            if (errorText != null)
            {
                errorText.text = message;
            }
        }
    }

    public void CloseErrorPopup()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false);
        }

        ShowLobby();

        if (codeInputField != null)
        {
            codeInputField.text = "";
            codeInputField.ActivateInputField();
        }
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnStartButtonPressed()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount < minPlayers)
        {
            ShowErrorPopup("최소 4명 이상 모여야 시작할 수 있습니다.");
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        RoleAssignmentManager.EnsurePhotonImposterActors();
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    public void QuitGame()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
