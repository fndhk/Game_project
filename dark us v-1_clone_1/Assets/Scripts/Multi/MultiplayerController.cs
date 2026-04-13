using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;

public class MultiplayerController : NetworkBehaviour
{
    public static MultiplayerController Instance;

    [Header("Panels")]
    [SerializeField] private GameObject lobbyPanel;  // 시작 화면 (방만들기/입장)
    [SerializeField] private GameObject roomPanel;   // 대기실 (코드표시/시작/나가기)
    [SerializeField] private GameObject loadingPanel; // "연결 중..." 메시지 패널
    [SerializeField] private GameObject errorPanel;   // 에러 팝업

    [Header("UI References")]
    [SerializeField] private TMP_InputField codeInputField; // 방 번호 입력창
    [SerializeField] private TMP_Text roomCodeText;        // 내 방 번호 표시
    [SerializeField] private TMP_Text errorText;           // 에러 메시지 내용
    [SerializeField] private Button startButton;           // 게임 시작 버튼
    [SerializeField] private Button leaveButton;           // 방 나가기 버튼

    [Header("Settings")]
    [SerializeField] private int minPlayers = 2;

    private UnityTransport transport;
    private const string DEFAULT_IP = "127.0.0.1";

    private void Awake()
    {
        Instance = this;
        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // 인풋필드 엔터키 이벤트 연결
        if (codeInputField != null)
        {
            codeInputField.onSubmit.AddListener(delegate { JoinRoomWithCode(); });
        }

        // 초기 화면 설정
        ShowLobby();
    }

    // --- [패널 제어 함수] ---
    private void ShowLobby()
    {
        lobbyPanel?.SetActive(true);
        roomPanel?.SetActive(false);
        loadingPanel?.SetActive(false);
        errorPanel?.SetActive(false);
    }

    private void ShowRoom()
    {
        lobbyPanel?.SetActive(false);
        roomPanel?.SetActive(true);
        loadingPanel?.SetActive(false);
    }

    // --- [방 만들기 - Host] ---
    public void CreateRoom()
    {
        ushort randomCode = (ushort)UnityEngine.Random.Range(10000, 50000);
        transport.ConnectionData.Port = randomCode;

        if (roomCodeText != null) roomCodeText.text = $"방 코드: {randomCode}";

        if (NetworkManager.Singleton.StartHost())
        {
            ShowRoom();
            if (startButton != null) startButton.gameObject.SetActive(true);
            if (leaveButton != null) leaveButton.gameObject.SetActive(true);
        }
    }

    // --- [방 입장 - Client] ---
    public void JoinRoomWithCode()
    {
        // 1. 이미 연결 시도 중인지 체크
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) return;

        // 2. 입력값 유효성 검사
        if (!ushort.TryParse(codeInputField.text, out ushort port))
        {
            ShowErrorPopup("숫자 5자리를 입력해주세요.");
            return;
        }

        // 3. 로딩창 띄우기
        if (loadingPanel != null) loadingPanel.SetActive(true);

        transport.ConnectionData.Address = DEFAULT_IP;
        transport.ConnectionData.Port = port;

        if (NetworkManager.Singleton.StartClient())
        {
            StopAllCoroutines();
            StartCoroutine(CheckConnectionTimeout());
        }
        else
        {
            CancelConnection();
            ShowErrorPopup("연결 시도를 시작할 수 없습니다.");
        }
    }

    // --- [연결 취소 버튼 로직] ---
    public void CancelConnection()
    {
        StopAllCoroutines();
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();

        if (loadingPanel != null) loadingPanel.SetActive(false);
        ShowLobby();
    }

    // --- [인원수 체크] ---
    private void Update()
    {
        // 방장인 경우에만 시작 버튼 활성화/비활성화 제어
        if (IsServer && startButton != null && startButton.gameObject.activeSelf)
        {
            int count = NetworkManager.Singleton.ConnectedClientsList.Count;
            startButton.interactable = (count >= minPlayers);
        }
    }

    private IEnumerator CheckConnectionTimeout()
    {
        float timeout = 5f;
        float timer = 0f;

        while (timer < timeout)
        {
            if (NetworkManager.Singleton.IsConnectedClient)
            {
                // 접속 성공
                ShowRoom();
                if (startButton != null) startButton.gameObject.SetActive(false); // 참여자는 시작버튼X
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // 타임아웃 발생
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            CancelConnection();
            ShowErrorPopup("서버를 찾을 수 없습니다.");
        }
    }

    // --- [에러 처리] ---
    private void ShowErrorPopup(string message)
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(true);
            if (errorText != null) errorText.text = message;
        }
    }

    // 에러 창의 '확인' 버튼에 연결된 함수
    public void CloseErrorPopup()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false); // 에러 패널 비활성화
        }

        // 에러를 확인했으니 로비 패널이 확실히 보이도록 처리
        ShowLobby();

        // 인풋필드 초기화 (선택 사항: 다시 입력하기 편하게)
        if (codeInputField != null)
        {
            codeInputField.text = "";
            codeInputField.ActivateInputField(); // 바로 타이핑 가능하게 포커스 주기
        }
    }

    // --- [방 나가기 - 완전 초기화] ---
    public void LeaveRoom()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();

        // 씬을 새로고침하여 모든 네트워크 상태와 UI 잔상을 한 번에 제거
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // --- [게임 시작 - 씬 전환] ---
    public void OnStartButtonPressed()
    {
        // 1. 오직 방장(Server)만 씬을 넘길 권한이 있음
        if (IsServer)
        {
            Debug.Log("게임 시작! 모든 플레이어를 이동시킵니다.");

            // 2. Netcode 전용 씬 매니저를 사용하여 동기화된 씬 전환 실행
            // 여기서 "GameScene"은 Build Settings에 등록된 이름과 정확히 같아야 함
            var status = NetworkManager.Singleton.SceneManager.LoadScene(
                "GameScene",
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );

            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"씬 로드 실패: {status}");
            }
        }
    }
}