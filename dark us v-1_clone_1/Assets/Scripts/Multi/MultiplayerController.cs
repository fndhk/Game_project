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
    [SerializeField] private GameObject lobbyPanel;  // ���� ȭ�� (�游���/����)
    [SerializeField] private GameObject roomPanel;   // ���� (�ڵ�ǥ��/����/������)
    [SerializeField] private GameObject loadingPanel; // "���� ��..." �޽��� �г�
    [SerializeField] private GameObject errorPanel;   // ���� �˾�
    [SerializeField] private GameObject backgroundPanel;

    [Header("UI References")]
    [SerializeField] private TMP_InputField codeInputField; // �� ��ȣ �Է�â
    [SerializeField] private TMP_Text roomCodeText;        // �� �� ��ȣ ǥ��
    [SerializeField] private TMP_Text errorText;           // ���� �޽��� ����
    [SerializeField] private Button startButton;           // ���� ���� ��ư
    [SerializeField] private Button leaveButton;           // �� ������ ��ư

    [Header("Settings")]
    [SerializeField] private int minPlayers = 2;

    private UnityTransport transport;
    private const string DEFAULT_IP = "127.0.0.1";

    private void Awake()
    {
        Instance = this;
        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // ��ǲ�ʵ� ����Ű �̺�Ʈ ����
        if (codeInputField != null)
        {
            codeInputField.onSubmit.AddListener(delegate { JoinRoomWithCode(); });
        }

        // �ʱ� ȭ�� ����
        ShowLobby();
    }

    // --- [�г� ���� �Լ�] ---
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
        backgroundPanel?.SetActive(false);
    }

    // --- [�� ����� - Host] ---
    public void CreateRoom()
    {
        ushort randomCode = (ushort)UnityEngine.Random.Range(10000, 99999);
        transport.ConnectionData.Port = randomCode;

        if (roomCodeText != null) roomCodeText.text = $"���ڵ�\n{randomCode}";

        if (NetworkManager.Singleton.StartHost())
        {
            ShowRoom();
            if (startButton != null) startButton.gameObject.SetActive(true);
            if (leaveButton != null) leaveButton.gameObject.SetActive(true);
        }
    }

    // --- [�� ���� - Client] ---
    public void JoinRoomWithCode()
    {
        // 1. �̹� ���� �õ� ������ üũ
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) return;

        // 2. �Է°� ��ȿ�� �˻�
        if (!ushort.TryParse(codeInputField.text, out ushort port))
        {
            ShowErrorPopup("���� 5�ڸ��� �Է����ּ���.");
            return;
        }

        // 3. �ε�â ����
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
            ShowErrorPopup("���� �õ��� ������ �� �����ϴ�.");
        }
    }

    // --- [���� ��� ��ư ����] ---
    public void CancelConnection()
    {
        StopAllCoroutines();
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();

        if (loadingPanel != null) loadingPanel.SetActive(false);
        ShowLobby();
    }

    // --- [�ο��� üũ] ---
    private void Update()
    {
        // ������ ��쿡�� ���� ��ư Ȱ��ȭ/��Ȱ��ȭ ����
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
                // ���� ����
                ShowRoom();
                if (startButton != null) startButton.gameObject.SetActive(false); // �����ڴ� ���۹�ưX
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        // Ÿ�Ӿƿ� �߻�
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            CancelConnection();
            ShowErrorPopup("������ ã�� �� �����ϴ�.");
        }
    }

    // --- [���� ó��] ---
    private void ShowErrorPopup(string message)
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(true);
            if (errorText != null) errorText.text = message;
        }
    }

    // ���� â�� 'Ȯ��' ��ư�� ����� �Լ�
    public void CloseErrorPopup()
    {
        if (errorPanel != null)
        {
            errorPanel.SetActive(false); // ���� �г� ��Ȱ��ȭ
        }

        // ������ Ȯ�������� �κ� �г��� Ȯ���� ���̵��� ó��
        ShowLobby();

        // ��ǲ�ʵ� �ʱ�ȭ (���� ����: �ٽ� �Է��ϱ� ���ϰ�)
        if (codeInputField != null)
        {
            codeInputField.text = "";
            codeInputField.ActivateInputField(); // �ٷ� Ÿ���� �����ϰ� ��Ŀ�� �ֱ�
        }
    }

    // --- [�� ������ - ���� �ʱ�ȭ] ---
    public void LeaveRoom()
    {
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();

        // ���� ���ΰ�ħ�Ͽ� ��� ��Ʈ��ũ ���¿� UI �ܻ��� �� ���� ����
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // --- [���� ���� - �� ��ȯ] ---
    public void OnStartButtonPressed()
    {
        // 1. ���� ����(Server)�� ���� �ѱ� ������ ����
        if (IsServer)
        {
            Debug.Log("���� ����! ��� �÷��̾ �̵���ŵ�ϴ�.");

            // 2. Netcode ���� �� �Ŵ����� ����Ͽ� ����ȭ�� �� ��ȯ ����
            // ���⼭ "GameScene"�� Build Settings�� ��ϵ� �̸��� ��Ȯ�� ���ƾ� ��
            var status = NetworkManager.Singleton.SceneManager.LoadScene(
                "labor",
                UnityEngine.SceneManagement.LoadSceneMode.Single
            );

            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"�� �ε� ����: {status}");
            }
        }
    }

    public void QuitGame()
    {
        Debug.Log("���� ���� �õ� ��...");

        // 1. ��Ʈ��ũ ������ �Ǿ� �ִٸ� ���� �����ϰ� ����
        if (NetworkManager.Singleton != null)
        {
            // ���� ����(Host)�̶�� ���� �����ϰ�, ������(Client)��� ���� �����ϴ�.
            NetworkManager.Singleton.Shutdown();
            Debug.Log("��Ʈ��ũ ������ ���������� �����߽��ϴ�.");
        }

        // 2. �÷����� ���� ���� ó��
        #if UNITY_EDITOR
    // ����Ƽ �����Ϳ��� �÷��� ��带 �������� ���� ȿ��
    UnityEditor.EditorApplication.isPlaying = false;
        #else
        // ���� ����� ���� ���α׷� ����
        Application.Quit();
        #endif
    }
}