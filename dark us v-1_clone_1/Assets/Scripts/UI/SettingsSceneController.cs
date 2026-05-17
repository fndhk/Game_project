using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class SettingsSceneController : MonoBehaviour
{
    public string mainMenuSceneName = "LobbyScene 1";
    public SettingsUIController settingsPanel;

    private bool returningToMainMenu;

    private void Start()
    {
        MenuCursorState.UnlockCursor();
        SettingsPanelLauncher.DestroyInstance();
        EnsureEventSystem();

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
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
