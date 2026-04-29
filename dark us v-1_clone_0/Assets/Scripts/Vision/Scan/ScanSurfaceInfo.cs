using UnityEngine;

// 스캔에 맞은 표면이 어떤 종류인지 구분하기 위한 enum이다.
public enum ScanSurfaceType
{
    Default = 0,

    // 연구소 맵에서 쓰는 타입이다.
    // 숫자는 이전 버전과 호환되도록 유지한다.
    Floor = 7,
    Wall = 8,
    Metal = 9,
    Glass = 10,
    AccessCore = 11,
    SecurityTerminal = 12,
    EmergencyExit = 13,
    PlayerBody = 14,
    Creature = 15
}

// 이 컴포넌트는 월드 오브젝트에 붙여서
// 스캔 시 어떤 색으로 점을 찍을지 구분하는 역할을 한다.
public class ScanSurfaceInfo : MonoBehaviour
{
    [Header("Surface Type")]
    // 이 오브젝트가 어떤 표면 종류인지 Inspector에서 지정한다.
    public ScanSurfaceType surfaceType = ScanSurfaceType.Default;
}