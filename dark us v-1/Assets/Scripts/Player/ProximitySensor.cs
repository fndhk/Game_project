using UnityEngine;

public class ProximitySensor : MonoBehaviour
{
    [Header("Detect")]
    public float detectRadius = 6f;
    public LayerMask playerMask;
    public float smoothSpeed = 6f;

    [Header("Read Only")]
    [Range(0f, 1f)] public float currentIntensity;

    private readonly Collider[] results = new Collider[16];

    private void Update()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, detectRadius, results, playerMask);

        bool foundOther = false;
        float nearest = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider hit = results[i];
            if (hit == null) continue;
            if (hit.transform.root == transform.root) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < nearest)
            {
                nearest = dist;
                foundOther = true;
            }
        }

        float target = 0f;
        if (foundOther)
        {
            target = 1f - Mathf.Clamp01(nearest / detectRadius);
        }

        currentIntensity = Mathf.Lerp(currentIntensity, target, Time.deltaTime * smoothSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
