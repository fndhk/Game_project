using UnityEngine;

// 이 스크립트는 플레이어의 체력과 스테미나를 따로 관리한다.
// 체력은 공격받을 때만 줄어들고,
// 스테미나는 달릴 때만 줄어든다.
// 스테미나의 최대치는 항상 현재 체력과 같게 유지된다.
public class PlayerStats : MonoBehaviour
{
    [Header("체력")]
    // 최대 체력이다.
    public float maxHealth = 100f;

    // 현재 체력이다.
    public float currentHealth = 100f;

    [Header("스테미나")]
    // 현재 스테미나이다.
    // 최대 스테미나는 currentHealth를 따라간다.
    public float currentStamina = 100f;

    [Header("스테미나 소모 / 회복")]
    // 달릴 때 초당 얼마나 줄어들지 정하는 값이다.
    public float sprintDrainPerSecond = 25f;

    // 달리지 않을 때 초당 얼마나 회복될지 정하는 값이다.
    public float staminaRecoverPerSecond = 18f;

    [Header("피해")]
    // 공격 한 번 맞았을 때 깎일 체력 값이다.
    public float attackDamage = 50f;

    // 사망 처리를 위해 PlayerCombatTarget을 저장한다.
    private PlayerCombatTarget combatTarget;

    // 시작 전에 필요한 컴포넌트를 가져온다.
    private void Awake()
    {
        // 같은 오브젝트에 붙어 있는 PlayerCombatTarget을 가져온다.
        combatTarget = GetComponent<PlayerCombatTarget>();

        // 시작 시 값들을 정상 범위로 맞춘다.
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentStamina = Mathf.Clamp(currentStamina, 0f, currentHealth);
    }

    // 현재 체력 비율을 0~1 값으로 반환한다.
    public float GetHealthNormalized()
    {
        // 최대 체력이 0 이하면 0을 반환한다.
        if (maxHealth <= 0f)
        {
            return 0f;
        }

        // 현재 체력을 최대 체력으로 나눈 값을 반환한다.
        return currentHealth / maxHealth;
    }

    // 현재 스테미나 비율을 0~1 값으로 반환한다.
    // 스테미나 최대치는 현재 체력이다.
    public float GetStaminaNormalized()
    {
        // 현재 체력이 0 이하면 스테미나도 0 취급한다.
        if (currentHealth <= 0f)
        {
            return 0f;
        }

        // 현재 스테미나를 현재 체력으로 나눈 값을 반환한다.
        return currentStamina / currentHealth;
    }

    // 현재 달릴 수 있는 상태인지 반환한다.
    public bool CanSprint()
    {
        // 스테미나가 조금이라도 남아 있으면 달릴 수 있다.
        return currentStamina > 0.1f;
    }

    // 달릴 때 스테미나를 줄이는 함수이다.
    public void DrainStaminaForSprint(float deltaTime)
    {
        // 이미 스테미나가 없으면 0으로 고정한다.
        if (currentStamina <= 0f)
        {
            currentStamina = 0f;
            return;
        }

        // 초당 소모량 기준으로 스테미나를 줄인다.
        currentStamina -= sprintDrainPerSecond * deltaTime;

        // 스테미나가 0 아래로 내려가지 않게 막는다.
        currentStamina = Mathf.Clamp(currentStamina, 0f, currentHealth);
    }

    // 달리지 않을 때 스테미나를 회복하는 함수이다.
    // 회복 최대치는 현재 체력까지만이다.
    public void RecoverStamina(float deltaTime)
    {
        // 체력이 0 이하면 회복할 필요가 없다.
        if (currentHealth <= 0f)
        {
            currentStamina = 0f;
            return;
        }

        // 이미 현재 체력만큼 차 있으면 더 회복하지 않는다.
        if (currentStamina >= currentHealth)
        {
            currentStamina = currentHealth;
            return;
        }

        // 초당 회복량 기준으로 스테미나를 회복한다.
        currentStamina += staminaRecoverPerSecond * deltaTime;

        // 현재 체력을 넘지 않게 막는다.
        currentStamina = Mathf.Clamp(currentStamina, 0f, currentHealth);
    }

    // 공격을 받았을 때 체력을 깎는 함수이다.
    public void TakeDamage(float damageAmount)
    {
        // 이미 체력이 0 이하면 더 처리하지 않는다.
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            currentStamina = 0f;
            return;
        }

        // 받은 피해량만큼 체력을 줄인다.
        currentHealth -= damageAmount;

        // 체력이 음수가 되지 않게 막는다.
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // 스테미나 최대치는 현재 체력이므로,
        // 현재 스테미나가 체력보다 크면 잘라낸다.
        currentStamina = Mathf.Clamp(currentStamina, 0f, currentHealth);

        // 체력이 0이 되면 사망 처리한다.
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            currentStamina = 0f;

            // PlayerCombatTarget이 있으면 죽음 처리를 호출한다.
            if (combatTarget != null)
            {
                combatTarget.Die();
            }
        }
    }

    // 기본 공격 피해량 50을 적용하는 함수이다.
    public void TakeDefaultAttackDamage()
    {
        // 미리 설정된 attackDamage 값만큼 체력을 깎는다.
        TakeDamage(attackDamage);
    }
}