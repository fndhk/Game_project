using System.Collections.Generic;
using UnityEngine;

// 플레이어가 우클릭으로 어두운 공간을 점 형태로 탐색하는 스캐너이다.
public class LidarSpotScanner : MonoBehaviour
{
    [Header("참조")]
    // 스캔 레이를 발사할 카메라이다.
    public Camera scanCamera;

    // 실제 점을 GPU 인스턴싱으로 그려주는 렌더러이다.
    public InstancedScanDotRenderer instancedDotRenderer;

    // 스캔 사운드를 재생할 오디오 소스이다.
    public AudioSource scanPulseSource;

    [Header("입력")]
    // true이면 우클릭을 누르고 있는 동안 쿨타임마다 자동으로 스캔한다.
    public bool holdToRepeat = true;

    [Header("자기 자신 제외")]
    // true이면 자기 자신의 콜라이더는 스캔에서 제외한다.
    public bool ignoreSelfHits = true;

    // 자기 자신 판정에 사용할 루트이다.
    // 비워두면 현재 오브젝트의 루트를 자동으로 사용한다.
    public Transform selfRootOverride;

    [Header("스캔 범위")]
    // 최대 스캔 거리이다.
    public float maxDistance = 18f;

    // 화면 중심 기준 가로 반 범위이다.
    [Range(0.05f, 0.5f)] public float screenHalfWidth = 0.42f;

    // 화면 중심 기준 세로 반 범위이다.
    [Range(0.05f, 0.5f)] public float screenHalfHeight = 0.28f;

    // 점을 표면에서 얼마나 띄울지 정한다.
    public float surfaceOffset = 0.02f;

    [Header("파동 설정")]
    // 우클릭 한 번의 쿨타임이다.
    public float pulseCooldown = 1.15f;

    // 한 번의 파동이 퍼지는 시간이다.
    public float pulseTravelDuration = 0.28f;

    // 파동 하나에서 목표로 하는 총 점 개수이다.
    public int pointsPerPulse = 650;

    // 파동 띠 두께이다.
    public float waveThickness = 1.35f;

    // 점 하나를 만들 때 최대 시도 횟수이다.
    public int maxSpawnAttemptsPerDot = 8;

    [Header("성능 제한")]
    // 한 프레임 최대 샘플 처리 수이다.
    public int maxSamplesPerFrame = 180;

    [Header("레이어")]
    // 스캔 대상 레이어 마스크이다.
    public LayerMask scanMask = ~0;

    // 트리거 포함 여부이다.
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    // 현재 살아 있는 파동 하나의 상태이다.
    private class ActivePulse
    {
        // 파동이 시작된 뒤 흐른 시간이다.
        public float elapsedTime;

        // 누적된 샘플 예산이다.
        public float sampleBudget;
    }

    // 현재 진행 중인 파동 목록이다.
    private readonly List<ActivePulse> activePulses = new List<ActivePulse>();

    // 다음 스캔이 가능해지는 시각이다.
    private float nextPulseTime;

    // 자기 자신 판정에 사용할 실제 루트 캐시이다.
    private Transform cachedSelfRoot;

    // 레이 위의 여러 히트를 재사용 버퍼로 받기 위한 배열이다.
    private readonly RaycastHit[] raycastHits = new RaycastHit[16];

    // 시작 시 기본 참조를 자동으로 찾는다.
    private void Awake()
    {
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }

        if (instancedDotRenderer == null)
        {
            instancedDotRenderer = GetComponent<InstancedScanDotRenderer>();
        }

