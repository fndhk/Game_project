using UnityEngine;

// Access Core를 삽입해서 탈출구를 여는 장치이다.
public class SecurityTerminal : MonoBehaviour, IPlayerInteractable
{
    [Header("Terminal")]
    // 한 번 E를 눌렀을 때 들고 있는 코어를 전부 넣을지 정한다.
    public bool installAllCarriedCoresAtOnce = true;

    // 탈출구가 열려도 계속 문구를 보여줄지 정한다.
    public bool showPromptAfterUnlocked = true;

    // 상호작용 문구를 반환한다.
    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        LabObjectiveManager manager = LabObjectiveManager.Instance;

        if (manager == null)
        {
            return "[E] Use Terminal";
        }

        if (manager.ExitUnlocked)
        {
            return showPromptAfterUnlocked ? "Exit Power Restored" : string.Empty;
        }

        if (manager.CarriedCoreCount <= 0)
        {
            return "Need Access Core " + manager.GetTerminalProgressText();
        }

        return "[E] Insert Access Core " + manager.GetTerminalProgressText();
    }

    // 탈출구가 이미 열렸더라도 안내를 보여줄 수 있다.
    public bool CanInteract(PlayerObjectiveInteractor interactor)
    {
        LabObjectiveManager manager = LabObjectiveManager.Instance;

        if (manager == null)
        {
            return true;
        }

        if (manager.ExitUnlocked)
        {
            return showPromptAfterUnlocked;
        }

        return true;
    }

    // Access Core를 Terminal에 삽입한다.
    public void Interact(PlayerObjectiveInteractor interactor)
    {
        LabObjectiveManager manager = LabObjectiveManager.Instance;

        if (manager == null || manager.ExitUnlocked)
        {
            return;
        }

        if (installAllCarriedCoresAtOnce)
        {
            manager.InstallAllCarriedCores();
        }
        else
        {
            manager.TryInstallOneCore();
        }
    }
}
