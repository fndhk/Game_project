using System.Collections.Generic;
using UnityEngine;

// 플레이어가 우클릭으로 주변 표면을 조금씩 드러내게 만드는 스크립트임
public class PlayerRevealTrail : MonoBehaviour
{
    [Header("Scan Origin")]
    // 점 스캔을 시작할 기준 위치임
    // 1인칭이면 Main Camera를 넣어 주면 됨
    public Transform scanOrigin;

    [Header("Input")]
    // 어떤 마우스 버튼으로 스캔할지 정하는 값임
    // 0은 좌클릭, 1은 우클릭이라서 지금은 우클릭으로 둠
    public int scanMouseButton = 1;

    [Header("Scan Timing")]
    // 우클릭을 누르고 있을 때 몇 초 간격으로 스캔할지 정하는 값임
    public float scanInterval = 0.18f;

    [Header("Dot Settings")]
    // 생성되는 점 하나의 크기값임
    public float dotSize = 0.12f;

    // 너무 가까운 점이 겹쳐 생기지 않게 막는 셀 크기값임
    public float cellSize = 0.35f;

    [Header("Ray Scan Count")]
    // 한 번 스캔할 때 최소 몇 개의 점을 만들지 정하는 값임
    public int minRaysPerScan = 3;

    // 한 번 스캔할 때 최대 몇 개의 점을 만들지 정하는 값임
    public int maxRaysPerScan = 6;

    [Header("Ray Scan Range")]
    // 점을 찍을 수 있는 최대 거리값임
    public float maxRevealDistance = 8f;

    // 좌우로 얼마나 넓게 퍼져서 스캔할지 정하는 각도값임
    public float horizontalAngle = 140f;

    // 위아래로 얼마나 퍼져서 스캔할지 정하는 각도값임
    public float verticalAngle = 80f;

    // 앞쪽 방향을 얼마나 더 우선해서 찍을지 정하는 값임
    public float forwardBias = 0.7f;

    [Header("Layers")]
    // 어떤 레이어에 점을 찍을지 정하는 마스크값임
    public LayerMask revealSurfaceMask;

    [Header("State")]
    // 지금까지 찍은 점들을 저장하는 리스트임
    public List<RevealDot> myDots = new List<RevealDot>();

    // 지금 우클릭 스캔 중인지 다른 스크립트가 읽을 수 있게 해 줌
    public bool IsScanning { get; private set; }

    // 다음 스캔이 가능한 시간을 저장해서 너무 자주 생성되지 않게 함
    private float nextScanTime = 0f;

    // 이미 점을 찍은 셀을 저장해서 같은 자리에 중복 생성되는 걸 막아 줌
    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    void Start()
    {
        // scanOrigin이 비어 있으면 일단 자기 자신을 기준으로 사용하게 함
        if (scanOrigin == null)
        {
            scanOrigin = transform;
        }
    }

    void Update()
    {
        // 실행 중 scanOrigin이 비어 있으면 다시 자기 자신을 넣어 줌
        if (scanOrigin == null)
        {
            scanOrigin = transform;
        }

        // 우클릭을 누르고 있는지 확인해서 스캔 상태를 저장함
        IsScanning = Input.GetMouseButton(scanMouseButton);

        // 우클릭 중이 아니면 점을 생성하지 않게 바로 끝냄
        if (!IsScanning)
        {
            return;
        }

        // 일정 시간 간격이 지났을 때만 스캔하게 해서 너무 빠르게 쏟아지지 않게 함
        if (Time.time >= nextScanTime)
        {
            // 현재 스캔 시점에 3~6개 정도 점이 나오게 함
            ScanAround();

            // 다음 스캔 가능 시간을 다시 예약해 둠
            nextScanTime = Time.time + scanInterval;
        }
    }

    void ScanAround()
    {
        // 스캔 시작 위치를 scanOrigin 위치로 잡음
        Vector3 origin = scanOrigin.position;

        // 카메라 또는 기준점의 정면 방향을 가져옴
        Vector3 forward = scanOrigin.forward;

        // 좌우 회전에 사용할 오른쪽 방향을 가져옴
        Vector3 right = scanOrigin.right;

        // 위아래 회전에 사용할 위쪽 방향을 가져옴
        Vector3 up = scanOrigin.up;

        // 이번 스캔에서 실제로 몇 개의 Ray를 쏠지 3~6개 사이에서 랜덤으로 정함
        int rayCount = Random.Range(minRaysPerScan, maxRaysPerScan + 1);

        // 정해진 개수만큼만 Ray를 쏴서 점이 적당히 퍼져 나오게 함
        for (int i = 0; i < rayCount; i++)
        {
            // 좌우로 퍼지는 랜덤 각도를 계산함
            float yaw = Random.Range(-horizontalAngle * 0.5f, horizontalAngle * 0.5f);

            // 위아래로 퍼지는 랜덤 각도를 계산함
            float pitch = Random.Range(-verticalAngle * 0.5f, verticalAngle * 0.5f);

            // yaw와 pitch를 합쳐서 랜덤 방향 회전을 만듦
            Quaternion rotation =
                Quaternion.AngleAxis(yaw, up) *
                Quaternion.AngleAxis(pitch, right);

            // 정면 기준으로 회전된 방향을 계산함
            Vector3 dir = rotation * forward;

            // 완전히 옆이나 뒤보다 앞쪽에 조금 더 치우치게 보정함
            dir = Vector3.Slerp(dir, forward, forwardBias * 0.35f).normalized;

            // 계산된 방향으로 실제 Raycast를 실행함
            CastRevealRay(origin, dir);
        }
    }

    void CastRevealRay(Vector3 origin, Vector3 dir)
    {
        // Raycast가 표면에 맞으면 그 위치에 점을 추가하게 함
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRevealDistance, revealSurfaceMask))
        {
            // 표면과 점이 너무 딱 붙지 않게 normal 방향으로 아주 살짝 띄워 줌
            Vector3 finalPos = hit.point + hit.normal * 0.02f;

            // 같은 셀에 점이 없을 때만 새 점을 추가하게 함
            AddDotIfNew(finalPos);
        }
    }

    void AddDotIfNew(Vector3 worldPos)
    {
        // 현재 점 위치를 셀 좌표로 바꿔서 중복 검사용으로 사용함
        Vector3Int cell = new Vector3Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.y / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize)
        );

        // 이미 같은 셀에 점이 있으면 새 점을 만들지 않게 함
        if (occupiedCells.Contains(cell))
        {
            return;
        }

        // 새 셀 위치를 저장해서 다음부터 중복 생성이 안 되게 함
        occupiedCells.Add(cell);

        // 실제 점 데이터를 리스트에 추가해서 렌더러가 그릴 수 있게 함
        myDots.Add(new RevealDot(worldPos, dotSize));
    }
}