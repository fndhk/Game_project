using System.Collections.Generic;
using UnityEngine;

// 숲 맵을 자동으로 생성하는 생성기이다.
// ground / tree / rock / branch / bush 프리팹 배열을 사용한다.
// 생성된 오브젝트에는 스캔 색 분기용 ScanSurfaceInfo를 자동으로 붙인다.
// 플레이 시작 시 생성된 맵 렌더러를 꺼서 인게임에서는 보이지 않게 할 수 있다.
public class ForestBlockoutGenerator : MonoBehaviour
{
    [Header("Generate")]
    // 시작 시 자동 생성할지 여부이다.
    [SerializeField] private bool generateOnStart = true;

    // 생성 전에 이전 결과물을 비울지 여부이다.
    [SerializeField] private bool clearBeforeGenerate = true;

    [Header("Seed")]
    // 고정 시드를 사용할지 여부이다.
    [SerializeField] private bool useFixedSeed = false;

    // 고정 시드 값이다.
    [SerializeField] private int fixedSeed = 12345;

    // 마지막으로 실제 생성에 사용된 시드이다.
    [SerializeField] private int lastGeneratedSeed = 0;

    [Header("Map Size")]
    // 맵 전체 가로 길이이다.
    [SerializeField] private float mapWidth = 80f;

    // 맵 전체 세로 길이이다.
    [SerializeField] private float mapLength = 80f;

    // 맵 중심 좌표이다.
    [SerializeField] private Vector3 mapCenter = Vector3.zero;

    [Header("Ground")]
    // 바닥 프리팹 배열이다. 이 중 하나만 랜덤 선택된다.
    [SerializeField] private GameObject[] groundPrefabs;

    // 바닥 프리팹 Bounds를 못 읽을 때 사용할 기본 크기이다.
    [SerializeField] private Vector2 groundFallbackSize = new Vector2(10f, 10f);

    // 바닥 바닥면을 어느 Y에 둘지 정하는 값이다.
    [SerializeField] private float groundBaseY = 0f;

    [Header("Prop Prefabs")]
    // 나무 프리팹 배열이다.
    [SerializeField] private GameObject[] treePrefabs;

    // 바위 프리팹 배열이다.
    [SerializeField] private GameObject[] rockPrefabs;

    // 브런치 프리팹 배열이다.
    [SerializeField] private GameObject[] branchPrefabs;

    // 부시 프리팹 배열이다.
    [SerializeField] private GameObject[] bushPrefabs;

    [Header("Counts")]
    // 생성할 나무 개수이다.
    [SerializeField] private int treeCount = 90;

    // 생성할 바위 개수이다.
    [SerializeField] private int rockCount = 22;

    // 생성할 브런치 개수이다.
    [SerializeField] private int branchCount = 28;

    // 생성할 부시 개수이다.
    [SerializeField] private int bushCount = 35;

    [Header("Placement")]
    // 맵 끝부분 여백이다.
    [SerializeField] private float edgePadding = 3f;

    // 생성 위치를 찾을 때 한 오브젝트당 최대 시도 횟수이다.
    [SerializeField] private int maxPlacementAttemptsPerObject = 30;

    // 바닥에 살짝 띄우는 값이다.
    [SerializeField] private float placementYOffset = 0.02f;

    [Header("Spacing")]
    // 나무끼리 최소 간격이다.
    [SerializeField] private float treeMinSpacing = 2.8f;

    // 바위끼리 최소 간격이다.
    [SerializeField] private float rockMinSpacing = 2.2f;

    // 브런치끼리 최소 간격이다.
    [SerializeField] private float branchMinSpacing = 1.3f;

    // 부시끼리 최소 간격이다.
    [SerializeField] private float bushMinSpacing = 1.8f;

    [Header("Scale")]
    // 나무 랜덤 배율 범위이다.
    [SerializeField] private Vector2 treeScaleRange = new Vector2(0.9f, 1.15f);

