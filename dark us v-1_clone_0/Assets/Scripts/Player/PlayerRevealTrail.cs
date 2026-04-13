using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 우클릭을 누르고 있는 동안
// 플레이어 기준으로 퍼져 나가는 스캔 파동을 계속 만들고,
// 화면 안의 표면에 점 데이터를 매우 빠르게 누적시키는 역할을 한다.
// 이 버전은 점이 타다다닥 찍히는 느낌을 위해 프레임마다 조금씩 계속 샘플링한다.
public class PlayerRevealTrail : MonoBehaviour
{
    [Header("References")]
    // 스캔 시작 기준이 되는 Transform이다.
    // 보통 Main Camera를 넣으면 된다.
    public Transform scanOrigin;

    // 레이를 쏠 카메라이다.
    // 비어 있으면 자동으로 Main Camera를 찾는다.
    public Camera scanCamera;

    [Header("Input")]
    // 스캔에 사용할 마우스 버튼이다.
    // 1이면 우클릭이다.
    public int scanMouseButton = 1;

    [Header("Point Cloud Dot Settings")]
    // 점의 최소 크기이다.
    public float minDotSize = 0.010f;

    // 점의 최대 크기이다.
    public float maxDotSize = 0.018f;

    // 점이 표면 안으로 파묻히지 않게 띄우는 값이다.
    public float surfaceOffset = 0.015f;

    // 점을 표면 위에서 살짝 퍼뜨려 자연스럽게 만드는 값이다.
    public float planarJitterRadius = 0.025f;

    // 점의 유지 시간이다.
    // 음수면 영구 유지이다.
    public float dotLifetime = -1f;

    [Header("Accumulation")]
    // 새 스캔 시작 시 기존 점을 지울지 정한다.
    public bool clearDotsBeforeScan = false;

    // 너무 많은 점이 쌓이지 않게 최대 개수를 제한한다.
    // 0 이하이면 제한 없이 계속 누적한다.
    public int maxStoredDots = 0;

    [Header("Pulse Scan")]
    // 파동이 최대 거리까지 퍼지는 데 걸리는 시간이다.
    public float pulseDuration = 0.28f;

    // 파동이 도달할 최대 거리이다.
    public float maxRevealDistance = 28f;

    [Header("Continuous Sampling")]
    // 1초에 몇 개의 샘플을 뿌릴지 정한다.
    // 높을수록 타다다닥 느낌이 강해진다.
    public float samplesPerSecond = 4200f;

    // 프레임이 잠깐 느려져도 너무 듬성해지지 않게
    // 프레임당 최소 몇 개는 찍을지 정한다.
    public int minSamplesPerFrame = 24;

    [Header("Viewport Sampling")]
    // 화면 좌우 여백이다.
    [Range(0f, 0.25f)]
    public float horizontalViewportMargin = 0.03f;

    // 화면 아래 여백이다.
    [Range(0f, 0.25f)]
    public float bottomViewportMargin = 0.06f;

    // 화면 위 여백이다.
    [Range(0f, 0.25f)]
    public float topViewportMargin = 0.04f;

    [Header("Density")]
    // 기본적으로 이 확률을 통과한 표면만 점으로 남긴다.
    [Range(0f, 1f)]
    public float baseKeepChance = 0.22f;

    // 바닥이나 위를 보는 면에 주는 밀도 보정값이다.
    public float floorDensityMultiplier = 1.0f;

    // 벽처럼 수직에 가까운 면에 주는 밀도 보정값이다.
    public float wallDensityMultiplier = 0.42f;

    // 먼 거리에서 점이 너무 많아지지 않게 줄이는 비율이다.
    [Range(0f, 1f)]
    public float farDistanceMultiplier = 0.55f;

    // 모서리나 깊이 변화가 큰 부분을 더 잘 남기기 위한 추가 배수이다.
    public float edgeBoostMultiplier = 2.1f;

    // 주변 깊이 차이가 이 값보다 크면 모서리 가능성이 높다고 본다.
    public float edgeDepthThreshold = 0.75f;

    // 주변 법선 차이가 이 각도보다 크면 모서리 가능성이 높다고 본다.
    public float edgeNormalAngleThreshold = 22f;

    [Header("Visual Debug")]
    // 현재 파동 반경을 에디터에서 보기 위한 값이다.
    public float debugCurrentRadius = 0f;

    [Header("Layers")]
    // 점을 찍을 수 있는 표면 레이어이다.
    public LayerMask revealSurfaceMask;

