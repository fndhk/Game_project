using System.Collections.Generic;
using UnityEngine;

// 우클릭을 누르고 있는 동안 화면 중심의 작은 범위를 스캔해서
// 맞은 표면에 흰 점을 계속 생성하는 스캐너이다.
public class LidarSpotScanner : MonoBehaviour
{
    [Header("References")]
    // 점 프리팹이다.
    [SerializeField] private GameObject dotPrefab;

    // 생성된 점을 담아둘 부모이다.
    [SerializeField] private Transform dotContainer;

    // 스캔 기준 카메라이다.
    [SerializeField] private Camera scanCamera;

    [Header("Scan Audio")]
    // 우클릭 중에만 재생할 스캔 루프 사운드 소스이다.
    [SerializeField] private AudioSource scanLoopSource;

    [Header("Scan Settings")]
    // 초당 생성할 점 개수이다.
    [SerializeField] private float pointsPerSecond = 900f;

    // 최대 스캔 거리이다.
    [SerializeField] private float maxDistance = 20f;

    // 화면 중심 기준 스캔 반경이다.
    [SerializeField] private float viewportRadius = 0.12f;

    // 점을 표면에서 조금 띄운다.
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Duplicate Block")]
    // 같은 위치에 중복 생성되지 않게 하는 셀 크기이다.
    [SerializeField] private float cellSize = 0.08f;

    [Header("Raycast")]
    // 스캔할 레이어이다.
    [SerializeField] private LayerMask scanMask = ~0;

    // 트리거 충돌 처리 방식이다.
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    // 프레임별 생성량을 누적한다.
    private float spawnBudget;

    // 이미 점이 찍힌 셀을 저장한다.
    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    // 직전 프레임에 스캔 중이었는지 저장한다.
    private bool wasScanningLastFrame;

    private void Reset()
    {
        // 같은 오브젝트의 카메라를 기본값으로 넣는다.
        scanCamera = GetComponent<Camera>();

        // 같은 오브젝트의 오디오 소스를 기본값으로 넣는다.
        scanLoopSource = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        // 카메라가 비어 있으면 메인 카메라를 넣는다.
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }

        // 컨테이너가 비어 있으면 자동 생성한다.
        if (dotContainer == null)
        {
            GameObject container = new GameObject("ScanDots");
            dotContainer = container.transform;
        }

        // 오디오 소스가 있으면 시작 시 자동 재생되지 않게 막는다.
        if (scanLoopSource != null)
        {
            scanLoopSource.playOnAwake = false;
        }
    }

    private void Update()
    {
        // 우클릭 중일 때만 스캔한다.
        bool isScanning = Input.GetMouseButton(1);

        // 스캔 시작 순간에 루프 사운드를 켠다.
        if (isScanning && !wasScanningLastFrame)
        {
            StartScanSound();
        }

        // 스캔 종료 순간에 루프 사운드를 끈다.
        if (!isScanning && wasScanningLastFrame)
        {
            StopScanSound();
        }

        // 다음 프레임 비교를 위해 현재 상태를 저장한다.
        wasScanningLastFrame = isScanning;

        // 스캔 중이 아니면 점 생성은 하지 않는다.
        if (!isScanning)
        {
            return;
        }

        // 초당 생성량을 프레임 단위로 누적한다.
        spawnBudget += pointsPerSecond * Time.deltaTime;

        // 누적된 만큼 점을 생성한다.
        while (spawnBudget >= 1f)
        {
            spawnBudget -= 1f;
            TrySpawnOneDot();
        }
    }

    private void StartScanSound()
    {
        // 오디오 소스가 없으면 종료한다.
        if (scanLoopSource == null)
        {
            return;
        }

        // 이미 재생 중이 아니면 재생한다.
        if (!scanLoopSource.isPlaying)
        {
            scanLoopSource.Play();
        }
    }

    private void StopScanSound()
    {
        // 오디오 소스가 없으면 종료한다.
        if (scanLoopSource == null)
        {
            return;
        }

        // 재생 중이면 정지한다.
        if (scanLoopSource.isPlaying)
        {
            scanLoopSource.Stop();
        }
    }

    private void TrySpawnOneDot()
    {
        // 화면 중심의 작은 원 안에서 랜덤 좌표를 뽑는다.
        Vector2 offset = Random.insideUnitCircle * viewportRadius;

        float viewX = 0.5f + offset.x;
        float viewY = 0.5f + offset.y;

        // 뷰포트 기준으로 레이를 만든다.
        Ray ray = scanCamera.ViewportPointToRay(new Vector3(viewX, viewY, 0f));

        // 표면에 맞았을 때만 진행한다.
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, scanMask, triggerInteraction))
        {
            return;
        }

        // 점이 표면 안에 박히지 않도록 살짝 띄운다.
        Vector3 spawnPos = hit.point + hit.normal * surfaceOffset;

        // 셀 좌표로 바꾼다.
        Vector3Int cell = WorldToCell(spawnPos);

        // 이미 점이 있으면 생성하지 않는다.
        if (occupiedCells.Contains(cell))
        {
            return;
        }

        // 점 생성 후 셀을 기록한다.
        SpawnDot(spawnPos);
        occupiedCells.Add(cell);
    }

    private void SpawnDot(Vector3 position)
    {
        // 점 프리팹을 생성한다.
        Instantiate(dotPrefab, position, Quaternion.identity, dotContainer);
    }

    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        // 월드 좌표를 셀 좌표로 바꾼다.
        int x = Mathf.RoundToInt(worldPos.x / cellSize);
        int y = Mathf.RoundToInt(worldPos.y / cellSize);
        int z = Mathf.RoundToInt(worldPos.z / cellSize);
        return new Vector3Int(x, y, z);
    }
}