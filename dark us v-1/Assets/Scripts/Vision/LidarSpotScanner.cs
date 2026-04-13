using System.Collections.Generic;
using UnityEngine;

// 우클릭을 누르고 있는 동안 화면 중심의 작은 범위를 스캔해서
// 맞은 표면에 흰 점을 계속 생성하는 스캐너이다.
public class LidarSpotScanner : MonoBehaviour
{
    [Header("References")]
    // 점으로 사용할 프리팹이다.
    [SerializeField] private GameObject dotPrefab;

    // 생성된 점들을 정리해서 담아둘 부모 오브젝트이다.
    [SerializeField] private Transform dotContainer;

    // 스캔 기준이 되는 카메라이다.
    [SerializeField] private Camera scanCamera;

    [Header("Scan Settings")]
    // 우클릭을 누르고 있을 때 1초에 몇 개의 점을 찍을지 정한다.
    [SerializeField] private float pointsPerSecond = 900f;

    // 스캔 최대 거리이다.
    [SerializeField] private float maxDistance = 20f;

    // 화면 중심 기준 스캔 반경이다. 0.08 ~ 0.18 정도가 적당하다.
    [SerializeField] private float viewportRadius = 0.12f;

    // 점을 표면에서 아주 살짝 띄워서 z-fighting을 막는다.
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Duplicate Block")]
    // 같은 위치 근처에 점이 이미 있으면 새로 만들지 않기 위한 셀 크기이다.
    [SerializeField] private float cellSize = 0.08f;

    // 너무 비스듬한 면까지 다 찍고 싶지 않을 때 사용할 수 있다.
    [SerializeField] private bool useNormalCheck = false;

    // 표면 노멀 비교 강도이다.
    [SerializeField] private float normalDotThreshold = 0.85f;

    [Header("Raycast")]
    // 점을 찍을 레이어만 선택한다.
    [SerializeField] private LayerMask scanMask = ~0;

    // 트리거 충돌체를 무시할지 정한다.
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    // 한 프레임에 누적해서 찍을 양을 보정한다.
    private float spawnBudget;

    // 이미 점이 찍힌 셀을 저장해서 중복 생성을 막는다.
    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    // 셀별 대표 노멀을 저장해서 같은 셀이어도 표면 방향이 완전히 다르면 허용할 수 있게 한다.
    private readonly Dictionary<Vector3Int, Vector3> cellNormals = new Dictionary<Vector3Int, Vector3>();

    private void Reset()
    {
        // 기본적으로 현재 오브젝트의 카메라를 자동 연결한다.
        scanCamera = GetComponent<Camera>();
    }

    private void Awake()
    {
        // 카메라가 비어 있으면 메인 카메라를 가져온다.
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }

        // 점 컨테이너가 없으면 자동 생성한다.
        if (dotContainer == null)
        {
            GameObject container = new GameObject("ScanDots");
            dotContainer = container.transform;
        }
    }

    private void Update()
    {
        // 우클릭을 누르고 있는 동안만 스캔한다.
        if (!Input.GetMouseButton(1))
        {
            return;
        }

        // 초당 점 개수를 프레임 단위로 누적한다.
        spawnBudget += pointsPerSecond * Time.deltaTime;

        // 누적된 만큼 점 생성을 반복한다.
        while (spawnBudget >= 1f)
        {
            spawnBudget -= 1f;
            TrySpawnOneDot();
        }
    }

    private void TrySpawnOneDot()
    {
        // 화면 중심 기준 작은 원 안에서 랜덤 샘플을 뽑는다.
        Vector2 offset = Random.insideUnitCircle * viewportRadius;

        float viewX = 0.5f + offset.x;
        float viewY = 0.5f + offset.y;

        // 뷰포트 좌표에서 레이를 만든다.
        Ray ray = scanCamera.ViewportPointToRay(new Vector3(viewX, viewY, 0f));

        // 표면에 맞은 경우에만 점을 생성한다.
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, scanMask, triggerInteraction))
        {
            return;
        }

        // 점이 표면 안으로 파묻히지 않게 살짝 띄운 위치를 만든다.
        Vector3 spawnPos = hit.point + hit.normal * surfaceOffset;

        // 위치를 그리드 셀로 바꿔서 중복 생성을 막는다.
        Vector3Int cell = WorldToCell(spawnPos);

        // 이미 같은 셀에 점이 있다면 생성하지 않는다.
        if (occupiedCells.Contains(cell))
        {
            // 노멀 비교를 쓰는 경우에는 방향이 크게 다를 때만 허용한다.
            if (useNormalCheck && cellNormals.TryGetValue(cell, out Vector3 savedNormal))
            {
                if (Vector3.Dot(savedNormal, hit.normal) < normalDotThreshold)
                {
                    SpawnDot(spawnPos, hit.normal);
                    cellNormals[cell] = hit.normal;
                }
            }

            return;
        }

        // 새 셀이라면 점을 생성하고 기록한다.
        SpawnDot(spawnPos, hit.normal);
        occupiedCells.Add(cell);
        cellNormals[cell] = hit.normal;
    }

    private void SpawnDot(Vector3 position, Vector3 normal)
    {
        // 점 프리팹을 생성해서 컨테이너 밑에 넣는다.
        GameObject dot = Instantiate(dotPrefab, position, Quaternion.identity, dotContainer);

        // 점이 표면 방향을 기준으로 눕도록 회전시킨다.
        dot.transform.rotation = Quaternion.LookRotation(normal);
    }

    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        // 월드 좌표를 일정 간격의 셀 좌표로 바꿔서 중복 체크에 사용한다.
        int x = Mathf.RoundToInt(worldPos.x / cellSize);
        int y = Mathf.RoundToInt(worldPos.y / cellSize);
        int z = Mathf.RoundToInt(worldPos.z / cellSize);
        return new Vector3Int(x, y, z);
    }

    public void ClearAllDots()
    {
        // 생성된 점을 전부 지우고 중복 기록도 초기화한다.
        for (int i = dotContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(dotContainer.GetChild(i).gameObject);
        }

        occupiedCells.Clear();
        cellNormals.Clear();
        spawnBudget = 0f;
    }
}