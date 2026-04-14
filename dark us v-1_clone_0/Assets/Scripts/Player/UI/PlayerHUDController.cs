using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 이 스크립트는 플레이어 HUD를 관리한다.
// 체력바, 스태미너바, 직업 텍스트, 설정 패널 표시를 담당한다.
public class PlayerHUDController : MonoBehaviour
{
    [Header("플레이어 참조")]
    // 값을 읽어올 플레이어의 스탯이다.
    public PlayerStats targetStats;

    // 플레이어 역할을 읽어올 컴포넌트이다.
    public PlayerCombatTarget targetCombatTarget;

    [Header("왼쪽 바")]
    // 체력바 슬라이더이다.
    public Slider healthBar;

    // 스태미너바 슬라이더이다.
    public Slider staminaBar;

    [Header("직업 UI")]
    // 직업을 표시할 텍스트이다.
    public TMP_Text roleText;

    [Header("설정 UI")]
    // 설정 버튼이다.
    public Button settingsButton;

    // 열고 닫을 설정 패널이다.
    public GameObject settingsPanel;

    [Header("인벤토리 UI")]
    // 인벤토리 패널이다.
    public GameObject inventoryPanel;

    // 시작할 때 UI 기본 상태를 맞춘다.
    private void Start()
    {
        // 바 범위를 0~1로 맞춘다.
        if (healthBar != null)
        {
            healthBar.minValue = 0f;
            healthBar.maxValue = 1f;
        }

        if (staminaBar != null)
        {
            staminaBar.minValue = 0f;
            staminaBar.maxValue = 1f;
        }

        // 설정 패널은 시작 시 닫아둔다.
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // 설정 버튼 클릭 이벤트를 연결한다.
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(ToggleSettingsPanel);
        }
    }

    // 매 프레임 HUD 값을 갱신한다.
    private void Update()
    {
        UpdateBars();
        UpdateRoleText();
        HandleInventoryToggle();
    }

    // 체력바와 스태미너바를 각각 따로 갱신한다.
    private void UpdateBars()
    {
        // 대상 스탯이 없으면 종료한다.
        if (targetStats == null)
        {
            return;
        }

        // 체력 비율을 가져온다.
        float healthNormalized = targetStats.GetHealthNormalized();

        // 스테미나 비율을 가져온다.
        float staminaNormalized = targetStats.GetStaminaNormalized();

        // 체력바를 갱신한다.
        if (healthBar != null)
        {
            healthBar.value = healthNormalized;
        }

        // 스태미나바를 갱신한다.
        if (staminaBar != null)
        {
            staminaBar.value = staminaNormalized;
        }
    }

    // 현재 역할을 텍스트로 표시한다.
    private void UpdateRoleText()
    {
        // 대상 역할 정보나 텍스트가 없으면 종료한다.
        if (targetCombatTarget == null || roleText == null)
        {
            return;
        }

        // 현재 역할 이름을 그대로 표시한다.
        roleText.text = targetCombatTarget.role.ToString();
    }

    // 인벤토리 패널 토글을 처리한다.
    private void HandleInventoryToggle()
    {
        // 인벤토리 패널이 없으면 종료한다.
        if (inventoryPanel == null)
        {
            return;
        }

        // Tab 키를 누르면 인벤토리 패널을 켜고 끈다.
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            inventoryPanel.SetActive(!inventoryPanel.activeSelf);
        }
    }

    // 설정 패널을 켜고 끄는 함수이다.
    public void ToggleSettingsPanel()
    {
        // 설정 패널이 없으면 종료한다.
        if (settingsPanel == null)
        {
            return;
        }

        // 현재 상태를 반대로 바꾼다.
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}