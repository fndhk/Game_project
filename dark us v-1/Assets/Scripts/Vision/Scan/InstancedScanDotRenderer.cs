using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 스캔 점의 색상 그룹이다.
public enum ScanDotColorGroup
{
    Default = 0,

    // 연구소 맵에서 쓰는 색상 그룹이다.
    // 숫자는 이전 버전과 호환되도록 유지한다.
    Floor = 7,
    Wall = 8,
    Metal = 9,
    Glass = 10,
    AccessCore = 11,
    SecurityTerminal = 12,
    EmergencyExit = 13,
    PlayerBody = 14,
    Creature = 15,

    // 복구 완료된 가짜 컴퓨터용 점 색상 그룹이다.
    WrongComputer = 16,

    // 복구 완료된 진짜 탈출 컴퓨터용 점 색상 그룹이다.
    RestoredEscapeComputer = 17,

    // 아이템 공통용 점 색상 그룹이다.
    Item = 18,

    PlayerColor0 = 19,
    PlayerColor1 = 20,
    PlayerColor2 = 21,
    PlayerColor3 = 22,
    PlayerColor4 = 23,
    PlayerColor5 = 24,
    PlayerColor6 = 25,
    PlayerColor7 = 26,
    PlayerColor8 = 27,
    PlayerColor9 = 28,
    PlayerColor10 = 29,
    PlayerColor11 = 30,

    // 도플갱어가 다시 망가뜨린 목표 컴퓨터용 점 색상 그룹이다.
    SabotagedComputer = 31
}

// 점을 GameObject로 만들지 않고 GPU 인스턴싱으로 그리는 렌더러이다.
// 점 위치와 색상 그룹만 저장하고, LateUpdate에서 한 번에 배치 렌더링한다.
public class InstancedScanDotRenderer : MonoBehaviour
{
    [Header("Source Prefab")]
    // 인스턴싱에 사용할 원본 프리팹이다.
    // 가능하면 작은 Sphere + Unlit Material 조합을 권장한다.
    [SerializeField] private GameObject sourceDotPrefab;

    [Header("Dot Shape")]
    // 점 크기이다.
    [SerializeField] private float dotScale = 0.042f;

    // 원본 메쉬를 표면 노멀에 맞춰 회전할지 여부이다.
    // Sphere를 쓸 거면 꺼도 된다.
    [SerializeField] private bool alignToSurfaceNormal = false;

    [Header("Capacity")]
    // 실제 내부 버퍼가 허용하는 최대 점 개수이다. HUD 표기 한도보다 약간 크게 둔다.
    [SerializeField] private int maxActiveDots = 85000;

    [Header("Memory Cleanup")]
    // HUD에 표시할 점 메모리 한도이자 자동 정리를 시작하는 기준이다.
    [SerializeField] private int softActiveDotLimit = 80000;

    // softActiveDotLimit를 넘으면 이 개수까지 오래된 점을 조금씩 지운다.
    [SerializeField] private int cleanupStopDotCount = 78000;

    // 자동 정리 중 한 프레임에 제거할 오래된 점 최대 개수이다.
    [SerializeField] private int cleanupDotsPerFrame = 120;

    // softActiveDotLimit보다 더 확보할 내부 여유 버퍼이다.
    [SerializeField] private int hardLimitExtraBuffer = 5000;

    // 같은 위치 중복 생성을 막기 위한 셀 크기이다.
    [SerializeField] private float cellSize = 0.055f;

