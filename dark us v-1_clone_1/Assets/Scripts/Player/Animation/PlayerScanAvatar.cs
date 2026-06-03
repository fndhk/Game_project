using System.Collections.Generic;
using UnityEngine;

// 플레이어가 직접 보이지는 않지만, 스캔 Raycast에는 사람 형태로 잡히게 만드는 숨김 아바타이다.
// Renderer는 꺼두고 Collider만 유지해서 LIDAR 점이 네모/원기둥이 아닌 캐릭터 실루엣으로 찍힌다.
[ExecuteAlways]
public class PlayerScanAvatar : MonoBehaviour
{
    private const string AvatarVersionMarkerName = "ScanAvatar_V2";

    [Header("Scan Visibility")]
    public bool buildOnEnable = true;
    public string avatarRootName = "ScanOnlyCharacterAvatar";
    public int scanLayer = 7;
    public ScanSurfaceType surfaceType = ScanSurfaceType.PlayerBody;
    public bool hideRenderers = true;
    public bool hideAttachedBodyRenderers = true;
    public bool disableSelfScanWhenScannerIsHere = true;

    [Header("Pose")]
    public float standingHeight = 2.05f;
    public float shoulderWidth = 0.62f;
    public float hipWidth = 0.36f;
    public float animationSpeed = 7.5f;
    public float armSwingAngle = 28f;
    public float legSwingAngle = 24f;
    public float breathingAmount = 0.018f;

    private Transform avatarRoot;
    private CharacterController characterController;
    private readonly List<LimbPose> limbPoses = new List<LimbPose>();
    private float walkCycle;
    private Vector3 lastWorldPosition;

    private class LimbPose
    {
        public Transform transform;
        public Quaternion baseRotation;
        public Vector3 baseLocalPosition;
        public float phase;
        public float swingScale;
        public bool swingsOnX;
    }

