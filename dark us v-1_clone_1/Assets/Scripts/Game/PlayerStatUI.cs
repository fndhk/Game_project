using UnityEngine;
using UnityEngine.UI;

// 이 스크립트는 PlayerStats의 같은 값을
// 체력바와 스태미너바 두 개에 동시에 표시한다.
public class PlayerStatUI : MonoBehaviour
{
    [Header("참조")]
    // 값을 읽어올 대상 PlayerStats이다.
    public PlayerStats targetStats;

    [Header("UI")]
    // 체력바로 사용할 Slider이다.
    public Slider healthBar;

    // 스태미너바로 사용할 Slider이다.
    public Slider staminaBar;

    // 시작할 때 UI 기본 범위를 맞춘다.
    private void Start()
    {
        // 체력바가 있으면 최소/최대 범위를 설정한다.
        if (healthBar != null)
        {
            healthBar.minValue = 0f;
            healthBar.maxValue = 1f;
        }

        // 스태미너바가 있으면 최소/최대 범위를 설정한다.
        if (staminaBar != null)
        {
            staminaBar.minValue = 0f;
            staminaBar.maxValue = 1f;
        }
    }

    // 매 프레임 PlayerStats 값을 읽어서 UI를 갱신한다.
    private void Update()
    {
        // 대상이 없으면 더 진행하지 않는다.
        if (targetStats == null)
        {
            return;
        }

        // 현재 값을 0~1 비율로 가져온다.
        float normalized = targetStats.GetNormalizedValue();

        // 체력바가 있으면 같은 값을 넣는다.
        if (healthBar != null)
        {
            healthBar.value = normalized;
        }

        // 스태미너바가 있으면 같은 값을 넣는다.
        if (staminaBar != null)
        {
            staminaBar.value = normalized;
        }
    }
}