    [Header("Render")]
    // 그림자 사용 여부이다.
    [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

    // 그림자 받기 여부이다.
    [SerializeField] private bool receiveShadows = false;

    [Header("Readability Colors")]
    // 시작할 때 가독성용 회백색 프리셋을 적용할지 여부이다.
    [SerializeField] private bool applyReadabilityColorPresetOnAwake = true;

    [Header("Dot Colors")]
    // 기본 점 색이다.
    [SerializeField] private Color defaultDotColor = new Color(0.7735849f, 0.7735849f, 0.77254903f, 1f);

    [Header("Laboratory Dot Colors")]
    // 연구소 바닥용 점 색이다.
    [SerializeField] private Color floorDotColor = new Color(0.7607843f, 0.7607843f, 0.7411765f, 1f);

    // 연구소 벽용 점 색이다.
    [SerializeField] private Color wallDotColor = new Color(0.76f, 0.76f, 0.74f, 1f);

    // 금속/기계류용 점 색이다.
    [SerializeField] private Color metalDotColor = new Color(0.5671057f, 0.63719296f, 0.6792453f, 1f);

    // 유리용 점 색이다.
    [SerializeField] private Color glassDotColor = new Color(0.5742702f, 0.75670004f, 0.8396226f, 1f);

    // 탈출 핵심 오브젝트용 점 색이다.
    [SerializeField] private Color accessCoreDotColor = new Color(0.8679245f, 0.86174536f, 0.46262014f, 1f);

    // 보안 단말기/복구 전 컴퓨터용 점 색이다.
    [SerializeField] private Color securityTerminalDotColor = new Color(0.16078432f, 0.5019608f, 1f, 1f);

    // 탈출구용 점 색이다.
    [SerializeField] private Color emergencyExitDotColor = new Color(1f, 0.9783717f, 0f, 1f);

    // 플레이어 신체용 점 색이다.
    [SerializeField] private Color playerBodyDotColor = new Color(0.7075472f, 0.7075472f, 0.49061057f, 1f);

    // 생명체/괴물용 점 색이다.
    [SerializeField] private Color creatureDotColor = new Color(0.72f, 0.08f, 0.08f, 1f);

    // 가짜 컴퓨터 복구 완료용 점 색이다.
    [SerializeField] private Color wrongComputerDotColor = new Color(1f, 0.1f, 0.08f, 1f);

    // 진짜 탈출 컴퓨터 복구 완료용 점 색이다.
    [SerializeField] private Color restoredEscapeComputerDotColor = new Color(0.29803923f, 0.9019608f, 0.41960785f, 1f);

    // 도플갱어가 다시 망가뜨린 목표 컴퓨터용 점 색이다.
    [SerializeField] private Color sabotagedComputerDotColor = new Color(0.78f, 0.18f, 1f, 1f);

    // 아이템 공통용 점 색이다.
    [SerializeField] private Color itemDotColor = new Color(1f, 0.72638464f, 0f, 1f);

    // 원본 프리팹에서 가져온 메쉬이다.
    private Mesh instanceMesh;

    // 원본 프리팹에서 가져온 기본 머티리얼이다.
    private Material sourceMaterial;

    // 색상 그룹별 런타임 머티리얼이다.
    private Material[] runtimeMaterials;

    // 점 1개의 데이터이다.
    private struct DotRecord
    {
        // 활성 상태 여부이다.
        public bool isActive;

        // 셀 점유 여부이다.
        public bool hasCell;

        // 현재 점유 셀이다.
        public Vector3Int cell;

        // 색상 그룹이다.
        public int colorGroupIndex;

        // 렌더링용 행렬이다.
        public Matrix4x4 matrix;

        // 색상 그룹 렌더링 리스트 안에서의 위치이다.
        public int groupListIndex;

        // 오래된 점 정리 큐에서 stale 항목을 걸러내기 위한 값이다.
        public int activeOrderVersion;
    }

    private struct ActiveDotOrderEntry
    {
        public int dotIndex;
        public int version;
    }

    // 전체 점 데이터 저장소이다.
    private readonly List<DotRecord> dots = new List<DotRecord>();

    // 셀 점유 정보이다.
    private readonly Dictionary<Vector3Int, int> occupiedCellToDotIndex = new Dictionary<Vector3Int, int>();

    // 가장 오래된 점부터 재사용하기 위한 큐이다.
    private readonly Queue<ActiveDotOrderEntry> activeOrder = new Queue<ActiveDotOrderEntry>();

    // 제거된 점 슬롯을 다시 쓰기 위한 큐이다.
    private readonly Queue<int> inactiveReusableOrder = new Queue<int>();

    // 프레임 렌더링용 그룹별 행렬 리스트이다.
    private List<Matrix4x4>[] frameMatricesByGroup;

    // frameMatricesByGroup과 같은 순서로 dot index를 저장해 제거 시 전체 검색을 없앤다.
    private List<int>[] frameDotIndicesByGroup;

    // DrawMeshInstanced 1회 최대 개수는 1023개라서 배치 버퍼를 쓴다.
    private readonly Matrix4x4[] drawBatch = new Matrix4x4[1023];
    private int activeDotCount;
    private int activeOrderVersionCounter;
    private bool automaticCleanupActive;
    private bool loggedFirstDraw;

    // 색상 프로퍼티 이름 캐시이다.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private float baseDotScale;

    // 색상 그룹 개수이다.
    // 도플갱어 방해 컴퓨터 색상이 31번까지 있으므로 총 32개가 필요하다.
    private const int ColorGroupCount = 32;

    private void Awake()
    {
        // 안전값으로 보정한다.
        softActiveDotLimit = Mathf.Max(1, softActiveDotLimit);
        hardLimitExtraBuffer = Mathf.Max(0, hardLimitExtraBuffer);
        maxActiveDots = Mathf.Max(softActiveDotLimit + hardLimitExtraBuffer, maxActiveDots);
        cleanupStopDotCount = Mathf.Clamp(cleanupStopDotCount, 0, softActiveDotLimit);
        cleanupDotsPerFrame = Mathf.Max(1, cleanupDotsPerFrame);
        cellSize = Mathf.Max(0.01f, cellSize);
        dotScale = Mathf.Max(0.001f, dotScale);
        baseDotScale = dotScale;

        // 가독성용 점 색 프리셋을 적용한다.
        if (applyReadabilityColorPresetOnAwake)
        {
            ApplyReadabilityColorPreset();
        }

        // 그룹별 프레임 리스트를 준비한다.
        InitializeFrameLists();

        // 원본 프리팹 리소스를 가져온다.
        TryResolveSourceResources();

        // 그룹별 런타임 머티리얼을 만든다.
        CreateRuntimeMaterials();
    }

    private void LateUpdate()
    {
        ApplySavedDotSettings();
        UpdateAutomaticCleanup();

        // 렌더링 준비가 안 됐으면 종료한다.
        if (!IsReadyToRender())
        {
            return;
        }

        // 그룹별로 나눠 그린다.
        DrawAllGroups();
    }

    private void ApplySavedDotSettings()
    {
        dotScale = Mathf.Max(0.001f, baseDotScale);
    }

    private void OnDestroy()
    {
        // 런타임 머티리얼을 정리한다.
        DestroyRuntimeMaterials();
    }

    public void SetSourceDotPrefab(GameObject prefab)
    {
        sourceDotPrefab = prefab;

        if (sourceDotPrefab == null)
        {
            return;
        }

        instanceMesh = null;
        sourceMaterial = null;
        DestroyRuntimeMaterials();
        TryResolveSourceResources();
        CreateRuntimeMaterials();
    }

    // 점을 추가하거나 오래된 점을 재사용하는 함수이다.
    public void AddDot(Vector3 worldPosition, Vector3 surfaceNormal, ScanDotColorGroup colorGroup)
    {
        // 셀 좌표를 계산한다.
        Vector3Int cell = WorldToCell(worldPosition);

        // 이미 같은 셀에 점이 있으면 생성하지 않는다.
        if (occupiedCellToDotIndex.TryGetValue(cell, out int existingDotIndex))
        {
            RecolorExistingDot(existingDotIndex, colorGroup);
            return;
        }

        // 사용할 점 인덱스를 구한다.
        int dotIndex = GetReusableDotIndex();

        // 사용할 수 없으면 종료한다.
        if (dotIndex < 0)
        {
            return;
        }

        DotRecord previousRecord = dots[dotIndex];
        bool wasActive = previousRecord.isActive;

        // 기존 렌더링 리스트 위치를 해제한다.
        RemoveDotFromRenderGroup(dotIndex);

        // 기존 점유 셀을 해제한다.
        ReleaseCellOwnership(dotIndex);

        // 회전을 결정한다.
        Quaternion rotation = Quaternion.identity;

        // 표면 노멀 정렬 옵션이 켜져 있으면 노멀 기준으로 회전한다.
        if (alignToSurfaceNormal && surfaceNormal.sqrMagnitude > 0.0001f)
        {
            rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal.normalized);
        }

        // 인스턴싱용 행렬을 만든다.
        // 거리별 점 크기 조절은 제거하고 항상 기본 dotScale만 사용한다.
        Matrix4x4 matrix = Matrix4x4.TRS(
            worldPosition,
            rotation,
            Vector3.one * dotScale
        );

        // 기존 값 수정 후 다시 넣는다.
        DotRecord record = dots[dotIndex];
        record.isActive = true;
        record.hasCell = true;
        record.cell = cell;
        record.colorGroupIndex = Mathf.Clamp((int)colorGroup, 0, ColorGroupCount - 1);
        record.matrix = matrix;
        record.activeOrderVersion = ++activeOrderVersionCounter;
        dots[dotIndex] = record;
        AddDotToRenderGroup(dotIndex);

        if (!wasActive)
        {
            activeDotCount++;
        }

        // 셀 점유를 기록한다.
        occupiedCellToDotIndex[cell] = dotIndex;

        // 재사용 순서를 기록한다.
        activeOrder.Enqueue(new ActiveDotOrderEntry
        {
            dotIndex = dotIndex,
            version = record.activeOrderVersion
        });
    }

