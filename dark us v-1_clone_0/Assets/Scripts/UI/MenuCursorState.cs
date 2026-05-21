using UnityEngine;
using UnityEngine.SceneManagement;

// 메뉴 계열 씬으로 돌아왔을 때 인게임에서 잠긴 커서를 확실히 해제한다.
public class MenuCursorState : MonoBehaviour
{
    private static MenuCursorState instance;

    private static readonly string[] MenuSceneNames =
    {
        "LobbyScene",
        "SettingsScene",
        "CreateRoomLobbyScene",
        "PublicRoomListScene"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsMenuScene(scene.name))
        {
            return;
        }

        UnlockCursor();
    }

    private static void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (IsMenuScene(nextScene.name))
        {
            UnlockCursor();
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject cursorObject = new GameObject("MenuCursorState");
        instance = cursorObject.AddComponent<MenuCursorState>();
        DontDestroyOnLoad(cursorObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (IsMenuScene(SceneManager.GetActiveScene().name))
        {
            UnlockCursor();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && IsMenuScene(SceneManager.GetActiveScene().name))
        {
            UnlockCursor();
        }
    }

    public static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static bool IsMenuScene(string sceneName)
    {
        for (int i = 0; i < MenuSceneNames.Length; i++)
        {
            if (sceneName == MenuSceneNames[i])
            {
                return true;
            }
        }

        return false;
    }
}
