using UnityEngine;

// 이 스크립트는 플레이어의 체력과 스태미너를 같은 값으로 관리한다.
// 달리면 값이 줄어들고, 쉬면 회복되며, 공격을 받으면 큰 폭으로 감소한다.
public class PlayerStats : MonoBehaviour
{
    [Header("공유 수치")]
    // 최대 체력/스태미너 값이다.
    public float maxSharedValue = 100f;

    // 현재 체력/스태미너 값이다.
    public float currentSharedValue = 100f;

    [Header("달리기 소모 / 회복")]
    // 달릴 때 초당 얼마나 줄어들지 정하는 값이다.
    public float sprintDrainPerSecond = 18f;

    // 달리지 않을 때 초당 얼마나 회복될지 정하는 값이다.
    public float recoverPerSecond = 12f;

    [Header("피해")]
    // 공격 한 번 맞았을 때 깎일 체력 값이다.
    public float attackDamage = 50f;

    // PlayerCombatTarget을 저장하는 변수이다.
    private PlayerCombatTarget combatTarget;

    // 시작 전에 필요한 컴포넌트를 가져온다.
    private void Awake()
    {
        // 같은 오브젝트에 붙어 있는 PlayerCombatTarget을 가져온다.
        combatTarget = GetComponent<PlayerCombatTarget>();

        // 시작 시 현재 값을 최대값 기준으로 보정한다.
        currentSharedValue = Mathf.Clamp(currentSharedValue, 0f, maxSharedValue);
    }

    // 현재 수치를 0~1 비율로 반환하는 함수이다.
    public float GetNormalizedValue()
    {
        // max 값이 0 이하이면 0을 반환해서 나누기 오류를 막는다.
        if (maxSharedValue <= 0f)
        {
            return 0f;
        }

        // 현재 값을 최대값으로 나눈 비율을 반환한다.
        return currentSharedValue / maxSharedValue;
    }

    // 현재 달릴 수 있는 상태인지 반환하는 함수이다.
    public bool CanSprint()
    {
        // 현재 값이 0보다 크면 달릴 수 있다고 본다.
        return currentSharedValue > 0.1f;
    }

    // 실제로 달릴 때 수치를 소모하는 함수이다.
    public void DrainForSprint(float deltaTime)
    {
        // 이미 값이 0이면 더 줄이지 않는다.
        if (currentSharedValue <= 0f)
        {
            currentSharedValue = 0f;
            return;
        }

        // 초당 소모량 기준으로 현재 값을 줄인다.
        currentSharedValue -= sprintDrainPerSecond * deltaTime;

        // 음수가 되지 않도록 0~최대 범위로 보정한다.
        currentSharedValue = Mathf.Clamp(currentSharedValue, 0f, maxSharedValue);
    }

    // 달리지 않을 때 수치를 회복하는 함수이다.
    public void RecoverSharedValue(float deltaTime)
    {
        // 이미 최대치면 더 회복하지 않는다.
        if (currentSharedValue >= maxSharedValue)
        {
            currentSharedValue = maxSharedValue;
            return;
        }

        // 초당 회복량 기준으로 현재 값을 늘린다.
        currentSharedValue += recoverPerSecond * deltaTime;

        // 최대치를 넘지 않도록 보정한다.
        currentSharedValue = Mathf.Clamp(currentSharedValue, 0f, maxSharedValue);
    }

    // 공격을 받았을 때 체력/스태미너를 깎는 함수이다.
    public void TakeDamage(float damageAmount)
    {
        // 이미 0 이하이면 더 처리하지 않는다.
        if (currentSharedValue <= 0f)
        {
            currentSharedValue = 0f;
            return;
        }

        // 받은 피해량만큼 현재 값을 줄인다.
        currentSharedValue -= damageAmount;

        // 음수가 되지 않도록 보정한다.
        currentSharedValue = Mathf.Clamp(currentSharedValue, 0f, maxSharedValue);

        // 값이 0 이하가 되었으면 사망 처리한다.
        if (currentSharedValue <= 0f)
        {
            currentSharedValue = 0f;

            // PlayerCombatTarget이 있으면 Die를 호출한다.
            if (combatTarget != null)
            {
                combatTarget.Die();
            }
        }
    }

    // Inspector에서 테스트하기 쉽게 기본 피해량으로 공격받는 함수이다.
    public void TakeDefaultAttackDamage()
    {
        // attackDamage 값만큼 피해를 준다.
        TakeDamage(attackDamage);
    }
}