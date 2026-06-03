using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayStartupGate
{
    private static bool loadingScreenBlocked;
    private static bool mapGenerationBlocked;
    private static bool roleRevealBlocked;
    private static bool hostDepartureBlocked;
    private static bool victoryScreenBlocked;
    private static bool sceneHooked;

    public static bool IsBlocked =>
        loadingScreenBlocked ||
        mapGenerationBlocked ||
        roleRevealBlocked ||
        hostDepartureBlocked ||
        victoryScreenBlocked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        ResetAll();
        EnsureSceneHooked();
    }

    public static void SetLoadingScreenBlocked(bool blocked)
    {
        loadingScreenBlocked = blocked;
    }

    public static void SetMapGenerationBlocked(bool blocked)
    {
        mapGenerationBlocked = blocked;
    }

    public static void SetRoleRevealBlocked(bool blocked)
    {
        roleRevealBlocked = blocked;
    }

    public static void SetHostDepartureBlocked(bool blocked)
    {
        hostDepartureBlocked = blocked;
    }

    public static void SetVictoryScreenBlocked(bool blocked)
    {
        victoryScreenBlocked = blocked;
    }

    public static void ResetAll()
    {
        loadingScreenBlocked = false;
        mapGenerationBlocked = false;
        roleRevealBlocked = false;
        hostDepartureBlocked = false;
        victoryScreenBlocked = false;
    }

    public static bool IsMenuScene(string sceneName)
    {
        return sceneName == "LobbyScene" ||
               sceneName == "LobbyScene 1" ||
               sceneName == "CreateRoomLobbyScene" ||
               sceneName == "PublicRoomListScene";
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
        if (IsMenuScene(scene.name))
        {
            ResetAll();
        }
    }
}