    [Header("State")]
    // 현재 살아 있는 점 데이터 목록이다.
    public List<RevealDot> myDots = new List<RevealDot>();

    // 현재 스캔 중인지 여부이다.
    public bool IsScanning { get; private set; }

    // 새 점에 부여할 고유 번호이다.
    private int nextDotId = 1;

    // 시작할 때 필요한 참조를 정리한다.
    private void Awake()
    {
        // 기준 Transform이 비어 있으면 자기 자신을 사용한다.
        if (scanOrigin == null)
        {
            scanOrigin = transform;
        }

        // 카메라가 비어 있으면 자동으로 찾는다.
        ResolveCamera();
    }

    // 매 프레임 입력과 만료 정리를 처리한다.
    private void Update()
    {
        // 기준 Transform이 비어 있으면 자기 자신으로 다시 맞춘다.
        if (scanOrigin == null)
        {
            scanOrigin = transform;
        }

        // 카메라가 비어 있으면 다시 찾는다.
        if (scanCamera == null)
        {
            ResolveCamera();
        }

        // 수명이 있는 점만 정리한다.
        if (dotLifetime > 0f)
        {
            PruneExpiredDots();
        }

        // 최대 점 개수를 넘으면 오래된 점부터 지운다.
        TrimOldDotsIfNeeded();

        // 우클릭을 누르고 있고 현재 스캔 중이 아니면 새 스캔을 시작한다.
        if (Input.GetMouseButton(scanMouseButton) && !IsScanning)
        {
            StartCoroutine(PulseScanRoutine());
        }
    }

