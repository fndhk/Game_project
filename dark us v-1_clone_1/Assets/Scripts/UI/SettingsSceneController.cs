using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SettingsSceneController : MonoBehaviour
{
    private const string LegacyMainMenuSceneName = "LobbyScene 1";
    private const string MainMenuSceneFallback = "LobbyScene";
    private const string MainMenuScenePath = "Assets/Scenes/LobbyScene.unity";

    public string mainMenuSceneName = "LobbyScene";
    public SettingsUIController settingsPanel;

    private bool returningToMainMenu;

    private void Start()
    {
        MenuCursorState.UnlockCursor();
        SettingsPanelLauncher.DestroyInstance();
        EnsureEventSystem();
        NormalizeSceneNames();

        if (settingsPanel == null)
        {
            settingsPanel = FindAnyObjectByType<SettingsUIController>();
        }

        if (settingsPanel == null)
        {
            Debug.LogWarning("SettingsPanel is not found in SettingsScene. Add a SettingsPanel object with SettingsUIController.");
            return;
        }

        returningToMainMenu = false;
        settingsPanel.Closed -= ReturnToMainMenu;
        settingsPanel.Closed += ReturnToMainMenu;
        settingsPanel.Show();
    }

    private void OnDestroy()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Closed -= ReturnToMainMenu;
        }
    }

    private void ReturnToMainMenu()
    {
        if (returningToMainMenu)
        {
            return;
        }

        returningToMainMenu = true;
        SceneManager.LoadScene(GetMainMenuSceneName());
    }

    private void NormalizeSceneNames()
    {
        if (mainMenuSceneName == LegacyMainMenuSceneName)
        {
            mainMenuSceneName = MainMenuSceneFallback;
        }
    }

    private string GetMainMenuSceneName()
    {
        NormalizeSceneNames();
        return SceneUtility.GetBuildIndexByScenePath(MainMenuScenePath) >= 0
            ? MainMenuSceneFallback
            : mainMenuSceneName;
    }

    private void EnsureEventSystem()
    {
        UiEventSystemUtility.EnsureSingle(gameObject);
    }
}
