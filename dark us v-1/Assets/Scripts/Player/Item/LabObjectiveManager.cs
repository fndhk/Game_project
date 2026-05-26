using UnityEngine;
using TMPro;
using UnityEngine.UI;

// 연구소 목표 흐름 종류이다.
public enum LabObjectiveFlow
{
    AccessCore,
    ComputerRestore
}

// 연구소 탈출 목표 흐름을 관리한다.
// 기존 Access Core 방식과 새 Computer 복구 방식을 모두 지원한다.
public class LabObjectiveManager : MonoBehaviour
{
    public static LabObjectiveManager Instance { get; private set; }

    [Header("Objective Flow")]
    // 현재 사용할 목표 방식이다.
    public LabObjectiveFlow objectiveFlow = LabObjectiveFlow.ComputerRestore;

    [Header("Access Core Rules")]
    // 탈출구를 열기 위해 필요한 Access Core 총 개수이다.
    public int requiredCoreCount = 4;

    [Header("Computer Rules")]
    // 탈출구를 열기 위해 복구해야 하는 컴퓨터 수이다.
    public int requiredComputerCount = 4;

    [Header("Optional HUD")]
    // 직접 연결하면 이 텍스트를 자동으로 갱신한다.
    public TMP_Text objectiveText;

    // 직접 연결하면 현재 상호작용 안내 문구를 표시한다.
    public TMP_Text promptText;

    [Header("Access Core Debug")]
    // 현재 플레이어가 들고 있는 Access Core 개수이다.
    [SerializeField] private int carriedCoreCount = 0;

    // Security Terminal에 이미 삽입된 Access Core 개수이다.
    [SerializeField] private int installedCoreCount = 0;

    [Header("Computer Debug")]
    // 현재 목표로 선택된 컴퓨터 목록이다.
    [SerializeField] private ObjectiveComputer[] selectedObjectiveComputers = new ObjectiveComputer[0];

    // 복구 완료된 컴퓨터 개수이다.
    [SerializeField] private int restoredComputerCount = 0;

    [Header("Exit Debug")]
    // 탈출구가 열렸는지 저장한다.
    [SerializeField] private bool exitUnlocked = false;

    // 현재 등록된 탈출구이다.
    private EmergencyExitDoor registeredExitDoor;
    private bool promptStylePrepared = false;

    public int RequiredCoreCount => Mathf.Max(1, requiredCoreCount);
    public int CarriedCoreCount => carriedCoreCount;
    public int InstalledCoreCount => installedCoreCount;
    public int RequiredComputerCount => Mathf.Max(1, requiredComputerCount);
    public int RestoredComputerCount => restoredComputerCount;
    public bool ExitUnlocked => exitUnlocked;

    // 새로 생성된 맵 기준으로 Access Core 목표 진행도를 초기화한다.
    public void ResetObjectiveState(int requiredCount)
    {
        objectiveFlow = LabObjectiveFlow.AccessCore;
        requiredCoreCount = Mathf.Max(1, requiredCount);
        carriedCoreCount = 0;
        installedCoreCount = 0;
        exitUnlocked = false;
        RefreshHud();
    }

