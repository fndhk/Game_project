using UnityEngine;

[RequireComponent(typeof(PlayerRole))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Attack")]
    public float attackRange = 1.6f;
    public float attackCooldown = 1.2f;
    public LayerMask playerMask;

    private float nextAttackTime;
    private PlayerRole role;
    private readonly Collider[] results = new Collider[8];

    private void Awake()
    {
        role = GetComponent<PlayerRole>();
    }

    private void Update()
    {
        if (!role.IsKiller)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;
        int count = Physics.OverlapSphereNonAlloc(transform.position, attackRange, results, playerMask);

        for (int i = 0; i < count; i++)
        {
            Collider hit = results[i];
            if (hit == null) continue;
            if (hit.transform.root == transform.root) continue;

            PlayerRole other = hit.GetComponent<PlayerRole>();
            if (other != null && other.IsCivilian)
            {
                Destroy(hit.gameObject);
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