    private void RecolorExistingDot(int dotIndex, ScanDotColorGroup colorGroup)
    {
        if (dotIndex < 0 || dotIndex >= dots.Count)
        {
            return;
        }

        DotRecord record = dots[dotIndex];

        if (!record.isActive)
        {
            return;
        }

        int targetGroupIndex = Mathf.Clamp((int)colorGroup, 0, ColorGroupCount - 1);

        if (record.colorGroupIndex == targetGroupIndex)
        {
            return;
        }

        RemoveDotFromRenderGroup(dotIndex);
        record.colorGroupIndex = targetGroupIndex;
        dots[dotIndex] = record;
        AddDotToRenderGroup(dotIndex);
    }

    // 예전 버전과의 코드 호환성을 위한 함수이다.
    // 현재 버전에서는 scaleMultiplier를 사용하지 않고 기본 dotScale만 사용한다.
    public void AddDot(Vector3 worldPosition, Vector3 surfaceNormal, ScanDotColorGroup colorGroup, float scaleMultiplier)
    {
        AddDot(worldPosition, surfaceNormal, colorGroup);
    }

    // 특정 반경 안의 기존 점 색을 바꾸는 함수이다.
    // ObjectiveComputer가 복구 완료 순간 이미 찍힌 컴퓨터 점 색을 바꿀 때 사용한다.
    public int RecolorDotsInSphere(Vector3 center, float radius, ScanDotColorGroup newColorGroup, ScanDotColorGroup onlyRecolorColorGroup)
    {
        // 반경이 0 이하이면 처리하지 않는다.
        if (radius <= 0f)
        {
            return 0;
        }

        int recoloredCount = 0;
        float sqrRadius = radius * radius;
        int targetGroupIndex = Mathf.Clamp((int)newColorGroup, 0, ColorGroupCount - 1);
        int filterGroupIndex = Mathf.Clamp((int)onlyRecolorColorGroup, 0, ColorGroupCount - 1);

        for (int i = 0; i < dots.Count; i++)
        {
            DotRecord record = dots[i];

            // 비활성 점은 무시한다.
            if (!record.isActive)
            {
                continue;
            }

            // 원하는 기존 색상 그룹만 바꾼다.
            if (record.colorGroupIndex != filterGroupIndex)
            {
                continue;
            }

            // 행렬에서 월드 위치를 꺼낸다.
            Vector3 dotPosition = GetPositionFromMatrix(record.matrix);

            // 반경 밖이면 무시한다.
            if ((dotPosition - center).sqrMagnitude > sqrRadius)
            {
                continue;
            }

            // 색상 그룹만 바꾸되, 실제 렌더링 그룹도 옮겨야 화면 색이 즉시 바뀐다.
            RemoveDotFromRenderGroup(i);
            record.colorGroupIndex = targetGroupIndex;
            dots[i] = record;
            AddDotToRenderGroup(i);
            recoloredCount++;
        }

        return recoloredCount;
    }

