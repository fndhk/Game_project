using UnityEngine;

// 플레이어의 실제 시각 외형을 붙이는 런타임 아바타이다.
// 스캔용 PlayerScanAvatar는 Collider만 담당하고, 이 스크립트는 렌더링만 담당한다.
public class PlayerVisibleAvatar : MonoBehaviour
{
    [Header("Model")]
    public string visualRootName = "VisibleCharacterAvatar";
    public string resourcesModelPath = "My Robot Kyle -done-";
    public float targetHeight = 1.78f;
    public float yawOffset = 0f;
    public bool rebuildOnEnable = true;

    [Header("Local Player")]
    public bool hideWhenLocalScannerOwner = true;

    [Header("Scan Surface")]
    public bool addScanColliders = true;
    public int scanLayer = 7;
    public ScanSurfaceType surfaceType = ScanSurfaceType.PlayerBody;

    private Transform visualRoot;

    private void OnEnable()
    {
        if (rebuildOnEnable)
        {
            EnsureAvatar();
        }
    }

    private void Awake()
    {
        if (rebuildOnEnable)
        {
            EnsureAvatar();
        }
    }

    private void LateUpdate()
    {
        EnsureAvatar();
        ApplyLocalVisibility();
    }

    [ContextMenu("Rebuild Visible Avatar")]
    public void RebuildAvatar()
    {
        Transform existing = transform.Find(visualRootName);

        if (existing != null)
        {
            DestroyAvatarObject(existing.gameObject);
        }

        visualRoot = null;
        EnsureAvatar();
    }

    private void EnsureAvatar()
    {
        if (visualRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(visualRootName);

        if (existing != null)
        {
            visualRoot = existing;
            ApplyLocalVisibility();
            return;
        }

        GameObject rootObject = new GameObject(visualRootName);
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f);
        rootObject.transform.localScale = Vector3.one;
        visualRoot = rootObject.transform;

        GameObject modelPrefab = Resources.Load<GameObject>(resourcesModelPath);

        if (modelPrefab != null)
        {
            GameObject model = Instantiate(modelPrefab, visualRoot);
            model.name = "HumanoidModel";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            StripGameplayComponents(model);
            DisablePhysics(model);
            FitModelToHeight(model.transform);
            AddScanCollidersToRenderers();
        }
        else
        {
            BuildFallbackHuman();
        }

        ApplyLocalVisibility();
    }

    private void StripGameplayComponents(GameObject model)
    {
        MonoBehaviour[] behaviours = model.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            if (behaviours[i] != null)
            {
                DestroyAvatarObject(behaviours[i]);
            }
        }
    }

    private void DisablePhysics(GameObject model)
    {
        Collider[] colliders = model.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        Rigidbody[] rigidbodies = model.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] != null)
            {
                DestroyAvatarObject(rigidbodies[i]);
            }
        }
    }

    private void FitModelToHeight(Transform model)
    {
        Bounds bounds;

        if (!TryGetRendererBounds(out bounds))
        {
            return;
        }

        float currentHeight = Mathf.Max(0.01f, bounds.size.y);
        float scale = targetHeight / currentHeight;
        model.localScale *= scale;

        if (!TryGetRendererBounds(out bounds))
        {
            return;
        }

        float bottomOffset = bounds.min.y - transform.position.y;
        model.position -= Vector3.up * bottomOffset;
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        Renderer[] renderers = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : null;
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;

        if (renderers == null)
        {
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void BuildFallbackHuman()
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard"));
        material.color = new Color(0.08f, 0.11f, 0.13f, 1f);

        CreateFallbackPart("Head", PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0f), new Vector3(0.24f, 0.28f, 0.22f), material);
        CreateFallbackPart("Body", PrimitiveType.Capsule, new Vector3(0f, 0.94f, 0f), new Vector3(0.42f, 0.72f, 0.28f), material);
        CreateFallbackPart("LeftArm", PrimitiveType.Capsule, new Vector3(-0.36f, 0.95f, 0f), new Vector3(0.14f, 0.58f, 0.14f), material);
        CreateFallbackPart("RightArm", PrimitiveType.Capsule, new Vector3(0.36f, 0.95f, 0f), new Vector3(0.14f, 0.58f, 0.14f), material);
        CreateFallbackPart("LeftLeg", PrimitiveType.Capsule, new Vector3(-0.14f, 0.29f, 0f), new Vector3(0.15f, 0.58f, 0.15f), material);
        CreateFallbackPart("RightLeg", PrimitiveType.Capsule, new Vector3(0.14f, 0.29f, 0f), new Vector3(0.15f, 0.58f, 0.15f), material);
    }

    private void CreateFallbackPart(string partName, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(visualRoot, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        part.layer = addScanColliders ? scanLayer : gameObject.layer;

        Collider collider = part.GetComponent<Collider>();

        if (collider != null)
        {
            collider.enabled = addScanColliders;
        }

        Renderer renderer = part.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        ScanSurfaceInfo surfaceInfo = part.GetComponent<ScanSurfaceInfo>();

        if (addScanColliders && surfaceInfo == null)
        {
            surfaceInfo = part.AddComponent<ScanSurfaceInfo>();
        }

        if (surfaceInfo != null)
        {
            surfaceInfo.surfaceType = surfaceType;
        }
    }

    private void AddScanCollidersToRenderers()
    {
        if (!addScanColliders || visualRoot == null)
        {
            return;
        }

        MeshFilter[] meshFilters = visualRoot.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            if (meshFilters[i] == null || meshFilters[i].sharedMesh == null)
            {
                continue;
            }

            AddMeshScanCollider(meshFilters[i].gameObject, meshFilters[i].sharedMesh);
        }

        SkinnedMeshRenderer[] skinnedRenderers = visualRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int i = 0; i < skinnedRenderers.Length; i++)
        {
            if (skinnedRenderers[i] == null || skinnedRenderers[i].sharedMesh == null)
            {
                continue;
            }

            AddMeshScanCollider(skinnedRenderers[i].gameObject, skinnedRenderers[i].sharedMesh);
        }
    }

    private void AddMeshScanCollider(GameObject target, Mesh mesh)
    {
        if (target == null || mesh == null)
        {
            return;
        }

        target.layer = scanLayer;

        MeshCollider meshCollider = target.GetComponent<MeshCollider>();

        if (meshCollider == null)
        {
            meshCollider = target.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false;
        meshCollider.enabled = true;

        ScanSurfaceInfo surfaceInfo = target.GetComponent<ScanSurfaceInfo>();

        if (surfaceInfo == null)
        {
            surfaceInfo = target.AddComponent<ScanSurfaceInfo>();
        }

        surfaceInfo.surfaceType = surfaceType;
    }

    private void ApplyLocalVisibility()
    {
        if (visualRoot == null)
        {
            return;
        }

        bool shouldHide = hideWhenLocalScannerOwner && GetComponent<LidarSpotScanner>() != null;
        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = !shouldHide;
            }
        }
    }

    private void DestroyAvatarObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
