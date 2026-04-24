using UnityEngine;

// 방 프리팹 안에서 문이 연결될 위치를 나타내는 스크립트이다.
// 반드시 파란색 Z축이 방 바깥쪽을 향하게 배치해야 한다.
public class ModularDoorPoint : MonoBehaviour
{
    [Header("Door State")]
    // 이 문이 다른 방과 연결되었는지 저장한다.
    [SerializeField] private bool isConnected = false;

    // 이 문이 막힌 문으로 처리되었는지 저장한다.
    [SerializeField] private bool isBlocked = false;

    [Header("Debug")]
    // Scene 뷰에서 문 방향을 보여줄지 정한다.
    [SerializeField] private bool drawDebugGizmo = true;

    // 현재 문이 연결되었는지 반환한다.
    public bool IsConnected
    {
        get
        {
            return isConnected;
        }
    }

    // 현재 문이 막혀 있는지 반환한다.
    public bool IsBlocked
    {
        get
        {
            return isBlocked;
        }
    }

    // 이 문을 새 방 연결 후보로 사용할 수 있는지 반환한다.
    public bool IsAvailable
    {
        get
        {
            return !isConnected && !isBlocked;
        }
    }

    // 생성 전에 문 상태를 초기화한다.
    public void ResetRuntimeState()
    {
        isConnected = false;
        isBlocked = false;
    }

    // 이 문을 연결된 상태로 바꾼다.
    public void MarkConnected()
    {
        isConnected = true;
        isBlocked = false;
    }

    // 이 문을 막힌 상태로 바꾼다.
    public void MarkBlocked()
    {
        isBlocked = true;
        isConnected = false;
    }

    // Scene 뷰에서 문 위치와 방향을 확인하기 위한 표시이다.
    private void OnDrawGizmos()
    {
        // 디버그 표시가 꺼져 있으면 종료한다.
        if (!drawDebugGizmo)
        {
            return;
        }

        // 연결 상태에 따라 색을 다르게 보여준다.
        if (isConnected)
        {
            Gizmos.color = Color.green;
        }
        else if (isBlocked)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }

        // 문 위치에 작은 구를 그린다.
        Gizmos.DrawSphere(transform.position, 0.15f);

        // 문 바깥 방향은 파란색 Z축 기준이다.
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
    }
}