using UnityEngine;

// 스캔에 맞은 표면이 어떤 종류인지 구분하기 위한 enum이다.
public enum ScanSurfaceType
{
    Default,
    Ground,
    Rock,
    TreeTrunk,
    TreeLeaf,
    Branch,
    Bush
}

// 이 컴포넌트는 월드 오브젝트에 붙여서
// 스캔 시 어떤 색으로 점을 찍을지 구분하는 역할을 한다.
public class ScanSurfaceInfo : MonoBehaviour
{
    [Header("Surface Type")]
    // 이 오브젝트가 어떤 표면 종류인지 Inspector에서 지정한다.
    public ScanSurfaceType surfaceType = ScanSurfaceType.Default;
}