using System.Collections.Generic;
using UnityEngine;

public class RemoteScanPulseRenderer : MonoBehaviour
{
    private static RemoteScanPulseRenderer instance;

    [Header("Pulse Settings")]
    [SerializeField] private float pulseTravelDuration = 0.34f;
    [SerializeField] private int pointsPerPulse = 300;
    [SerializeField] private int maxSpawnAttemptsPerDot = 14;
    [SerializeField] private float maxDistance = 16f;
    [SerializeField] private float viewHalfWidth = 0.42f;
    [SerializeField] private float viewHalfHeight = 0.30f;
    [SerializeField] private float waveThickness = 1.05f;
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Readability Filter")]
    [SerializeField] private float minDotDistanceFromOrigin = 1.25f;

    [Range(0f, 1f)]
    [SerializeField] private float groundDotChance = 0.45f;

    [Range(0f, 1f)]
    [SerializeField] private float ceilingDotChance = 0.14f;

    [Range(0f, 1f)]
    [SerializeField] private float wallAndObjectDotChance = 1f;

    [Range(0.1f, 0.95f)]
    [SerializeField] private float horizontalSurfaceNormalThreshold = 0.55f;

    [Header("Performance")]
    [SerializeField] private int maxSamplesPerFrame = 42;

    [Header("Raycast")]
    [SerializeField] private LayerMask scanMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private bool preferMeshColliderHits = true;
    [SerializeField] private float meshColliderPreferenceDepth = 0.75f;

    private readonly List<RemotePulse> activePulses = new List<RemotePulse>();
    private readonly RaycastHit[] raycastHits = new RaycastHit[64];
    private InstancedScanDotRenderer cachedDotRenderer;

    private class RemotePulse
    {
        public Vector3 origin;
        public Vector3 forward;
        public Vector3 right;
        public Vector3 up;
        public float elapsedTime;
        public float sampleBudget;
    }

