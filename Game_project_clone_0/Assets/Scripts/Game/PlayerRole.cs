using UnityEngine;

public class PlayerRole : MonoBehaviour
{
    [Header("Role")]
    public RoleType role = RoleType.Civilian;

    public bool IsKiller => role == RoleType.Killer;
    public bool IsCivilian => role == RoleType.Civilian;
}
