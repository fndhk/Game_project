using System.Collections;
using UnityEngine;

// 이 스크립트는 킬러의 근접 공격을 처리한다.
// 좌클릭으로 가까운 시민을 공격할 수 있고,
// 시민은 이 스크립트가 있어도 공격하지 못하게 만든다.
public class KillerAttack : MonoBehaviour
{
    [Header("참조")]
    // 공격 방향 기준이 되는 카메라이다.
    // 보통 Main Camera를 넣는다.
    public Transform playerCamera;

    // 자기 자신의 역할/사망 상태를 확인하기 위한 컴포넌트이다.
    private PlayerCombatTarget selfTarget;

    [Header("공격 입력")]
    // 좌클릭으로 공격하도록 기본값을 0으로 둔다.
    public int attackMouseButton = 0;

    [Header("공격 범위")]
    // 카메라 앞쪽으로 얼마 떨어진 곳을 공격 중심으로 잡을지 정한다.
    public float attackDistance = 1.6f;

    // 공격 판정 반지름이다.
    public float attackRadius = 0.6f;

    [Header("공격 타이밍")]
    // 클릭 후 실제 판정이 나가기까지의 짧은 딜레이이다.
    public float attackDelay = 0.08f;

    // 한 번 공격한 뒤 다시 공격할 수 있기까지의 쿨타임이다.
    public float attackCooldown = 0.9f;

    [Header("레이어 설정")]
    // 공격 대상으로 볼 레이어이다.
    public LayerMask targetMask = ~0;

    // 벽 가림 판정에 사용할 레이어이다.
    public LayerMask obstacleMask = ~0;

    [Header("상태")]
    // 현재 공격 가능한 상태인지 저장한다.
    public bool canAttack = true;

    // 현재 공격 중인지 저장한다.
    public bool isAttacking = false;

