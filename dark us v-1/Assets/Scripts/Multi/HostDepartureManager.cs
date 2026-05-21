using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HostDepartureManager : MonoBehaviourPunCallbacks
{
    private const string MainMenuSceneName = "LobbyScene";

    private static HostDepartureManager instance;
    private static bool sceneHooked;
    private static bool isForcingRoomExit;
    private static bool sceneLoadRequested;

    public static bool IsForcingRoomExit => isForcingRoomExit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
        EnsureSceneHooked();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        HostDepartureManager existing = UnityEngine.Object.FindAnyObjectByType<HostDepartureManager>();
        if (existing != null)
        {
            instance = existing;
            UnityEngine.Object.DontDestroyOnLoad(existing.gameObject);
            return;
        }

        GameObject root = new GameObject("Host Departure Manager");
        UnityEngine.Object.DontDestroyOnLoad(root);
        instance = root.AddComponent<HostDepartureManager>();
    }

    private static void EnsureSceneHooked()
    {
        if (sceneHooked)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        sceneHooked = true;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!GameplayStartupGate.IsMenuScene(scene.name))
        {
            return;
        }

        isForcingRoomExit = false;
        sceneLoadRequested = false;
        GameplayStartupGate.ResetAll();
        DarkScanLoadingScreen.ForceHideImmediate();
        MenuCursorState.UnlockCursor();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            UnityEngine.Object.Destroy(gameObject);
            return;
        }

        instance = this;
        UnityEngine.Object.DontDestroyOnLoad(gameObject);
        EnsureSceneHooked();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!Application.isPlaying || !PhotonNetwork.InRoom || isForcingRoomExit)
        {
            return;
        }

        isForcingRoomExit = true;
        sceneLoadRequested = false;
        GameplayStartupGate.ResetAll();
        GameplayStartupGate.SetHostDepartureBlocked(true);
        DarkScanLoadingScreen.ForceHideImmediate();

        Debug.LogWarning("[HostDepartureManager] Host left the room. Leaving room for all remaining clients.");
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        if (!isForcingRoomExit)
        {
            return;
        }

        PhotonNetwork.AutomaticallySyncScene = true;
        LoadMainMenuOnce();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (isForcingRoomExit)
        {
            LoadMainMenuOnce();
        }
    }

    private static void LoadMainMenuOnce()
    {
        if (sceneLoadRequested)
        {
            return;
        }

        sceneLoadRequested = true;
        GameplayStartupGate.ResetAll();
        GameplayStartupGate.SetHostDepartureBlocked(true);
        DarkScanLoadingScreen.ForceHideImmediate();
        MenuCursorState.UnlockCursor();

        if (SceneManager.GetActiveScene().name == MainMenuSceneName && SceneManager.sceneCount == 1)
        {
            isForcingRoomExit = false;
            sceneLoadRequested = false;
            GameplayStartupGate.ResetAll();
            return;
        }

        SceneManager.LoadScene(MainMenuSceneName);
    }
}