        CacheSelfRoot();
    }

    // 매 프레임 입력과 파동 진행을 처리한다.
    private void Update()
    {
        if (!CanUseScanner())
        {
            return;
        }

        HandlePulseInput();
        UpdateActivePulses();
    }

    // 자기 자신 루트를 캐시한다.
    private void CacheSelfRoot()
    {
        if (selfRootOverride != null)
        {
            cachedSelfRoot = selfRootOverride;
            return;
        }

        cachedSelfRoot = transform.root;
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

        if (cachedSelfRoot == null)
        {
            CacheSelfRoot();
        }

        return true;
    }

    // 우클릭 입력을 처리하는 함수이다.
    private void HandlePulseInput()
    {
        // 토글형 반복이면 누르고 있는 동안 쿨타임마다 처리한다.
        if (holdToRepeat)
        {
            if (!Input.GetMouseButton(1))
            {
                return;
            }
        }
        // 기존 방식이면 누른 순간 한 번만 처리한다.
        else
        {
            if (!Input.GetMouseButtonDown(1))
            {
                return;
            }
        }

        // 쿨타임 중이면 종료한다.
        if (Time.time < nextPulseTime)
        {
            return;
        }

        // 새 파동을 시작한다.
        StartPulse();

        // 다음 사용 시간을 갱신한다.
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

            // 자기 자신을 제외한 유효 히트를 찾지 못하면 다음 시도다.
            if (!TryGetFirstValidHit(ray, out RaycastHit hit))
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

            // 표면에서 살짝 띄운 위치를 계산한다.
            Vector3 spawnPosition = hit.point + hit.normal * surfaceOffset;

            // 표면 타입에 따라 색상 그룹을 정한다.
            ScanDotColorGroup colorGroup = ResolveDotColorGroup(hit);

            // 실제 점은 GPU 인스턴싱 렌더러에 넘긴다.
            instancedDotRenderer.AddDot(spawnPosition, hit.normal, colorGroup);
            return;
        }
    }

    // 레이 위에서 자기 자신을 제외한 가장 가까운 유효 히트를 찾는다.
    private bool TryGetFirstValidHit(Ray ray, out RaycastHit bestHit)
    {
        bestHit = default;

        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, maxDistance, scanMask, triggerInteraction);
        if (hitCount <= 0)
        {
            return false;
        }

        bool found = false;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = raycastHits[i];

            if (candidate.collider == null)
            {
                continue;
            }

            // 자기 자신의 히트면 무시한다.
            if (ignoreSelfHits && IsSelfCollider(candidate.collider))
            {
                continue;
            }

            if (candidate.distance < closestDistance)
            {
                closestDistance = candidate.distance;
                bestHit = candidate;
                found = true;
            }
        }

        return found;
    }

    // 전달받은 콜라이더가 자기 자신 루트 소속인지 판정한다.
    private bool IsSelfCollider(Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        if (cachedSelfRoot == null)
        {
            CacheSelfRoot();
        }

        if (cachedSelfRoot == null)
        {
            return false;
        }

        return targetCollider.transform.root == cachedSelfRoot;
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
            return ScanDotColorGroup.Default;
        }

        // 자기 자신에서 먼저 찾는다.
        ScanSurfaceInfo surfaceInfo = hit.collider.GetComponent<ScanSurfaceInfo>();

        // 없으면 부모에서 찾는다.
        if (surfaceInfo == null)
        {
            surfaceInfo = hit.collider.GetComponentInParent<ScanSurfaceInfo>();
        }

        // 없으면 기본 색이다.
        if (surfaceInfo == null)
        {
            return ScanDotColorGroup.Default;
        }

        switch (surfaceInfo.surfaceType)
        {
            case ScanSurfaceType.Ground:
                return ScanDotColorGroup.Ground;

            case ScanSurfaceType.Rock:
                return ScanDotColorGroup.Rock;

            case ScanSurfaceType.TreeTrunk:
                return ScanDotColorGroup.TreeTrunk;

            case ScanSurfaceType.TreeLeaf:
                return ScanDotColorGroup.TreeLeaf;

            case ScanSurfaceType.Branch:
                return ScanDotColorGroup.Branch;

            case ScanSurfaceType.Bush:
                return ScanDotColorGroup.Bush;

            case ScanSurfaceType.EscapeItem:
                return ScanDotColorGroup.EscapeItem;

            case ScanSurfaceType.ExitDoor:
                return ScanDotColorGroup.ExitDoor;
                
            default:
                return ScanDotColorGroup.Default;
        }
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