    // 바위 랜덤 배율 범위이다.
    [SerializeField] private Vector2 rockScaleRange = new Vector2(0.85f, 1.2f);

    // 브런치 랜덤 배율 범위이다.
    [SerializeField] private Vector2 branchScaleRange = new Vector2(0.9f, 1.1f);

    // 부시 랜덤 배율 범위이다.
    [SerializeField] private Vector2 bushScaleRange = new Vector2(0.9f, 1.15f);

    [Header("Hierarchy")]
    // 생성된 오브젝트를 담는 루트이다.
    [SerializeField] private Transform generatedRoot;

    [Header("Runtime Visibility")]
    // 플레이 시작 시 생성된 맵 렌더러를 끌지 여부이다.
    [SerializeField] private bool hideGeneratedRenderersOnPlay = true;

    [Header("Scan Surface Auto Setup")]
    // 생성된 오브젝트들에 RevealSurface 레이어를 자동 적용할지 여부이다.
    [SerializeField] private bool applyRevealLayerAutomatically = true;

    [Header("Tree Surface Guess Keywords")]
    // 이름에 이 키워드가 들어가면 잎으로 판정한다.
    [SerializeField] private string[] leafKeywords =
    {
        "leaf",
        "leaves",
        "foliage",
        "crown",
        "canopy",
        "bush"
    };

    // 현재 생성된 바닥 인스턴스이다.
    private GameObject currentGroundInstance;

    // 생성된 오브젝트 기록용 리스트이다.
    private readonly List<GameObject> generatedObjects = new List<GameObject>();

    // 배치된 위치 기록용 리스트이다.
    private readonly List<Vector3> placedPoints = new List<Vector3>();

    private void Start()
    {
        // 시작 시 자동 생성 옵션이 켜져 있으면 즉시 생성한다.
        if (generateOnStart)
        {
            Generate();
        }

        // 플레이 중에는 렌더러 상태를 설정값에 맞게 맞춘다.
        RefreshGeneratedRendererState();
    }

    [ContextMenu("Generate Forest")]
    public void Generate()
    {
        // 이전에 생성된 맵을 먼저 지운다.
        if (clearBeforeGenerate)
        {
            ClearGenerated();
        }

        // 생성 루트를 보장한다.
        EnsureGeneratedRoot();

        // 이번 생성에 사용할 시드를 결정한다.
        lastGeneratedSeed = useFixedSeed ? fixedSeed : Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(lastGeneratedSeed);

        // 바닥부터 먼저 생성한다.
        SpawnGround();

        // 그 다음 나머지 오브젝트를 생성한다.
        SpawnGroup(treePrefabs, treeCount, treeScaleRange, treeMinSpacing, true, ScanSurfaceType.TreeTrunk);
        SpawnGroup(rockPrefabs, rockCount, rockScaleRange, rockMinSpacing, false, ScanSurfaceType.Rock);
        SpawnGroup(branchPrefabs, branchCount, branchScaleRange, branchMinSpacing, false, ScanSurfaceType.Branch);
        SpawnGroup(bushPrefabs, bushCount, bushScaleRange, bushMinSpacing, false, ScanSurfaceType.Bush);

        // 플레이 중에는 생성 직후 렌더러를 꺼준다.
        RefreshGeneratedRendererState();
    }