    // 새로 생성된 맵 기준으로 컴퓨터 목표를 설정한다.
    public void SetupComputerObjectives(ObjectiveComputer[] selectedComputers, int requiredCount)
    {
        objectiveFlow = LabObjectiveFlow.ComputerRestore;
        requiredComputerCount = Mathf.Max(1, requiredCount);
        selectedObjectiveComputers = selectedComputers != null ? selectedComputers : new ObjectiveComputer[0];
        exitUnlocked = false;

        RecountRestoredComputers();

        if (restoredComputerCount >= RequiredComputerCount)
        {
            UnlockExit();
        }
        else
        {
            RefreshHud();
        }
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
        GameLoopManager.EnsureExists();
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

    // 컴퓨터 하나가 복구 완료되었을 때 호출된다.
    public void CompleteComputer(ObjectiveComputer computer)
    {
        if (computer == null)
        {
            return;
        }

        if (exitUnlocked)
        {
            RefreshHud();
            return;
        }

        objectiveFlow = LabObjectiveFlow.ComputerRestore;
        RecountRestoredComputers();

        if (restoredComputerCount >= RequiredComputerCount)
        {
            UnlockExit();
            return;
        }

        RefreshHud();
    }

    // 선택된 목표 컴퓨터 중 복구 완료된 개수를 다시 계산한다.
    private void RecountRestoredComputers()
    {
        restoredComputerCount = 0;

        if (selectedObjectiveComputers == null)
        {
            return;
        }

        for (int i = 0; i < selectedObjectiveComputers.Length; i++)
        {
            if (selectedObjectiveComputers[i] != null && selectedObjectiveComputers[i].IsRestored)
            {
                restoredComputerCount++;
            }
        }
    }

    // 컴퓨터 복구/방해 상태가 외부에서 바뀐 뒤 HUD와 탈출 조건을 다시 계산한다.
    public void RefreshComputerObjectiveState()
    {
        if (objectiveFlow != LabObjectiveFlow.ComputerRestore)
        {
            RefreshHud();
            return;
        }

        RecountRestoredComputers();

        if (!exitUnlocked && restoredComputerCount >= RequiredComputerCount)
        {
            UnlockExit();
            return;
        }

        RefreshHud();
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
        if (objectiveFlow == LabObjectiveFlow.ComputerRestore)
        {
            return GetComputerHudObjectiveText();
        }

        return GetAccessCoreHudObjectiveText();
    }

    // HUD 진행률 바에 사용할 목표 진행도이다.
    public float GetHudObjectiveProgress01()
    {
        if (exitUnlocked)
        {
            return 1f;
        }

        if (objectiveFlow == LabObjectiveFlow.ComputerRestore)
        {
            return Mathf.Clamp01(restoredComputerCount / (float)RequiredComputerCount);
        }

        return Mathf.Clamp01(installedCoreCount / (float)RequiredCoreCount);
    }

    // HUD 목표 아래에 표시할 상세 진행 문구이다.
    public string GetHudObjectiveDetailText()
    {
        if (exitUnlocked)
        {
            return T("Reach The Exit");
        }

        if (objectiveFlow == LabObjectiveFlow.ComputerRestore)
        {
            return T("Progress") + " " + restoredComputerCount + "/" + RequiredComputerCount + " (" + Mathf.RoundToInt(GetHudObjectiveProgress01() * 100f) + "%)";
        }

        if (carriedCoreCount > 0)
        {
            return T("Progress") + " " + installedCoreCount + "/" + RequiredCoreCount + "  /  " + T("Carrying") + " " + carriedCoreCount;
        }

        return T("Progress") + " " + installedCoreCount + "/" + RequiredCoreCount;
    }

    // 컴퓨터 복구 방식의 HUD 문구를 반환한다.
    private string GetComputerHudObjectiveText()
    {
        if (exitUnlocked)
        {
            return T("Exit Open");
        }

        return T("Find Target Computers") + " " + restoredComputerCount + "/" + RequiredComputerCount;
    }

    // Access Core 방식의 HUD 문구를 반환한다.
    private string GetAccessCoreHudObjectiveText()
    {
        if (exitUnlocked)
        {
            return T("Exit Open");
        }

        if (installedCoreCount <= 0 && carriedCoreCount <= 0)
        {
            return T("Find Access Cores") + " 0/" + RequiredCoreCount;
        }

        if (installedCoreCount < RequiredCoreCount)
        {
            return T("Install Access Cores") + " " + installedCoreCount + "/" + RequiredCoreCount;
        }

        return T("Reach The Exit");
    }

    // Terminal에서 보여줄 진행 문구를 반환한다.
    public string GetTerminalProgressText()
    {
        return installedCoreCount + "/" + RequiredCoreCount;
    }

    // 컴퓨터 진행 문구를 반환한다.
    public string GetComputerProgressText()
    {
        return restoredComputerCount + "/" + RequiredComputerCount;
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

        PreparePromptStyle();
        promptText.text = message;
        promptText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    private void PreparePromptStyle()
    {
        if (promptStylePrepared || promptText == null)
        {
            return;
        }

        promptStylePrepared = true;
        promptText.color = new Color(0.94f, 0.98f, 1f, 0.98f);
        promptText.fontSize = Mathf.Max(promptText.fontSize, 22f);
        promptText.enableAutoSizing = true;
        promptText.fontSizeMin = 14f;
        promptText.fontSizeMax = Mathf.Max(promptText.fontSize, 24f);
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.raycastTarget = false;

        if (promptText.GetComponent<Shadow>() == null)
        {
            Shadow shadow = promptText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.6f, -1.6f);
        }
    }

    private string T(string key)
    {
        return InGameLocalization.Text(key);
    }
}
