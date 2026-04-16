using UnityEngine;

// 숲 맵을 랜덤으로 생성하는 생성기이다.
// Ground, Tree, Rock, Branch, Bush 다섯 종류만 생성한다.
public class ForestBlockoutGenerator : MonoBehaviour
{
    [Header("Generate")]
    // 시작 시 자동 생성할지 정한다.
    public bool generateOnStart = true;

    // 생성 전에 기존 자식 오브젝트를 지울지 정한다.
    public bool clearChildrenBeforeGenerate = true;

    [Header("Map Area")]
    // 맵의 전체 배치 범위이다.
    public Vector2 mapSize = new Vector2(80f, 80f);

    // 맵 중심 위치이다.
    public Vector3 center = Vector3.zero;

    [Header("Counts")]
    // 생성할 나무 개수이다.
    public int treeCount = 70;

    // 생성할 돌 개수이다.
    public int rockCount = 20;

    // 생성할 브런치 개수이다.
    public int branchCount = 18;

    // 생성할 부시 개수이다.
    public int bushCount = 25;

    [Header("Prefabs")]
    // Ground 배열 중 하나만 랜덤으로 선택해서 생성한다.
    public GameObject[] groundPrefabs;

    // Tree 배열에서 랜덤으로 선택해서 생성한다.
    public GameObject[] treePrefabs;

    // Rock 배열에서 랜덤으로 선택해서 생성한다.
    public GameObject[] rockPrefabs;

    // Branch 배열에서 랜덤으로 선택해서 생성한다.
    public GameObject[] branchPrefabs;

    // Bush 배열에서 랜덤으로 선택해서 생성한다.
    public GameObject[] bushPrefabs;

    [Header("Ground Fit")]
    // Ground의 최저점이 맞춰질 기준 높이이다.
    public float groundYOffset = 0f;

    // Ground의 두께 방향 배율이다.
    public float groundHeightScale = 1f;

    [Header("Placement Offset")]
    // Tree를 표면 위로 얼마나 더 띄울지 정하는 보정값이다.
    public float treeSurfaceOffset = 0f;

    // Rock을 표면 위로 얼마나 더 띄울지 정하는 보정값이다.
    public float rockSurfaceOffset = 0f;

    // Branch를 표면 위로 얼마나 더 띄울지 정하는 보정값이다.
    public float branchSurfaceOffset = 0f;

    // Bush를 표면 위로 얼마나 더 띄울지 정하는 보정값이다.
    public float bushSurfaceOffset = 0f;

    [Header("Random Scale")]
    // Tree 랜덤 배율 범위이다.
    public Vector2 treeScaleRange = new Vector2(0.9f, 1.15f);

    // Rock 랜덤 배율 범위이다.
    public Vector2 rockScaleRange = new Vector2(0.9f, 1.15f);

    // Branch 랜덤 배율 범위이다.
    public Vector2 branchScaleRange = new Vector2(0.9f, 1.1f);

    // Bush 랜덤 배율 범위이다.
    public Vector2 bushScaleRange = new Vector2(0.9f, 1.15f);

    [Header("Random Rotation")]
    // Tree의 Y축 랜덤 회전 여부이다.
    public bool randomTreeYaw = true;

    // Rock의 Y축 랜덤 회전 여부이다.
    public bool randomRockYaw = true;

    // Branch의 Y축 랜덤 회전 여부이다.
    public bool randomBranchYaw = true;

    // Bush의 Y축 랜덤 회전 여부이다.
    public bool randomBushYaw = true;

    [Header("Layer")]
    // RevealSurface 레이어를 자동으로 넣을지 정한다.
    public bool assignRevealSurfaceLayer = true;

    // 자동으로 넣을 레이어 이름이다.
    public string revealSurfaceLayerName = "RevealSurface";

    // 현재 생성된 Ground를 저장한다.
    private GameObject currentGroundInstance;

    // 현재 Ground의 Bounds를 저장한다.
    private Bounds currentGroundBounds;

    // Ground에 사용 가능한 Collider 목록이다.
    private Collider[] currentGroundColliders;

