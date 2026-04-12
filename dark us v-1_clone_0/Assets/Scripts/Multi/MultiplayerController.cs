using UnityEngine;
using Unity.Netcode;

public class MultiplayerController : NetworkBehaviour
{
    public static MultiplayerController Instance;

    [Header("Network Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private int minPlayers = 2;

    private NetworkVariable<int> currentConnectedPlayers = new NetworkVariable<int>(0);

    private void Awake() => Instance = this;

    public void StartHost() => NetworkManager.Singleton.StartHost();
    public void StartClient() => NetworkManager.Singleton.StartClient();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // 서버에 클라이언트가 접속할 때마다 실행될 이벤트 등록
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientJoined;
        }
    }

    private void OnClientJoined(ulong clientId)
    {
        if (!IsServer) return;

        // 1. 현재 접속 인원 갱신 (자동으로 모든 클라이언트에게 동기화됨)
        currentConnectedPlayers.Value = NetworkManager.Singleton.ConnectedClientsList.Count;

        // 2. 인원 체크 및 게임 시작 로직 실행
        if (currentConnectedPlayers.Value >= minPlayers)
        {
            Debug.Log($"[Server] {minPlayers}명 접속 완료! 게임을 시작합니다.");
            // 여기에 실제 게임 시작 코드(예: Scene 전환)를 넣습니다.
        }
    }

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 200));

        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host (Server + Player)")) StartHost();
            if (GUILayout.Button("Client (Join)")) StartClient();
        }
        else
        {
            GUILayout.Label($"Role: {(IsHost ? "Host" : "Client")}");
            // currentConnectedPlayers.Value를 사용하여 화면에 표시
            GUILayout.Label($"Players: {currentConnectedPlayers.Value} / 12");
        }

        GUILayout.EndArea();
    }
}