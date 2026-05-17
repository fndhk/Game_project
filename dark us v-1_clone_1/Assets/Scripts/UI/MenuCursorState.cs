using UnityEngine;
using UnityEngine.SceneManagement;

// 메뉴 계열 씬으로 돌아왔을 때 인게임에서 잠긴 커서를 확실히 해제한다.
public class MenuCursorState : MonoBehaviour
{
    private static readonly string[] MenuSceneNames =
    {
        "LobbyScene",
        "LobbyScene 1",
        "SettingsScene",
        "CreateRoomLobbyScene",
        "PublicRoomListScene"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsMenuScene(scene.name))
        {
            return;
        }

        UnlockCursor();
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