    private void Start()
    {
        // 시작 시 자동 생성 옵션이 켜져 있으면 바로 생성한다.
        if (generateOnStart)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Forest")]
    public void Generate()
    {
        // 기존 생성 결과를 지우도록 설정되어 있으면 먼저 정리한다.
        if (clearChildrenBeforeGenerate)
        {
            ClearChildren();
        }

        // 캐시를 먼저 비운다.
        currentGroundInstance = null;
        currentGroundColliders = null;

        // Ground를 먼저 생성하고 표면 정보를 준비한다.
        CreateGround();

        // Ground가 준비된 뒤 나머지 오브젝트를 표면 기준으로 배치한다.
        ScatterTrees();
        ScatterRocks();
        ScatterBranches();
        ScatterBushes();
    }

    private void ClearChildren()
    {
        // 생성기 하위의 기존 오브젝트를 뒤에서부터 안전하게 삭제한다.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

#if UNITY_EDITOR
            // 에디터 모드에서는 즉시 삭제한다.
            if (!Application.isPlaying)
            {
                DestroyImmediate(child.gameObject);
            }
            else
            {
                Destroy(child.gameObject);
            }
#else
            Destroy(child.gameObject);
#endif
        }
    }

    private void CreateGround()
    {
        // Ground 프리팹 배열이 비어 있으면 생성하지 않는다.
        if (groundPrefabs == null || groundPrefabs.Length == 0)
        {
            Debug.LogWarning("[ForestBlockoutGenerator] Ground Prefabs 배열이 비어 있다.");
            return;
        }

        // Ground 배열에서 하나를 랜덤 선택한다.
        GameObject selectedGroundPrefab = GetRandomPrefab(groundPrefabs);
        if (selectedGroundPrefab == null)
        {
            Debug.LogWarning("[ForestBlockoutGenerator] 선택된 Ground 프리팹이 null 이다.");
            return;
        }

        // Ground를 우선 생성기 하위에 생성한다.
        currentGroundInstance = Instantiate(
            selectedGroundPrefab,
            center,
            Quaternion.identity,
            transform
        );

        // 이름을 보기 쉽게 정리한다.
        currentGroundInstance.name = $"Ground_{selectedGroundPrefab.name}";

        // Ground 레이어를 먼저 맞춘다.
        ApplyRevealLayerRecursively(currentGroundInstance);

        // Ground의 실제 크기를 mapSize에 맞게 자동으로 조절한다.
        FitGroundToMap(currentGroundInstance);

        // Ground에 Collider가 없다면 자동으로 추가해서 표면 판정을 가능하게 한다.
        EnsureGroundColliders(currentGroundInstance);

        // 이후 배치 계산에서 사용할 Ground Bounds를 다시 갱신한다.
        if (!TryGetCombinedBounds(currentGroundInstance, out currentGroundBounds))
        {
            currentGroundBounds = new Bounds(center, new Vector3(mapSize.x, 10f, mapSize.y));
        }

        // Ground Collider 목록을 캐시한다.
        currentGroundColliders = currentGroundInstance.GetComponentsInChildren<Collider>();
    }

    private void FitGroundToMap(GameObject groundRoot)
    {
        // Ground가 비어 있으면 중단한다.
        if (groundRoot == null)
        {
            return;
        }

        // 현재 Ground의 실제 Bounds를 구한다.
        if (!TryGetCombinedBounds(groundRoot, out Bounds originalBounds))
        {
            Debug.LogWarning("[ForestBlockoutGenerator] Ground Bounds를 계산하지 못했다.");
            return;
        }

        // Bounds 크기가 비정상이면 스케일 계산을 하지 않는다.
        if (originalBounds.size.x <= 0.001f || originalBounds.size.z <= 0.001f)
        {
            Debug.LogWarning("[ForestBlockoutGenerator] Ground Bounds 크기가 너무 작아서 자동 스케일을 할 수 없다.");
            return;
        }

        // 현재 로컬 스케일을 기준으로 원하는 맵 크기에 맞는 배율을 계산한다.
        Vector3 baseScale = groundRoot.transform.localScale;
        float scaleX = mapSize.x / originalBounds.size.x;
        float scaleZ = mapSize.y / originalBounds.size.z;

        // XZ는 맵 크기에 맞추고, Y는 두께 보정 배율만 적용한다.
        groundRoot.transform.localScale = new Vector3(
            baseScale.x * scaleX,
            baseScale.y * groundHeightScale,
            baseScale.z * scaleZ
        );

        // 스케일 적용 후 Bounds를 다시 구한다.
        if (!TryGetCombinedBounds(groundRoot, out Bounds fittedBounds))
        {
            return;
        }

        // Ground 중심을 map center에 맞추고, Ground의 최저점을 groundYOffset에 맞춘다.
        Vector3 positionOffset = new Vector3(
            center.x - fittedBounds.center.x,
            groundYOffset - fittedBounds.min.y,
            center.z - fittedBounds.center.z
        );

        groundRoot.transform.position += positionOffset;
    }