    // 필터 없이 특정 반경 안의 기존 점 색을 모두 바꾸는 오버로드이다.
    public int RecolorDotsInSphere(Vector3 center, float radius, ScanDotColorGroup newColorGroup)
    {
        // 반경이 0 이하이면 처리하지 않는다.
        if (radius <= 0f)
        {
            return 0;
        }

        int recoloredCount = 0;
        float sqrRadius = radius * radius;
        int targetGroupIndex = Mathf.Clamp((int)newColorGroup, 0, ColorGroupCount - 1);

        for (int i = 0; i < dots.Count; i++)
        {
            DotRecord record = dots[i];

            if (!record.isActive)
            {
                continue;
            }

            Vector3 dotPosition = GetPositionFromMatrix(record.matrix);

            if ((dotPosition - center).sqrMagnitude > sqrRadius)
            {
                continue;
            }

            RemoveDotFromRenderGroup(i);
            record.colorGroupIndex = targetGroupIndex;
            dots[i] = record;
            AddDotToRenderGroup(i);
            recoloredCount++;
        }

        return recoloredCount;
    }

    // 특정 반경 안에 있는 기존 점을 제거하는 함수이다.
    // 아이템을 주웠을 때 이미 찍혀 있던 아이템 점을 바로 지울 때 사용한다.
    public int RemoveDotsInSphere(Vector3 center, float radius)
    {
        if (radius <= 0f)
        {
            return 0;
        }

        int removedCount = 0;
        float sqrRadius = radius * radius;

        for (int i = 0; i < dots.Count; i++)
        {
            DotRecord record = dots[i];

            if (!record.isActive)
            {
                continue;
            }

            Vector3 dotPosition = GetPositionFromMatrix(record.matrix);

            if ((dotPosition - center).sqrMagnitude > sqrRadius)
            {
                continue;
            }

            RemoveDotAtIndex(i);
            removedCount++;
        }

        return removedCount;
    }

    // 특정 반경 안에 있는 점 중 원하는 색상 그룹만 제거하는 함수이다.
    public int RemoveDotsInSphere(Vector3 center, float radius, ScanDotColorGroup onlyRemoveColorGroup)
    {
        if (radius <= 0f)
        {
            return 0;
        }

        int removedCount = 0;
        float sqrRadius = radius * radius;
        int filterGroupIndex = Mathf.Clamp((int)onlyRemoveColorGroup, 0, ColorGroupCount - 1);

        for (int i = 0; i < dots.Count; i++)
        {
            DotRecord record = dots[i];

            if (!record.isActive)
            {
                continue;
            }

            if (record.colorGroupIndex != filterGroupIndex)
            {
                continue;
            }

            Vector3 dotPosition = GetPositionFromMatrix(record.matrix);

            if ((dotPosition - center).sqrMagnitude > sqrRadius)
            {
                continue;
            }

            RemoveDotAtIndex(i);
            removedCount++;
        }

        return removedCount;
    }

    // 전체 점을 비우는 함수이다.
    public void ClearDots()
    {
        // 전체 데이터 초기화한다.
        dots.Clear();
        occupiedCellToDotIndex.Clear();
        activeOrder.Clear();
        inactiveReusableOrder.Clear();
        activeDotCount = 0;
        activeOrderVersionCounter = 0;
        automaticCleanupActive = false;

        // 프레임 리스트도 비운다.
        if (frameMatricesByGroup != null)
        {
            for (int i = 0; i < frameMatricesByGroup.Length; i++)
            {
                frameMatricesByGroup[i].Clear();
            }
        }

        if (frameDotIndicesByGroup != null)
        {
            for (int i = 0; i < frameDotIndicesByGroup.Length; i++)
            {
                frameDotIndicesByGroup[i].Clear();
            }
        }
    }

