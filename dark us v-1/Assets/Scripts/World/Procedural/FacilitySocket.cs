using UnityEngine;

// 방과 방, 방과 복도, 복도와 복도를 연결할 때 사용하는 소켓이다.
// 파란색 Z축이 반드시 방 바깥쪽을 향해야 한다.
public class FacilitySocket : MonoBehaviour
{
    [Header("Floor")]
    // 0은 1층, 1은 2층이다.
    public int floorIndex = 0;

    [Header("Connection Rule")]
    // 이 소켓이 복도와 연결될 수 있는지 정한다.
    public bool canConnectCorridor = true;

    // 이 소켓이 방과 직접 연결될 수 있는지 정한다.
    public bool canConnectRoom = true;

    [Header("Runtime State")]
    [SerializeField] private bool isConnected = false;
    [SerializeField] private bool isBlocked = false;
    [SerializeField] private bool isExit = false;

    [Header("Debug")]
    public bool drawDebugGizmo = true;

    public bool IsAvailable
    {
        get
        {
            return !isConnected && !isBlocked && !isExit;
        }
    }

    public bool IsConnected
    {
        get
        {
            return isConnected;
        }
    }

    public void ResetRuntimeState()
    {
        isConnected = false;
        isBlocked = false;
        isExit = false;
    }

    public void MarkConnected()
    {
        isConnected = true;
        isBlocked = false;
        isExit = false;
    }

    public void MarkBlocked()
    {
        isConnected = false;
        isBlocked = true;
        isExit = false;
    }

    public void MarkExit()
    {
        isConnected = false;
        isBlocked = false;
        isExit = true;
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmo)
        {
            return;
        }

        if (isExit)
        {
            Gizmos.color = Color.cyan;
        }
        else if (isConnected)
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

        Gizmos.DrawSphere(transform.position, 0.16f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.9f);
    }
}