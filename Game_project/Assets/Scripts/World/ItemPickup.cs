using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public static int CollectedCount;

    private void OnTriggerEnter(Collider other)
    {
        PlayerRole role = other.GetComponent<PlayerRole>();
        if (role == null || !role.IsCivilian)
        {
            return;
        }

        CollectedCount++;
        Destroy(gameObject);
    }
}
