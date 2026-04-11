using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 우클릭할 때 정면 중심에서 바깥쪽으로 점이 퍼지듯이 보이게 만드는 스크립트임
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

    [Header("Dot Settings")]
    // 생성되는 점 하나의 크기값임
    public float dotSize = 0.05f;

    // 너무 가까운 점이 겹쳐 생기지 않게 막는 셀 크기값임
    // 실제 점이 너무 적게 나오면 이 값을 조금 줄이면 됨
    public float cellSize = 0.18f;

    [Header("Burst Visible Dot Count")]
    // 한 번 클릭했을 때 실제로 화면에 보이게 만들 최소 점 개수임
    public int minVisibleDotsPerScan = 45;

    // 한 번 클릭했을 때 실제로 화면에 보이게 만들 최대 점 개수임
    public int maxVisibleDotsPerScan = 55;

    // 목표 개수를 채우기 위해 최대 몇 번까지 시도할지 정하는 값임
    // 이 값이 너무 낮으면 목표 개수보다 적게 끝날 수 있음
    public int maxAttemptsPerScan = 220;

    [Header("Burst Timing")]
    // 점이 하나씩 퍼지듯이 나올 때 최소 대기 시간값임
    public float minSpawnDelay = 0.002f;

    // 점이 하나씩 퍼지듯이 나올 때 최대 대기 시간값임
    public float maxSpawnDelay = 0.008f;

    [Header("Ray Scan Range")]
    // 점을 찍을 수 있는 최대 거리값임
    public float maxRevealDistance = 8f;

    // 좌우로 얼마나 넓게 퍼져서 스캔할지 정하는 최대 각도값임
    public float horizontalAngle = 150f;

    // 위아래로 얼마나 퍼져서 스캔할지 정하는 최대 각도값임
    public float verticalAngle = 90f;

    // 앞쪽 방향을 얼마나 더 우선해서 찍을지 정하는 값임
    public float forwardBias = 0.8f;

    [Header("Layers")]
    // 어떤 레이어에 점을 찍을지 정하는 마스크값임
    public LayerMask revealSurfaceMask;

    [Header("State")]
    // 지금까지 찍은 점들을 저장하는 리스트임
    public List<RevealDot> myDots = new List<RevealDot>();

    // 지금 점이 퍼지는 중인지 다른 스크립트가 확인할 수 있게 해 줌
    public bool IsScanning { get; private set; }

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

        // 우클릭을 누른 순간에만 한 번 점 퍼뜨리기를 시작하게 함
        if (Input.GetMouseButtonDown(scanMouseButton))
        {
            StartCoroutine(ScanBurst());
        }
    }

    IEnumerator ScanBurst()
    {
        // 점이 퍼지는 동안은 스캔 중 상태로 표시해 둠
        IsScanning = true;

        // 이번 클릭에서 실제로 보이게 만들 목표 점 개수를 랜덤으로 정함
        int targetVisibleDots = Random.Range(minVisibleDotsPerScan, maxVisibleDotsPerScan + 1);

        // 현재 클릭에서 실제로 새로 추가된 점 개수를 저장함
        int successCount = 0;

        // 몇 번 시도했는지 세서 무한히 반복되지 않게 막아 줌
        int attemptCount = 0;

        // 스캔 시작 위치를 scanOrigin 위치로 잡음
        Vector3 origin = scanOrigin.position;

        // 카메라 또는 기준점의 정면 방향을 가져옴
        Vector3 forward = scanOrigin.forward;

        // 좌우 회전에 사용할 오른쪽 방향을 가져옴
        Vector3 right = scanOrigin.right;

        // 위아래 회전에 사용할 위쪽 방향을 가져옴
        Vector3 up = scanOrigin.up;

        // 목표 개수만큼 실제 점이 추가될 때까지 계속 시도하게 함
        while (successCount < targetVisibleDots && attemptCount < maxAttemptsPerScan)
        {
            // 현재까지 몇 개를 성공했는지 비율로 바꿔서 처음엔 중앙, 뒤로 갈수록 바깥쪽으로 퍼지게 만듦
            float progress = targetVisibleDots <= 1 ? 1f : (float)successCount / (targetVisibleDots - 1);

            // 초반 점은 정면 근처에, 후반 점은 더 넓은 범위에 퍼지게 각도 범위를 점점 키움
            float currentHorizontalRange = Mathf.Lerp(6f, horizontalAngle * 0.5f, progress);
            float currentVerticalRange = Mathf.Lerp(4f, verticalAngle * 0.5f, progress);

            // 좌우로 퍼지는 랜덤 각도를 현재 진행도에 맞는 범위 안에서 계산함
            float yaw = Random.Range(-currentHorizontalRange, currentHorizontalRange);

            // 위아래로 퍼지는 랜덤 각도를 현재 진행도에 맞는 범위 안에서 계산함
            float pitch = Random.Range(-currentVerticalRange, currentVerticalRange);

            // yaw와 pitch를 합쳐서 랜덤 방향 회전을 만듦
            Quaternion rotation =
                Quaternion.AngleAxis(yaw, up) *
                Quaternion.AngleAxis(pitch, right);

            // 정면 기준으로 회전된 방향을 계산함
            Vector3 dir = rotation * forward;

            // 앞쪽 방향을 조금 더 우선해서 자연스럽게 정면 중심 느낌이 나게 보정함
            dir = Vector3.Slerp(dir, forward, forwardBias * 0.35f).normalized;

            // 실제로 새 점이 추가됐는지 여부를 받아서 성공 개수에 반영함
            bool added = CastRevealRay(origin, dir);

            // 실제로 새 점이 생겼을 때만 성공 개수를 올리게 함
            if (added)
            {
                successCount++;

                // 점이 한꺼번에 생기지 않고 퍼지듯이 나오게 살짝 기다리게 함
                float delay = Random.Range(minSpawnDelay, maxSpawnDelay);
                yield return new WaitForSeconds(delay);
            }

            // 시도 횟수는 성공 여부와 상관없이 계속 올려서 무한루프를 막아 줌
            attemptCount++;
        }

        // 한 번의 점 퍼뜨리기가 끝났으니 스캔 상태를 꺼 줌
        IsScanning = false;
    }

    bool CastRevealRay(Vector3 origin, Vector3 dir)
    {
        // Raycast가 표면에 맞으면 그 위치에 점을 추가하게 함
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxRevealDistance, revealSurfaceMask))
        {
            // 표면과 점이 너무 딱 붙지 않게 normal 방향으로 아주 살짝 띄워 줌
            Vector3 finalPos = hit.point + hit.normal * 0.02f;

            // 실제로 새 점이 추가됐는지 결과를 그대로 반환하게 함
            return AddDotIfNew(finalPos);
        }

        // 아무 표면도 못 맞췄으면 실패로 처리하게 함
        return false;
    }

    bool AddDotIfNew(Vector3 worldPos)
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
            return false;
        }

        // 새 셀 위치를 저장해서 다음부터 중복 생성이 안 되게 함
        occupiedCells.Add(cell);

        // 실제 점 데이터를 리스트에 추가해서 렌더러가 그릴 수 있게 함
        myDots.Add(new RevealDot(worldPos, dotSize));

        // 실제로 새 점이 추가됐다고 알려 줌
        return true;
    }
}