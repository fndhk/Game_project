using UnityEngine;
using TMPro;

// 연구소 탈출 목표 흐름을 관리한다.
// 흐름: Access Core 획득 -> Security Terminal에 삽입 -> Exit Door 잠금 해제.
public class LabObjectiveManager : MonoBehaviour
{
    public static LabObjectiveManager Instance { get; private set; }

    [Header("Objective Rules")]
    // 탈출구를 열기 위해 필요한 Access Core 총 개수이다.
    public int requiredCoreCount = 4;

    [Header("Optional HUD")]
    // 직접 연결하면 이 텍스트를 자동으로 갱신한다.
    public TMP_Text objectiveText;

    // 직접 연결하면 현재 상호작용 안내 문구를 표시한다.
    public TMP_Text promptText;

    [Header("Debug")]
    // 현재 플레이어가 들고 있는 Access Core 개수이다.
    [SerializeField] private int carriedCoreCount = 0;

    // Security Terminal에 이미 삽입된 Access Core 개수이다.
    [SerializeField] private int installedCoreCount = 0;

    // 탈출구가 열렸는지 저장한다.
    [SerializeField] private bool exitUnlocked = false;

    // 현재 등록된 탈출구이다.
    private EmergencyExitDoor registeredExitDoor;

    public int RequiredCoreCount => Mathf.Max(1, requiredCoreCount);
    public int CarriedCoreCount => carriedCoreCount;
    public int InstalledCoreCount => installedCoreCount;
    public bool ExitUnlocked => exitUnlocked;

    // 새로 생성된 맵 기준으로 목표 진행도를 초기화한다.
    public void ResetObjectiveState(int requiredCount)
    {
        requiredCoreCount = Mathf.Max(1, requiredCount);
        carriedCoreCount = 0;
        installedCoreCount = 0;
        exitUnlocked = false;
        registeredExitDoor = null;
        RefreshHud();
    }

    // 싱글톤 참조를 준비한다.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 시작 시 HUD를 한 번 갱신한다.
    private void Start()
    {
        RefreshHud();
    }

    // Access Core를 획득했을 때 호출된다.
    public void CollectCore(int amount)
    {
        carriedCoreCount += Mathf.Max(1, amount);
        RefreshHud();
    }

    // Terminal에 Access Core를 1개 삽입한다.
    public bool TryInstallOneCore()
    {
        if (exitUnlocked)
        {
            return false;
        }

        if (carriedCoreCount <= 0)
        {
            return false;
        }

        carriedCoreCount--;
        installedCoreCount = Mathf.Clamp(installedCoreCount + 1, 0, RequiredCoreCount);

        if (installedCoreCount >= RequiredCoreCount)
        {
            UnlockExit();
        }

        RefreshHud();
        return true;
    }

    // Terminal에 들고 있는 Access Core를 전부 삽입한다.
    public int InstallAllCarriedCores()
    {
        int installedNow = 0;

        while (carriedCoreCount > 0 && installedCoreCount < RequiredCoreCount)
        {
            if (!TryInstallOneCore())
            {
                break;
            }

            installedNow++;
        }

        RefreshHud();
        return installedNow;
    }

    // 탈출구를 등록한다.
    public void RegisterExitDoor(EmergencyExitDoor exitDoor)
    {
        if (exitDoor == null)
        {
            return;
        }

        registeredExitDoor = exitDoor;

        if (exitUnlocked)
        {
            registeredExitDoor.UnlockDoor();
        }
    }

    // 탈출구 잠금을 해제한다.
    public void UnlockExit()
    {
        if (exitUnlocked)
        {
            return;
        }

        exitUnlocked = true;

        if (registeredExitDoor != null)
        {
            registeredExitDoor.UnlockDoor();
        }

        RefreshHud();
    }

    // HUD 목표 문구를 반환한다.
    public string GetHudObjectiveText()
    {
        if (exitUnlocked)
        {
            return "Exit Unlocked";
        }

        if (installedCoreCount <= 0 && carriedCoreCount <= 0)
        {
            return "Find Access Cores 0/" + RequiredCoreCount;
        }

        if (installedCoreCount < RequiredCoreCount)
        {
            return "Install Access Cores " + installedCoreCount + "/" + RequiredCoreCount;
        }

        return "Reach The Exit";
    }

    // Terminal에서 보여줄 진행 문구를 반환한다.
    public string GetTerminalProgressText()
    {
        return installedCoreCount + "/" + RequiredCoreCount;
    }

    // HUD 텍스트를 갱신한다.
    public void RefreshHud()
    {
        if (objectiveText != null)
        {
            objectiveText.text = GetHudObjectiveText();
        }
    }

    // 상호작용 안내 문구를 갱신한다.
    public void SetPromptText(string message)
    {
        if (promptText == null)
        {
            return;
        }

        promptText.text = message;
        promptText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }
}