    [ContextMenu("Clear Generated")]
    public void ClearGenerated()
    {
        // 내부 기록을 먼저 비운다.
        placedPoints.Clear();
        generatedObjects.Clear();
        currentGroundInstance = null;

        // 현재 오브젝트 아래에 남아 있는 GeneratedForest 루트들을 전부 찾는다.
        List<Transform> rootsToDelete = new List<Transform>();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child.name == "GeneratedForest")
            {
                rootsToDelete.Add(child);
            }
        }

        // 혹시 generatedRoot가 따로 잡혀 있으면 그것도 삭제 후보에 넣는다.
        if (generatedRoot != null && generatedRoot.parent == transform && !rootsToDelete.Contains(generatedRoot))
        {
            rootsToDelete.Add(generatedRoot);
        }

        // 찾은 루트들을 전부 삭제한다.
        for (int i = rootsToDelete.Count - 1; i >= 0; i--)
        {
            Transform root = rootsToDelete[i];

            if (root == null)
            {
                continue;
            }

            // 플레이 중에는 먼저 꺼서 즉시 화면/충돌에서 빠지게 한다.
            if (Application.isPlaying)
            {
                root.gameObject.SetActive(false);
                Destroy(root.gameObject);
            }
            else
            {
#if UNITY_EDITOR
                DestroyImmediate(root.gameObject);
#else
                Destroy(root.gameObject);
#endif
            }
        }

        // 삭제 후 참조를 끊는다.
        generatedRoot = null;
    }

    // 생성 루트가 없으면 자동으로 만든다.
    private void EnsureGeneratedRoot()
    {
        // 이미 연결되어 있으면 그대로 사용한다.
        if (generatedRoot != null)
        {
            return;
        }

        // 혹시 이전에 만들어진 같은 이름의 루트가 남아 있으면 그걸 다시 잡는다.
        Transform oldRoot = transform.Find("GeneratedForest");
        if (oldRoot != null)
        {
            generatedRoot = oldRoot;
            return;
        }

        // 없으면 새로 만든다.
        GameObject root = new GameObject("GeneratedForest");
        root.transform.SetParent(transform, false);
        generatedRoot = root.transform;
    }

    // 바닥 하나를 선택해서 맵 크기에 맞게 생성한다.
    private void SpawnGround()
    {
        // 바닥 프리팹이 없으면 종료한다.
        if (groundPrefabs == null || groundPrefabs.Length == 0)
        {
            return;
        }

        // null이 아닌 바닥 프리팹 하나를 랜덤으로 선택한다.
        GameObject groundPrefab = GetRandomPrefab(groundPrefabs);
        if (groundPrefab == null)
        {
            return;
        }

        // 바닥을 맵 중심에 생성한다.
        currentGroundInstance = Instantiate(
            groundPrefab,
            new Vector3(mapCenter.x, groundBaseY, mapCenter.z),
            Quaternion.identity,
            generatedRoot
        );

        // 바닥 Bounds를 계산한다.
        Bounds groundBounds;
        bool hasBounds = TryGetCombinedBounds(currentGroundInstance, out groundBounds);

        // Bounds가 없으면 fallback 크기를 사용한다.
        float baseWidth = hasBounds && groundBounds.size.x > 0.001f ? groundBounds.size.x : groundFallbackSize.x;
        float baseLength = hasBounds && groundBounds.size.z > 0.001f ? groundBounds.size.z : groundFallbackSize.y;

        // 바닥이 맵 크기에 맞도록 X/Z 스케일을 조정한다.
        Vector3 scale = currentGroundInstance.transform.localScale;
        scale.x *= mapWidth / Mathf.Max(0.001f, baseWidth);
        scale.z *= mapLength / Mathf.Max(0.001f, baseLength);
        currentGroundInstance.transform.localScale = scale;

        // 바닥 아래면이 groundBaseY에 오도록 정렬한다.
        AlignObjectBottomToY(currentGroundInstance, groundBaseY);

        // 스캔용 레이어와 표면 타입을 세팅한다.
        SetupGeneratedSurface(currentGroundInstance, ScanSurfaceType.Ground);

        // 콜라이더가 하나도 없으면 최소한 루트 BoxCollider를 만들어준다.
        EnsureGroundColliderIfMissing(currentGroundInstance);

        // 기록용 리스트에 추가한다.
        generatedObjects.Add(currentGroundInstance);
        placedPoints.Add(new Vector3(mapCenter.x, 0f, mapCenter.z));
    }

    // 프리팹 그룹을 여러 개 생성한다.
    private void SpawnGroup(
        GameObject[] prefabs,
        int count,
        Vector2 scaleRange,
        float minSpacing,
        bool treatAsTree,
        ScanSurfaceType defaultSurfaceType)
    {
        // 프리팹 배열이 비어 있으면 종료한다.
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }

        // 개수가 0 이하이면 종료한다.
        if (count <= 0)
        {
            return;
        }

        // 원하는 개수만큼 생성한다.
        for (int i = 0; i < count; i++)
        {
            // 랜덤 프리팹 하나를 고른다.
            GameObject selectedPrefab = GetRandomPrefab(prefabs);
            if (selectedPrefab == null)
            {
                continue;
            }

            // 배치 위치를 찾지 못하면 이번 오브젝트는 건너뛴다.
            if (!TryGetSpawnPoint(minSpacing, out Vector3 spawnPoint))
            {
                continue;
            }

            // Y축 회전만 랜덤으로 준다.
            Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // 샘플된 지면 높이에 우선 생성한다.
            GameObject instance = Instantiate(selectedPrefab, spawnPoint, spawnRotation, generatedRoot);

            // 균일 랜덤 배율을 적용한다.
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            instance.transform.localScale *= randomScale;

            // 오브젝트 하단이 바닥에 맞도록 정렬한다.
            AlignObjectBottomToY(instance, spawnPoint.y + placementYOffset);

            // 스캔용 타입과 레이어를 세팅한다.
            if (treatAsTree)
            {
                SetupGeneratedTree(instance);
            }
            else
            {
                SetupGeneratedSurface(instance, defaultSurfaceType);
            }

            // 기록용 리스트에 추가한다.
            generatedObjects.Add(instance);
            placedPoints.Add(new Vector3(instance.transform.position.x, 0f, instance.transform.position.z));
        }
    }

    // 맵 내부에서 새 배치 지점을 찾는다.
    private bool TryGetSpawnPoint(float minSpacing, out Vector3 spawnPoint)
    {
        // 기본 반환값을 넣어둔다.
        spawnPoint = Vector3.zero;

        // 여러 번 시도해서 적당한 위치를 찾는다.
        for (int attempt = 0; attempt < maxPlacementAttemptsPerObject; attempt++)
        {
            // 맵 내부 랜덤 X/Z를 뽑는다.
            float x = Random.Range(
                mapCenter.x - (mapWidth * 0.5f) + edgePadding,
                mapCenter.x + (mapWidth * 0.5f) - edgePadding
            );

            float z = Random.Range(
                mapCenter.z - (mapLength * 0.5f) + edgePadding,
                mapCenter.z + (mapLength * 0.5f) - edgePadding
            );

            // 거리 체크용 후보 좌표이다.
            Vector3 candidate = new Vector3(x, 0f, z);

            // 기존 배치 위치와 너무 가까우면 다른 위치를 다시 찾는다.
            if (!IsFarEnough(candidate, minSpacing))
            {
                continue;
            }

            // ground 위 실제 높이를 샘플링한다.
            float sampledGroundY = SampleGroundY(x, z);

            // 최종 좌표를 만든다.
            spawnPoint = new Vector3(x, sampledGroundY, z);
            return true;
        }

        // 끝까지 실패하면 false를 반환한다.
        return false;
    }

    // 기존 배치 지점들과 충분히 떨어져 있는지 검사한다.
    private bool IsFarEnough(Vector3 candidate, float minSpacing)
    {
        // 최소 간격이 0 이하이면 항상 통과한다.
        if (minSpacing <= 0f)
        {
            return true;
        }

        // 수평 거리만 비교한다.
        for (int i = 0; i < placedPoints.Count; i++)
        {
            Vector3 placed = placedPoints[i];

            float distance = Vector2.Distance(
                new Vector2(candidate.x, candidate.z),
                new Vector2(placed.x, placed.z)
            );

            if (distance < minSpacing)
            {
                return false;
            }
        }

        return true;
    }

    // ground 위 실제 높이를 샘플링한다.
    private float SampleGroundY(float x, float z)
{
    // ground가 없으면 기본 높이를 반환한다.
    if (currentGroundInstance == null)
    {
        return groundBaseY;
    }

    // 위에서 아래로 레이를 쏴서 ground 콜라이더를 찾는다.
    Vector3 origin = new Vector3(x, groundBaseY + 200f, z);
    RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore);

    float nearestDistance = float.MaxValue;
    bool found = false;
    float bestY = groundBaseY;

    for (int i = 0; i < hits.Length; i++)
    {
        RaycastHit hit = hits[i];
        Transform hitTransform = hit.collider.transform;

        // ground 루트 collider도 허용하고, 자식 collider도 허용한다.
        bool isGroundRoot = hitTransform == currentGroundInstance.transform;
        bool isGroundChild = hitTransform.IsChildOf(currentGroundInstance.transform);

        if (!isGroundRoot && !isGroundChild)
        {
            continue;
        }

        // 가장 가까운 ground 히트만 사용한다.
        if (hit.distance < nearestDistance)
        {
            nearestDistance = hit.distance;
            bestY = hit.point.y;
            found = true;
        }
    }

    // ground를 찾았으면 그 높이를 반환한다.
    if (found)
    {
        return bestY;
    }

    // 못 찾았으면 bounds 상단 대신 기본 높이를 반환한다.
    // bounds.max.y를 쓰면 오브젝트가 떠 보일 수 있어서 fallback을 더 안전하게 바꾼다.
    return groundBaseY;
}

    // 오브젝트 하단이 목표 Y에 오도록 정렬한다.
    private void AlignObjectBottomToY(GameObject target, float targetBottomY)
    {
        // 대상이 없으면 종료한다.
        if (target == null)
        {
            return;
        }

        // Bounds를 구하지 못하면 종료한다.
        Bounds bounds;
        if (!TryGetCombinedBounds(target, out bounds))
        {
            return;
        }

        // 아래면 차이만큼 이동한다.
        float offsetY = targetBottomY - bounds.min.y;
        target.transform.position += new Vector3(0f, offsetY, 0f);
    }

    // 오브젝트 전체 Bounds를 계산한다.
    private bool TryGetCombinedBounds(GameObject target, out Bounds combinedBounds)
    {
        // 기본값이다.
        combinedBounds = default;

        // 대상이 없으면 실패이다.
        if (target == null)
        {
            return false;
        }

        // Renderer Bounds를 우선 사용한다.
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasAnyRenderer = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer current = renderers[i];

            if (current == null)
            {
                continue;
            }

            if (!hasAnyRenderer)
            {
                combinedBounds = current.bounds;
                hasAnyRenderer = true;
            }
            else
            {
                combinedBounds.Encapsulate(current.bounds);
            }
        }

        // Renderer가 하나라도 있었으면 성공이다.
        if (hasAnyRenderer)
        {
            return true;
        }

        // Renderer가 없으면 Collider Bounds를 대신 사용한다.
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        bool hasAnyCollider = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider current = colliders[i];

            if (current == null)
            {
                continue;
            }

            if (!hasAnyCollider)
            {
                combinedBounds = current.bounds;
                hasAnyCollider = true;
            }
            else
            {
                combinedBounds.Encapsulate(current.bounds);
            }
        }

        return hasAnyCollider;
    }

    // 배열에서 null이 아닌 프리팹 하나를 랜덤으로 가져온다.
    private GameObject GetRandomPrefab(GameObject[] prefabs)
    {
        // 배열이 비어 있으면 null이다.
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        // 유효 프리팹만 모은다.
        List<GameObject> validPrefabs = new List<GameObject>();

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                validPrefabs.Add(prefabs[i]);
            }
        }

        // 유효 프리팹이 없으면 null이다.
        if (validPrefabs.Count == 0)
        {
            return null;
        }

        // 그중 하나를 랜덤 반환한다.
        int randomIndex = Random.Range(0, validPrefabs.Count);
        return validPrefabs[randomIndex];
    }

    // 바닥에 콜라이더가 하나도 없으면 최소한 루트 BoxCollider를 넣는다.
    private void EnsureGroundColliderIfMissing(GameObject groundObject)
    {
        // 대상이 없으면 종료한다.
        if (groundObject == null)
        {
            return;
        }

        // 자식 포함 Collider가 이미 있으면 그대로 둔다.
        Collider[] colliders = groundObject.GetComponentsInChildren<Collider>(true);
        if (colliders != null && colliders.Length > 0)
        {
            return;
        }

        // Bounds를 읽을 수 없으면 종료한다.
        Bounds bounds;
        if (!TryGetCombinedBounds(groundObject, out bounds))
        {
            return;
        }

        // 루트에 BoxCollider를 추가한다.
        BoxCollider box = groundObject.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = groundObject.AddComponent<BoxCollider>();
        }

        // 월드 Bounds를 로컬 center/size로 대략 환산한다.
        Vector3 lossy = groundObject.transform.lossyScale;

        box.center = groundObject.transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            SafeDivide(bounds.size.x, Mathf.Abs(lossy.x)),
            SafeDivide(bounds.size.y, Mathf.Abs(lossy.y)),
            SafeDivide(bounds.size.z, Mathf.Abs(lossy.z))
        );
    }

    // 0으로 나누는 상황을 막기 위한 안전 나눗셈이다.
    private float SafeDivide(float value, float divisor)
    {
        // divisor가 너무 작으면 원래 값을 그대로 반환한다.
        if (Mathf.Abs(divisor) <= 0.0001f)
        {
            return value;
        }

        return value / divisor;
    }

    // 플레이 중 렌더러 상태를 설정값에 맞게 갱신한다.
    private void RefreshGeneratedRendererState()
    {
        // 플레이 중이 아니면 에디터 작업 화면은 그대로 보이게 둔다.
        if (!Application.isPlaying)
        {
            return;
        }

        // 생성 루트가 없으면 종료한다.
        if (generatedRoot == null)
        {
            return;
        }

        // 옵션에 따라 보이기/숨기기를 적용한다.
        bool shouldBeVisible = !hideGeneratedRenderersOnPlay;
        SetGeneratedRenderersVisible(shouldBeVisible);
    }

    // 생성 루트 아래 모든 렌더러를 한 번에 보이거나 숨긴다.
    private void SetGeneratedRenderersVisible(bool visible)
    {
        // 생성 루트가 없으면 종료한다.
        if (generatedRoot == null)
        {
            return;
        }

        // 생성된 맵 아래의 모든 Renderer를 가져온다.
        Renderer[] renderers = generatedRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];

            if (currentRenderer == null)
            {
                continue;
            }

            // 렌더러만 끄고, 콜라이더나 다른 컴포넌트는 건드리지 않는다.
            currentRenderer.enabled = visible;
        }
    }

    // 생성된 일반 오브젝트에 표면 타입과 레이어를 자동으로 세팅한다.
    private void SetupGeneratedSurface(GameObject instance, ScanSurfaceType surfaceType)
    {
        // 생성 실패 시 더 진행하지 않는다.
        if (instance == null)
        {
            return;
        }

        // 필요하면 RevealSurface 레이어를 전체 자식까지 재귀적으로 맞춘다.
        if (applyRevealLayerAutomatically)
        {
            ApplyRevealLayerRecursively(instance);
        }

        // 루트에 표면 타입 정보를 넣거나 기존 값을 갱신한다.
        SetSurfaceInfo(instance, surfaceType);
    }

    // 생성된 트리 오브젝트에 줄기/잎 타입을 자동 분리해서 세팅한다.
    private void SetupGeneratedTree(GameObject treeInstance)
    {
        // 생성 실패 시 더 진행하지 않는다.
        if (treeInstance == null)
        {
            return;
        }

        // 필요하면 RevealSurface 레이어를 전체 자식까지 재귀적으로 맞춘다.
        if (applyRevealLayerAutomatically)
        {
            ApplyRevealLayerRecursively(treeInstance);
        }

        // 트리 루트에는 타입을 붙이지 않는 편이 안전하다.
        RemoveSurfaceInfoIfExists(treeInstance);

        // 자식 콜라이더들을 모두 가져온다.
        Collider[] colliders = treeInstance.GetComponentsInChildren<Collider>(true);

        // 콜라이더가 하나도 없으면 최소한 루트에 줄기 타입이라도 넣는다.
        if (colliders == null || colliders.Length == 0)
        {
            SetSurfaceInfo(treeInstance, ScanSurfaceType.TreeTrunk);
            return;
        }

        // 각 콜라이더 이름을 보고 잎/줄기를 추정해서 타입을 넣는다.
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider currentCollider = colliders[i];

            if (currentCollider == null)
            {
                continue;
            }

            ScanSurfaceType detectedType = GuessTreeSurfaceType(currentCollider.gameObject.name);
            SetSurfaceInfo(currentCollider.gameObject, detectedType);
        }
    }

    // 오브젝트 이름을 보고 트리의 잎/줄기를 추정한다.
    private ScanSurfaceType GuessTreeSurfaceType(string objectName)
    {
        // 이름이 비어 있으면 기본적으로 줄기로 본다.
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return ScanSurfaceType.TreeTrunk;
        }

        // 비교를 쉽게 하기 위해 소문자로 바꾼다.
        string lowerName = objectName.ToLowerInvariant();

        // 잎 관련 키워드가 하나라도 있으면 잎으로 처리한다.
        for (int i = 0; i < leafKeywords.Length; i++)
        {
            string keyword = leafKeywords[i];

            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (lowerName.Contains(keyword.ToLowerInvariant()))
            {
                return ScanSurfaceType.TreeLeaf;
            }
        }

        // 그 외는 줄기로 처리한다.
        return ScanSurfaceType.TreeTrunk;
    }

    // 루트와 모든 자식의 레이어를 RevealSurface로 맞춘다.
    private void ApplyRevealLayerRecursively(GameObject root)
    {
        // 루트가 없으면 종료한다.
        if (root == null)
        {
            return;
        }

        // RevealSurface 레이어를 찾는다.
        int revealLayer = LayerMask.NameToLayer("RevealSurface");

        // 레이어가 없으면 아무 것도 하지 않는다.
        if (revealLayer < 0)
        {
            return;
        }

        // 모든 자식 Transform을 포함해서 순회한다.
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.layer = revealLayer;
        }
    }

    // 대상 오브젝트에 ScanSurfaceInfo를 넣거나 기존 값을 갱신한다.
    private void SetSurfaceInfo(GameObject target, ScanSurfaceType surfaceType)
    {
        // 대상이 없으면 종료한다.
        if (target == null)
        {
            return;
        }

        // 기존 컴포넌트를 찾고 없으면 새로 붙인다.
        ScanSurfaceInfo info = target.GetComponent<ScanSurfaceInfo>();
        if (info == null)
        {
            info = target.AddComponent<ScanSurfaceInfo>();
        }

        // 표면 타입 값을 갱신한다.
        info.surfaceType = surfaceType;
    }

    // 트리 루트에 잘못 붙은 ScanSurfaceInfo를 제거한다.
    private void RemoveSurfaceInfoIfExists(GameObject target)
    {
        // 대상이 없으면 종료한다.
        if (target == null)
        {
            return;
        }

        // 루트에 붙은 컴포넌트를 찾는다.
        ScanSurfaceInfo info = target.GetComponent<ScanSurfaceInfo>();
        if (info == null)
        {
            return;
        }

        // 에디터 상태와 플레이 상태를 나눠서 안전하게 제거한다.
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(info);
        }
        else
        {
            Destroy(info);
        }
#else
        Destroy(info);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        // 맵 범위를 Scene 뷰에서 확인하기 위한 기즈모이다.
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(
            new Vector3(mapCenter.x, groundBaseY, mapCenter.z),
            new Vector3(mapWidth, 0.1f, mapLength)
        );
    }
}