    // 사용할 카메라를 자동으로 찾는 함수이다.
    private void ResolveCamera()
    {
        // 기준 Transform에 Camera가 붙어 있으면 우선 사용한다.
        if (scanOrigin != null)
        {
            scanCamera = scanOrigin.GetComponent<Camera>();
        }

        // 그래도 없으면 Main Camera를 사용한다.
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }
    }

    // 우클릭을 누르고 있는 동안 계속 이어지는 파동 스캔을 만드는 코루틴이다.
    private IEnumerator PulseScanRoutine()
    {
        // 카메라가 없으면 종료한다.
        if (scanCamera == null)
        {
            yield break;
        }

        // 스캔 시작 상태로 바꾼다.
        IsScanning = true;

        // 옵션이 켜져 있으면 새 스캔 시작 시 기존 점을 먼저 지운다.
        if (clearDotsBeforeScan)
        {
            ClearDotData();
        }

        // 파동이 1초에 얼마나 빠르게 퍼질지 계산한다.
        float pulseSpeed = maxRevealDistance / Mathf.Max(0.0001f, pulseDuration);

        // 현재 파동 반경을 0으로 시작한다.
        float currentRadius = 0f;

        // 우클릭을 누르고 있는 동안 계속 스캔한다.
        while (Input.GetMouseButton(scanMouseButton))
        {
            // 프레임 시간이 0이 되지 않게 보정한다.
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // 이전 반경을 저장한다.
            float previousRadius = currentRadius;

            // 이번 프레임만큼 반경을 전진시킨다.
            currentRadius += pulseSpeed * dt;

            // 최대 반경을 넘지 않게 현재 반경을 보정한다.
            float clampedRadius = Mathf.Min(currentRadius, maxRevealDistance);

            // 이번 프레임에 몇 개를 샘플링할지 계산한다.
            int samplesThisFrame = Mathf.Max(
                minSamplesPerFrame,
                Mathf.RoundToInt(samplesPerSecond * dt)
            );

            // 디버그용 반경 값을 갱신한다.
            debugCurrentRadius = clampedRadius;

            // 이번 프레임의 얇은 반경 구간만 샘플링한다.
            SamplePulseBand(previousRadius, clampedRadius, samplesThisFrame);

            // 파동이 끝까지 갔으면 0부터 다시 시작해서
            // 누르고 있는 동안 계속 이어지게 만든다.
            if (currentRadius >= maxRevealDistance)
            {
                currentRadius = 0f;
            }

            // 다음 프레임까지 기다린다.
            yield return null;
        }

        // 버튼을 떼면 반경 값을 초기화한다.
        debugCurrentRadius = 0f;

        // 스캔 종료 상태로 바꾼다.
        IsScanning = false;
    }

    // 주어진 반경 구간 안에 들어오는 표면만 샘플링하는 함수이다.
    private void SamplePulseBand(float minRadius, float maxRadius, int sampleCount)
    {
        // 카메라가 없으면 종료한다.
        if (scanCamera == null)
        {
            return;
        }

        // 최소 1개 이상 샘플링하게 보정한다.
        sampleCount = Mathf.Max(1, sampleCount);

        // 화면 샘플을 여러 개 뽑아 본다.
        for (int i = 0; i < sampleCount; i++)
        {
            // 화면 좌우 범위 안에서 랜덤한 x를 고른다.
            float vx = Random.Range(horizontalViewportMargin, 1f - horizontalViewportMargin);

            // 화면 상하 범위 안에서 랜덤한 y를 고른다.
            float vy = Random.Range(bottomViewportMargin, 1f - topViewportMargin);

            // 화면 좌표로부터 레이를 만든다.
            Ray ray = scanCamera.ViewportPointToRay(new Vector3(vx, vy, 0f));

            // 레이가 표면을 맞췄을 때만 계속 진행한다.
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRevealDistance, revealSurfaceMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            // 현재 파동 구간 밖의 표면이면 이번 프레임에서는 건너뛴다.
            if (hit.distance < minRadius || hit.distance > maxRadius)
            {
                continue;
            }

            // 이 표면을 점으로 남길지 확률적으로 결정한다.
            if (!ShouldKeepHit(vx, vy, hit))
            {
                continue;
            }

            // 표면 위 약간 랜덤한 위치에 점을 추가한다.
            AddDotFromHit(hit);
        }
    }

    // 현재 맞은 표면이 점으로 남을 가치가 있는지 결정하는 함수이다.
    private bool ShouldKeepHit(float viewportX, float viewportY, RaycastHit hit)
    {
        // 표면이 위쪽을 보는 정도를 계산한다.
        float upness = Mathf.Abs(Vector3.Dot(hit.normal.normalized, Vector3.up));

        // 바닥은 조금 더 많이, 벽은 조금 덜 남기도록 밀도 배수를 만든다.
        float surfaceDensity = Mathf.Lerp(wallDensityMultiplier, floorDensityMultiplier, upness);

        // 가까운 점은 조금 더 잘 남고 먼 점은 조금 덜 남도록 배수를 만든다.
        float distanceT = Mathf.InverseLerp(0f, maxRevealDistance, hit.distance);
        float distanceDensity = Mathf.Lerp(1f, farDistanceMultiplier, distanceT);

        // 기본 확률에 표면/거리 보정을 곱한다.
        float keepChance = baseKeepChance * surfaceDensity * distanceDensity;

        // 현재 위치가 모서리나 깊이 변화가 큰 부분이면 확률을 올린다.
        if (LooksLikeEdge(viewportX, viewportY, hit))
        {
            keepChance *= edgeBoostMultiplier;
        }

        // 확률이 1을 넘지 않게 막는다.
        keepChance = Mathf.Clamp01(keepChance);

        // 최종 확률로 남길지 결정한다.
        return Random.value <= keepChance;
    }

    // 주변 깊이와 법선을 비교해서 모서리처럼 보이는지 검사하는 함수이다.
    private bool LooksLikeEdge(float viewportX, float viewportY, RaycastHit centerHit)
    {
        // 카메라가 없으면 모서리 검사 없이 false를 반환한다.
        if (scanCamera == null)
        {
            return false;
        }

        // 주변을 볼 때 사용할 작은 화면 오프셋 값이다.
        const float offset = 0.012f;

        // 검사할 주변 좌표 4개를 만든다.
        Vector2[] samples = new Vector2[]
        {
            new Vector2(viewportX + offset, viewportY),
            new Vector2(viewportX - offset, viewportY),
            new Vector2(viewportX, viewportY + offset),
            new Vector2(viewportX, viewportY - offset)
        };

        // 주변 샘플을 하나씩 검사한다.
        for (int i = 0; i < samples.Length; i++)
        {
            // 화면 좌표를 0~1 범위로 제한한다.
            float sx = Mathf.Clamp01(samples[i].x);
            float sy = Mathf.Clamp01(samples[i].y);

            // 주변 좌표에서 레이를 만든다.
            Ray ray = scanCamera.ViewportPointToRay(new Vector3(sx, sy, 0f));

            // 주변 레이가 아무것도 못 맞췄다면 실루엣 근처일 가능성이 있으므로 true로 본다.
            if (!Physics.Raycast(ray, out RaycastHit neighborHit, maxRevealDistance, revealSurfaceMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // 깊이 차이가 크면 모서리처럼 본다.
            if (Mathf.Abs(neighborHit.distance - centerHit.distance) >= edgeDepthThreshold)
            {
                return true;
            }

            // 법선 차이가 크면 모서리나 면 변화로 본다.
            float normalAngle = Vector3.Angle(neighborHit.normal, centerHit.normal);
            if (normalAngle >= edgeNormalAngleThreshold)
            {
                return true;
            }
        }

        // 어느 검사에도 걸리지 않았으면 일반 표면으로 본다.
        return false;
    }

    // Raycast 결과를 바탕으로 실제 점 데이터를 추가하는 함수이다.
    private void AddDotFromHit(RaycastHit hit)
    {
        // 표면 법선을 정규화해서 사용한다.
        Vector3 normal = hit.normal.normalized;

        // 법선에 너무 가까운 축을 피해서 기준 축을 만든다.
        Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.98f ? Vector3.right : Vector3.up;

        // 표면 위 첫 번째 축을 만든다.
        Vector3 tangent = Vector3.Cross(normal, referenceAxis).normalized;

        // 표면 위 두 번째 축을 만든다.
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

        // 원형 랜덤 오프셋 각도를 만든다.
        float angle = Random.Range(0f, Mathf.PI * 2f);

        // 중심에서 바깥으로 자연스럽게 퍼지는 랜덤 거리를 만든다.
        float distance = Mathf.Sqrt(Random.value) * planarJitterRadius;

        // 표면 위 랜덤 오프셋을 계산한다.
        Vector3 planarOffset = tangent * Mathf.Cos(angle) * distance + bitangent * Mathf.Sin(angle) * distance;

        // 최종 점 위치를 계산한다.
        Vector3 finalPos = hit.point + normal * surfaceOffset + planarOffset;

        // 점 겹침을 허용하므로 그대로 추가한다.
        AddDot(finalPos);
    }

    // 점 데이터를 그대로 추가하는 함수이다.
    private void AddDot(Vector3 worldPos)
    {
        // 점 크기를 범위 안에서 랜덤으로 만든다.
        float dotSize = Random.Range(minDotSize, maxDotSize);

        // 새 점 데이터를 목록에 추가한다.
        myDots.Add(new RevealDot(nextDotId, worldPos, dotSize, Time.time, dotLifetime));

        // 다음 점 번호를 증가시킨다.
        nextDotId++;
    }

    // 수명이 끝난 점을 목록에서 제거하는 함수이다.
    private void PruneExpiredDots()
    {
        // 비어 있으면 바로 종료한다.
        if (myDots.Count == 0)
        {
            return;
        }

        // 현재 시간을 저장한다.
        float currentTime = Time.time;

        // 뒤에서부터 검사하며 만료된 점을 지운다.
        for (int i = myDots.Count - 1; i >= 0; i--)
        {
            if (myDots[i].IsExpired(currentTime))
            {
                myDots.RemoveAt(i);
            }
        }
    }

    // 점 개수가 너무 많아졌을 때 오래된 점부터 줄이는 함수이다.
    private void TrimOldDotsIfNeeded()
    {
        // 최대 개수가 1 미만이면 제한을 쓰지 않는 것으로 본다.
        if (maxStoredDots < 1)
        {
            return;
        }

        // 최대 개수 이하면 종료한다.
        if (myDots.Count <= maxStoredDots)
        {
            return;
        }

        // 앞에서부터 오래된 점을 제거한다.
        int removeCount = myDots.Count - maxStoredDots;
        myDots.RemoveRange(0, removeCount);
    }

    // 점 데이터와 상태를 전부 비우는 함수이다.
    public void ClearDotData()
    {
        // 점 데이터를 지운다.
        myDots.Clear();

        // 다음 점 번호를 초기화한다.
        nextDotId = 1;
    }

    // Scene 뷰에서 현재 파동 반경을 보기 쉽게 그리는 함수이다.
    private void OnDrawGizmosSelected()
    {
        // 기준 Transform이 없으면 자기 자신을 사용한다.
        Transform origin = scanOrigin != null ? scanOrigin : transform;

        // 현재 파동 반경이 0 이하이면 그리지 않는다.
        if (debugCurrentRadius <= 0.001f)
        {
            return;
        }

        // 현재 반경을 초록색 와이어 구로 그린다.
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(origin.position, debugCurrentRadius);
    }
}