    private void UpdateAutomaticCleanup()
    {
        if (activeDotCount > softActiveDotLimit)
        {
            automaticCleanupActive = true;
        }

        if (!automaticCleanupActive)
        {
            return;
        }

        int targetCount = Mathf.Clamp(cleanupStopDotCount, 0, softActiveDotLimit);
        int removeBudget = Mathf.Max(1, cleanupDotsPerFrame);
        int removedCount = 0;

        while (activeDotCount > targetCount && removedCount < removeBudget)
        {
            if (!TryRemoveOldestActiveDot())
            {
                automaticCleanupActive = false;
                return;
            }

            removedCount++;
        }

        if (activeDotCount <= targetCount)
        {
            automaticCleanupActive = false;
        }
    }

    private bool TryRemoveOldestActiveDot()
    {
        while (activeOrder.Count > 0)
        {
            ActiveDotOrderEntry oldest = activeOrder.Dequeue();

            if (oldest.dotIndex < 0 || oldest.dotIndex >= dots.Count)
            {
                continue;
            }

            DotRecord record = dots[oldest.dotIndex];
            if (!record.isActive || record.activeOrderVersion != oldest.version)
            {
                continue;
            }

            RemoveDotAtIndex(oldest.dotIndex);
            return true;
        }

        return false;
    }

    // 현재 활성 점 개수이다.
    public int GetActiveDotCount()
    {
        return activeDotCount;
    }

    // HUD에서 점 용량을 표시할 때 사용한다.
    public int GetMaxActiveDotCount()
    {
        return softActiveDotLimit;
    }

    public int GetHardActiveDotCount()
    {
        return maxActiveDots;
    }

    // 가독성용 회백색 점 색 프리셋을 적용하는 함수이다.
    private void ApplyReadabilityColorPreset()
    {
        // 기본 구조물은 완전 흰색보다 낮은 회백색을 쓴다.
        defaultDotColor = new Color(0.7735849f, 0.7735849f, 0.77254903f, 1f);

        // 연구소 기본 구조물은 labor 씬과 같은 색을 쓴다.
        floorDotColor = new Color(0.7607843f, 0.7607843f, 0.7411765f, 1f);
        wallDotColor = new Color(0.76f, 0.76f, 0.74f, 1f);
        metalDotColor = new Color(0.5671057f, 0.63719296f, 0.6792453f, 1f);
        glassDotColor = new Color(0.5742702f, 0.75670004f, 0.8396226f, 1f);

        // 목표 오브젝트만 색이 확실히 보이게 한다.
        accessCoreDotColor = new Color(0.8679245f, 0.86174536f, 0.46262014f, 1f);
        securityTerminalDotColor = new Color(0.16078432f, 0.5019608f, 1f, 1f);
        emergencyExitDotColor = new Color(1f, 0.9783717f, 0f, 1f);

        // 플레이어와 생명체는 역할 구분이 아니라 생체 신호 느낌만 준다.
        playerBodyDotColor = new Color(0.7075472f, 0.7075472f, 0.49061057f, 1f);
        creatureDotColor = new Color(0.72f, 0.08f, 0.08f, 1f);

        // 복구 결과 컴퓨터는 오답 빨강, 정답 초록으로 구분한다.
        wrongComputerDotColor = new Color(1f, 0.1f, 0.08f, 1f);
        restoredEscapeComputerDotColor = new Color(0.29803923f, 0.9019608f, 0.41960785f, 1f);
        sabotagedComputerDotColor = new Color(0.78f, 0.18f, 1f, 1f);

        // 아이템은 labor 씬과 같은 주황색으로 둔다.
        itemDotColor = new Color(1f, 0.72638464f, 0f, 1f);
    }

    // 그룹별 리스트를 준비하는 함수이다.
    private void InitializeFrameLists()
    {
        frameMatricesByGroup = new List<Matrix4x4>[ColorGroupCount];
        frameDotIndicesByGroup = new List<int>[ColorGroupCount];

        for (int i = 0; i < ColorGroupCount; i++)
        {
            frameMatricesByGroup[i] = new List<Matrix4x4>(1024);
            frameDotIndicesByGroup[i] = new List<int>(1024);
        }
    }

    private void RebuildRenderGroups()
    {
        if (frameMatricesByGroup == null ||
            frameMatricesByGroup.Length != ColorGroupCount ||
            frameDotIndicesByGroup == null ||
            frameDotIndicesByGroup.Length != ColorGroupCount)
        {
            InitializeFrameLists();
        }

        for (int i = 0; i < ColorGroupCount; i++)
        {
            frameMatricesByGroup[i].Clear();
            frameDotIndicesByGroup[i].Clear();
        }

        for (int i = 0; i < dots.Count; i++)
        {
            DotRecord record = dots[i];
            if (!record.isActive)
            {
                continue;
            }

            int groupIndex = Mathf.Clamp(record.colorGroupIndex, 0, ColorGroupCount - 1);
            record.groupListIndex = frameMatricesByGroup[groupIndex].Count;
            frameMatricesByGroup[groupIndex].Add(record.matrix);
            frameDotIndicesByGroup[groupIndex].Add(i);
            dots[i] = record;
        }
    }

