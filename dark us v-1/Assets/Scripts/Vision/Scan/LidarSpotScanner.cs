using System.Collections.Generic;
using UnityEngine;

// 우클릭 시 화면에 보이는 넓은 범위를 훑어서 표면에 점을 찍는 스캐너이다.
// 실제 점 렌더링은 InstancedScanDotRenderer가 맡고,
// 이 스크립트는 스캔 파동 진행과 Raycast 샘플링만 담당한다.
public class LidarSpotScanner : MonoBehaviour
{
    [Header("References")]
    // 스캔 기준 카메라이다.
    [SerializeField] private Camera scanCamera;

    // 실제 점을 GPU 인스턴싱으로 그리는 렌더러이다.
    [SerializeField] private InstancedScanDotRenderer instancedDotRenderer;

    [Header("Scan Audio")]
    // 스캔 사운드 소스이다.
    [SerializeField] private AudioSource scanPulseSource;

    [Header("Pulse Settings")]
    // 우클릭 후 다음 사용까지의 쿨타임이다.
    [SerializeField] private float pulseCooldown = 0.42f;

    // 파동이 최대 거리까지 퍼지는 시간이다.
    [SerializeField] private float pulseTravelDuration = 0.34f;

    // 파동 1회 총 샘플 수이다.
    [SerializeField] private int pointsPerPulse = 380;

    // 점 1개를 만들기 위해 재시도할 최대 횟수이다.
    [SerializeField] private int maxSpawnAttemptsPerDot = 14;

    // 최대 스캔 거리이다.
    [SerializeField] private float maxDistance = 16f;

    // 화면 가로 절반 범위이다.
    [SerializeField] private float screenHalfWidth = 0.42f;

    // 화면 세로 절반 범위이다.
    [SerializeField] private float screenHalfHeight = 0.30f;

    // 현재 파동 띠 두께이다.
    [SerializeField] private float waveThickness = 1.05f;

    // 표면에서 점을 살짝 띄우는 값이다.
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Readability Filter")]
    // 카메라와 너무 가까운 점은 화면을 크게 가리기 때문에 생성하지 않는다.
    [SerializeField] private float minDotDistanceFromCamera = 1.25f;

    // 바닥 점 생성 확률이다.
    [Range(0f, 1f)]
    [SerializeField] private float groundDotChance = 0.45f;

    // 천장 점 생성 확률이다.
    [Range(0f, 1f)]
    [SerializeField] private float ceilingDotChance = 0.14f;

    // 벽과 일반 오브젝트 점 생성 확률이다.
    [Range(0f, 1f)]
    [SerializeField] private float wallAndObjectDotChance = 1f;

    // 바닥/천장 판정을 위한 노멀 기준값이다.
    [Range(0.1f, 0.95f)]
    [SerializeField] private float horizontalSurfaceNormalThreshold = 0.55f;

    [Header("Performance")]
    // 한 프레임에 처리할 최대 샘플 수이다.
    // 점이 많이 찍혀도 프레임이 뚝 끊기지 않게 분산한다.
    [SerializeField] private int maxSamplesPerFrame = 42;

    [Header("Raycast")]
    // 스캔 대상 레이어이다.
    [SerializeField] private LayerMask scanMask = ~0;

    // 트리거 처리 방식이다.
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    // 다음 스캔 가능 시간이다.
    private float nextPulseTime = 0f;

    // 현재 진행 중인 파동 목록이다.
    private readonly List<ActivePulse> activePulses = new List<ActivePulse>();

    // 파동 1개의 진행 상태이다.
    private class ActivePulse
    {
        // 파동 시작 후 지난 시간이다.
        public float elapsedTime = 0f;

        // 이번 프레임까지 누적된 샘플 예산이다.
        public float sampleBudget = 0f;
    }

    private void Reset()
    {
        // 기본 참조를 자동으로 채운다.
        scanCamera = GetComponent<Camera>();
        scanPulseSource = GetComponent<AudioSource>();
        instancedDotRenderer = GetComponent<InstancedScanDotRenderer>();
    }

    private void Awake()
    {
        // 최소값 보정이다.
        pointsPerPulse = Mathf.Max(1, pointsPerPulse);
        maxSpawnAttemptsPerDot = Mathf.Max(1, maxSpawnAttemptsPerDot);
        pulseTravelDuration = Mathf.Max(0.01f, pulseTravelDuration);
        waveThickness = Mathf.Max(0.05f, waveThickness);
        maxSamplesPerFrame = Mathf.Max(1, maxSamplesPerFrame);
        minDotDistanceFromCamera = Mathf.Max(0f, minDotDistanceFromCamera);
        groundDotChance = Mathf.Clamp01(groundDotChance);
        ceilingDotChance = Mathf.Clamp01(ceilingDotChance);
        wallAndObjectDotChance = Mathf.Clamp01(wallAndObjectDotChance);
        horizontalSurfaceNormalThreshold = Mathf.Clamp(horizontalSurfaceNormalThreshold, 0.1f, 0.95f);
        // 카메라가 비어 있으면 메인 카메라를 사용한다.
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }

