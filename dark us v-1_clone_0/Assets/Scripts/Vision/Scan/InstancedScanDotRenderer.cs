using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 스캔 점의 색상 그룹이다.
public enum ScanDotColorGroup
{
    Default = 0,
    Ground = 1,
    Rock = 2,
    TreeTrunk = 3,
    TreeLeaf = 4,
    Branch = 5,
    Bush = 6
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
    [SerializeField] private float dotScale = 0.06f;

    // 원본 메쉬를 표면 노멀에 맞춰 회전할지 여부이다.
    // Sphere를 쓸 거면 꺼도 된다.
    [SerializeField] private bool alignToSurfaceNormal = false;

    [Header("Capacity")]
    // 동시에 유지할 최대 점 개수이다.
    [SerializeField] private int maxActiveDots = 50000;

    // 같은 위치 중복 생성을 막기 위한 셀 크기이다.
    [SerializeField] private float cellSize = 0.055f;

    [Header("Render")]
    // 그림자 사용 여부이다.
    [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

    // 그림자 받기 여부이다.
    [SerializeField] private bool receiveShadows = false;

    [Header("Dot Colors")]
    // 기본 점 색이다.
    [SerializeField] private Color defaultDotColor = Color.white;

    // 바닥용 점 색이다.
    [SerializeField] private Color groundDotColor = new Color(0.72f, 0.72f, 0.69f, 1f);

    // 바위용 점 색이다.
    [SerializeField] private Color rockDotColor = new Color(0.56f, 0.59f, 0.63f, 1f);

    // 나무 줄기용 점 색이다.
    [SerializeField] private Color treeTrunkDotColor = new Color(0.43f, 0.35f, 0.28f, 1f);

    // 나뭇잎용 점 색이다.
    [SerializeField] private Color treeLeafDotColor = new Color(0.36f, 0.45f, 0.34f, 1f);

    // 브런치용 점 색이다.
    [SerializeField] private Color branchDotColor = new Color(0.45f, 0.41f, 0.36f, 1f);

    // 부시용 점 색이다.
    [SerializeField] private Color bushDotColor = new Color(0.36f, 0.45f, 0.34f, 1f);

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
    }

    // 전체 점 데이터 저장소이다.
    private readonly List<DotRecord> dots = new List<DotRecord>();

    // 셀 점유 정보이다.
    private readonly Dictionary<Vector3Int, int> occupiedCellToDotIndex = new Dictionary<Vector3Int, int>();

    // 가장 오래된 점부터 재사용하기 위한 큐이다.
    private readonly Queue<int> activeOrder = new Queue<int>();

    // 프레임 렌더링용 그룹별 행렬 리스트이다.
    private List<Matrix4x4>[] frameMatricesByGroup;

    // DrawMeshInstanced 1회 최대 개수는 1023개라서 배치 버퍼를 쓴다.
    private readonly Matrix4x4[] drawBatch = new Matrix4x4[1023];

    // 색상 프로퍼티 이름 캐시이다.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // 색상 그룹 개수이다.
    private const int ColorGroupCount = 7;

    private void Awake()
    {
        // 안전값으로 보정한다.
        maxActiveDots = Mathf.Max(1, maxActiveDots);
        cellSize = Mathf.Max(0.01f, cellSize);
        dotScale = Mathf.Max(0.001f, dotScale);

        // 그룹별 프레임 리스트를 준비한다.
        InitializeFrameLists();

        // 원본 프리팹 리소스를 가져온다.
        TryResolveSourceResources();

        // 그룹별 런타임 머티리얼을 만든다.
        CreateRuntimeMaterials();
    }

    private void LateUpdate()
    {
        // 렌더링 준비가 안 됐으면 종료한다.
        if (!IsReadyToRender())
        {
            return;
        }

        // 현재 활성 점들을 색상 그룹별 리스트에 모은다.
        RebuildFrameMatrices();

        // 그룹별로 나눠 그린다.
        DrawAllGroups();
    }

    private void OnDestroy()
    {
        // 런타임 머티리얼을 정리한다.
        DestroyRuntimeMaterials();
    }