    // 렌더링 준비가 되었는지 확인하는 함수이다.
    private bool IsReadyToRender()
    {
        // 메쉬/머티리얼이 없으면 한 번 더 초기화를 시도한다.
        if (instanceMesh == null ||
            runtimeMaterials == null ||
            runtimeMaterials.Length != ColorGroupCount ||
            frameMatricesByGroup == null ||
            frameMatricesByGroup.Length != ColorGroupCount ||
            frameDotIndicesByGroup == null ||
            frameDotIndicesByGroup.Length != ColorGroupCount)
        {
            if (frameMatricesByGroup == null ||
                frameMatricesByGroup.Length != ColorGroupCount ||
                frameDotIndicesByGroup == null ||
                frameDotIndicesByGroup.Length != ColorGroupCount)
            {
                InitializeFrameLists();
                RebuildRenderGroups();
            }

            TryResolveSourceResources();
            CreateRuntimeMaterials();
        }

        if (instanceMesh == null)
        {
            return false;
        }

        if (runtimeMaterials == null || runtimeMaterials.Length != ColorGroupCount)
        {
            return false;
        }

        if (frameMatricesByGroup == null ||
            frameMatricesByGroup.Length != ColorGroupCount ||
            frameDotIndicesByGroup == null ||
            frameDotIndicesByGroup.Length != ColorGroupCount)
        {
            return false;
        }

        return true;
    }

    // 원본 프리팹에서 메쉬와 머티리얼을 가져오는 함수이다.
    private void TryResolveSourceResources()
    {
        if (sourceDotPrefab != null)
        {
            // 메쉬 필터를 찾는다.
            MeshFilter meshFilter = sourceDotPrefab.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter != null)
            {
                instanceMesh = meshFilter.sharedMesh;
            }

            // 렌더러를 찾는다.
            Renderer sourceRenderer = sourceDotPrefab.GetComponentInChildren<Renderer>(true);
            if (sourceRenderer != null)
            {
                sourceMaterial = sourceRenderer.sharedMaterial;
            }
        }

        if (instanceMesh != null && sourceMaterial != null)
        {
            return;
        }