        // 오디오 설정을 정리한다.
        if (scanPulseSource != null)
        {
            scanPulseSource.playOnAwake = false;
            scanPulseSource.loop = false;
        }
    }

    private void Update()
    {
        // 준비가 안 되었으면 종료한다.
        if (!CanUseScanner())
        {
            return;
        }

        // 입력을 처리한다.
        HandlePulseInput();

        // 진행 중인 파동을 갱신한다.
        UpdateActivePulses();
    }

    // 스캐너를 사용할 수 있는지 확인하는 함수이다.
    private bool CanUseScanner()
    {
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }

        if (scanCamera == null)
        {
            return false;
        }

        if (!scanCamera.isActiveAndEnabled)
        {
            return false;
        }

        if (instancedDotRenderer == null)
        {
            return false;
        }

        return true;
    }

    // 우클릭 입력을 처리하는 함수이다.
    private void HandlePulseInput()
    {
        // 우클릭을 누르고 있는 동안 계속 처리한다.
        if (!Input.GetMouseButton(1))
        {
            return;
        }

        // 쿨타임 중이면 새 파동을 만들지 않는다.
        if (Time.time < nextPulseTime)
        {
            return;
        }

        // 새 스캔 파동을 시작한다.
        StartPulse();

        // 우클릭을 계속 누르고 있으면 쿨타임마다 다시 스캔되게 한다.
        nextPulseTime = Time.time + pulseCooldown;
    }

    // 새 파동을 시작하는 함수이다.
    private void StartPulse()
    {
        ActivePulse pulse = new ActivePulse();
        activePulses.Add(pulse);

        // 사운드를 재생한다.
        PlayPulseSound();
    }

    // 스캔 사운드를 재생하는 함수이다.
    private void PlayPulseSound()
    {
        if (scanPulseSource == null)
        {
            return;
        }

        if (scanPulseSource.clip != null)
        {
            scanPulseSource.PlayOneShot(scanPulseSource.clip);
            return;
        }

        scanPulseSource.Play();
    }

    // 진행 중인 파동들을 갱신하는 함수이다.
    private void UpdateActivePulses()
    {
        // 총 샘플 속도를 계산한다.
        float samplesPerSecond = pointsPerPulse / pulseTravelDuration;

        // 한 프레임 총 처리 샘플 수를 제한한다.
        int processedSamplesThisFrame = 0;

        for (int i = activePulses.Count - 1; i >= 0; i--)
        {
            ActivePulse pulse = activePulses[i];

            // 시간 누적이다.
            pulse.elapsedTime += Time.deltaTime;

            // 예산 누적이다.
            pulse.sampleBudget += samplesPerSecond * Time.deltaTime;

            // 현재 반경이다.
            float normalizedTime = Mathf.Clamp01(pulse.elapsedTime / pulseTravelDuration);
            float currentRadius = normalizedTime * maxDistance;

            // 이번 프레임 허용량까지만 샘플링한다.
            while (pulse.sampleBudget >= 1f && processedSamplesThisFrame < maxSamplesPerFrame)
            {
                pulse.sampleBudget -= 1f;
                processedSamplesThisFrame++;

                TrySpawnOneDotForCurrentWave(currentRadius);
            }

            // 파동이 끝났으면 제거한다.
            if (pulse.elapsedTime >= pulseTravelDuration)
            {
                activePulses.RemoveAt(i);
            }

            // 이미 프레임 예산을 다 썼으면 더 돌지 않는다.
            if (processedSamplesThisFrame >= maxSamplesPerFrame)
            {
                break;
            }
        }
    }

    // 현재 반경 띠에 맞는 점 1개를 만들려고 시도하는 함수이다.
    private void TrySpawnOneDotForCurrentWave(float currentRadius)
    {
        for (int attempt = 0; attempt < maxSpawnAttemptsPerDot; attempt++)
        {
            // 화면 넓은 사각 범위에서 랜덤 샘플링한다.
            Vector2 viewportPoint = GetRandomViewportPoint();

            // 카메라 기준 레이를 만든다.
            Ray ray = scanCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));

            // 표면을 맞추지 못하면 다음 시도다.
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, scanMask, triggerInteraction))
            {
                continue;
            }

            // 현재 파동 띠 범위를 계산한다.
            float bandStart = Mathf.Max(0f, currentRadius - waveThickness);
            float bandEnd = currentRadius;

            // 현재 띠 밖이면 제외한다.
            if (hit.distance < bandStart || hit.distance > bandEnd)
            {
                continue;
            }

            // 가독성 필터를 통과하지 못하면 제외한다.
            if (!ShouldKeepHitForReadability(hit))
            {
                continue;
            }

            // 표면에서 살짝 띄운 위치를 계산한다.
            Vector3 spawnPosition = hit.point + hit.normal * surfaceOffset;

            // 표면 타입에 따라 색상 그룹을 정한다.
            ScanDotColorGroup colorGroup = ResolveDotColorGroup(hit);

            // 실제 점은 GPU 인스턴싱 렌더러에 넘긴다.
            // 거리별 점 크기 조절은 제거하고, 렌더러의 기본 dotScale만 사용한다.
            instancedDotRenderer.AddDot(spawnPosition, hit.normal, colorGroup);
            return;
        }
    }

    // 스캔 표면의 큰 분류이다.
    private enum HitSurfaceClass
    {
        WallOrObject,
        Ground,
        Ceiling
    }

    // 현재 히트를 점으로 남길지 결정하는 함수이다.
    private bool ShouldKeepHitForReadability(RaycastHit hit)
    {
        // 너무 가까운 점은 화면을 가리므로 제외한다.
        if (hit.distance < minDotDistanceFromCamera)
        {
            return false;
        }

        // 표면 분류에 따라 생성 확률을 다르게 적용한다.
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

    // 표면 노멀을 기준으로 바닥/천장/벽을 구분하는 함수이다.
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

    // 화면 넓은 범위에서 랜덤 뷰포트 좌표를 뽑는 함수이다.
    private Vector2 GetRandomViewportPoint()
    {
        float viewX = 0.5f + Random.Range(-screenHalfWidth, screenHalfWidth);
        float viewY = 0.5f + Random.Range(-screenHalfHeight, screenHalfHeight);

        viewX = Mathf.Clamp(viewX, 0.02f, 0.98f);
        viewY = Mathf.Clamp(viewY, 0.02f, 0.98f);

        return new Vector2(viewX, viewY);
    }

    // 표면 타입에 따라 색상 그룹을 정하는 함수이다.
    private ScanDotColorGroup ResolveDotColorGroup(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }

        // 자기 자신에서 먼저 찾는다.
        ScanSurfaceInfo surfaceInfo = hit.collider.GetComponent<ScanSurfaceInfo>();

        // 없으면 부모에서 찾는다.
        if (surfaceInfo == null)
        {
            surfaceInfo = hit.collider.GetComponentInParent<ScanSurfaceInfo>();
        }

        // 없으면 표면 노멀 기준으로 바닥/벽을 자동 분리한다.
        if (surfaceInfo == null)
        {
            return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }

        switch (surfaceInfo.surfaceType)
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
                return ScanDotColorGroup.PlayerBody;

            case ScanSurfaceType.Creature:
                return ScanDotColorGroup.Creature;

            default:
                return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }
    }

    // ScanSurfaceInfo가 없는 오브젝트를 노멀 기준으로 최소한 바닥/벽 구분하는 함수이다.
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

    // 현재 스캔이 사용 가능한지 반환하는 프로퍼티이다.
    public bool IsPulseReady
    {
        get
        {
            return Time.time >= nextPulseTime;
        }
    }

    // 쿨타임 진행률을 0~1로 반환하는 함수이다.
    public float GetCooldownNormalized()
    {
        if (pulseCooldown <= 0f)
        {
            return 1f;
        }

        if (Time.time >= nextPulseTime)
        {
            return 1f;
        }

        float remaining = nextPulseTime - Time.time;
        float normalized = 1f - (remaining / pulseCooldown);
        return Mathf.Clamp01(normalized);
    }

    // 테스트용으로 모든 점을 지우는 함수이다.
    [ContextMenu("Clear Scan Dots")]
    public void ClearScanDots()
    {
        // 진행 중 파동 초기화이다.
        activePulses.Clear();

        // 렌더러 쪽 점도 전부 지운다.
        if (instancedDotRenderer != null)
        {
            instancedDotRenderer.ClearDots();
        }

        // 즉시 다시 사용 가능하게 만든다.
        nextPulseTime = 0f;
    }
}