    private void EnsureGroundColliders(GameObject groundRoot)
    {
        // Ground가 비어 있으면 중단한다.
        if (groundRoot == null)
        {
            return;
        }

        // 이미 Collider가 있으면 그대로 사용한다.
        Collider[] existingColliders = groundRoot.GetComponentsInChildren<Collider>();
        if (existingColliders != null && existingColliders.Length > 0)
        {
            return;
        }

        // Collider가 전혀 없으면 MeshFilter가 있는 자식마다 MeshCollider를 추가한다.
        MeshFilter[] meshFilters = groundRoot.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            // 이미 같은 오브젝트에 Collider가 있으면 추가하지 않는다.
            if (meshFilter.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
        }
    }

    private void ScatterTrees()
    {
        // Tree만 생성하도록 별도 호출한다.
        ScatterPrefabs(treePrefabs, treeCount, "Tree", treeSurfaceOffset, treeScaleRange, randomTreeYaw);
    }

    private void ScatterRocks()
    {
        // Rock만 생성하도록 별도 호출한다.
        ScatterPrefabs(rockPrefabs, rockCount, "Rock", rockSurfaceOffset, rockScaleRange, randomRockYaw);
    }

    private void ScatterBranches()
    {
        // Branch만 생성하도록 별도 호출한다.
        ScatterPrefabs(branchPrefabs, branchCount, "Branch", branchSurfaceOffset, branchScaleRange, randomBranchYaw);
    }

    private void ScatterBushes()
    {
        // Bush만 생성하도록 별도 호출한다.
        ScatterPrefabs(bushPrefabs, bushCount, "Bush", bushSurfaceOffset, bushScaleRange, randomBushYaw);
    }

    private void ScatterPrefabs(
        GameObject[] prefabs,
        int count,
        string objectPrefix,
        float surfaceOffset,
        Vector2 scaleRange,
        bool randomYaw)
    {
        // 사용할 프리팹 배열이 비어 있으면 생성하지 않는다.
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning($"[ForestBlockoutGenerator] {objectPrefix} Prefabs 배열이 비어 있다.");
            return;
        }

        // Ground가 없으면 표면 배치를 할 수 없으므로 생성하지 않는다.
        if (currentGroundInstance == null)
        {
            Debug.LogWarning($"[ForestBlockoutGenerator] Ground가 없어 {objectPrefix}를 생성할 수 없다.");
            return;
        }