    private class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }
    }

    public static void RenderPulse(Vector3 origin, Vector3 forward, Vector3 up)
    {
        RemoteScanPulseRenderer renderer = EnsureExists();
        if (renderer != null)
        {
            renderer.AddPulse(origin, forward, up);
        }
    }

    private static RemoteScanPulseRenderer EnsureExists()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = Object.FindAnyObjectByType<RemoteScanPulseRenderer>();
        if (instance != null)
        {
            return instance;
        }

        GameObject pulseObject = new GameObject("RemoteScanPulseRenderer");
        instance = pulseObject.AddComponent<RemoteScanPulseRenderer>();
        DontDestroyOnLoad(pulseObject);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        pulseTravelDuration = Mathf.Max(0.01f, pulseTravelDuration);
        pointsPerPulse = Mathf.Max(1, pointsPerPulse);
        maxSpawnAttemptsPerDot = Mathf.Max(1, maxSpawnAttemptsPerDot);
        maxDistance = Mathf.Max(0.1f, maxDistance);
        waveThickness = Mathf.Max(0.05f, waveThickness);
        minDotDistanceFromOrigin = Mathf.Max(0f, minDotDistanceFromOrigin);
        maxSamplesPerFrame = Mathf.Max(1, maxSamplesPerFrame);
        meshColliderPreferenceDepth = Mathf.Max(0f, meshColliderPreferenceDepth);
        groundDotChance = Mathf.Clamp01(groundDotChance);
        ceilingDotChance = Mathf.Clamp01(ceilingDotChance);
        wallAndObjectDotChance = Mathf.Clamp01(wallAndObjectDotChance);
        horizontalSurfaceNormalThreshold = Mathf.Clamp(horizontalSurfaceNormalThreshold, 0.1f, 0.95f);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        UpdateActivePulses();
    }

    private void AddPulse(Vector3 origin, Vector3 forward, Vector3 up)
    {
        InstancedScanDotRenderer dotRenderer = ResolveDotRenderer();
        if (dotRenderer == null)
        {
            return;
        }

        Vector3 normalizedForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        Vector3 normalizedUp = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
        Vector3 right = Vector3.Cross(normalizedUp, normalizedForward);

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, normalizedForward);
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();
        normalizedUp = Vector3.Cross(normalizedForward, right).normalized;

        activePulses.Add(new RemotePulse
        {
            origin = origin,
            forward = normalizedForward,
            right = right,
            up = normalizedUp
        });
    }

    private void UpdateActivePulses()
    {
        if (activePulses.Count == 0)
        {
            return;
        }

        InstancedScanDotRenderer dotRenderer = ResolveDotRenderer();
        if (dotRenderer == null)
        {
            activePulses.Clear();
            return;
        }

        float samplesPerSecond = pointsPerPulse / pulseTravelDuration;
        int processedSamplesThisFrame = 0;

        for (int i = activePulses.Count - 1; i >= 0; i--)
        {
            RemotePulse pulse = activePulses[i];
            pulse.elapsedTime += Time.deltaTime;
            pulse.sampleBudget += samplesPerSecond * Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(pulse.elapsedTime / pulseTravelDuration);
            float currentRadius = normalizedTime * maxDistance;

            while (pulse.sampleBudget >= 1f && processedSamplesThisFrame < maxSamplesPerFrame)
            {
                pulse.sampleBudget -= 1f;
                processedSamplesThisFrame++;
                TrySpawnOneDotForPulse(dotRenderer, pulse, currentRadius);
            }

            if (pulse.elapsedTime >= pulseTravelDuration)
            {
                activePulses.RemoveAt(i);
            }

            if (processedSamplesThisFrame >= maxSamplesPerFrame)
            {
                break;
            }
        }
    }

    private void TrySpawnOneDotForPulse(InstancedScanDotRenderer dotRenderer, RemotePulse pulse, float currentRadius)
    {
        for (int attempt = 0; attempt < maxSpawnAttemptsPerDot; attempt++)
        {
            float horizontalOffset = Random.Range(-viewHalfWidth, viewHalfWidth);
            float verticalOffset = Random.Range(-viewHalfHeight, viewHalfHeight);
            Vector3 direction = pulse.forward + pulse.right * horizontalOffset + pulse.up * verticalOffset;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            Ray ray = new Ray(pulse.origin, direction.normalized);
            float bandStart = Mathf.Max(0f, currentRadius - waveThickness);
            float bandEnd = currentRadius;

            if (!TryGetBestScanHit(ray, bandStart, bandEnd, out RaycastHit hit))
            {
                continue;
            }

            Vector3 spawnPosition = hit.point + hit.normal * surfaceOffset;
            ScanDotColorGroup colorGroup = ResolveDotColorGroup(hit);
            dotRenderer.AddDot(spawnPosition, hit.normal, colorGroup);
            return;
        }
    }

    private InstancedScanDotRenderer ResolveDotRenderer()
    {
        if (cachedDotRenderer != null && cachedDotRenderer.isActiveAndEnabled)
        {
            return cachedDotRenderer;
        }

        InstancedScanDotRenderer[] renderers = Object.FindObjectsByType<InstancedScanDotRenderer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].isActiveAndEnabled)
            {
                cachedDotRenderer = renderers[i];
                return cachedDotRenderer;
            }
        }

        cachedDotRenderer = null;
        return null;
    }

    private bool TryGetBestScanHit(Ray ray, float bandStart, float bandEnd, out RaycastHit bestHit)
    {
        bestHit = default;

        if (!preferMeshColliderHits)
        {
            if (!Physics.Raycast(ray, out RaycastHit singleHit, maxDistance, scanMask, triggerInteraction))
            {
                return false;
            }

            if (!IsUsableHit(singleHit, bandStart, bandEnd))
            {
                return false;
            }

            bestHit = singleHit;
            return true;
        }

        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, maxDistance, scanMask, triggerInteraction);

        if (hitCount <= 0)
        {
            return false;
        }

        System.Array.Sort(raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        RaycastHit firstSurfaceHit = default;
        bool hasFirstSurface = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];

            if (hit.collider == null || ShouldIgnoreScanHit(hit))
            {
                continue;
            }

            firstSurfaceHit = hit;
            hasFirstSurface = true;
            break;
        }

        if (!hasFirstSurface || !IsHitInCurrentBand(firstSurfaceHit, bandStart, bandEnd) || !ShouldKeepHitForReadability(firstSurfaceHit))
        {
            return false;
        }

        if (firstSurfaceHit.collider is MeshCollider)
        {
            bestHit = firstSurfaceHit;
            return true;
        }

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];

            if (hit.collider == null || !(hit.collider is MeshCollider))
            {
                continue;
            }

            if (ShouldIgnoreScanHit(hit))
            {
                continue;
            }

            if (hit.distance < firstSurfaceHit.distance || hit.distance > firstSurfaceHit.distance + meshColliderPreferenceDepth)
            {
                continue;
            }

            if (!IsHitInCurrentBand(hit, bandStart, bandEnd))
            {
                continue;
            }

            bestHit = hit;
            return true;
        }

        bestHit = firstSurfaceHit;
        return true;
    }

    private bool IsUsableHit(RaycastHit hit, float bandStart, float bandEnd)
    {
        return IsHitInCurrentBand(hit, bandStart, bandEnd) &&
               !ShouldIgnoreScanHit(hit) &&
               ShouldKeepHitForReadability(hit);
    }

    private bool IsHitInCurrentBand(RaycastHit hit, float bandStart, float bandEnd)
    {
        return hit.collider != null && hit.distance >= bandStart && hit.distance <= bandEnd;
    }

    private bool ShouldIgnoreScanHit(RaycastHit hit)
    {
        return hit.collider == null || IsGeneratedHelperTransform(hit.collider.transform);
    }

    private bool IsGeneratedHelperTransform(Transform target)
    {
        Transform current = target;

        while (current != null && current != transform)
        {
            string objectName = current.name;

            if (ContainsIgnoreCase(objectName, "DoorPoint") ||
                ContainsIgnoreCase(objectName, "TempPortal") ||
                ContainsIgnoreCase(objectName, "PlayerSpawnPoint") ||
                ContainsIgnoreCase(objectName, "ItemSpawnPoint"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) &&
               source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private enum HitSurfaceClass
    {
        WallOrObject,
        Ground,
        Ceiling
    }

    private bool ShouldKeepHitForReadability(RaycastHit hit)
    {
        if (hit.distance < minDotDistanceFromOrigin)
        {
            return false;
        }

        HitSurfaceClass surfaceClass = ResolveHitSurfaceClass(hit.normal);
        float keepChance = wallAndObjectDotChance;

        if (surfaceClass == HitSurfaceClass.Ground)
        {
            keepChance = groundDotChance;
        }
        else if (surfaceClass == HitSurfaceClass.Ceiling)
        {
            keepChance = ceilingDotChance;
        }

        return Random.value <= keepChance;
    }

    private HitSurfaceClass ResolveHitSurfaceClass(Vector3 normal)
    {
        if (normal.y >= horizontalSurfaceNormalThreshold)
        {
            return HitSurfaceClass.Ground;
        }

        if (normal.y <= -horizontalSurfaceNormalThreshold)
        {
            return HitSurfaceClass.Ceiling;
        }

        return HitSurfaceClass.WallOrObject;
    }

    private ScanDotColorGroup ResolveDotColorGroup(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }

        ObjectiveComputer objectiveComputer = hit.collider.GetComponentInParent<ObjectiveComputer>();
        if (objectiveComputer != null)
        {
            return SurfaceTypeToDotColorGroup(objectiveComputer.CurrentScanSurfaceType, hit.normal, hit.collider);
        }

        ScanSurfaceInfo surfaceInfo = hit.collider.GetComponent<ScanSurfaceInfo>();
        if (surfaceInfo == null)
        {
            surfaceInfo = hit.collider.GetComponentInParent<ScanSurfaceInfo>();
        }

        if (surfaceInfo == null)
        {
            return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }

        return SurfaceTypeToDotColorGroup(surfaceInfo.surfaceType, hit.normal, hit.collider);
    }

    private ScanDotColorGroup SurfaceTypeToDotColorGroup(ScanSurfaceType surfaceType, Vector3 fallbackNormal, Collider hitCollider)
    {
        switch (surfaceType)
        {
            case ScanSurfaceType.Floor:
                return ScanDotColorGroup.Floor;

            case ScanSurfaceType.Wall:
                return ScanDotColorGroup.Wall;

            case ScanSurfaceType.Metal:
                return ScanDotColorGroup.Metal;

            case ScanSurfaceType.Glass:
                return ScanDotColorGroup.Glass;

            case ScanSurfaceType.AccessCore:
                return ScanDotColorGroup.AccessCore;

            case ScanSurfaceType.SecurityTerminal:
                return ScanDotColorGroup.SecurityTerminal;

            case ScanSurfaceType.EmergencyExit:
                return ScanDotColorGroup.EmergencyExit;

            case ScanSurfaceType.PlayerBody:
                PlayerCombatTarget playerTarget = hitCollider != null ? hitCollider.GetComponentInParent<PlayerCombatTarget>() : null;
                return playerTarget != null
                    ? PlayerColorPalette.GetScanColorGroupForActor(playerTarget.GetActorNumber())
                    : ScanDotColorGroup.PlayerBody;

            case ScanSurfaceType.Creature:
                return ScanDotColorGroup.Creature;

            case ScanSurfaceType.WrongComputer:
                return ScanDotColorGroup.WrongComputer;

            case ScanSurfaceType.RestoredEscapeComputer:
                return ScanDotColorGroup.RestoredEscapeComputer;

            case ScanSurfaceType.Item:
                return ScanDotColorGroup.Item;

            default:
                return ResolveFallbackDotColorGroupByNormal(fallbackNormal);
        }
    }

    private ScanDotColorGroup ResolveFallbackDotColorGroupByNormal(Vector3 normal)
    {
        HitSurfaceClass surfaceClass = ResolveHitSurfaceClass(normal);

        if (surfaceClass == HitSurfaceClass.Ground)
        {
            return ScanDotColorGroup.Floor;
        }

        if (surfaceClass == HitSurfaceClass.Ceiling)
        {
            return ScanDotColorGroup.Wall;
        }

        return ScanDotColorGroup.Wall;
    }
}
