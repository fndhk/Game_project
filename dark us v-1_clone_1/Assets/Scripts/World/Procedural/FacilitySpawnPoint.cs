using UnityEngine;

// 시작방 안에서 플레이어가 스폰될 위치이다.
public class FacilitySpawnPoint : MonoBehaviour
{
    [Header("Runtime State")]
    // 이미 플레이어 스폰 위치로 사용되었는지 저장한다.
    public bool isOccupied = false;

    // 런타임 상태 초기화이다.
    public void ResetRuntimeState()
    {
        isOccupied = false;
    }
}