    private void OnEnable()
    {
        if (buildOnEnable)
        {
            EnsureAvatar();
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        lastWorldPosition = transform.position;

        if (buildOnEnable)
        {
            EnsureAvatar();
        }
    }

    private void Update()
    {
        EnsureAvatar();
        ApplyVisibilityState();

        if (Application.isPlaying)
        {
            AnimateAvatar();
        }
    }

    [ContextMenu("Rebuild Scan Avatar")]
    public void RebuildAvatar()
    {
        Transform existing = transform.Find(avatarRootName);

        if (existing != null)
        {
            DestroyAvatarObject(existing.gameObject);
        }

        avatarRoot = null;
        limbPoses.Clear();
        EnsureAvatar();
    }

    private void EnsureAvatar()
    {
        if (avatarRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(avatarRootName);

        if (existing != null)
        {
            if (existing.Find(AvatarVersionMarkerName) == null)
            {
                DestroyAvatarObject(existing.gameObject);
                existing = null;
            }
        }

        if (existing != null)
        {
            avatarRoot = existing;
            CacheExistingLimbPoses();
            ApplyVisibilityState();
            return;
        }

        GameObject rootObject = new GameObject(avatarRootName);
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        rootObject.layer = scanLayer;
        avatarRoot = rootObject.transform;

        BuildBody();
        ApplyVisibilityState();
        IgnoreOwnControllerCollision();
    }

    private void BuildBody()
    {
        limbPoses.Clear();

        GameObject marker = new GameObject(AvatarVersionMarkerName);
        marker.transform.SetParent(avatarRoot, false);
        marker.layer = scanLayer;

        CreateEllipsoidPart("Head", new Vector3(0f, 1.77f, 0.02f), Quaternion.identity, new Vector3(0.16f, 0.21f, 0.145f), false, 0f, 0f);
        CreateEllipsoidPart("FaceBridge", new Vector3(0f, 1.76f, 0.155f), Quaternion.identity, new Vector3(0.055f, 0.09f, 0.045f), false, 0f, 0f);
        CreateTaperedPart("Neck", new Vector3(0f, 1.53f, 0f), Quaternion.identity, 0.085f, 0.07f, 0.075f, 0.065f, 0.18f, false, 0f, 0f);

        CreateTaperedPart("Chest", new Vector3(0f, 1.24f, 0f), Quaternion.identity, 0.34f, 0.19f, 0.22f, 0.15f, 0.62f, false, 0f, 0f);
        CreateTaperedPart("Abdomen", new Vector3(0f, 0.91f, 0f), Quaternion.identity, 0.22f, 0.145f, 0.19f, 0.13f, 0.34f, false, 0f, 0f);
        CreateEllipsoidPart("Pelvis", new Vector3(0f, 0.68f, 0f), Quaternion.identity, new Vector3(0.25f, 0.16f, 0.135f), false, 0f, 0f);

        CreateTaperedPart("LeftShoulder", new Vector3(-shoulderWidth * 0.37f, 1.43f, 0f), Quaternion.Euler(0f, 0f, 74f), 0.085f, 0.075f, 0.09f, 0.07f, 0.25f, false, 0f, 0f);
        CreateTaperedPart("RightShoulder", new Vector3(shoulderWidth * 0.37f, 1.43f, 0f), Quaternion.Euler(0f, 0f, -74f), 0.085f, 0.075f, 0.09f, 0.07f, 0.25f, false, 0f, 0f);

        CreateTaperedPart("LeftUpperArm", new Vector3(-shoulderWidth * 0.61f, 1.20f, 0.005f), Quaternion.Euler(0f, 0f, 13f), 0.08f, 0.07f, 0.062f, 0.055f, 0.46f, true, 0f, 0.85f);
        CreateTaperedPart("RightUpperArm", new Vector3(shoulderWidth * 0.61f, 1.20f, 0.005f), Quaternion.Euler(0f, 0f, -13f), 0.08f, 0.07f, 0.062f, 0.055f, 0.46f, true, Mathf.PI, 0.85f);
        CreateTaperedPart("LeftForearm", new Vector3(-shoulderWidth * 0.69f, 0.84f, 0.035f), Quaternion.Euler(0f, 0f, 5f), 0.063f, 0.055f, 0.048f, 0.045f, 0.41f, true, Mathf.PI * 0.25f, 0.75f);
        CreateTaperedPart("RightForearm", new Vector3(shoulderWidth * 0.69f, 0.84f, 0.035f), Quaternion.Euler(0f, 0f, -5f), 0.063f, 0.055f, 0.048f, 0.045f, 0.41f, true, Mathf.PI * 1.25f, 0.75f);
        CreateEllipsoidPart("LeftHand", new Vector3(-shoulderWidth * 0.71f, 0.58f, 0.045f), Quaternion.Euler(0f, 0f, 8f), new Vector3(0.055f, 0.085f, 0.045f), true, Mathf.PI * 0.25f, 0.35f);
        CreateEllipsoidPart("RightHand", new Vector3(shoulderWidth * 0.71f, 0.58f, 0.045f), Quaternion.Euler(0f, 0f, -8f), new Vector3(0.055f, 0.085f, 0.045f), true, Mathf.PI * 1.25f, 0.35f);

        CreateTaperedPart("LeftThigh", new Vector3(-hipWidth * 0.43f, 0.34f, 0.01f), Quaternion.Euler(0f, 0f, 2f), 0.105f, 0.085f, 0.075f, 0.065f, 0.58f, true, Mathf.PI, 1f);
        CreateTaperedPart("RightThigh", new Vector3(hipWidth * 0.43f, 0.34f, 0.01f), Quaternion.Euler(0f, 0f, -2f), 0.105f, 0.085f, 0.075f, 0.065f, 0.58f, true, 0f, 1f);
        CreateTaperedPart("LeftShin", new Vector3(-hipWidth * 0.43f, -0.12f, 0.025f), Quaternion.Euler(0f, 0f, 1f), 0.075f, 0.062f, 0.055f, 0.047f, 0.54f, true, 0.35f, 0.75f);
        CreateTaperedPart("RightShin", new Vector3(hipWidth * 0.43f, -0.12f, 0.025f), Quaternion.Euler(0f, 0f, -1f), 0.075f, 0.062f, 0.055f, 0.047f, 0.54f, true, Mathf.PI + 0.35f, 0.75f);
        CreateEllipsoidPart("LeftFoot", new Vector3(-hipWidth * 0.43f, -0.43f, 0.13f), Quaternion.Euler(0f, 0f, 0f), new Vector3(0.075f, 0.045f, 0.16f), true, 0.35f, 0.35f);
        CreateEllipsoidPart("RightFoot", new Vector3(hipWidth * 0.43f, -0.43f, 0.13f), Quaternion.Euler(0f, 0f, 0f), new Vector3(0.075f, 0.045f, 0.16f), true, Mathf.PI + 0.35f, 0.35f);
    }

    private GameObject CreateTaperedPart(string name, Vector3 localPosition, Quaternion localRotation, float topRadiusX, float topRadiusZ, float bottomRadiusX, float bottomRadiusZ, float height, bool animated, float phase, float swingScale)
    {
        Mesh mesh = CreateTaperedCylinderMesh(name + "_ScanMesh", topRadiusX, topRadiusZ, bottomRadiusX, bottomRadiusZ, height, 14, 5);
        return CreateMeshPart(name, mesh, localPosition, localRotation, animated, phase, swingScale);
    }

    private GameObject CreateEllipsoidPart(string name, Vector3 localPosition, Quaternion localRotation, Vector3 radii, bool animated, float phase, float swingScale)
    {
        Mesh mesh = CreateEllipsoidMesh(name + "_ScanMesh", radii, 14, 7);
        return CreateMeshPart(name, mesh, localPosition, localRotation, animated, phase, swingScale);
    }

    private GameObject CreateMeshPart(string name, Mesh mesh, Vector3 localPosition, Quaternion localRotation, bool animated, float phase, float swingScale)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(avatarRoot, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = Vector3.one;
        part.layer = scanLayer;

        MeshCollider meshCollider = part.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false;

        SetupPart(part);

        if (animated)
        {
            limbPoses.Add(new LimbPose
            {
                transform = part.transform,
                baseRotation = localRotation,
                baseLocalPosition = localPosition,
                phase = phase,
                swingScale = swingScale,
                swingsOnX = true
            });
        }

        return part;
    }

    private Mesh CreateTaperedCylinderMesh(string meshName, float topRadiusX, float topRadiusZ, float bottomRadiusX, float bottomRadiusZ, float height, int radialSegments, int heightSegments)
    {
        Mesh mesh = new Mesh();
        mesh.name = meshName;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int y = 0; y <= heightSegments; y++)
        {
            float t = y / (float)heightSegments;
            float radiusX = Mathf.Lerp(bottomRadiusX, topRadiusX, t);
            float radiusZ = Mathf.Lerp(bottomRadiusZ, topRadiusZ, t);
            float localY = Mathf.Lerp(-height * 0.5f, height * 0.5f, t);

            for (int i = 0; i < radialSegments; i++)
            {
                float angle = i / (float)radialSegments * Mathf.PI * 2f;
                float subtleAsymmetry = 1f + Mathf.Sin(angle * 2f + t * Mathf.PI) * 0.045f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radiusX * subtleAsymmetry, localY, Mathf.Sin(angle) * radiusZ));
            }
        }