    // 시작할 때 필요한 참조를 가져온다.
    private void Start()
    {
        // 자기 자신의 PlayerCombatTarget을 가져온다.
        selfTarget = GetComponent<PlayerCombatTarget>();

        // 카메라가 비어 있으면 Main Camera를 자동으로 찾아 넣는다.
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }
    }

    // 매 프레임 공격 입력을 확인한다.
    private void Update()
    {
        // 자기 정보가 없으면 더 진행하지 않는다.
        if (selfTarget == null)
        {
            return;
        }

        // 죽었으면 공격하지 못한다.
        if (selfTarget.isDead)
        {
            return;
        }

        // 킬러가 아니면 공격하지 못한다.
        if (selfTarget.role != PlayerRole.Killer)
        {
            return;
        }

        // 공격 가능한 상태에서 좌클릭을 누르면 공격을 시작한다.
        if (canAttack && Input.GetMouseButtonDown(attackMouseButton))
        {
            StartCoroutine(AttackRoutine());
        }
    }

    // 공격 시작, 판정, 쿨타임을 처리하는 코루틴이다.
    private IEnumerator AttackRoutine()
    {
        // 공격 중 상태로 바꾼다.
        isAttacking = true;

        // 바로 다시 공격하지 못하게 막는다.
        canAttack = false;

        // 짧은 선딜을 준다.
        yield return new WaitForSeconds(attackDelay);

        // 실제 공격 판정을 시도한다.
        TryAttack();

        // 공격 판정이 끝났으므로 공격 중 상태를 해제한다.
        isAttacking = false;

        // 쿨타임 동안 기다린다.
        yield return new WaitForSeconds(attackCooldown);

        // 다시 공격 가능 상태로 바꾼다.
        canAttack = true;
    }

    // 실제로 주변에서 맞는 시민이 있는지 찾고, 있으면 죽이는 함수이다.
    private void TryAttack()
    {
        // 카메라가 없으면 종료한다.
        if (playerCamera == null)
        {
            return;
        }

        // 시작 위치를 카메라 바로 앞쪽으로 잡는다.
        Vector3 origin = playerCamera.position + playerCamera.forward * 0.05f;

        // 공격 중심 위치를 카메라 앞쪽으로 만든다.
        Vector3 attackCenter = origin + playerCamera.forward * attackDistance;

        // 공격 범위 안에 있는 Collider들을 찾는다.
        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            attackRadius,
            targetMask,
            QueryTriggerInteraction.Ignore
        );

        // 가장 적합한 타겟을 저장할 변수이다.
        PlayerCombatTarget bestTarget = null;

        // 가장 높은 점수를 저장할 변수이다.
        float bestScore = float.NegativeInfinity;

        // 범위 안의 모든 Collider를 검사한다.
        for (int i = 0; i < hits.Length; i++)
        {
            // 부모 쪽에서 PlayerCombatTarget을 찾는다.
            PlayerCombatTarget candidate = hits[i].GetComponentInParent<PlayerCombatTarget>();

            // 대상이 아니면 넘긴다.
            if (candidate == null)
            {
                continue;
            }

            // 자기 자신이면 넘긴다.
            if (candidate == selfTarget)
            {
                continue;
            }

            // 이미 죽은 대상이면 넘긴다.
            if (candidate.isDead)
            {
                continue;
            }

            // 시민만 공격 대상으로 허용한다.
            if (candidate.role != PlayerRole.Citizen)
            {
                continue;
            }

            // 벽에 가려져 있으면 맞지 않게 한다.
            if (!HasLineOfSight(hits[i], candidate, origin))
            {
                continue;
            }

            // 카메라 정면에 더 가까운 대상을 우선하기 위해 점수를 계산한다.
            Vector3 toTarget = (hits[i].bounds.center - origin).normalized;
            float dot = Vector3.Dot(playerCamera.forward, toTarget);
            float distance = Vector3.Distance(origin, hits[i].bounds.center);
            float score = dot * 10f - distance;

            // 더 좋은 타겟이면 갱신한다.
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        // 최종 타겟이 있으면 죽인다.
        // 최종 타겟이 있으면 바로 죽이지 말고 50 피해를 준다.
        if (bestTarget != null)
        {
            // 맞은 대상의 PlayerStats를 가져온다.
            PlayerStats targetStats = bestTarget.GetComponent<PlayerStats>();

            // PlayerStats가 있으면 기본 공격 피해량 50을 적용한다.
            if (targetStats != null)
            {
                targetStats.TakeDamage(50f);
            }
            else
            {
                // 혹시 PlayerStats가 없으면 기존처럼 바로 죽게 처리한다.
                bestTarget.Die();
            }

            // 콘솔에 공격 로그를 남긴다.
            Debug.Log("Killer attacked: " + bestTarget.name);
        }
    }

    // 벽 너머의 대상을 맞지 않게 하기 위해 시야가 통하는지 검사하는 함수이다.
    private bool HasLineOfSight(Collider targetCollider, PlayerCombatTarget target, Vector3 origin)
    {
        // 목표 지점을 타겟 Collider 중심으로 잡는다.
        Vector3 targetPoint = targetCollider.bounds.center;

        // 방향 벡터를 구한다.
        Vector3 direction = targetPoint - origin;

        // 거리를 구한다.
        float distance = direction.magnitude;

        // 거리가 거의 0이면 바로 보인다고 처리한다.
        if (distance <= 0.001f)
        {
            return true;
        }

        // 방향 벡터를 정규화한다.
        direction /= distance;

        // Raycast로 중간에 벽이 있는지 검사한다.
        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // 먼저 맞은 Collider가 같은 타겟이면 시야가 통한다고 본다.
            PlayerCombatTarget hitTarget = hit.collider.GetComponentInParent<PlayerCombatTarget>();

            if (hitTarget != null && hitTarget == target)
            {
                return true;
            }

            // 다른 물체를 먼저 맞았으면 가려졌다고 본다.
            return false;
        }

        // 아무것도 막지 않았다면 시야가 통한다고 본다.
        return true;
    }

    // Scene 뷰에서 공격 범위를 보기 쉽게 그린다.
    private void OnDrawGizmosSelected()
    {
        // 카메라가 없으면 현재 오브젝트 기준으로 대체한다.
        Transform view = playerCamera != null ? playerCamera : transform;

        // 공격 중심 위치를 계산한다.
        Vector3 origin = view.position + view.forward * 0.05f;
        Vector3 attackCenter = origin + view.forward * attackDistance;

        // 공격 범위를 빨간색 구로 그린다.
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackCenter, attackRadius);
    }
}