        CreateFallbackSourceResources();
    }

    private void CreateFallbackSourceResources()
    {
        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        primitive.name = "RuntimeScanDotSource";

        MeshFilter meshFilter = primitive.GetComponent<MeshFilter>();
        if (instanceMesh == null && meshFilter != null)
        {
            instanceMesh = meshFilter.sharedMesh;
        }

        Renderer primitiveRenderer = primitive.GetComponent<Renderer>();
        if (sourceMaterial == null && primitiveRenderer != null && primitiveRenderer.sharedMaterial != null)
        {
            sourceMaterial = primitiveRenderer.sharedMaterial;
        }

        if (sourceMaterial == null)
        {
            sourceMaterial = CreateFallbackSourceMaterial();
        }

        if (Application.isPlaying)
        {
            Destroy(primitive);
        }
        else
        {
            DestroyImmediate(primitive);
        }
    }

    private Material CreateFallbackSourceMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.enableInstancing = true;
        SetMaterialColor(material, Color.white);
        return material;
    }

    // 런타임 머티리얼을 만드는 함수이다.
    private void CreateRuntimeMaterials()
    {
        // 원본 머티리얼이 없으면 종료한다.
        if (sourceMaterial == null)
        {
            return;
        }

        // 기존 런타임 머티리얼이 있으면 먼저 정리한다.
        DestroyRuntimeMaterials();

        // 그룹별 머티리얼 배열을 만든다.
        runtimeMaterials = new Material[ColorGroupCount];

        // 그룹 색상표를 준비한다.
        Color[] colors = GetGroupColors();

        // 그룹별 머티리얼을 복제 생성한다.
        for (int i = 0; i < ColorGroupCount; i++)
        {
            Material runtimeMat = new Material(sourceMaterial);

            // GPU 인스턴싱을 켠다.
            runtimeMat.enableInstancing = true;

            // 색을 적용한다.
            SetMaterialColor(runtimeMat, colors[i]);

            runtimeMaterials[i] = runtimeMat;
        }
    }

    // 런타임 머티리얼을 정리하는 함수이다.
    private void DestroyRuntimeMaterials()
    {
        // 없으면 종료한다.
        if (runtimeMaterials == null)
        {
            return;
        }

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            if (runtimeMaterials[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeMaterials[i]);
            }
            else
            {
                DestroyImmediate(runtimeMaterials[i]);
            }
        }

        runtimeMaterials = null;
    }

    // 그룹별 색상표를 반환하는 함수이다.
    private Color[] GetGroupColors()
    {
        Color[] colors = new Color[ColorGroupCount];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = defaultDotColor;
        }

        colors[(int)ScanDotColorGroup.Floor] = floorDotColor;
        colors[(int)ScanDotColorGroup.Wall] = wallDotColor;
        colors[(int)ScanDotColorGroup.Metal] = metalDotColor;
        colors[(int)ScanDotColorGroup.Glass] = glassDotColor;
        colors[(int)ScanDotColorGroup.AccessCore] = accessCoreDotColor;
        colors[(int)ScanDotColorGroup.SecurityTerminal] = securityTerminalDotColor;
        colors[(int)ScanDotColorGroup.EmergencyExit] = emergencyExitDotColor;
        colors[(int)ScanDotColorGroup.PlayerBody] = playerBodyDotColor;
        colors[(int)ScanDotColorGroup.Creature] = creatureDotColor;
        colors[(int)ScanDotColorGroup.WrongComputer] = wrongComputerDotColor;
        colors[(int)ScanDotColorGroup.RestoredEscapeComputer] = restoredEscapeComputerDotColor;
        colors[(int)ScanDotColorGroup.SabotagedComputer] = sabotagedComputerDotColor;
        colors[(int)ScanDotColorGroup.Item] = itemDotColor;

        for (int i = 0; i < PlayerColorPalette.ColorCount; i++)
        {
            colors[PlayerColorPalette.FirstScanColorGroupIndex + i] = PlayerColorPalette.GetColor(i);
        }

        return colors;
    }

    // 머티리얼에 색을 적용하는 함수이다.
    private void SetMaterialColor(Material targetMaterial, Color targetColor)
    {
        // 머티리얼이 없으면 종료한다.
        if (targetMaterial == null)
        {
            return;
        }

        // URP/HDRP 계열일 수 있는 _BaseColor를 먼저 시도한다.
        if (targetMaterial.HasProperty(BaseColorId))
        {
            targetMaterial.SetColor(BaseColorId, targetColor);
        }

        // 기본 셰이더 계열의 _Color도 시도한다.
        if (targetMaterial.HasProperty(ColorId))
        {
            targetMaterial.SetColor(ColorId, targetColor);
        }
    }

    // 점 하나를 현재 색상 그룹 렌더링 리스트에 등록한다.
    private void AddDotToRenderGroup(int dotIndex)
    {
        if (dotIndex < 0 || dotIndex >= dots.Count || frameMatricesByGroup == null || frameDotIndicesByGroup == null)
        {
            return;
        }

        DotRecord record = dots[dotIndex];

        if (!record.isActive)
        {
            return;
        }

        int groupIndex = Mathf.Clamp(record.colorGroupIndex, 0, ColorGroupCount - 1);
        List<Matrix4x4> group = frameMatricesByGroup[groupIndex];
        List<int> groupDotIndices = frameDotIndicesByGroup[groupIndex];
        record.groupListIndex = group.Count;
        group.Add(record.matrix);
        groupDotIndices.Add(dotIndex);
        dots[dotIndex] = record;
    }

    // 점 하나를 렌더링 리스트에서 제거한다. 마지막 원소와 교체해서 O(1)에 가깝게 처리한다.
    private void RemoveDotFromRenderGroup(int dotIndex)
    {
        if (dotIndex < 0 || dotIndex >= dots.Count || frameMatricesByGroup == null || frameDotIndicesByGroup == null)
        {
            return;
        }

        DotRecord record = dots[dotIndex];

        if (!record.isActive)
        {
            return;
        }

        int groupIndex = Mathf.Clamp(record.colorGroupIndex, 0, ColorGroupCount - 1);
        List<Matrix4x4> group = frameMatricesByGroup[groupIndex];
        List<int> groupDotIndices = frameDotIndicesByGroup[groupIndex];
        int removeIndex = record.groupListIndex;
        int lastIndex = group.Count - 1;

        if (removeIndex < 0 ||
            removeIndex > lastIndex ||
            groupDotIndices == null ||
            groupDotIndices.Count != group.Count ||
            groupDotIndices[removeIndex] != dotIndex)
        {
            RebuildRenderGroups();
            record = dots[dotIndex];
            groupIndex = Mathf.Clamp(record.colorGroupIndex, 0, ColorGroupCount - 1);
            group = frameMatricesByGroup[groupIndex];
            groupDotIndices = frameDotIndicesByGroup[groupIndex];
            removeIndex = record.groupListIndex;
            lastIndex = group.Count - 1;

            if (removeIndex < 0 ||
                removeIndex > lastIndex ||
                groupDotIndices == null ||
                groupDotIndices.Count != group.Count ||
                groupDotIndices[removeIndex] != dotIndex)
            {
                return;
            }
        }

        if (removeIndex != lastIndex)
        {
            Matrix4x4 movedMatrix = group[lastIndex];
            int movedDotIndex = groupDotIndices[lastIndex];
            group[removeIndex] = movedMatrix;
            groupDotIndices[removeIndex] = movedDotIndex;

            if (movedDotIndex >= 0 && movedDotIndex < dots.Count && movedDotIndex != dotIndex)
            {
                DotRecord movedRecord = dots[movedDotIndex];
                movedRecord.groupListIndex = removeIndex;
                dots[movedDotIndex] = movedRecord;
            }
        }

        group.RemoveAt(lastIndex);
        groupDotIndices.RemoveAt(lastIndex);
        record.groupListIndex = -1;
        dots[dotIndex] = record;
    }

    // 그룹별로 인스턴싱 렌더링하는 함수이다.
    private void DrawAllGroups()
    {
        for (int groupIndex = 0; groupIndex < ColorGroupCount; groupIndex++)
        {
            List<Matrix4x4> currentGroup = frameMatricesByGroup[groupIndex];

            if (currentGroup == null || currentGroup.Count == 0)
            {
                continue;
            }

            Material groupMaterial = runtimeMaterials[groupIndex];

            if (groupMaterial == null)
            {
                continue;
            }

            int remaining = currentGroup.Count;
            int offset = 0;

            // DrawMeshInstanced는 한 번에 1023개까지 가능하다.
            while (remaining > 0)
            {
                int drawCount = Mathf.Min(1023, remaining);

                // 이번 배치 분량만 복사한다.
                currentGroup.CopyTo(offset, drawBatch, 0, drawCount);

                // 실제 GPU 인스턴싱 드로우 호출이다.
                Graphics.DrawMeshInstanced(
                    instanceMesh,
                    0,
                    groupMaterial,
                    drawBatch,
                    drawCount,
                    null,
                    shadowCastingMode,
                    receiveShadows,
                    gameObject.layer,
                    null,
                    LightProbeUsage.Off,
                    null
                );

                if (!loggedFirstDraw)
                {
                    loggedFirstDraw = true;
                    string shaderName = groupMaterial.shader != null ? groupMaterial.shader.name : "null";
                    Debug.Log("[InstancedScanDotRenderer] First draw. activeDots=" + activeDotCount + ", group=" + groupIndex + ", batch=" + drawCount + ", mesh=" + instanceMesh.name + ", shader=" + shaderName + ", layer=" + gameObject.layer);
                }

                offset += drawCount;
                remaining -= drawCount;
            }
        }
    }

    // 재사용할 점 인덱스를 가져오는 함수이다.
    private int GetReusableDotIndex()
    {
        // 제거된 비활성 슬롯이 있으면 먼저 재사용한다.
        while (inactiveReusableOrder.Count > 0)
        {
            int reusableIndex = inactiveReusableOrder.Dequeue();

            if (reusableIndex < 0 || reusableIndex >= dots.Count)
            {
                continue;
            }

            if (!dots[reusableIndex].isActive)
            {
                return reusableIndex;
            }
        }

        // 아직 최대치 미만이면 새 슬롯을 만든다.
        if (dots.Count < maxActiveDots)
        {
            DotRecord newRecord = new DotRecord();
            newRecord.isActive = false;
            newRecord.hasCell = false;
            newRecord.colorGroupIndex = 0;
            newRecord.matrix = Matrix4x4.identity;
            newRecord.groupListIndex = -1;
            newRecord.activeOrderVersion = 0;

            dots.Add(newRecord);
            return dots.Count - 1;
        }

        // 하드캡에 닿으면 AddDot 안에서 점을 지우지 않는다.
        // 오래된 점은 LateUpdate의 자동 정리 루틴이 조금씩 지우고, 그 전까지 새 점은 버린다.
        return -1;
    }

    // 지정한 점 인덱스를 비활성화하고 셀 점유를 해제한다.
    private void RemoveDotAtIndex(int dotIndex)
    {
        if (dotIndex < 0 || dotIndex >= dots.Count)
        {
            return;
        }

        DotRecord record = dots[dotIndex];

        if (!record.isActive)
        {
            return;
        }

        ReleaseCellOwnership(dotIndex);
        RemoveDotFromRenderGroup(dotIndex);

        record = dots[dotIndex];
        record.isActive = false;
        record.colorGroupIndex = 0;
        record.groupListIndex = -1;
        record.activeOrderVersion = 0;
        dots[dotIndex] = record;
        activeDotCount = Mathf.Max(0, activeDotCount - 1);

        inactiveReusableOrder.Enqueue(dotIndex);
    }

    // 이전 셀 점유를 해제하는 함수이다.
    private void ReleaseCellOwnership(int dotIndex)
    {
        if (dotIndex < 0 || dotIndex >= dots.Count)
        {
            return;
        }

        DotRecord record = dots[dotIndex];

        if (!record.hasCell)
        {
            return;
        }

        if (occupiedCellToDotIndex.TryGetValue(record.cell, out int ownerIndex))
        {
            if (ownerIndex == dotIndex)
            {
                occupiedCellToDotIndex.Remove(record.cell);
            }
        }

        record.hasCell = false;
        dots[dotIndex] = record;
    }

    // 렌더링 행렬에서 월드 위치를 꺼내는 함수이다.
    private Vector3 GetPositionFromMatrix(Matrix4x4 matrix)
    {
        return new Vector3(matrix.m03, matrix.m13, matrix.m23);
    }

    // 월드 좌표를 셀 좌표로 바꾸는 함수이다.
    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / cellSize);
        int y = Mathf.RoundToInt(worldPos.y / cellSize);
        int z = Mathf.RoundToInt(worldPos.z / cellSize);

        return new Vector3Int(x, y, z);
    }
}
