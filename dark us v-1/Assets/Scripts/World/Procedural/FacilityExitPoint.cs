using UnityEngine;

// 탈출구 문이 방 안에 생성될 위치이다.
// 탈출구는 Socket이 아니라 ExitPoint에 생성된다.
public class FacilityExitPoint : MonoBehaviour
{
    [Header("Runtime State")]
    // 이미 탈출구가 배치되었는지 저장한다.
    public bool isOccupied = false;

    // 런타임 상태 초기화이다.
    public void ResetRuntimeState()
    {
        isOccupied = false;
    }
}