        for (int y = 0; y < heightSegments; y++)
        {
            int row = y * radialSegments;
            int nextRow = (y + 1) * radialSegments;

            for (int i = 0; i < radialSegments; i++)
            {
                int next = (i + 1) % radialSegments;
                triangles.Add(row + i);
                triangles.Add(nextRow + i);
                triangles.Add(row + next);
                triangles.Add(row + next);
                triangles.Add(nextRow + i);
                triangles.Add(nextRow + next);
            }
        }

        AddCap(vertices, triangles, radialSegments, 0, true);
        AddCap(vertices, triangles, radialSegments, heightSegments * radialSegments, false);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private Mesh CreateEllipsoidMesh(string meshName, Vector3 radii, int radialSegments, int verticalSegments)
    {
        Mesh mesh = new Mesh();
        mesh.name = meshName;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int y = 0; y <= verticalSegments; y++)
        {
            float v = y / (float)verticalSegments;
            float polar = Mathf.Lerp(0f, Mathf.PI, v);
            float ringY = Mathf.Cos(polar) * radii.y;
            float ringScale = Mathf.Sin(polar);

            for (int i = 0; i < radialSegments; i++)
            {
                float angle = i / (float)radialSegments * Mathf.PI * 2f;
                float cheek = Mathf.Max(0f, Mathf.Sin(angle)) * Mathf.Sin(v * Mathf.PI) * 0.035f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radii.x * ringScale, ringY, Mathf.Sin(angle) * (radii.z + cheek) * ringScale));
            }
        }

        for (int y = 0; y < verticalSegments; y++)
        {
            int row = y * radialSegments;
            int nextRow = (y + 1) * radialSegments;

            for (int i = 0; i < radialSegments; i++)
            {
                int next = (i + 1) % radialSegments;
                triangles.Add(row + i);
                triangles.Add(nextRow + i);
                triangles.Add(row + next);
                triangles.Add(row + next);
                triangles.Add(nextRow + i);
                triangles.Add(nextRow + next);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private void AddCap(List<Vector3> vertices, List<int> triangles, int radialSegments, int rowStart, bool bottom)
    {
        Vector3 center = Vector3.zero;

        for (int i = 0; i < radialSegments; i++)
        {
            center += vertices[rowStart + i];
        }

        center /= radialSegments;
        int centerIndex = vertices.Count;
        vertices.Add(center);

        for (int i = 0; i < radialSegments; i++)
        {
            int next = (i + 1) % radialSegments;

            if (bottom)
            {
                triangles.Add(centerIndex);
                triangles.Add(rowStart + next);
                triangles.Add(rowStart + i);
            }
            else
            {
                triangles.Add(centerIndex);
                triangles.Add(rowStart + i);
                triangles.Add(rowStart + next);
            }
        }
    }

    private void SetupPart(GameObject part)
    {
        ScanSurfaceInfo surfaceInfo = part.GetComponent<ScanSurfaceInfo>();

        if (surfaceInfo == null)
        {
            surfaceInfo = part.AddComponent<ScanSurfaceInfo>();
        }

        surfaceInfo.surfaceType = surfaceType;

        Renderer renderer = part.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.enabled = !hideRenderers;
        }
    }

    private void ApplyVisibilityState()
    {
        if (avatarRoot == null)
        {
            return;
        }

        if (hideAttachedBodyRenderers)
        {
            Renderer[] attachedRenderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < attachedRenderers.Length; i++)
            {
                Renderer attachedRenderer = attachedRenderers[i];

                if (attachedRenderer == null || attachedRenderer.transform.IsChildOf(avatarRoot))
                {
                    continue;
                }

                attachedRenderer.enabled = false;
            }
        }

        SetLayerRecursively(avatarRoot, scanLayer);

        Renderer[] renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = !hideRenderers;
            }
        }

        ScanSurfaceInfo[] surfaceInfos = avatarRoot.GetComponentsInChildren<ScanSurfaceInfo>(true);

        for (int i = 0; i < surfaceInfos.Length; i++)
        {
            if (surfaceInfos[i] != null)
            {
                surfaceInfos[i].surfaceType = surfaceType;
            }
        }

        bool shouldDisableSelfScan = ShouldDisableSelfScan();
        Collider[] avatarColliders = avatarRoot.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < avatarColliders.Length; i++)
        {
            if (avatarColliders[i] != null)
            {
                avatarColliders[i].enabled = !shouldDisableSelfScan;
            }
        }

        IgnoreOwnControllerCollision();
    }

    private void CacheExistingLimbPoses()
    {
        limbPoses.Clear();

        if (avatarRoot == null)
        {
            return;
        }

        CacheLimb("LeftUpperArm", 0f, 0.85f);
        CacheLimb("RightUpperArm", Mathf.PI, 0.85f);
        CacheLimb("LeftForearm", Mathf.PI * 0.25f, 0.75f);
        CacheLimb("RightForearm", Mathf.PI * 1.25f, 0.75f);
        CacheLimb("LeftThigh", Mathf.PI, 1f);
        CacheLimb("RightThigh", 0f, 1f);
        CacheLimb("LeftShin", 0.35f, 0.75f);
        CacheLimb("RightShin", Mathf.PI + 0.35f, 0.75f);
    }

    private void CacheLimb(string childName, float phase, float swingScale)
    {
        Transform limb = avatarRoot.Find(childName);

        if (limb == null)
        {
            return;
        }

        limbPoses.Add(new LimbPose
        {
            transform = limb,
            baseRotation = limb.localRotation,
            baseLocalPosition = limb.localPosition,
            phase = phase,
            swingScale = swingScale,
            swingsOnX = true
        });
    }

    private void AnimateAvatar()
    {
        float normalizedSpeed = GetNormalizedSpeed();
        walkCycle += Time.deltaTime * animationSpeed * Mathf.Lerp(0.35f, 1f, normalizedSpeed);
        float swingWeight = Mathf.Clamp01(normalizedSpeed * 1.4f);

        if (avatarRoot != null)
        {
            Vector3 rootPos = Vector3.zero;
            rootPos.y = Mathf.Sin(Time.time * 2.2f) * breathingAmount * (1f - swingWeight * 0.35f);
            avatarRoot.localPosition = rootPos;
        }

        for (int i = 0; i < limbPoses.Count; i++)
        {
            LimbPose pose = limbPoses[i];

            if (pose == null || pose.transform == null)
            {
                continue;
            }

            float swing = Mathf.Sin(walkCycle + pose.phase) * swingWeight;
            float angle = swing * (pose.swingScale >= 0.95f ? legSwingAngle : armSwingAngle) * pose.swingScale;
            pose.transform.localRotation = pose.baseRotation * Quaternion.Euler(angle, 0f, 0f);
            pose.transform.localPosition = pose.baseLocalPosition + new Vector3(0f, Mathf.Abs(swing) * 0.015f, 0f);
        }
    }

    private float GetNormalizedSpeed()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController == null)
        {
            Vector3 currentPosition = transform.position;
            Vector3 delta = currentPosition - lastWorldPosition;
            delta.y = 0f;
            lastWorldPosition = currentPosition;

            if (Time.deltaTime <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(delta.magnitude / Time.deltaTime / 3.5f);
        }

        Vector3 velocity = characterController.velocity;
        lastWorldPosition = transform.position;
        velocity.y = 0f;
        return Mathf.Clamp01(velocity.magnitude / 3.5f);
    }

    private void IgnoreOwnControllerCollision()
    {
        if (avatarRoot == null)
        {
            return;
        }

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController == null)
        {
            return;
        }

        Collider[] avatarColliders = avatarRoot.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < avatarColliders.Length; i++)
        {
            Collider avatarCollider = avatarColliders[i];

            if (avatarCollider != null)
            {
                Physics.IgnoreCollision(characterController, avatarCollider, true);
            }
        }
    }

    private bool ShouldDisableSelfScan()
    {
        if (!disableSelfScanWhenScannerIsHere)
        {
            return false;
        }

        return GetComponent<LidarSpotScanner>() != null;
    }

    private void SetLayerRecursively(Transform root, int targetLayer)
    {
        root.gameObject.layer = targetLayer;

        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), targetLayer);
        }
    }

    private void DestroyAvatarObject(GameObject target)
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
