using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MultiplayerController : NetworkBehaviour
{
    public static MultiplayerController Instance;

    [Header("Network Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private int minPlayers = 2;

    [Header("UI References")]
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_Text statusText;

    private NetworkVariable<int> currentConnectedPlayers = new NetworkVariable<int>(0);

    private void Awake() => Instance = this;

    private void Start()
    {
        // 초기 버튼 상태: 비활성화
        if (startButton != null)
        {
            startButton.interactable = false;
            startButton.onClick.AddListener(OnStartButtonPressed);
        }
    }

    public void StartHost() => NetworkManager.Singleton.StartHost();
    public void StartClient() => NetworkManager.Singleton.StartClient();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientJoined;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientLeft;
        }
    }

    private void OnClientJoined(ulong clientId) => UpdatePlayerCount();
    private void OnClientLeft(ulong clientId) => UpdatePlayerCount();

    private void UpdatePlayerCount()
    {
        if (!IsServer) return;

        int count = NetworkManager.Singleton.ConnectedClientsList.Count;
        currentConnectedPlayers.Value = count;

        // [핵심 로직] 최소 인원 충족 시 버튼 활성화 (서버/방장만 가능)
        if (startButton != null)
        {
            startButton.interactable = (count >= minPlayers);
        }
    }

    // 버튼을 눌렀을 때 실행될 함수
    private void OnStartButtonPressed()
    {
        if (IsServer)
        {
            Debug.Log("게임 시작 버튼 클릭! 모든 클라이언트 씬 전환 시작.");
            StartGame();
        }
    }

    private void StartGame()
    {
        // Netcode의 SceneManager를 사용하여 모든 인원을 동시에 이동시킴
        // "MainGameScene"은 본인의 실제 게임 씬 이름으로 바꿔야 합니다.
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(10, 150, 250, 200));
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host (Server)")) StartHost();
            if (GUILayout.Button("Client (Join)")) StartClient();
        }
        else
        {
            GUILayout.Label($"현 인원: {currentConnectedPlayers.Value} / 최소 필요: {minPlayers}");
            if (IsHost && currentConnectedPlayers.Value < minPlayers)
            {
                GUILayout.Label("<color=yellow>인원이 부족합니다.</color>");
            }
        }
        GUILayout.EndArea();
    }

    public void LeaveRoom()
    {
        if (NetworkManager.Singleton != null)
        {
            // 1. 네트워크 연결 종료
            // 서버(Host)가 호출하면 모든 클라이언트가 튕겨나가고, 클라이언트는 본인만 나감
            NetworkManager.Singleton.Shutdown();

            // 2. 메인 로비 씬으로 이동
            // "LobbyScene"은 본인의 시작 씬 이름으로 변경하세요.
            SceneManager.LoadScene("LobbyScene");

            Debug.Log("방에서 나갔습니다. 로비로 이동합니다.");
        }
    }
}