    // 점을 추가하거나 오래된 점을 재사용하는 함수이다.
    public void AddDot(Vector3 worldPosition, Vector3 surfaceNormal, ScanDotColorGroup colorGroup)
    {
        // 셀 좌표를 계산한다.
        Vector3Int cell = WorldToCell(worldPosition);

        // 이미 같은 셀에 점이 있으면 생성하지 않는다.
        if (occupiedCellToDotIndex.ContainsKey(cell))
        {
            return;
        }

        // 사용할 점 인덱스를 구한다.
        int dotIndex = GetReusableDotIndex();

        // 사용할 수 없으면 종료한다.
        if (dotIndex < 0)
        {
            return;
        }

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
        record.colorGroupIndex = (int)colorGroup;
        record.matrix = matrix;
        dots[dotIndex] = record;

        // 셀 점유를 기록한다.
        occupiedCellToDotIndex[cell] = dotIndex;

        // 재사용 순서를 기록한다.
        activeOrder.Enqueue(dotIndex);
    }

    // 전체 점을 비우는 함수이다.
    public void ClearDots()
    {
        // 전체 데이터 초기화한다.
        dots.Clear();
        occupiedCellToDotIndex.Clear();
        activeOrder.Clear();

        // 프레임 리스트도 비운다.
        if (frameMatricesByGroup != null)
        {
            for (int i = 0; i < frameMatricesByGroup.Length; i++)
            {
                frameMatricesByGroup[i].Clear();
            }
        }
    }

    // 현재 활성 점 개수이다.
    public int GetActiveDotCount()
    {
        int count = 0;

        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i].isActive)
            {
                count++;
            }
        }

        return count;
    }

    // 그룹별 리스트를 준비하는 함수이다.
    private void InitializeFrameLists()
    {
        frameMatricesByGroup = new List<Matrix4x4>[ColorGroupCount];

        for (int i = 0; i < ColorGroupCount; i++)
        {
            frameMatricesByGroup[i] = new List<Matrix4x4>(1024);
        }
    }

    // 렌더링 준비가 되었는지 확인하는 함수이다.
    private bool IsReadyToRender()
    {
        // 메쉬/머티리얼이 없으면 한 번 더 초기화를 시도한다.
        if (instanceMesh == null || runtimeMaterials == null || runtimeMaterials.Length != ColorGroupCount)
        {
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

        return true;
    }

    // 원본 프리팹에서 메쉬와 머티리얼을 가져오는 함수이다.
    private void TryResolveSourceResources()
    {
        // 원본 프리팹이 없으면 종료한다.
        if (sourceDotPrefab == null)
        {
            return;
        }

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
        return new Color[]
        {
            defaultDotColor,
            groundDotColor,
            rockDotColor,
            treeTrunkDotColor,
            treeLeafDotColor,
            branchDotColor,
            bushDotColor
        };
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

    // 프레임 렌더링용 행렬 리스트를 다시 만드는 함수이다.
    private void RebuildFrameMatrices()
    {
        // 기존 리스트를 비운다.
        for (int i = 0; i < frameMatricesByGroup.Length; i++)
        {
            frameMatricesByGroup[i].Clear();
        }

        // 활성 점만 그룹별로 모은다.
        for (int i = 0; i < dots.Count; i++)
        {
            if (!dots[i].isActive)
            {
                continue;
            }

            int groupIndex = Mathf.Clamp(dots[i].colorGroupIndex, 0, ColorGroupCount - 1);
            frameMatricesByGroup[groupIndex].Add(dots[i].matrix);
        }
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

                offset += drawCount;
                remaining -= drawCount;
            }
        }
    }

    // 재사용할 점 인덱스를 가져오는 함수이다.
    private int GetReusableDotIndex()
    {
        // 아직 최대치 미만이면 새 슬롯을 만든다.
        if (dots.Count < maxActiveDots)
        {
            DotRecord newRecord = new DotRecord();
            newRecord.isActive = false;
            newRecord.hasCell = false;
            newRecord.colorGroupIndex = 0;
            newRecord.matrix = Matrix4x4.identity;

            dots.Add(newRecord);
            return dots.Count - 1;
        }

        // 최대치면 가장 오래된 활성 점을 재사용한다.
        while (activeOrder.Count > 0)
        {
            int oldestIndex = activeOrder.Dequeue();

            if (oldestIndex < 0 || oldestIndex >= dots.Count)
            {
                continue;
            }

            if (dots[oldestIndex].isActive)
            {
                return oldestIndex;
            }
        }

        return -1;
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

    // 월드 좌표를 셀 좌표로 바꾸는 함수이다.
    private Vector3Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / cellSize);
        int y = Mathf.RoundToInt(worldPos.y / cellSize);
        int z = Mathf.RoundToInt(worldPos.z / cellSize);

        return new Vector3Int(x, y, z);
    }
}