        // 개수만큼 반복 생성한다.
        for (int i = 0; i < count; i++)
        {
            GameObject selectedPrefab = GetRandomPrefab(prefabs);
            if (selectedPrefab == null)
            {
                continue;
            }

            // 우선 XZ만 결정하고 Y는 나중에 표면에 맞춘다.
            Vector3 randomXZ = RandomInsideMap();

            // 임시 높은 위치에 먼저 생성한다.
            Vector3 temporaryPosition = new Vector3(randomXZ.x, GetPlacementRayStartY(), randomXZ.z);
            Quaternion rotation = randomYaw
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Quaternion.identity;

            GameObject instance = Instantiate(selectedPrefab, temporaryPosition, rotation, transform);
            instance.name = $"{objectPrefix}_{i}_{selectedPrefab.name}";

            // 기본 스케일에 랜덤 배율을 곱한다.
            float randomScale = Random.Range(scaleRange.x, scaleRange.y);
            instance.transform.localScale *= randomScale;

            // 표면 위로 정확히 올려놓는다.
            SnapInstanceToGroundSurface(instance, randomXZ, surfaceOffset);

            // RevealSurface 레이어를 하위까지 맞춘다.
            ApplyRevealLayerRecursively(instance);
        }
    }

    private void SnapInstanceToGroundSurface(GameObject instance, Vector3 xzPosition, float surfaceOffset)
    {
        // 대상이 없으면 중단한다.
        if (instance == null)
        {
            return;
        }

        // Ground 표면을 찾지 못하면 임시로 중심 높이에 배치한다.
        if (!TryGetGroundHit(xzPosition, out RaycastHit hit))
        {
            instance.transform.position = new Vector3(xzPosition.x, center.y + surfaceOffset, xzPosition.z);
            return;
        }

        // 오브젝트의 실제 시각 Bounds를 구해서 바닥과 맞춘다.
        if (!TryGetCombinedBounds(instance, out Bounds instanceBounds))
        {
            instance.transform.position = hit.point + Vector3.up * surfaceOffset;
            return;
        }

        // 현재 pivot에서 Bounds 최저점까지의 거리를 구한다.
        float bottomToPivot = instance.transform.position.y - instanceBounds.min.y;

        // Ground 표면 위에 딱 얹히도록 최종 위치를 계산한다.
        Vector3 finalPosition = new Vector3(
            xzPosition.x,
            hit.point.y + bottomToPivot + surfaceOffset,
            xzPosition.z
        );

        instance.transform.position = finalPosition;
    }

    private bool TryGetGroundHit(Vector3 xzPosition, out RaycastHit bestHit)
    {
        // 기본값을 초기화한다.
        bestHit = default(RaycastHit);

        // Ground Collider가 없으면 실패 처리한다.
        if (currentGroundColliders == null || currentGroundColliders.Length == 0)
        {
            return false;
        }

        // Ground 상단보다 충분히 높은 곳에서 아래로 레이를 쏜다.
        Vector3 rayStart = new Vector3(xzPosition.x, GetPlacementRayStartY(), xzPosition.z);
        Ray ray = new Ray(rayStart, Vector3.down);

        bool hitFound = false;
        float closestDistance = float.MaxValue;

        // Ground Collider만 직접 검사해서 나무/돌 같은 다른 오브젝트를 무시한다.
        for (int i = 0; i < currentGroundColliders.Length; i++)
        {
            Collider groundCollider = currentGroundColliders[i];
            if (groundCollider == null)
            {
                continue;
            }

            if (groundCollider.Raycast(ray, out RaycastHit hit, 10000f))
            {
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    bestHit = hit;
                    hitFound = true;
                }
            }
        }

        return hitFound;
    }

    private float GetPlacementRayStartY()
    {
        // Ground Bounds 위쪽에서 충분히 여유 있게 시작한다.
        return currentGroundBounds.max.y + 100f;
    }

    private GameObject GetRandomPrefab(GameObject[] prefabs)
    {
        // 배열이 비어 있으면 null 을 반환한다.
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        // 배열 중 랜덤 하나를 반환한다.
        return prefabs[Random.Range(0, prefabs.Length)];
    }

    private Vector3 RandomInsideMap()
    {
        // 중심 기준으로 맵 내부의 랜덤 XZ를 만든다.
        float x = Random.Range(-mapSize.x * 0.5f, mapSize.x * 0.5f);
        float z = Random.Range(-mapSize.y * 0.5f, mapSize.y * 0.5f);
        return center + new Vector3(x, 0f, z);
    }

    private bool TryGetCombinedBounds(GameObject target, out Bounds combinedBounds)
    {
        // 기본값을 먼저 만든다.
        combinedBounds = default(Bounds);

        // Renderer가 있으면 시각 기준 Bounds를 우선 사용한다.
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            bool initialized = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer rendererComponent = renderers[i];
                if (rendererComponent == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    combinedBounds = rendererComponent.bounds;
                    initialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(rendererComponent.bounds);
                }
            }

            if (initialized)
            {
                return true;
            }
        }

        // Renderer가 없으면 Collider Bounds를 대체로 사용한다.
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            bool initialized = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider colliderComponent = colliders[i];
                if (colliderComponent == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    combinedBounds = colliderComponent.bounds;
                    initialized = true;
                }
                else
                {
                    combinedBounds.Encapsulate(colliderComponent.bounds);
                }
            }

            if (initialized)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyRevealLayerRecursively(GameObject root)
    {
        // 자동 레이어 변경이 꺼져 있으면 아무것도 하지 않는다.
        if (!assignRevealSurfaceLayer)
        {
            return;
        }

        // 대상이 없으면 중단한다.
        if (root == null)
        {
            return;
        }

        // 레이어가 없으면 기본 레이어를 유지한다.
        int targetLayer = LayerMask.NameToLayer(revealSurfaceLayerName);
        if (targetLayer < 0)
        {
            return;
        }

        // 루트와 자식 전체에 같은 레이어를 적용한다.
        SetLayerRecursively(root.transform, targetLayer);
    }

    private void SetLayerRecursively(Transform current, int targetLayer)
    {
        // 현재 오브젝트 레이어를 바꾼다.
        current.gameObject.layer = targetLayer;

        // 모든 자식에게 재귀적으로 적용한다.
        for (int i = 0; i < current.childCount; i++)
        {
            SetLayerRecursively(current.GetChild(i), targetLayer);
        }
    }
}