using UnityEngine;

// 방 안에서 아이템이 놓일 수 있는 위치이다.
public class FacilityItemPoint : MonoBehaviour
{
    [Header("Item Rule")]
    // 이 위치에 배치 가능한 아이템 종류이다.
    public FacilityItemKind allowedKind = FacilityItemKind.Any;

    [Header("Runtime State")]
    // 이미 아이템이 배치되었는지 저장한다.
    public bool isOccupied = false;

    // 런타임 상태 초기화이다.
    public void ResetRuntimeState()
    {
        isOccupied = false;
    }
}