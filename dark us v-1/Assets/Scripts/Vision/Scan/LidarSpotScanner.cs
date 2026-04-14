using System.Collections.Generic;
using UnityEngine;

// 우클릭 클릭 1회마다 파동을 한 번 발사하고,
// 파동이 지나간 표면에만 흰 점을 생성하는 스캐너이다.
// 생성된 점은 사라지지 않고 계속 남는다.
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
    // 클릭할 때 단발로 재생할 파동 사운드 소스이다.
    [SerializeField] private AudioSource scanPulseSource;

    [Header("Pulse Settings")]
    // 우클릭 클릭 후 다음 파동을 쏠 때까지의 최소 간격이다.
    [SerializeField] private float pulseCooldown = 0.55f;

    // 파동이 0 거리에서 최대 거리까지 퍼지는 시간이다.
    [SerializeField] private float pulseTravelDuration = 0.4f;

    // 파동 1회 동안 목표로 하는 점 생성 시도 수이다.
    [SerializeField] private int pointsPerPulse = 120;

    // 점 1개를 만들기 위해 몇 번까지 레이를 다시 시도할지 정하는 값이다.
    [SerializeField] private int maxSpawnAttemptsPerDot = 6;

    // 최대 스캔 거리이다.
    [SerializeField] private float maxDistance = 14f;

    // 화면 중심 기준으로 얼마나 넓게 퍼져서 찍을지 정하는 반경이다.
    [SerializeField] private float viewportRadius = 0.14f;

    // 파동이 지나간 직후까지 점 생성이 허용되는 두께이다.
    [SerializeField] private float waveThickness = 0.55f;

    // 점을 표면에서 살짝 띄우는 값이다.
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Duplicate Block")]
    // 같은 위치에 중복 생성되지 않게 하는 셀 크기이다.
    [SerializeField] private float cellSize = 0.08f;

    [Header("Raycast")]
    // 스캔할 레이어이다.
    [SerializeField] private LayerMask scanMask = ~0;

    // 트리거 충돌 처리 방식이다.
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    // 다음 파동을 사용할 수 있는 시간이다.
    private float nextPulseTime = 0f;

    // 이미 점이 찍힌 셀을 저장한다.
    private readonly HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();

    // 현재 진행 중인 파동들을 저장한다.
    private readonly List<ActivePulse> activePulses = new List<ActivePulse>();

    // 파동 1개의 진행 상태를 담는 내부 클래스이다.
    private class ActivePulse
    {
        // 파동이 시작된 뒤 지난 시간이다.
        public float elapsedTime = 0f;

        // 프레임마다 점 생성 시도를 누적하기 위한 예산이다.
        public float sampleBudget = 0f;
    }

    private void Reset()
    {
        // 같은 오브젝트의 카메라를 기본값으로 넣는다.
        scanCamera = GetComponent<Camera>();

        // 같은 오브젝트의 오디오 소스를 기본값으로 넣는다.
        scanPulseSource = GetComponent<AudioSource>();
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

        // 오디오 소스가 있으면 시작 시 자동 재생과 루프를 꺼둔다.
        if (scanPulseSource != null)
        {
            scanPulseSource.playOnAwake = false;
            scanPulseSource.loop = false;
        }
    }

    private void Update()
    {
        // 로컬에서 실제로 스캔 가능한 상태인지 먼저 확인한다.
        if (!CanUseScanner())
        {
            return;
        }

        // 우클릭 클릭 입력을 받아 새 파동을 시작한다.
        HandlePulseInput();

        // 현재 진행 중인 파동들을 갱신한다.
        UpdateActivePulses();
    }

    // 현재 이 스캐너가 실제로 동작해도 되는 상태인지 확인한다.
    private bool CanUseScanner()
    {
        // 카메라가 없으면 메인 카메라를 다시 찾아본다.
        if (scanCamera == null)
        {
            scanCamera = Camera.main;
        }

        // 카메라가 없으면 스캔할 수 없다.
        if (scanCamera == null)
        {
            return false;
        }

        // 비활성화된 카메라에서는 입력을 받아도 스캔하지 않는다.
        if (!scanCamera.isActiveAndEnabled)
        {
            return false;
        }

        // 점 프리팹이 없으면 생성할 수 없다.
        if (dotPrefab == null)
        {
            return false;
        }

        return true;
    }

    // 우클릭 클릭으로 파동 발사를 처리하는 함수이다.
    private void HandlePulseInput()
    {
        // 우클릭을 누른 순간이 아니면 종료한다.
        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        // 쿨타임이 아직 남아 있으면 종료한다.
        if (Time.time < nextPulseTime)
        {
            return;
        }

        // 새 파동을 시작한다.
        StartPulse();

        // 다음 사용 가능 시간을 갱신한다.
        nextPulseTime = Time.time + pulseCooldown;
    }

    // 새 파동을 하나 시작하는 함수이다.
    private void StartPulse()
    {
        // 진행 중인 파동 목록에 새 파동을 추가한다.
        activePulses.Add(new ActivePulse());

        // 파동 사운드를 단발로 재생한다.
        PlayPulseSound();
    }

    // 파동 사운드를 재생하는 함수이다.
    private void PlayPulseSound()
    {
        // 오디오 소스가 없으면 종료한다.
        if (scanPulseSource == null)
        {
            return;
        }

        // 클립이 연결되어 있으면 단발 재생한다.
        if (scanPulseSource.clip != null)
        {
            scanPulseSource.PlayOneShot(scanPulseSource.clip);
            return;
        }

        // 클립이 따로 없어도 기본 재생은 가능하게 한다.
        scanPulseSource.Play();
    }

    // 현재 진행 중인 파동들을 갱신하는 함수이다.
    private void UpdateActivePulses()
    {
        // 파동 이동 시간이 너무 작아지는 것을 막는다.
        float safeDuration = Mathf.Max(0.01f, pulseTravelDuration);

        // 파동 1회당 점 생성 시도를 초당 값으로 바꾼다.
        float samplesPerSecond = Mathf.Max(1, pointsPerPulse) / safeDuration;

        // 뒤에서부터 순회해서 끝난 파동을 안전하게 제거한다.
        for (int i = activePulses.Count - 1; i >= 0; i--)
        {
            // 현재 파동을 가져온다.
            ActivePulse pulse = activePulses[i];

            // 파동 진행 시간을 누적한다.
            pulse.elapsedTime += Time.deltaTime;

            // 이번 프레임에 사용할 점 생성 예산을 누적한다.
            pulse.sampleBudget += samplesPerSecond * Time.deltaTime;

            // 현재 파동의 진행률을 계산한다.
            float normalizedTime = Mathf.Clamp01(pulse.elapsedTime / safeDuration);

            // 진행률을 기반으로 현재 파동 반경을 계산한다.
            float currentRadius = normalizedTime * maxDistance;

            // 누적된 예산만큼 점 생성을 시도한다.
            while (pulse.sampleBudget >= 1f)
            {
                pulse.sampleBudget -= 1f;
                TrySpawnOneDotForCurrentWave(currentRadius);
            }

            // 파동 진행 시간이 끝났으면 목록에서 제거한다.
            if (pulse.elapsedTime >= safeDuration)
            {
                activePulses.RemoveAt(i);
            }
        }
    }

    // 현재 파동 반경에 해당하는 위치에 점 1개 생성을 시도하는 함수이다.
    private void TrySpawnOneDotForCurrentWave(float currentRadius)
    {
        // 파동 두께가 너무 작아지지 않게 보정한다.
        float safeWaveThickness = Mathf.Max(0.01f, waveThickness);

        // 점 1개를 만들기 위해 여러 번 레이를 시도한다.
        for (int attempt = 0; attempt < maxSpawnAttemptsPerDot; attempt++)
        {
            // 화면 중심의 작은 원 안에서 랜덤 좌표를 뽑는다.
            Vector2 offset = Random.insideUnitCircle * viewportRadius;

            // 뷰포트 좌표를 만든다.
            float viewX = 0.5f + offset.x;
            float viewY = 0.5f + offset.y;

            // 카메라 기준으로 레이를 만든다.
            Ray ray = scanCamera.ViewportPointToRay(new Vector3(viewX, viewY, 0f));

            // 표면에 맞지 않으면 다음 시도로 넘어간다.
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, scanMask, triggerInteraction))
            {
                continue;
            }

            // 현재 파동이 이미 지나간 바로 뒤쪽 범위까지만 점을 허용한다.
            float bandStart = Mathf.Max(0f, currentRadius - safeWaveThickness);
            float bandEnd = currentRadius;

            // 맞은 지점 거리가 현재 파동 띠 범위 밖이면 제외한다.
            if (hit.distance < bandStart || hit.distance > bandEnd)
            {
                continue;
            }

            // 점이 표면 안에 박히지 않도록 살짝 띄운다.
            Vector3 spawnPos = hit.point + hit.normal * surfaceOffset;

            // 월드 좌표를 셀 좌표로 바꾼다.
            Vector3Int cell = WorldToCell(spawnPos);

            // 이미 점이 있는 셀이면 생성하지 않는다.
            if (occupiedCells.Contains(cell))
            {
                continue;
            }

            // 점을 생성하고 셀을 기록한다.
            SpawnDot(spawnPos);
            occupiedCells.Add(cell);

            // 점 1개 생성에 성공했으니 종료한다.
            return;
        }
    }

    // 실제 점 프리팹을 생성하는 함수이다.
    private void SpawnDot(Vector3 position)
    {
        // 점 프리팹을 생성한다.
        Instantiate(dotPrefab, position, Quaternion.identity, dotContainer);
    }

    // 월드 좌표를 중복 방지용 셀 좌표로 바꾸는 함수이다.
    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        // x, y, z를 셀 크기 단위로 나눠 반올림한다.
        int x = Mathf.RoundToInt(worldPos.x / cellSize);
        int y = Mathf.RoundToInt(worldPos.y / cellSize);
        int z = Mathf.RoundToInt(worldPos.z / cellSize);

        // 셀 좌표를 반환한다.
        return new Vector3Int(x, y, z);
    }

        // 현재 스캔을 바로 사용할 수 있는 상태인지 반환한다.
    public bool IsPulseReady
    {
        get
        {
            return Time.time >= nextPulseTime;
        }
    }

    // 현재 쿨타임 진행률을 0~1로 반환한다.
    // 0이면 방금 사용한 상태이고, 1이면 다시 사용 가능한 상태이다.
    public float GetCooldownNormalized()
    {
        // 쿨타임이 0 이하이면 항상 사용 가능으로 본다.
        if (pulseCooldown <= 0f)
        {
            return 1f;
        }

        // 이미 다시 사용할 수 있으면 1을 반환한다.
        if (Time.time >= nextPulseTime)
        {
            return 1f;
        }

        // 남은 시간을 기준으로 진행률을 계산한다.
        float remaining = nextPulseTime - Time.time;
        float normalized = 1f - (remaining / pulseCooldown);

        // 0~1 범위로 고정해서 반환한다.
        return Mathf.Clamp01(normalized);
    }
}