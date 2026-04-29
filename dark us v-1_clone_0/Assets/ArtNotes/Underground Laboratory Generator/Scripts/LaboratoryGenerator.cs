using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArtNotes.UndergroundLaboratoryGenerator
{
    public class LaboratoryGenerator : MonoBehaviour
    {
        [Header("Basic")]
        public bool GenerateOnStart = true;

        [Tooltip("메인 맵에 생성할 방/복도 개수. 시작방은 별도 생성")]
        [Range(3, 200)]
        public int RoomCount = 30;

        [Tooltip("플레이어 수. 이 수만큼 시작방을 생성함")]
        [Range(1, 12)]
        public int PlayerCount = 4;

        [Tooltip("겹침 검사에 사용할 Cell 레이어")]
        public LayerMask CellLayer;

        [Header("Map Bounds")]
        [Tooltip("맵 크기 제한을 사용할지 여부")]
        public bool UseMapBounds = true;

        [Tooltip("맵 중심 위치")]
        public Vector3 MapCenter = Vector3.zero;

        [Tooltip("맵 X/Z 크기. 80,80이면 X -40~40 / Z -40~40 안에 생성됨")]
        public Vector2 MapSize = new Vector2(80f, 80f);

        [Tooltip("맵 경계에서 살짝 안쪽으로 넣고 싶을 때 사용")]
        public float MapBoundsPadding = 0f;

        [Tooltip("Scene View에서 맵 생성 가능 범위를 보여줄지 여부")]
        public bool DrawMapBoundsGizmo = true;

        [Header("Floor")]
        [Tooltip("1층과 2층의 높이 차이. 예: 4")]
        public float FloorHeight = 4f;

        [Header("Connection Rules")]
        [Tooltip("큰방과 큰방이 직접 붙는 것을 금지")]
        public bool BlockBigRoomToBigRoom = true;

        [Tooltip("복도와 복도가 직접 붙는 것을 금지")]
        public bool BlockCorridorToCorridor = true;

        [Tooltip("켜면 방-방 직접 연결을 막고 방-복도-방 구조를 더 강하게 유도")]
        public bool ForceCorridorBetweenRooms = false;

        [Tooltip("ForceCorridorBetweenRooms가 켜져 있어도 작은방-큰방 직접 연결은 허용")]
        public bool AllowSmallBigDirectConnection = true;

        [Header("Start Rooms")]
        [Tooltip("시작방끼리 최소 거리")]
        public float StartRoomMinDistance = 18f;

        [Tooltip("시작방은 1층 출구에만 붙게 할지 여부")]
        public bool StartRoomsOnlyOnFirstFloor = true;

        [Header("Exit Door")]
        [Tooltip("탈출구가 시작방에서 이 거리보다 가까우면 생성 금지")]
        public float ExitMinDistanceFromStart = 25f;

        [Tooltip("탈출구 문 프리팹. 없으면 InsteadDoor를 사용")]
        public GameObject ExitDoorPrefab;

        [Header("Attempts")]
        [Tooltip("방 하나를 붙일 때 최대 시도 횟수")]
        public int MaxPlacementAttempts = 300;

        [Tooltip("전체 맵 생성 재시도 횟수")]
        public int MaxFullGenerationAttempts = 10;

        [Header("Original Prefabs")]
        [Tooltip("막힌 출구에 놓을 벽/문 프리팹")]
        public GameObject InsteadDoor;

        [Tooltip("방과 방이 연결될 때 놓을 문 프리팹")]
        public GameObject[] DoorPrefabs;

        [Header("Door Placement")]
        [Tooltip("연결 문 위치 보정값. 출구 기준 로컬 좌표")]
        public Vector3 ConnectedDoorPositionOffset = Vector3.zero;

        [Tooltip("연결 문 회전 보정값. 출구 회전에 추가됨")]
        public Vector3 ConnectedDoorRotationOffset = Vector3.zero;

        [Tooltip("막힌 문/벽 위치 보정값. 출구 기준 로컬 좌표. Cube 막는 벽이면 보통 Y 1.5")]
        public Vector3 BlockDoorPositionOffset = new Vector3(0f, 1.5f, 0f);

        [Tooltip("막힌 문/벽 회전 보정값. 방향이 틀어지면 Y 90 또는 -90을 테스트")]
        public Vector3 BlockDoorRotationOffset = Vector3.zero;

        [Tooltip("탈출구 위치 보정값. 출구 기준 로컬 좌표")]
        public Vector3 ExitDoorPositionOffset = Vector3.zero;

        [Tooltip("탈출구 회전 보정값. 출구 회전에 추가됨")]
        public Vector3 ExitDoorRotationOffset = Vector3.zero;

        [Header("Exit Visual Cleanup")]
        [Tooltip("DoorPoint가 사용되거나 막히면 Cell.ExitVisuals의 같은 순서 마커도 같이 끔")]
        public bool HideExitVisualsWhenExitUsed = true;

        [Tooltip("ExitVisuals를 끌 때 비활성화가 아니라 완전 삭제")]
        public bool DestroyExitVisualsInsteadOfDisable = false;

        [Tooltip("ExitVisuals를 직접 안 넣어도 출구 주변의 빨간 Renderer를 찾아 자동으로 끔")]
        public bool AutoHideRedExitMarkers = true;

        [Tooltip("빨간 출구 마커 자동 탐색 반경")]
        public float RedExitMarkerSearchRadius = 3.2f;

        [Tooltip("빨간 출구 마커 자동 탐색 시 Y 높이 차이 허용값")]
        public float RedExitMarkerMaxHeightDifference = 1.2f;

        [Header("Runtime Visibility")]
        [Tooltip("게임 시작 후 생성된 맵의 Renderer만 꺼서 보이지 않게 함. Collider는 유지됨")]
        public bool HideGeneratedVisualsInGame = true;

        [Tooltip("체크하면 Play Mode일 때만 Renderer를 끔. 에디터에서 맵 확인할 때는 보임")]
        public bool HideGeneratedVisualsOnlyInPlayMode = true;

        [Tooltip("생성된 일반문/막는벽/탈출구 문 Renderer도 같이 끔. 스캔 게임에서는 보통 켜는 것을 추천")]
        public bool HideGeneratedDoorVisualsToo = true;

        [Header("Runtime Lighting")]
        [Tooltip("게임 시작 후 생성된 맵 안의 Light 컴포넌트를 꺼서 그림자 경고와 성능 낭비를 줄임")]
        public bool DisableGeneratedLightsInGame = true;

        [Tooltip("체크하면 Play Mode일 때만 생성된 맵 조명을 끔. 에디터에서 맵 확인할 때는 조명이 유지됨")]
        public bool DisableGeneratedLightsOnlyInPlayMode = true;

        [Tooltip("Light 자체를 끄기 전에 Shadows만 먼저 None으로 바꿈")]
        public bool DisableGeneratedLightShadows = true;

        [Tooltip("생성된 일반문/막는벽/탈출구 문 안에 Light가 있으면 같이 끔")]
        public bool DisableGeneratedDoorLightsToo = true;

        [Tooltip("일반 방, 큰방, 복도, 계단방, 수직복도 등을 전부 넣는 배열")]
        public Cell[] CellPrefabs;

        [Tooltip("시작방 후보 프리팹. 비워두면 CellPrefabs에서 StartRoom 타입을 자동으로 찾음")]
        public Cell[] StartRoomPrefabs;

        [Header("Cleanup")]
        [Tooltip("생성 전에 이 오브젝트의 기존 자식들을 삭제")]
        public bool ClearPreviousGeneratedChildren = true;

        // 생성된 방 목록을 저장한다.
        private readonly List<Cell> generatedCells = new List<Cell>();

        // 시작방 위치를 저장한다.
        private readonly List<Vector3> startRoomPositions = new List<Vector3>();

        // 프리팹별 생성 개수를 저장한다.
        private readonly Dictionary<string, int> spawnedCountByPrefabName = new Dictionary<string, int>();

        // 생성된 문/막는벽/탈출구 오브젝트를 저장한다.
        private readonly List<GameObject> generatedDoorObjects = new List<GameObject>();

        // 아직 막히지 않은 출구 정보를 저장한다.
        private class OpenExit
        {
            public Transform Exit;
            public Cell Owner;

            public OpenExit(Transform exit, Cell owner)
            {
                Exit = exit;
                Owner = owner;
            }
        }

        private void Start()
        {
            if (GenerateOnStart)
            {
                StartCoroutine(StartGeneration());
            }
        }

        // 외부에서 조건으로 맵을 다시 생성할 수 있도록 public으로 둔다.
        public IEnumerator StartGeneration()
        {
            for (int attempt = 0; attempt < MaxFullGenerationAttempts; attempt++)
            {
                if (ClearPreviousGeneratedChildren)
                {
                    ClearGeneratedChildren();
                }

                ResetRuntimeData();

                bool success = TryGenerateOnce();

                if (success)
                {
                    Debug.Log("[LaboratoryGenerator] Generation finished.");
                    yield break;
                }

                Debug.LogWarning("[LaboratoryGenerator] Generation retry: " + (attempt + 1));
                yield return null;
            }

            Debug.LogError("[LaboratoryGenerator] Generation failed.");
        }

        // 맵 1회 생성을 시도한다.
        private bool TryGenerateOnce()
        {
            List<OpenExit> openExits = new List<OpenExit>();

            Cell firstRoomPrefab = GetFirstRoomPrefab();

            if (firstRoomPrefab == null)
            {
                Debug.LogError("[LaboratoryGenerator] 첫 방으로 사용할 CellPrefab이 없음.");
                return false;
            }

            Cell firstRoom = Instantiate(firstRoomPrefab, MapCenter, Quaternion.identity, transform);
            firstRoom.name = firstRoomPrefab.name;

            PrepareCreatedCell(firstRoom, firstRoomPrefab);

            if (!IsCellInsideMapBounds(firstRoom))
            {
                Debug.LogWarning("[LaboratoryGenerator] 첫 방이 맵 크기 제한 밖으로 나감.");
                return false;
            }

            AddOpenExitsFromCell(openExits, firstRoom, null);

            int createdMainCells = 1;
            int failStreak = 0;

            while (createdMainCells < RoomCount && failStreak < MaxPlacementAttempts)
            {
                CleanupOpenExits(openExits);

                if (openExits.Count <= 0)
                {
                    break;
                }

                OpenExit targetExit = openExits[Random.Range(0, openExits.Count)];

                Cell placedCell;
                Transform selectedExit;

                bool placed = TryPlaceNormalCell(targetExit, out placedCell, out selectedExit);

                if (placed)
                {
                    createdMainCells++;
                    failStreak = 0;

                    InstantiateConnectedDoor(targetExit.Exit);

                    AddOpenExitsFromCell(openExits, placedCell, selectedExit);

                    RemoveOpenExit(openExits, targetExit);

                    DestroyExitObject(targetExit.Owner, targetExit.Exit);
                    DestroyExitObject(placedCell, selectedExit);
                }
                else
                {
                    failStreak++;
                }
            }

            if (createdMainCells < 3)
            {
                Debug.LogWarning("[LaboratoryGenerator] 메인 맵이 너무 적게 생성됨.");
                return false;
            }

            bool startRoomsOk = GenerateStartRooms(openExits);

            if (!startRoomsOk)
            {
                Debug.LogWarning("[LaboratoryGenerator] 시작방 생성 실패.");
                return false;
            }

            bool exitDoorOk = GenerateExitDoor(openExits);

            if (!exitDoorOk)
            {
                Debug.LogWarning("[LaboratoryGenerator] 탈출구 생성 실패.");
                return false;
            }

            BlockRemainingExits();
            DisableGeneratedLightsForGameplay();
            HideGeneratedVisualsForGameplay();

            return true;
        }

        // 일반 방/복도/계단방 하나를 열린 출구에 붙인다.
        private bool TryPlaceNormalCell(OpenExit targetExit, out Cell placedCell, out Transform selectedExit)
        {
            placedCell = null;
            selectedExit = null;

            List<Cell> candidates = GetAllowedCandidates(targetExit, false);

            if (candidates.Count <= 0)
            {
                return false;
            }

            for (int i = 0; i < MaxPlacementAttempts; i++)
            {
                Cell candidatePrefab = GetWeightedRandomCell(candidates);

                if (candidatePrefab == null)
                {
                    continue;
                }

                if (TryInstantiateAndAttach(candidatePrefab, targetExit, false, out placedCell, out selectedExit))
                {
                    // 일반 방/복도는 여기서 바로 생성 목록에 등록한다.
                    // 이 등록이 빠지면 BlockRemainingExits()가 해당 Cell의 남은 빨간 출구를 못 찾아서
                    // 막는 벽이 생성되지 않는다.
                    PrepareCreatedCell(placedCell, candidatePrefab);
                    return true;
                }
            }

            return false;
        }

        // 시작방들을 플레이어 수만큼 만든다.
        private bool GenerateStartRooms(List<OpenExit> openExits)
        {
            List<Cell> startCandidates = GetStartRoomCandidates();

            if (startCandidates.Count <= 0)
            {
                Debug.LogError("[LaboratoryGenerator] StartRoom 타입 프리팹이 없음.");
                return false;
            }

            for (int i = 0; i < PlayerCount; i++)
            {
                bool success = TryGenerateOneStartRoom(openExits, startCandidates);

                if (!success)
                {
                    return false;
                }
            }

            return true;
        }

        // 시작방 하나를 메인 맵의 열린 출구에 복도를 통해 연결한다.
        private bool TryGenerateOneStartRoom(List<OpenExit> openExits, List<Cell> startCandidates)
        {
            List<Cell> corridorCandidates = GetCorridorCandidates();

            if (corridorCandidates.Count <= 0)
            {
                Debug.LogError("[LaboratoryGenerator] 시작방 연결에 사용할 Corridor 타입 프리팹이 없음.");
                return false;
            }

            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                CleanupOpenExits(openExits);

                if (openExits.Count <= 0)
                {
                    return false;
                }

                OpenExit mainTargetExit = openExits[Random.Range(0, openExits.Count)];

                if (StartRoomsOnlyOnFirstFloor && GetWorldFloor(mainTargetExit.Exit.position) != 1)
                {
                    continue;
                }

                Cell corridorPrefab = GetWeightedRandomCell(corridorCandidates);

                Cell placedCorridor;
                Transform corridorExitConnectedToMain;

                if (!TryInstantiateAndAttach(corridorPrefab, mainTargetExit, false, out placedCorridor, out corridorExitConnectedToMain))
                {
                    continue;
                }

                placedCorridor.TriggerBox.enabled = true;

                List<Transform> corridorOpenExits = GetActiveExitTransforms(placedCorridor, corridorExitConnectedToMain);

                if (corridorOpenExits.Count <= 0)
                {
                    DestroyCreatedCell(placedCorridor);
                    continue;
                }

                Transform corridorExitForStart = corridorOpenExits[Random.Range(0, corridorOpenExits.Count)];

                if (StartRoomsOnlyOnFirstFloor && GetWorldFloor(corridorExitForStart.position) != 1)
                {
                    DestroyCreatedCell(placedCorridor);
                    continue;
                }

                OpenExit startTargetExit = new OpenExit(corridorExitForStart, placedCorridor);
                Cell startPrefab = GetWeightedRandomCell(startCandidates);

                Cell placedStartRoom;
                Transform startRoomSelectedExit;

                if (!TryInstantiateAndAttach(startPrefab, startTargetExit, true, out placedStartRoom, out startRoomSelectedExit))
                {
                    DestroyCreatedCell(placedCorridor);
                    continue;
                }

                if (!IsStartRoomDistanceValid(placedStartRoom.transform.position))
                {
                    DestroyCreatedCell(placedStartRoom);
                    DestroyCreatedCell(placedCorridor);
                    continue;
                }

                PrepareCreatedCell(placedCorridor, corridorPrefab);
                PrepareCreatedCell(placedStartRoom, startPrefab);

                startRoomPositions.Add(placedStartRoom.transform.position);

                InstantiateConnectedDoor(mainTargetExit.Exit);
                InstantiateConnectedDoor(corridorExitForStart);

                RemoveOpenExit(openExits, mainTargetExit);

                DestroyExitObject(mainTargetExit.Owner, mainTargetExit.Exit);
                DestroyExitObject(placedCorridor, corridorExitConnectedToMain);
                DestroyExitObject(placedCorridor, corridorExitForStart);
                DestroyExitObject(placedStartRoom, startRoomSelectedExit);

                // 시작방은 더 이상 맵을 확장하지 않게 한다.
                // 복도의 남은 출구도 나중에 BlockRemainingExits에서 막힌다.
                return true;
            }

            return false;
        }

        // 탈출구를 방의 열린 출구 하나에 만든다.
        private bool GenerateExitDoor(List<OpenExit> openExits)
        {
            CleanupOpenExits(openExits);

            List<OpenExit> candidates = new List<OpenExit>();

            for (int i = 0; i < openExits.Count; i++)
            {
                OpenExit openExit = openExits[i];

                if (!IsValidOpenExit(openExit))
                {
                    continue;
                }

                if (openExit.Owner == null)
                {
                    continue;
                }

                if (!openExit.Owner.IsRoomLike)
                {
                    continue;
                }

                if (openExit.Owner.CellType == FacilityCellType.StartRoom)
                {
                    continue;
                }

                if (IsNearAnyStartRoom(openExit.Exit.position, ExitMinDistanceFromStart))
                {
                    continue;
                }

                candidates.Add(openExit);
            }

            if (candidates.Count <= 0)
            {
                return false;
            }

            OpenExit selectedExit = candidates[Random.Range(0, candidates.Count)];

            GameObject prefab = ExitDoorPrefab != null ? ExitDoorPrefab : InsteadDoor;

            if (prefab != null)
            {
                InstantiateDoorObject(prefab, selectedExit.Exit, ExitDoorPositionOffset, ExitDoorRotationOffset);
            }

            RemoveOpenExit(openExits, selectedExit);
            DestroyExitObject(selectedExit.Owner, selectedExit.Exit);

            return true;
        }

        // 실제 프리팹을 생성해서 targetExit에 맞춰 붙인다.
        private bool TryInstantiateAndAttach(Cell prefab, OpenExit targetExit, bool allowStartRoom, out Cell placedCell, out Transform selectedExit)
        {
            placedCell = null;
            selectedExit = null;

            if (prefab == null || !IsValidOpenExit(targetExit))
            {
                return false;
            }

            if (!CanUsePrefabForTarget(prefab, targetExit, allowStartRoom))
            {
                return false;
            }

            Cell tempCell = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            tempCell.name = prefab.name;
            tempCell.CacheTriggerBox();

            if (tempCell.TriggerBox != null)
            {
                tempCell.TriggerBox.enabled = false;
            }

            List<Transform> usableExits = GetUsableExitTransformsForTarget(tempCell, targetExit);

            if (usableExits.Count <= 0)
            {
                DestroyCreatedCell(tempCell);
                return false;
            }

            for (int i = 0; i < MaxPlacementAttempts; i++)
            {
                Transform tempSelectedExit = usableExits[Random.Range(0, usableExits.Count)];

                AlignCellToExit(tempCell, tempSelectedExit, targetExit.Exit);

                bool insideBounds = IsCellInsideMapBounds(tempCell);
                bool collided = IsCellColliding(tempCell);

                if (!insideBounds || collided)
                {
                    continue;
                }

                if (tempCell.TriggerBox != null)
                {
                    tempCell.TriggerBox.enabled = true;
                }

                placedCell = tempCell;
                selectedExit = tempSelectedExit;
                return true;
            }

            DestroyCreatedCell(tempCell);
            return false;
        }

        // 후보 프리팹이 현재 출구에 붙을 수 있는지 검사한다.
        private bool CanUsePrefabForTarget(Cell prefab, OpenExit targetExit, bool allowStartRoom)
        {
            if (prefab == null || targetExit == null || targetExit.Owner == null)
            {
                return false;
            }

            if (!allowStartRoom && prefab.CellType == FacilityCellType.StartRoom)
            {
                return false;
            }

            if (!HasSpawnLimitLeft(prefab))
            {
                return false;
            }

            int targetFloor = GetWorldFloor(targetExit.Exit.position);

            // 2층에서는 계단방, 수직복도, 1+2층 방이 다시 생성되면 안 된다.
            if (targetFloor == 2)
            {
                if (prefab.IsVerticalConnector)
                {
                    return false;
                }

                if (prefab.CellType == FacilityCellType.StairRoom ||
                    prefab.CellType == FacilityCellType.VerticalCorridor ||
                    prefab.CellType == FacilityCellType.MultiFloorRoom)
                {
                    return false;
                }
            }

            if (targetFloor == 1 && prefab.FloorRule == FacilityFloorRule.SecondFloorOnly)
            {
                return false;
            }

            if (targetFloor == 2 && prefab.FloorRule == FacilityFloorRule.FirstFloorOnly)
            {
                return false;
            }

            // 큰방 - 큰방 직접 연결 금지
            if (BlockBigRoomToBigRoom)
            {
                if (targetExit.Owner.CellSize == FacilityCellSize.Big &&
                    prefab.CellSize == FacilityCellSize.Big)
                {
                    return false;
                }
            }

            // 복도 - 복도 직접 연결 금지
            if (BlockCorridorToCorridor)
            {
                if (targetExit.Owner.IsCorridorLike && prefab.IsCorridorLike)
                {
                    return false;
                }
            }

            // 방 - 방 직접 연결을 막고 싶을 때 사용
            if (ForceCorridorBetweenRooms)
            {
                if (targetExit.Owner.IsRoomLike && prefab.IsRoomLike)
                {
                    if (AllowSmallBigDirectConnection && IsSmallBigPair(targetExit.Owner, prefab))
                    {
                        return true;
                    }

                    return false;
                }
            }

            return true;
        }

        // 특정 출구에 붙일 수 있는 후보 Cell 목록을 만든다.
        private List<Cell> GetAllowedCandidates(OpenExit targetExit, bool allowStartRoom)
        {
            List<Cell> result = new List<Cell>();

            if (CellPrefabs == null)
            {
                return result;
            }

            for (int i = 0; i < CellPrefabs.Length; i++)
            {
                Cell prefab = CellPrefabs[i];

                if (prefab == null)
                {
                    continue;
                }

                if (CanUsePrefabForTarget(prefab, targetExit, allowStartRoom))
                {
                    result.Add(prefab);
                }
            }

            return result;
        }

        // 처음 중앙에 배치할 방 후보를 고른다.
        private Cell GetFirstRoomPrefab()
        {
            List<Cell> candidates = new List<Cell>();

            if (CellPrefabs == null)
            {
                return null;
            }

            for (int i = 0; i < CellPrefabs.Length; i++)
            {
                Cell prefab = CellPrefabs[i];

                if (prefab == null)
                {
                    continue;
                }

                if (prefab.CellType == FacilityCellType.StartRoom)
                {
                    continue;
                }

                if (prefab.CellType == FacilityCellType.Corridor ||
                    prefab.CellType == FacilityCellType.VerticalCorridor)
                {
                    continue;
                }

                if (prefab.FloorRule == FacilityFloorRule.SecondFloorOnly)
                {
                    continue;
                }

                candidates.Add(prefab);
            }

            if (candidates.Count <= 0)
            {
                return null;
            }

            return GetWeightedRandomCell(candidates);
        }

        // 시작방 후보 목록을 가져온다.
        private List<Cell> GetStartRoomCandidates()
        {
            List<Cell> result = new List<Cell>();

            if (StartRoomPrefabs != null && StartRoomPrefabs.Length > 0)
            {
                for (int i = 0; i < StartRoomPrefabs.Length; i++)
                {
                    if (StartRoomPrefabs[i] != null)
                    {
                        result.Add(StartRoomPrefabs[i]);
                    }
                }

                return result;
            }

            if (CellPrefabs == null)
            {
                return result;
            }

            for (int i = 0; i < CellPrefabs.Length; i++)
            {
                Cell prefab = CellPrefabs[i];

                if (prefab != null && prefab.CellType == FacilityCellType.StartRoom)
                {
                    result.Add(prefab);
                }
            }

            return result;
        }

        // 일반 복도 후보 목록을 가져온다.
        private List<Cell> GetCorridorCandidates()
        {
            List<Cell> result = new List<Cell>();

            if (CellPrefabs == null)
            {
                return result;
            }

            for (int i = 0; i < CellPrefabs.Length; i++)
            {
                Cell prefab = CellPrefabs[i];

                if (prefab == null)
                {
                    continue;
                }

                if (prefab.CellType == FacilityCellType.Corridor)
                {
                    result.Add(prefab);
                }
            }

            return result;
        }

        // 가중치를 반영해서 랜덤 Cell을 고른다.
        private Cell GetWeightedRandomCell(List<Cell> candidates)
        {
            if (candidates == null || candidates.Count <= 0)
            {
                return null;
            }

            int totalWeight = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                totalWeight += Mathf.Max(1, candidates[i].SpawnWeight);
            }

            int randomValue = Random.Range(0, totalWeight);
            int current = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                current += Mathf.Max(1, candidates[i].SpawnWeight);

                if (randomValue < current)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        // 배치된 Cell을 런타임 목록에 등록한다.
        private void PrepareCreatedCell(Cell cell, Cell sourcePrefab)
        {
            if (cell == null)
            {
                return;
            }

            cell.CacheTriggerBox();

            if (cell.TriggerBox != null)
            {
                cell.TriggerBox.enabled = true;
                cell.TriggerBox.isTrigger = true;
            }

            if (!generatedCells.Contains(cell))
            {
                generatedCells.Add(cell);
            }

            IncreaseSpawnCount(sourcePrefab);
        }

        // Cell의 사용 가능한 출구들을 열린 출구 목록에 추가한다.
        private void AddOpenExitsFromCell(List<OpenExit> openExits, Cell cell, Transform exceptExit)
        {
            if (cell == null || cell.Exits == null)
            {
                return;
            }

            for (int i = 0; i < cell.Exits.Length; i++)
            {
                GameObject exitObject = cell.Exits[i];

                if (exitObject == null)
                {
                    continue;
                }

                if (!exitObject.activeInHierarchy)
                {
                    continue;
                }

                if (exceptExit != null && exitObject.transform == exceptExit)
                {
                    continue;
                }

                openExits.Add(new OpenExit(exitObject.transform, cell));
            }
        }

        // Cell에서 현재 살아 있는 출구들을 가져온다.
        private List<Transform> GetActiveExitTransforms(Cell cell, Transform exceptExit)
        {
            List<Transform> result = new List<Transform>();

            if (cell == null || cell.Exits == null)
            {
                return result;
            }

            for (int i = 0; i < cell.Exits.Length; i++)
            {
                GameObject exitObject = cell.Exits[i];

                if (exitObject == null)
                {
                    continue;
                }

                if (!exitObject.activeInHierarchy)
                {
                    continue;
                }

                if (exceptExit != null && exitObject.transform == exceptExit)
                {
                    continue;
                }

                result.Add(exitObject.transform);
            }

            return result;
        }

        // 현재 targetExit에 사용할 수 있는 tempCell의 출구들을 구한다.
        private List<Transform> GetUsableExitTransformsForTarget(Cell tempCell, OpenExit targetExit)
        {
            List<Transform> result = new List<Transform>();

            if (tempCell == null || tempCell.Exits == null || targetExit == null)
            {
                return result;
            }

            int targetFloor = GetWorldFloor(targetExit.Exit.position);

            for (int i = 0; i < tempCell.Exits.Length; i++)
            {
                GameObject exitObject = tempCell.Exits[i];

                if (exitObject == null)
                {
                    continue;
                }

                Transform exitTransform = exitObject.transform;

                // 수직 연결 방은 1층 출구로만 1층에 붙게 한다.
                // 2층 출구로 1층에 붙으면 방 전체가 아래로 내려가서 층이 꼬인다.
                if (tempCell.IsVerticalConnector && targetFloor == 1)
                {
                    int localExitFloor = GetLocalFloor(tempCell, exitTransform);

                    if (localExitFloor != 1)
                    {
                        continue;
                    }
                }

                result.Add(exitTransform);
            }

            return result;
        }

        // 방을 회전/이동시켜 selectedExit이 targetExit에 정확히 붙게 만든다.
        private void AlignCellToExit(Cell cell, Transform selectedExit, Transform targetExit)
        {
            cell.transform.position = Vector3.zero;
            cell.transform.rotation = Quaternion.identity;

            float shiftAngle = targetExit.eulerAngles.y + 180f - selectedExit.eulerAngles.y;
            cell.transform.Rotate(new Vector3(0f, shiftAngle, 0f), Space.World);

            Vector3 shiftPosition = targetExit.position - selectedExit.position;
            cell.transform.position += shiftPosition;
        }

        // BoxCollider 기준으로 기존 Cell과 겹치는지 검사한다.
        private bool IsCellColliding(Cell cell)
        {
            if (cell == null || cell.TriggerBox == null)
            {
                return true;
            }

            BoxCollider box = cell.TriggerBox;

            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;
            Quaternion rotation = box.transform.rotation;

            return Physics.CheckBox(center, halfExtents, rotation, CellLayer, QueryTriggerInteraction.Collide);
        }

        // Cell 전체 Bounds가 맵 크기 안에 들어오는지 검사한다.
        private bool IsCellInsideMapBounds(Cell cell)
        {
            if (!UseMapBounds)
            {
                return true;
            }

            if (cell == null || cell.TriggerBox == null)
            {
                return false;
            }

            BoxCollider box = cell.TriggerBox;

            Vector3 half = box.size * 0.5f;

            float minX = MapCenter.x - MapSize.x * 0.5f + MapBoundsPadding;
            float maxX = MapCenter.x + MapSize.x * 0.5f - MapBoundsPadding;
            float minZ = MapCenter.z - MapSize.y * 0.5f + MapBoundsPadding;
            float maxZ = MapCenter.z + MapSize.y * 0.5f - MapBoundsPadding;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localCorner = box.center + new Vector3(half.x * x, half.y * y, half.z * z);
                        Vector3 worldCorner = box.transform.TransformPoint(localCorner);

                        if (worldCorner.x < minX || worldCorner.x > maxX ||
                            worldCorner.z < minZ || worldCorner.z > maxZ)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        // 월드 Y 위치를 기준으로 1층/2층을 판단한다.
        private int GetWorldFloor(Vector3 worldPosition)
        {
            float secondFloorStartY = MapCenter.y + FloorHeight * 0.5f;

            if (worldPosition.y >= secondFloorStartY)
            {
                return 2;
            }

            return 1;
        }

        // 프리팹 내부 출구의 로컬 Y 기준으로 1층/2층 출구를 판단한다.
        private int GetLocalFloor(Cell cell, Transform exit)
        {
            if (cell == null || exit == null)
            {
                return 1;
            }

            float localY = cell.transform.InverseTransformPoint(exit.position).y;

            if (localY >= FloorHeight * 0.5f)
            {
                return 2;
            }

            return 1;
        }

        // 시작방끼리 너무 가까운지 검사한다.
        private bool IsStartRoomDistanceValid(Vector3 position)
        {
            for (int i = 0; i < startRoomPositions.Count; i++)
            {
                float distance = Vector2.Distance(
                    new Vector2(position.x, position.z),
                    new Vector2(startRoomPositions[i].x, startRoomPositions[i].z)
                );

                if (distance < StartRoomMinDistance)
                {
                    return false;
                }
            }

            return true;
        }

        // 특정 위치가 시작방 근처인지 검사한다.
        private bool IsNearAnyStartRoom(Vector3 position, float distanceLimit)
        {
            for (int i = 0; i < startRoomPositions.Count; i++)
            {
                float distance = Vector2.Distance(
                    new Vector2(position.x, position.z),
                    new Vector2(startRoomPositions[i].x, startRoomPositions[i].z)
                );

                if (distance < distanceLimit)
                {
                    return true;
                }
            }

            return false;
        }

        // 작은방-큰방 조합인지 확인한다.
        private bool IsSmallBigPair(Cell a, Cell b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            bool aSmallBbig = a.CellSize == FacilityCellSize.Small && b.CellSize == FacilityCellSize.Big;
            bool aBigBsmall = a.CellSize == FacilityCellSize.Big && b.CellSize == FacilityCellSize.Small;

            return aSmallBbig || aBigBsmall;
        }

        // 생성 개수 제한이 남아 있는지 확인한다.
        private bool HasSpawnLimitLeft(Cell prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            if (prefab.MaxSpawnCount <= 0)
            {
                return true;
            }

            int currentCount = 0;
            spawnedCountByPrefabName.TryGetValue(prefab.name, out currentCount);

            return currentCount < prefab.MaxSpawnCount;
        }

        // 생성 개수를 증가시킨다.
        private void IncreaseSpawnCount(Cell prefab)
        {
            if (prefab == null)
            {
                return;
            }

            if (!spawnedCountByPrefabName.ContainsKey(prefab.name))
            {
                spawnedCountByPrefabName.Add(prefab.name, 0);
            }

            spawnedCountByPrefabName[prefab.name]++;
        }

        // 연결된 방 사이에 문을 생성한다.
        private void InstantiateConnectedDoor(Transform exit)
        {
            if (exit == null)
            {
                return;
            }

            GameObject prefab = GetRandomDoorPrefab();

            if (prefab != null)
            {
                InstantiateDoorObject(prefab, exit, ConnectedDoorPositionOffset, ConnectedDoorRotationOffset);
            }
        }

        // 출구 기준으로 문/막는벽/탈출구를 생성하고 목록에 저장한다.
        private GameObject InstantiateDoorObject(GameObject prefab, Transform exit, Vector3 localPositionOffset, Vector3 localRotationOffset)
        {
            if (prefab == null || exit == null)
            {
                return null;
            }

            Vector3 position = exit.TransformPoint(localPositionOffset);
            Quaternion rotation = exit.rotation * Quaternion.Euler(localRotationOffset);

            GameObject doorObject = Instantiate(prefab, position, rotation, transform);

            if (doorObject != null && !generatedDoorObjects.Contains(doorObject))
            {
                generatedDoorObjects.Add(doorObject);
            }

            return doorObject;
        }

        // 랜덤 문 프리팹을 가져온다.
        private GameObject GetRandomDoorPrefab()
        {
            if (DoorPrefabs == null || DoorPrefabs.Length <= 0)
            {
                return null;
            }

            List<GameObject> validDoors = new List<GameObject>();

            for (int i = 0; i < DoorPrefabs.Length; i++)
            {
                if (DoorPrefabs[i] != null)
                {
                    validDoors.Add(DoorPrefabs[i]);
                }
            }

            if (validDoors.Count <= 0)
            {
                return null;
            }

            return validDoors[Random.Range(0, validDoors.Count)];
        }

        // 남아 있는 모든 DoorPoint를 막힌 문으로 막는다.
        private void BlockRemainingExits()
        {
            int blockedExitCount = 0;

            if (InsteadDoor == null)
            {
                Debug.LogWarning("[LaboratoryGenerator] InsteadDoor가 비어 있어서 남은 출구에 막는 벽을 생성할 수 없음.");
            }

            for (int i = 0; i < generatedCells.Count; i++)
            {
                Cell cell = generatedCells[i];

                if (cell == null || cell.Exits == null)
                {
                    continue;
                }

                for (int j = 0; j < cell.Exits.Length; j++)
                {
                    GameObject exitObject = cell.Exits[j];

                    if (exitObject == null)
                    {
                        continue;
                    }

                    if (!exitObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (InsteadDoor != null)
                    {
                        InstantiateDoorObject(InsteadDoor, exitObject.transform, BlockDoorPositionOffset, BlockDoorRotationOffset);
                        blockedExitCount++;
                    }

                    DestroyExitObject(cell, exitObject.transform);
                }
            }
            Debug.Log("[LaboratoryGenerator] Blocked remaining exits: " + blockedExitCount);
        }

        // 열린 출구 목록에서 유효하지 않은 출구를 제거한다.
        private void CleanupOpenExits(List<OpenExit> openExits)
        {
            for (int i = openExits.Count - 1; i >= 0; i--)
            {
                if (!IsValidOpenExit(openExits[i]))
                {
                    openExits.RemoveAt(i);
                }
            }
        }

        // 열린 출구가 아직 사용 가능한지 확인한다.
        private bool IsValidOpenExit(OpenExit openExit)
        {
            if (openExit == null)
            {
                return false;
            }

            if (openExit.Exit == null)
            {
                return false;
            }

            if (openExit.Owner == null)
            {
                return false;
            }

            if (!openExit.Exit.gameObject.activeInHierarchy)
            {
                return false;
            }

            return true;
        }

        // 열린 출구 목록에서 특정 출구를 제거한다.
        private void RemoveOpenExit(List<OpenExit> openExits, OpenExit target)
        {
            if (target == null)
            {
                return;
            }

            for (int i = openExits.Count - 1; i >= 0; i--)
            {
                if (openExits[i] == target || openExits[i].Exit == target.Exit)
                {
                    openExits.RemoveAt(i);
                }
            }
        }

        // DoorPoint 오브젝트를 비활성화하고 제거한다.
        private void DestroyExitObject(Cell owner, Transform exit)
        {
            if (exit == null)
            {
                return;
            }

            DisableExitVisualForExit(owner, exit);

            exit.gameObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(exit.gameObject);
            }
            else
            {
                DestroyImmediate(exit.gameObject);
            }
        }

        // 사용된 DoorPoint와 같은 번호의 빨간 바닥/마커를 끈다.
        private void DisableExitVisualForExit(Cell owner, Transform exit)
        {
            if (!HideExitVisualsWhenExitUsed || owner == null || exit == null)
            {
                return;
            }

            int exitIndex = GetExitIndex(owner, exit);

            if (exitIndex >= 0 && owner.ExitVisuals != null && exitIndex < owner.ExitVisuals.Length)
            {
                GameObject visual = owner.ExitVisuals[exitIndex];

                if (visual != null)
                {
                    RemoveOrDisableGameObject(visual);
                }
            }

            if (AutoHideRedExitMarkers)
            {
                AutoHideRedExitMarkersNearExit(owner, exit.position);
            }
        }

        // Cell.Exits 배열에서 특정 DoorPoint의 번호를 찾는다.
        private int GetExitIndex(Cell owner, Transform exit)
        {
            if (owner == null || owner.Exits == null || exit == null)
            {
                return -1;
            }

            for (int i = 0; i < owner.Exits.Length; i++)
            {
                GameObject exitObject = owner.Exits[i];

                if (exitObject == null)
                {
                    continue;
                }

                if (exitObject.transform == exit)
                {
                    return i;
                }
            }

            return -1;
        }

        // 출구 근처에 있는 빨간 Renderer를 찾아 끈다. ExitVisuals를 수동 연결하지 않았을 때의 보조 기능이다.
        private void AutoHideRedExitMarkersNearExit(Cell owner, Vector3 exitPosition)
        {
            if (owner == null)
            {
                return;
            }

            Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
            float radius = Mathf.Max(0.1f, RedExitMarkerSearchRadius);
            float maxHeight = Mathf.Max(0.1f, RedExitMarkerMaxHeightDifference);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                if (!IsRedExitMarkerRenderer(renderer))
                {
                    continue;
                }

                Vector3 center = renderer.bounds.center;
                Vector2 flatA = new Vector2(center.x, center.z);
                Vector2 flatB = new Vector2(exitPosition.x, exitPosition.z);

                if (Vector2.Distance(flatA, flatB) > radius)
                {
                    continue;
                }

                if (Mathf.Abs(center.y - exitPosition.y) > maxHeight)
                {
                    continue;
                }

                RemoveOrDisableGameObject(renderer.gameObject);
            }
        }

        // Renderer의 머티리얼 색이 빨간 출구 마커로 보이는지 확인한다.
        private bool IsRedExitMarkerRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterials == null)
            {
                return false;
            }

            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Color color;

                if (TryGetMaterialColor(materials[i], out color))
                {
                    if (color.r >= 0.55f && color.g <= 0.35f && color.b <= 0.35f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // URP Lit/Unlit과 Built-in Standard의 대표 색상 프로퍼티를 읽는다.
        private bool TryGetMaterialColor(Material material, out Color color)
        {
            color = Color.white;

            if (material == null)
            {
                return false;
            }

            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                return true;
            }

            return false;
        }

        // 오브젝트를 끄거나 삭제한다.
        private void RemoveOrDisableGameObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (DestroyExitVisualsInsteadOfDisable)
            {
                if (Application.isPlaying)
                {
                    Destroy(target);
                }
                else
                {
                    DestroyImmediate(target);
                }
            }
            else
            {
                target.SetActive(false);
            }
        }

        // 인게임에서 생성된 맵 조명을 꺼서 그림자 경고와 성능 낭비를 줄인다.
        private void DisableGeneratedLightsForGameplay()
        {
            if (!DisableGeneratedLightsInGame)
            {
                return;
            }

            if (DisableGeneratedLightsOnlyInPlayMode && !Application.isPlaying)
            {
                return;
            }

            for (int i = 0; i < generatedCells.Count; i++)
            {
                Cell cell = generatedCells[i];

                if (cell == null)
                {
                    continue;
                }

                SetLightsEnabled(cell.gameObject, false);
            }

            if (DisableGeneratedDoorLightsToo)
            {
                for (int i = 0; i < generatedDoorObjects.Count; i++)
                {
                    GameObject doorObject = generatedDoorObjects[i];

                    if (doorObject == null)
                    {
                        continue;
                    }

                    SetLightsEnabled(doorObject, false);
                }
            }
        }

        // Light만 켜고 끈다. Collider와 Renderer는 건드리지 않는다.
        private void SetLightsEnabled(GameObject root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            Light[] lights = root.GetComponentsInChildren<Light>(true);

            for (int i = 0; i < lights.Length; i++)
            {
                Light targetLight = lights[i];

                if (targetLight == null)
                {
                    continue;
                }

                if (!enabled && DisableGeneratedLightShadows)
                {
                    targetLight.shadows = LightShadows.None;
                }

                targetLight.enabled = enabled;
            }
        }

        // 인게임에서 맵 모델은 보이지 않게 하고 Collider는 그대로 둔다.
        private void HideGeneratedVisualsForGameplay()
        {
            if (!HideGeneratedVisualsInGame)
            {
                return;
            }

            if (HideGeneratedVisualsOnlyInPlayMode && !Application.isPlaying)
            {
                return;
            }

            for (int i = 0; i < generatedCells.Count; i++)
            {
                Cell cell = generatedCells[i];

                if (cell == null)
                {
                    continue;
                }

                SetRenderersEnabled(cell.gameObject, false);
            }

            if (HideGeneratedDoorVisualsToo)
            {
                for (int i = 0; i < generatedDoorObjects.Count; i++)
                {
                    GameObject doorObject = generatedDoorObjects[i];

                    if (doorObject == null)
                    {
                        continue;
                    }

                    SetRenderersEnabled(doorObject, false);
                }
            }
        }

        // Renderer만 켜고 끈다. Collider는 건드리지 않는다.
        private void SetRenderersEnabled(GameObject root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        // 생성 실패한 Cell을 제거한다.
        private void DestroyCreatedCell(Cell cell)
        {
            if (cell == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(cell.gameObject);
            }
            else
            {
                DestroyImmediate(cell.gameObject);
            }
        }

        // 이전에 생성된 자식 오브젝트들을 제거한다.
        private void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        // 런타임 데이터를 초기화한다.
        private void ResetRuntimeData()
        {
            generatedCells.Clear();
            startRoomPositions.Clear();
            spawnedCountByPrefabName.Clear();
            generatedDoorObjects.Clear();
        }

        // Scene View에서 80x80 같은 맵 제한 범위를 보여준다.
        private void OnDrawGizmosSelected()
        {
            if (!DrawMapBoundsGizmo || !UseMapBounds)
            {
                return;
            }

            Gizmos.color = Color.yellow;

            Vector3 size = new Vector3(MapSize.x, 0.1f, MapSize.y);
            Gizmos.DrawWireCube(MapCenter, size);
        }
    }
}