using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ArtNotes.UndergroundLaboratoryGenerator
{
    public class LaboratoryGenerator : MonoBehaviour
    {
        [Header("Basic")]
        public bool GenerateOnStart = true;

        [Tooltip("PUN 방에서 시작하면 호스트가 저장한 seed로 모든 클라이언트가 같은 맵을 생성")]
        public bool UsePhotonRoomSeed = true;

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

        [Tooltip("켜면 예전 방식처럼 플레이어 수만큼 전용 시작방을 따로 생성함. 지금 방식에서는 끄는 것을 추천")]
        public bool GenerateDedicatedStartRooms = false;

        [Header("Player Spawn Points")]
        [Tooltip("방 프리팹 안에 직접 배치할 플레이어 스폰 포인트 이름 접두어")]
        public string PlayerSpawnPointPrefix = "PlayerSpawnPoint";

        [Tooltip("비활성화된 PlayerSpawnPoint도 후보에 포함")]
        public bool IncludeInactivePlayerSpawnPoints = true;

        [Tooltip("플레이어 스폰은 방 타입에서만 허용. 복도 스폰을 막고 싶으면 켜둠")]
        public bool PlayerSpawnOnlyInRooms = true;

        [Tooltip("플레이어끼리 같은 방에서 시작하지 못하게 함")]
        public bool OnePlayerPerRoom = true;

        [Tooltip("탈출구가 붙은 방에서는 플레이어가 시작하지 못하게 함")]
        public bool AvoidExitRoomForPlayerSpawn = true;

        [Header("Player Spawn")]
        [Tooltip("맵 생성 후 PlayerSpawnPoint 중 하나로 플레이어를 이동/생성")]
        public bool SpawnPlayersAfterGeneration = true;

        [Tooltip("씬에 이미 있는 플레이어 1명을 옮길 때 사용")]
        public Transform ExistingPlayer;

        [Tooltip("씬에 이미 있는 여러 플레이어를 옮길 때 사용")]
        public Transform[] ExistingPlayers;

        [Tooltip("기존 플레이어가 부족할 때 생성할 플레이어 프리팹. 싱글 테스트면 비워도 됨")]
        public GameObject PlayerPrefab;

        [Tooltip("0이면 자동. 기존 플레이어가 있으면 그 수만큼, 없고 PlayerPrefab이 있으면 PlayerCount만큼 생성")]
        [Range(0, 12)]
        public int PlayerSpawnCount = 0;

        [Tooltip("생성된 플레이어의 부모. 비워두면 씬 루트에 생성")]
        public Transform PlayerParent;

        [Tooltip("ExistingPlayer를 비워도 Player 태그를 가진 오브젝트를 자동으로 찾음")]
        public bool AutoFindExistingPlayerByTag = true;

        [Tooltip("PlayerSpawnPoint 위치에서 추가로 보정할 값")]
        public Vector3 PlayerSpawnPositionOffset = Vector3.zero;

        [Tooltip("PlayerSpawnPoint의 회전을 플레이어에게 적용")]
        public bool AlignPlayerRotationToSpawnPoint = true;

        [Tooltip("켜면 SpawnPoint 회전 대신 플레이어 Y축 회전을 무작위로 설정")]
        public bool RandomizePlayerSpawnYaw = false;

        [Header("Item Spawn Points")]
        [Tooltip("방 프리팹 안에 직접 배치할 아이템 스폰 포인트 이름 접두어")]
        public string ItemSpawnPointPrefix = "ItemSpawnPoint";

        [Tooltip("비활성화된 ItemSpawnPoint도 후보에 포함")]
        public bool IncludeInactiveItemSpawnPoints = true;

        [Tooltip("아이템 스폰은 방 타입에서만 허용. 복도 아이템 스폰을 막고 싶으면 켜둠")]
        public bool ItemSpawnOnlyInRooms = true;

        [Header("Item Spawn")]
        [Tooltip("맵 생성 후 ItemSpawnPoint 중 무작위 위치에 아이템 생성")]
        public bool SpawnItemsAfterGeneration = false;

        [Tooltip("생성할 아이템 프리팹 배열. Access Core 하나만 넣고 ItemSpawnCount를 4로 두면 4개 생성 가능")]
        public GameObject[] ItemPrefabs;

        [Tooltip("생성할 아이템 개수")]
        [Range(0, 100)]
        public int ItemSpawnCount = 4;

        [Tooltip("플레이어가 스폰된 방에도 아이템이 생성될 수 있게 허용")]
        public bool AllowItemSpawnInPlayerRooms = true;

        [Tooltip("아이템이 플레이어 스폰 위치와 너무 가까우면 제외. 0이면 거리 제한 없음")]
        public float MinItemDistanceFromPlayerSpawn = 2f;

        [Tooltip("같은 ItemSpawnPoint에 아이템이 중복 생성되지 않게 함")]
        public bool PreventDuplicateItemSpawnPointUse = true;

        [Tooltip("생성된 아이템의 부모. 비워두면 이 Generator 밑에 생성")]
        public Transform ItemParent;

        [Tooltip("ItemSpawnPoint 위치에서 추가로 보정할 값")]
        public Vector3 ItemSpawnPositionOffset = Vector3.zero;

        [Tooltip("ItemSpawnPoint의 회전을 아이템에게 적용")]
        public bool AlignItemRotationToSpawnPoint = true;

        [Header("Item Runtime State")]
        [Tooltip("생성된 아이템의 Renderer만 끔. Collider, WorldItemPickup, ScanSurfaceInfo는 유지됨")]
        public bool HideSpawnedItemRenderers = true;

        [Tooltip("방 하나에 생성될 수 있는 최대 아이템 개수. 2면 한 방에 3개 이상 생성 금지")]
        [Range(1, 20)]
        public int MaxItemsPerRoom = 2;

        [Header("Item Ground Snap")]
        [Tooltip("바닥 Collider 없이 방의 층 바닥 높이를 기준으로 아이템 Bounds의 아래쪽을 맞춤")]
        public bool SnapSpawnedItemsToSpawnPointGround = true;

        [Tooltip("아이템을 바닥에 완전히 붙이지 않고 살짝 띄우는 값")]
        public float ItemGroundOffset = 0.03f;

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

        [Tooltip("일반 방, 큰방, 복도, 계단방, 수직복도 등을 전부 넣는 배열")]
        public Cell[] CellPrefabs;

        [Tooltip("시작방 후보 프리팹. 비워두면 CellPrefabs에서 StartRoom 타입을 자동으로 찾음")]
        public Cell[] StartRoomPrefabs;

        [Header("Door Placement Offset")]
        [Tooltip("일반 연결 문 생성 위치 보정값. DoorPoint 기준 로컬 좌표")]
        public Vector3 ConnectedDoorPositionOffset = Vector3.zero;

        [Tooltip("일반 연결 문 생성 회전 보정값")]
        public Vector3 ConnectedDoorRotationOffset = Vector3.zero;

        [Tooltip("막힌 문/막힌 벽 생성 위치 보정값. Cube 벽 프리팹이면 보통 Y를 벽 높이의 절반 정도로 올림")]
        public Vector3 BlockDoorPositionOffset = new Vector3(0f, 1.5f, 0f);

        [Tooltip("막힌 문/막힌 벽 생성 회전 보정값. 방향이 90도 틀어지면 Y를 90 또는 -90으로 조정")]
        public Vector3 BlockDoorRotationOffset = Vector3.zero;

        [Tooltip("탈출구 문 생성 위치 보정값")]
        public Vector3 ExitDoorPositionOffset = Vector3.zero;

        [Tooltip("탈출구 문 생성 회전 보정값")]
        public Vector3 ExitDoorRotationOffset = Vector3.zero;

        [Header("Runtime Visibility")]
        [Tooltip("게임 시작 후 생성된 맵의 Renderer를 꺼서 직접 보이지 않게 함. Collider는 유지되므로 스캔은 가능")]
        public bool HideGeneratedVisualsInGame = true;

        [Tooltip("켜면 Play Mode에서만 Renderer를 끔. 에디터에서 Generate했을 때는 계속 보이게 두기 좋음")]
        public bool HideGeneratedVisualsOnlyInPlayMode = true;

        [Tooltip("켜면 생성된 맵/아이템의 일반 BoxCollider를 끄고 MeshCollider 표면만 스캔/충돌에 사용")]
        public bool UseMeshCollidersInsteadOfBoxColliders = false;

        [Header("Generated Light Optimization")]
        [Tooltip("생성된 방/복도/문 안에 있는 Light의 그림자 설정을 자동으로 정리해서 URP shadow atlas 경고를 줄임")]
        public bool OptimizeGeneratedLightShadows = true;

        [Tooltip("켜면 생성된 모든 Light의 Shadows를 None으로 바꿔서 Reduced additional punctual light shadows 경고를 막음")]
        public bool DisableGeneratedLightShadows = true;

        [Header("Block Door Fallback")]
        [Tooltip("InsteadDoor가 비어있어도 남은 출구를 임시 큐브 벽으로 막음")]
        public bool CreateFallbackBlockDoorWhenPrefabMissing = true;

        [Tooltip("InsteadDoor가 없을 때 생성할 임시 막는 벽 큐브 크기")]
        public Vector3 FallbackBlockDoorLocalScale = new Vector3(3.0f, 3.0f, 0.25f);

        [Tooltip("InsteadDoor가 없을 때 생성할 임시 막는 벽 큐브 머티리얼")]
        public Material FallbackBlockDoorMaterial;

        [Tooltip("Cell.Exits 배열에 빠져있는 남은 문 포인트도 이름으로 찾아서 막음")]
        public bool BlockExtraExitObjectsByName = true;

        [Tooltip("남은 출구 오브젝트로 판단할 이름 키워드. Underground Laboratory Generator 기본값은 보통 TempPortal 계열임")]
        public string[] ExtraExitObjectNameKeywords = { "TempPortal", "DoorPoint" };

        [Header("Cleanup")]
        [Tooltip("생성 전에 이 오브젝트의 기존 자식들을 삭제")]
        public bool ClearPreviousGeneratedChildren = true;

        [Header("BFS Expansion Shape")]
        [Tooltip("켜면 가장 안쪽 깊이의 열린 출구부터 처리해서 일자형 생성을 줄이고 가지가 퍼지는 형태로 생성")]
        public bool UseBfsExpansion = true;

        [Tooltip("0이면 완전 BFS. 1 이상이면 현재 깊이 근처 출구도 섞어서 조금 더 자연스럽게 생성")]
        [Range(0, 5)]
        public int BfsDepthRandomSpread = 0;

        [Tooltip("특정 열린 출구에서 배치 실패가 이 횟수 이상이면 그 출구는 확장 후보에서 제외")]
        [Range(1, 20)]
        public int MaxFailedAttemptsPerOpenExit = 2;

        // 생성된 방 목록을 저장한다.
        private readonly List<Cell> generatedCells = new List<Cell>();

        // 이미 방 연결, 시작방 연결, 탈출구 생성에 사용된 출구를 저장한다.
        // Destroy는 Play Mode에서 즉시 사라지지 않기 때문에, 사용된 출구를 따로 기록해서 중간 벽 생성을 막는다.
        private readonly HashSet<Transform> consumedExitTransforms = new HashSet<Transform>();

        // 시작방 위치를 저장한다.
        private readonly List<Vector3> startRoomPositions = new List<Vector3>();

        // 프리팹별 생성 개수를 저장한다.
        private readonly Dictionary<string, int> spawnedCountByPrefabName = new Dictionary<string, int>();

        // 플레이어가 실제로 스폰된 방 목록을 저장한다.
        private readonly List<Cell> playerSpawnedCells = new List<Cell>();

        // 플레이어가 실제로 스폰된 위치 목록을 저장한다.
        private readonly List<Vector3> playerSpawnedPositions = new List<Vector3>();

        // 탈출구가 붙은 방을 저장해서 플레이어 스폰 후보에서 제외할 수 있게 한다.
        private Cell exitDoorOwnerCell;

        // 탈출구 위치를 저장한다.
        private Vector3 exitDoorPosition;

        // 탈출구 생성 여부를 저장한다.
        private bool hasExitDoorPosition;

        // 아직 막히지 않은 출구 정보를 저장한다.
        private class OpenExit
        {
            public Transform Exit;
            public Cell Owner;
            public int Depth;
            public int FailCount;

            public OpenExit(Transform exit, Cell owner, int depth)
            {
                Exit = exit;
                Owner = owner;
                Depth = depth;
                FailCount = 0;
            }
        }

        // 방 안에 직접 배치한 PlayerSpawnPoint / ItemSpawnPoint 정보를 저장한다.
        private class SpawnPointRecord
        {
            public Transform Point;
            public Cell OwnerCell;

            public SpawnPointRecord(Transform point, Cell ownerCell)
            {
                Point = point;
                OwnerCell = ownerCell;
            }
        }

        private bool hasPhotonMapSeed;
        private int photonMapSeed;

        public static event Action<string, float> LoadingPhaseChanged;
        public static event Action GenerationFinished;

        public static bool IsAnyGenerationRunning { get; private set; }
        public bool IsGenerationComplete { get; private set; }

        private void Start()
        {
            ApplyPhotonRoomSeed();

            if (GenerateOnStart)
            {
                StartCoroutine(StartGeneration());
            }
            else
            {
                // 미리 생성해 둔 맵을 그대로 쓰는 경우에도 게임 시작 시에는 보이지 않게 처리한다.
                ApplyRuntimeVisualState();
                IsGenerationComplete = true;
                SetLoadingPhase("SCAN READY", 1f);
                GenerationFinished?.Invoke();
            }
        }

        // 외부에서 조건으로 맵을 다시 생성할 수 있도록 public으로 둔다.
        public IEnumerator StartGeneration()
        {
            IsAnyGenerationRunning = true;
            IsGenerationComplete = false;
            SetLoadingPhase("SCANNING AREA...", 0.06f);
            yield return null;

            bool generated = false;

            for (int attempt = 0; attempt < MaxFullGenerationAttempts; attempt++)
            {
                SetLoadingPhase("BUILDING PATHS...", Mathf.Lerp(0.12f, 0.42f, MaxFullGenerationAttempts <= 1 ? 1f : attempt / (float)(MaxFullGenerationAttempts - 1)));

                if (ClearPreviousGeneratedChildren)
                {
                    ClearGeneratedChildren();
                }

                ResetRuntimeData();

                bool success = TryGenerateOnce();

                if (success)
                {
                    Debug.Log("[LaboratoryGenerator] Generation finished.");
                    generated = true;
                    break;
                }

                Debug.LogWarning("[LaboratoryGenerator] Generation retry: " + (attempt + 1));
                yield return null;
            }

            if (!generated)
            {
                Debug.LogError("[LaboratoryGenerator] Generation failed.");
                SetLoadingPhase("SCAN FAILED", 1f);
            }

            IsAnyGenerationRunning = false;
            IsGenerationComplete = generated;

            if (generated)
            {
                SetLoadingPhase("SCAN READY", 1f);
                GenerationFinished?.Invoke();
            }
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

            AddOpenExitsFromCell(openExits, firstRoom, null, 0);

            int createdMainCells = 1;
            int failStreak = 0;

            while (createdMainCells < RoomCount && failStreak < MaxPlacementAttempts)
            {
                CleanupOpenExits(openExits);

                if (openExits.Count <= 0)
                {
                    break;
                }

                OpenExit targetExit = SelectOpenExitForExpansion(openExits);

                if (targetExit == null)
                {
                    break;
                }

                Cell placedCell;
                Transform selectedExit;

                bool placed = TryPlaceNormalCell(targetExit, out placedCell, out selectedExit);

                if (placed)
                {
                    createdMainCells++;
                    failStreak = 0;

                    MarkExitAsConsumed(targetExit.Exit);
                    MarkExitAsConsumed(selectedExit);

                    InstantiateConnectedDoor(targetExit.Exit);

                    AddOpenExitsFromCell(openExits, placedCell, selectedExit, targetExit.Depth + 1);

                    RemoveOpenExit(openExits, targetExit);

                    DestroyExitObject(targetExit.Exit);
                    DestroyExitObject(selectedExit);
                }
                else
                {
                    failStreak++;
                    targetExit.FailCount++;

                    // 한 출구에서 계속 실패하면 그 출구는 더 이상 확장하지 않는다.
                    // DoorPoint는 삭제하지 않기 때문에 마지막에 막힌 문으로 정리된다.
                    if (targetExit.FailCount >= Mathf.Max(1, MaxFailedAttemptsPerOpenExit))
                    {
                        RemoveOpenExit(openExits, targetExit);
                    }
                }
            }

            if (createdMainCells < 3)
            {
                Debug.LogWarning("[LaboratoryGenerator] 메인 맵이 너무 적게 생성됨.");
                return false;
            }

            if (GenerateDedicatedStartRooms)
            {
                bool startRoomsOk = GenerateStartRooms(openExits);

                if (!startRoomsOk)
                {
                    Debug.LogWarning("[LaboratoryGenerator] 시작방 생성 실패.");
                    return false;
                }
            }

            bool exitDoorOk = GenerateExitDoor(openExits);

            if (!exitDoorOk)
            {
                Debug.LogWarning("[LaboratoryGenerator] 탈출구 생성 실패.");
                return false;
            }

            SetLoadingPhase("CLOSING PATHS...", 0.64f);
            BlockRemainingExits();
            SetLoadingPhase("CALIBRATING SCANNER...", 0.72f);
            ApplyRuntimeVisualState();
            SetLoadingPhase("SYNCING PLAYERS...", 0.82f);
            SpawnPlayersAfterGeneratedMap();
            ApplyPhotonStageSeed(2000003, "item");
            SetLoadingPhase("PLACING SIGNALS...", 0.92f);
            SpawnItemsAfterGeneratedMap();

            return true;
        }

        private static void SetLoadingPhase(string message, float progress)
        {
            LoadingPhaseChanged?.Invoke(message, Mathf.Clamp01(progress));
        }

        // 메인 맵 확장에 사용할 열린 출구를 고른다.
        private OpenExit SelectOpenExitForExpansion(List<OpenExit> openExits)
        {
            CleanupOpenExits(openExits);

            if (openExits.Count <= 0)
            {
                return null;
            }

            if (!UseBfsExpansion)
            {
                return openExits[Random.Range(0, openExits.Count)];
            }

            int maxFail = Mathf.Max(1, MaxFailedAttemptsPerOpenExit);
            int minDepth = int.MaxValue;

            for (int i = 0; i < openExits.Count; i++)
            {
                OpenExit openExit = openExits[i];

                if (!IsValidOpenExit(openExit))
                {
                    continue;
                }

                if (openExit.FailCount >= maxFail)
                {
                    continue;
                }

                if (openExit.Depth < minDepth)
                {
                    minDepth = openExit.Depth;
                }
            }

            if (minDepth == int.MaxValue)
            {
                return null;
            }

            int allowedMaxDepth = minDepth + Mathf.Max(0, BfsDepthRandomSpread);
            List<OpenExit> candidates = new List<OpenExit>();

            for (int i = 0; i < openExits.Count; i++)
            {
                OpenExit openExit = openExits[i];

                if (!IsValidOpenExit(openExit))
                {
                    continue;
                }

                if (openExit.FailCount >= maxFail)
                {
                    continue;
                }

                if (openExit.Depth <= allowedMaxDepth)
                {
                    candidates.Add(openExit);
                }
            }

            if (candidates.Count <= 0)
            {
                return null;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        // 시작방 연결에 사용할 열린 출구를 고른다.
        private OpenExit SelectOpenExitForStartRoom(List<OpenExit> openExits)
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

                if (StartRoomsOnlyOnFirstFloor && GetWorldFloor(openExit.Exit.position) != 1)
                {
                    continue;
                }

                candidates.Add(openExit);
            }

            if (candidates.Count <= 0)
            {
                return null;
            }

            return candidates[Random.Range(0, candidates.Count)];
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

                OpenExit mainTargetExit = SelectOpenExitForStartRoom(openExits);

                if (mainTargetExit == null)
                {
                    return false;
                }

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

                OpenExit startTargetExit = new OpenExit(corridorExitForStart, placedCorridor, mainTargetExit.Depth + 1);
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

                MarkExitAsConsumed(mainTargetExit.Exit);
                MarkExitAsConsumed(corridorExitConnectedToMain);
                MarkExitAsConsumed(corridorExitForStart);
                MarkExitAsConsumed(startRoomSelectedExit);

                InstantiateConnectedDoor(mainTargetExit.Exit);
                InstantiateConnectedDoor(corridorExitForStart);

                RemoveOpenExit(openExits, mainTargetExit);

                DestroyExitObject(mainTargetExit.Exit);
                DestroyExitObject(corridorExitConnectedToMain);
                DestroyExitObject(corridorExitForStart);
                DestroyExitObject(startRoomSelectedExit);

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

            exitDoorOwnerCell = selectedExit.Owner;
            exitDoorPosition = selectedExit.Exit.position;
            hasExitDoorPosition = true;

            GameObject prefab = ExitDoorPrefab != null ? ExitDoorPrefab : InsteadDoor;

            MarkExitAsConsumed(selectedExit.Exit);

            if (prefab != null)
            {
                InstantiateDoorPrefab(prefab, selectedExit.Exit, ExitDoorPositionOffset, ExitDoorRotationOffset);
            }

            RemoveOpenExit(openExits, selectedExit);
            DestroyExitObject(selectedExit.Exit);

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

            // 복도 - 복도 직접 연결을 막는다.
            // 이렇게 하면 복도가 한 줄로 길게 이어지는 구조를 줄일 수 있다.
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

            ApplyGeneratedLightOptimization(cell.gameObject);
            EnsureScanMeshColliders(cell.gameObject);
            DisableRuntimeBoxColliders(cell.gameObject, cell.TriggerBox);
            DisableGeneratedHelperColliders(cell.gameObject);

            IncreaseSpawnCount(sourcePrefab);
        }

        // Cell의 사용 가능한 출구들을 열린 출구 목록에 추가한다.
        private void AddOpenExitsFromCell(List<OpenExit> openExits, Cell cell, Transform exceptExit, int depth)
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

                openExits.Add(new OpenExit(exitObject.transform, cell, depth));
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

        // 맵 생성 후 플레이어를 PlayerSpawnPoint 중 랜덤 위치에 배치한다.
        private void SpawnPlayersAfterGeneratedMap()
        {
            if (!SpawnPlayersAfterGeneration)
            {
                return;
            }

            if (TrySpawnLocalPhotonPlayer())
            {
                return;
            }

            int desiredSpawnCount = GetDesiredPlayerSpawnCount();

            if (desiredSpawnCount <= 0)
            {
                return;
            }

            List<SpawnPointRecord> spawnPoints = CollectSpawnPoints(PlayerSpawnPointPrefix, IncludeInactivePlayerSpawnPoints, PlayerSpawnOnlyInRooms);

            if (spawnPoints.Count <= 0)
            {
                Debug.LogWarning("[LaboratoryGenerator] PlayerSpawnPoint를 찾지 못함. 방 프리팹 안에 빈 오브젝트 이름을 PlayerSpawnPoint_01 같은 방식으로 만들어야 함.");
                return;
            }

            List<Transform> existingPlayers = CollectExistingPlayers();
            List<SpawnPointRecord> usedPoints = new List<SpawnPointRecord>();
            HashSet<Cell> usedRooms = new HashSet<Cell>();

            int spawnedCount = 0;

            for (int i = 0; i < desiredSpawnCount; i++)
            {
                SpawnPointRecord selectedPoint = SelectPlayerSpawnPoint(spawnPoints, usedPoints, usedRooms);

                if (selectedPoint == null)
                {
                    Debug.LogWarning("[LaboratoryGenerator] 플레이어를 배치할 수 있는 PlayerSpawnPoint가 부족함.");
                    break;
                }

                Transform playerTransform = GetOrCreatePlayerTransform(i, existingPlayers);

                if (playerTransform == null)
                {
                    Debug.LogWarning("[LaboratoryGenerator] 배치할 플레이어가 없고 PlayerPrefab도 비어 있음.");
                    break;
                }

                MovePlayerToSpawnPoint(playerTransform, selectedPoint.Point);

                usedPoints.Add(selectedPoint);
                usedRooms.Add(selectedPoint.OwnerCell);
                playerSpawnedCells.Add(selectedPoint.OwnerCell);
                playerSpawnedPositions.Add(playerTransform.position);
                spawnedCount++;
            }

            Debug.Log("[LaboratoryGenerator] Player spawn finished. Count: " + spawnedCount);
        }

        private bool TrySpawnLocalPhotonPlayer()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            {
                return false;
            }

            List<Transform> existingPlayers = CollectExistingPlayers();
            Transform localPlayerTransform = null;

            if (existingPlayers != null && existingPlayers.Count > 0)
            {
                localPlayerTransform = existingPlayers[0];
            }

            if (localPlayerTransform == null)
            {
                return false;
            }

            List<SpawnPointRecord> spawnPoints = CollectSpawnPoints(PlayerSpawnPointPrefix, IncludeInactivePlayerSpawnPoints, PlayerSpawnOnlyInRooms);

            if (spawnPoints.Count <= 0)
            {
                Debug.LogWarning("[LaboratoryGenerator] PlayerSpawnPoint를 찾지 못함.");
                return true;
            }

            int localPlayerIndex = GetPhotonLocalPlayerIndex();
            int playerCount = Mathf.Max(1, PhotonNetwork.PlayerList.Length);
            List<SpawnPointRecord> usedPoints = new List<SpawnPointRecord>();
            HashSet<Cell> usedRooms = new HashSet<Cell>();
            SpawnPointRecord selectedPoint = null;

            for (int i = 0; i < playerCount; i++)
            {
                SpawnPointRecord point = SelectPlayerSpawnPoint(spawnPoints, usedPoints, usedRooms);
                if (point == null)
                {
                    break;
                }

                usedPoints.Add(point);
                usedRooms.Add(point.OwnerCell);

                if (i == localPlayerIndex)
                {
                    selectedPoint = point;
                }
            }

            if (selectedPoint == null)
            {
                selectedPoint = SelectPlayerSpawnPoint(spawnPoints, usedPoints, usedRooms);
            }

            if (selectedPoint != null)
            {
                MovePlayerToSpawnPoint(localPlayerTransform, selectedPoint.Point);
                playerSpawnedCells.Add(selectedPoint.OwnerCell);
                playerSpawnedPositions.Add(localPlayerTransform.position);
            }

            Debug.Log("[LaboratoryGenerator] Photon local player spawn index: " + localPlayerIndex);
            return true;
        }

        private int GetPhotonLocalPlayerIndex()
        {
            Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && players[i].ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                {
                    return i;
                }
            }

            return 0;
        }

        // 맵 생성 후 아이템을 ItemSpawnPoint 중 랜덤 위치에 생성한다.
        private void SpawnItemsAfterGeneratedMap()
        {
            if (!SpawnItemsAfterGeneration)
            {
                return;
            }

            List<GameObject> validItemPrefabs = GetValidItemPrefabs();

            if (validItemPrefabs.Count <= 0)
            {
                Debug.LogWarning("[LaboratoryGenerator] ItemPrefabs가 비어 있어서 아이템을 생성하지 않음.");
                return;
            }

            if (ItemSpawnCount <= 0)
            {
                return;
            }

            List<SpawnPointRecord> spawnPoints = CollectSpawnPoints(ItemSpawnPointPrefix, IncludeInactiveItemSpawnPoints, ItemSpawnOnlyInRooms);

            if (spawnPoints.Count <= 0)
            {
                Debug.LogWarning("[LaboratoryGenerator] ItemSpawnPoint를 찾지 못함. 방 프리팹 안에 빈 오브젝트 이름을 ItemSpawnPoint_01 같은 방식으로 만들어야 함.");
                return;
            }

            List<SpawnPointRecord> usedPoints = new List<SpawnPointRecord>();
            Dictionary<Cell, int> spawnedItemCountByRoom = new Dictionary<Cell, int>();
            int spawnedCount = 0;

            for (int i = 0; i < ItemSpawnCount; i++)
            {
                SpawnPointRecord selectedPoint = SelectItemSpawnPoint(spawnPoints, usedPoints, spawnedItemCountByRoom);

                if (selectedPoint == null)
                {
                    Debug.LogWarning("[LaboratoryGenerator] 아이템을 배치할 수 있는 ItemSpawnPoint가 부족함.");
                    break;
                }

                GameObject itemPrefab = validItemPrefabs[Random.Range(0, validItemPrefabs.Count)];
                Vector3 spawnPosition = selectedPoint.Point.position + ItemSpawnPositionOffset;
                Quaternion spawnRotation = AlignItemRotationToSpawnPoint ? selectedPoint.Point.rotation : Quaternion.identity;
                Transform parent = ItemParent != null ? ItemParent : transform;

                GameObject createdItem = Instantiate(itemPrefab, spawnPosition, spawnRotation, parent);

                SnapSpawnedItemToSpawnPointGround(createdItem, selectedPoint.Point.position, selectedPoint.OwnerCell);
                EnsureScanMeshColliders(createdItem);
                DisableRuntimeBoxColliders(createdItem, null);
                ApplyGeneratedItemVisualState(createdItem);

                usedPoints.Add(selectedPoint);
                IncreaseSpawnedItemCountForRoom(selectedPoint.OwnerCell, spawnedItemCountByRoom);
                spawnedCount++;
            }

            Debug.Log("[LaboratoryGenerator] Item spawn finished. Count: " + spawnedCount);
        }

        // 이름 접두어로 생성된 Cell 내부의 스폰 포인트를 수집한다.
        private List<SpawnPointRecord> CollectSpawnPoints(string pointNamePrefix, bool includeInactive, bool onlyRoomLike)
        {
            List<SpawnPointRecord> result = new List<SpawnPointRecord>();

            if (string.IsNullOrEmpty(pointNamePrefix))
            {
                return result;
            }

            for (int i = 0; i < generatedCells.Count; i++)
            {
                Cell cell = generatedCells[i];

                if (cell == null)
                {
                    continue;
                }

                if (onlyRoomLike && !cell.IsRoomLike)
                {
                    continue;
                }

                Transform[] children = cell.GetComponentsInChildren<Transform>(includeInactive);

                for (int j = 0; j < children.Length; j++)
                {
                    Transform child = children[j];

                    if (child == null)
                    {
                        continue;
                    }

                    if (child == cell.transform)
                    {
                        continue;
                    }

                    if (child.name.StartsWith(pointNamePrefix))
                    {
                        result.Add(new SpawnPointRecord(child, cell));
                    }
                }
            }

            return result;
        }

        // 이번 실행에서 실제로 몇 명의 플레이어를 배치할지 계산한다.
        private int GetDesiredPlayerSpawnCount()
        {
            if (PlayerSpawnCount > 0)
            {
                return PlayerSpawnCount;
            }

            int existingCount = CountConfiguredExistingPlayers();

            if (existingCount > 0)
            {
                return existingCount;
            }

            if (AutoFindExistingPlayerByTag && HasPlayerTaggedObject())
            {
                return 1;
            }

            if (PlayerPrefab != null)
            {
                return Mathf.Max(1, PlayerCount);
            }

            return 0;
        }

        // 인스펙터에 등록된 기존 플레이어 수를 센다.
        private int CountConfiguredExistingPlayers()
        {
            int count = 0;
            HashSet<Transform> uniquePlayers = new HashSet<Transform>();

            if (ExistingPlayer != null)
            {
                uniquePlayers.Add(ExistingPlayer);
            }

            if (ExistingPlayers != null)
            {
                for (int i = 0; i < ExistingPlayers.Length; i++)
                {
                    if (ExistingPlayers[i] != null)
                    {
                        uniquePlayers.Add(ExistingPlayers[i]);
                    }
                }
            }

            foreach (Transform player in uniquePlayers)
            {
                if (player != null)
                {
                    count++;
                }
            }

            return count;
        }

        // Player 태그 오브젝트가 있는지 안전하게 검사한다.
        private bool HasPlayerTaggedObject()
        {
            try
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                return playerObject != null;
            }
            catch
            {
                return false;
            }
        }

        // 기존 플레이어들을 수집한다.
        private List<Transform> CollectExistingPlayers()
        {
            List<Transform> result = new List<Transform>();
            HashSet<Transform> uniquePlayers = new HashSet<Transform>();

            if (ExistingPlayer != null)
            {
                uniquePlayers.Add(ExistingPlayer);
            }

            if (ExistingPlayers != null)
            {
                for (int i = 0; i < ExistingPlayers.Length; i++)
                {
                    if (ExistingPlayers[i] != null)
                    {
                        uniquePlayers.Add(ExistingPlayers[i]);
                    }
                }
            }

            if (AutoFindExistingPlayerByTag)
            {
                try
                {
                    GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");

                    for (int i = 0; i < taggedPlayers.Length; i++)
                    {
                        if (taggedPlayers[i] != null)
                        {
                            uniquePlayers.Add(taggedPlayers[i].transform);
                        }
                    }
                }
                catch
                {
                    // Player 태그가 프로젝트에 없으면 Unity가 예외를 던질 수 있으므로 무시한다.
                }
            }

            foreach (Transform player in uniquePlayers)
            {
                if (player != null)
                {
                    result.Add(player);
                }
            }

            return result;
        }

        // 기존 플레이어를 가져오거나 부족하면 프리팹으로 새로 생성한다.
        private Transform GetOrCreatePlayerTransform(int index, List<Transform> existingPlayers)
        {
            if (existingPlayers != null && index >= 0 && index < existingPlayers.Count)
            {
                return existingPlayers[index];
            }

            if (PlayerPrefab == null)
            {
                return null;
            }

            Transform parent = PlayerParent != null ? PlayerParent : null;
            GameObject playerObject = Instantiate(PlayerPrefab, Vector3.zero, Quaternion.identity, parent);
            playerObject.name = PlayerPrefab.name + "_" + (index + 1);
            return playerObject.transform;
        }

        // 플레이어 스폰 포인트 하나를 고른다.
        private SpawnPointRecord SelectPlayerSpawnPoint(List<SpawnPointRecord> allPoints, List<SpawnPointRecord> usedPoints, HashSet<Cell> usedRooms)
        {
            List<SpawnPointRecord> candidates = new List<SpawnPointRecord>();

            AddPlayerSpawnCandidates(allPoints, usedPoints, usedRooms, candidates, true, true);

            if (candidates.Count <= 0)
            {
                AddPlayerSpawnCandidates(allPoints, usedPoints, usedRooms, candidates, false, true);
            }

            if (candidates.Count <= 0)
            {
                AddPlayerSpawnCandidates(allPoints, usedPoints, usedRooms, candidates, false, false);
            }

            if (candidates.Count <= 0)
            {
                return null;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        // 플레이어 후보 목록을 조건에 맞춰 추가한다.
        private void AddPlayerSpawnCandidates(List<SpawnPointRecord> allPoints, List<SpawnPointRecord> usedPoints, HashSet<Cell> usedRooms, List<SpawnPointRecord> candidates, bool useStrictRules, bool keepOnePlayerPerRoom)
        {
            for (int i = 0; i < allPoints.Count; i++)
            {
                SpawnPointRecord point = allPoints[i];

                if (point == null || point.Point == null || point.OwnerCell == null)
                {
                    continue;
                }

                if (usedPoints.Contains(point))
                {
                    continue;
                }

                if (keepOnePlayerPerRoom && OnePlayerPerRoom && usedRooms.Contains(point.OwnerCell))
                {
                    continue;
                }

                if (useStrictRules && AvoidExitRoomForPlayerSpawn && hasExitDoorPosition && point.OwnerCell == exitDoorOwnerCell)
                {
                    continue;
                }

                candidates.Add(point);
            }
        }

        // 실제 플레이어 Transform을 스폰 포인트로 이동시킨다.
        private void MovePlayerToSpawnPoint(Transform playerTransform, Transform spawnPoint)
        {
            if (playerTransform == null || spawnPoint == null)
            {
                return;
            }

            CharacterController characterController = playerTransform.GetComponent<CharacterController>();

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            playerTransform.position = spawnPoint.position + PlayerSpawnPositionOffset;

            if (RandomizePlayerSpawnYaw)
            {
                playerTransform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            else if (AlignPlayerRotationToSpawnPoint)
            {
                playerTransform.rotation = spawnPoint.rotation;
            }

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }

        // 유효한 아이템 프리팹만 수집한다.
        private List<GameObject> GetValidItemPrefabs()
        {
            List<GameObject> result = new List<GameObject>();

            if (ItemPrefabs == null)
            {
                return result;
            }

            for (int i = 0; i < ItemPrefabs.Length; i++)
            {
                if (ItemPrefabs[i] != null)
                {
                    result.Add(ItemPrefabs[i]);
                }
            }

            return result;
        }

        // 아이템 스폰 포인트 하나를 고른다.
        private SpawnPointRecord SelectItemSpawnPoint(List<SpawnPointRecord> allPoints, List<SpawnPointRecord> usedPoints, Dictionary<Cell, int> spawnedItemCountByRoom)
        {
            List<SpawnPointRecord> candidates = new List<SpawnPointRecord>();

            AddItemSpawnCandidates(allPoints, usedPoints, spawnedItemCountByRoom, candidates, true);

            if (candidates.Count <= 0)
            {
                AddItemSpawnCandidates(allPoints, usedPoints, spawnedItemCountByRoom, candidates, false);
            }

            if (candidates.Count <= 0)
            {
                return null;
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        // 아이템 후보 목록을 조건에 맞춰 추가한다.
        private void AddItemSpawnCandidates(List<SpawnPointRecord> allPoints, List<SpawnPointRecord> usedPoints, Dictionary<Cell, int> spawnedItemCountByRoom, List<SpawnPointRecord> candidates, bool useStrictRules)
        {
            for (int i = 0; i < allPoints.Count; i++)
            {
                SpawnPointRecord point = allPoints[i];

                if (point == null || point.Point == null || point.OwnerCell == null)
                {
                    continue;
                }

                if (PreventDuplicateItemSpawnPointUse && usedPoints.Contains(point))
                {
                    continue;
                }

                // 한 방에 3개 이상 아이템이 생성되지 않게 방별 개수를 항상 검사한다.
                if (!CanSpawnMoreItemsInRoom(point.OwnerCell, spawnedItemCountByRoom))
                {
                    continue;
                }

                if (useStrictRules && !AllowItemSpawnInPlayerRooms && playerSpawnedCells.Contains(point.OwnerCell))
                {
                    continue;
                }

                if (useStrictRules && IsTooCloseToPlayerSpawn(point.Point.position))
                {
                    continue;
                }

                candidates.Add(point);
            }
        }

        // 방 하나에 아이템을 더 생성할 수 있는지 확인한다.
        private bool CanSpawnMoreItemsInRoom(Cell ownerCell, Dictionary<Cell, int> spawnedItemCountByRoom)
        {
            if (ownerCell == null)
            {
                return false;
            }

            int maxItems = Mathf.Max(1, MaxItemsPerRoom);
            int currentCount = 0;

            if (spawnedItemCountByRoom != null)
            {
                spawnedItemCountByRoom.TryGetValue(ownerCell, out currentCount);
            }

            return currentCount < maxItems;
        }

        // 방별 생성 아이템 수를 증가시킨다.
        private void IncreaseSpawnedItemCountForRoom(Cell ownerCell, Dictionary<Cell, int> spawnedItemCountByRoom)
        {
            if (ownerCell == null || spawnedItemCountByRoom == null)
            {
                return;
            }

            int currentCount = 0;
            spawnedItemCountByRoom.TryGetValue(ownerCell, out currentCount);
            spawnedItemCountByRoom[ownerCell] = currentCount + 1;
        }

        // 바닥 Collider 없이 생성된 Cell의 층 높이를 바닥으로 보고 아이템을 붙인다.
        private void SnapSpawnedItemToSpawnPointGround(GameObject itemObject, Vector3 spawnPointPosition, Cell ownerCell)
        {
            if (!SnapSpawnedItemsToSpawnPointGround || itemObject == null)
            {
                return;
            }

            Bounds itemBounds;

            if (!TryGetCombinedItemBounds(itemObject, out itemBounds))
            {
                return;
            }

            float targetBottomY = ResolveSpawnedItemGroundY(spawnPointPosition, ownerCell) + Mathf.Max(0f, ItemGroundOffset);
            float deltaY = targetBottomY - itemBounds.min.y;

            if (Mathf.Abs(deltaY) <= 0.001f)
            {
                return;
            }

            itemObject.transform.position += Vector3.up * deltaY;
        }

        private void ApplyPhotonRoomSeed()
        {
            hasPhotonMapSeed = false;

            if (!UsePhotonRoomSeed || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            if (PhotonNetwork.CurrentRoom.CustomProperties == null ||
                !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("mapSeed", out object value))
            {
                return;
            }

            int seed;
            if (value is int intSeed)
            {
                seed = intSeed;
            }
            else if (!int.TryParse(value.ToString(), out seed))
            {
                return;
            }

            Random.InitState(seed);
            photonMapSeed = seed;
            hasPhotonMapSeed = true;
            Debug.Log("[LaboratoryGenerator] Photon map seed applied: " + seed);
        }

        private void ApplyPhotonStageSeed(int salt, string stageName)
        {
            if (!hasPhotonMapSeed)
            {
                return;
            }

            int stageSeed = unchecked(photonMapSeed + salt);
            Random.InitState(stageSeed);
            Debug.Log("[LaboratoryGenerator] Photon " + stageName + " seed applied: " + stageSeed);
        }

        // ItemSpawnPoint가 선반/가구 높이에 있어도 실제로 보이는 지지대가 없으므로 Cell의 층 바닥 높이를 우선 사용한다.
        private float ResolveSpawnedItemGroundY(Vector3 spawnPointPosition, Cell ownerCell)
        {
            if (ownerCell == null)
            {
                return spawnPointPosition.y;
            }

            Transform cellTransform = ownerCell.transform;

            if (cellTransform == null)
            {
                return spawnPointPosition.y;
            }

            Vector3 localPoint = cellTransform.InverseTransformPoint(spawnPointPosition);
            float localFloorY = GetLocalFloorYAtPoint(localPoint.y);
            Vector3 worldFloorPoint = cellTransform.TransformPoint(new Vector3(localPoint.x, localFloorY, localPoint.z));

            return worldFloorPoint.y + ItemSpawnPositionOffset.y;
        }

        // 현재 로컬 높이보다 위에 있는 장식용 스폰 높이는 무시하고, 아래쪽에 있는 가장 가까운 층 바닥을 고른다.
        private float GetLocalFloorYAtPoint(float localY)
        {
            float safeFloorHeight = Mathf.Max(0.01f, FloorHeight);

            if (localY < safeFloorHeight)
            {
                return 0f;
            }

            return Mathf.Floor(localY / safeFloorHeight) * safeFloorHeight;
        }

        // 아이템의 전체 Bounds를 구한다. Collider를 우선 사용하고, 없으면 Renderer를 사용한다.
        private bool TryGetCombinedItemBounds(GameObject itemObject, out Bounds combinedBounds)
        {
            combinedBounds = new Bounds(itemObject != null ? itemObject.transform.position : Vector3.zero, Vector3.zero);

            if (itemObject == null)
            {
                return false;
            }

            bool hasBounds = false;
            Collider[] colliders = itemObject.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider currentCollider = colliders[i];

                if (currentCollider == null || !currentCollider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = currentCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(currentCollider.bounds);
                }
            }

            if (hasBounds)
            {
                return true;
            }

            Renderer[] renderers = itemObject.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer currentRenderer = renderers[i];

                if (currentRenderer == null || !currentRenderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = currentRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(currentRenderer.bounds);
                }
            }

            return hasBounds;
        }

        // 생성된 아이템의 Renderer만 끈다. Collider, WorldItemPickup, ScanSurfaceInfo는 건드리지 않는다.
        private void ApplyGeneratedItemVisualState(GameObject itemObject)
        {
            if (!HideSpawnedItemRenderers || itemObject == null)
            {
                return;
            }

            Renderer[] renderers = itemObject.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                renderers[i].enabled = false;
            }
        }

        private void EnsureScanMeshColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];

                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                if (!meshFilter.sharedMesh.isReadable)
                {
                    continue;
                }

                if (IsGeneratedHelperObject(meshFilter.transform))
                {
                    continue;
                }

                Renderer renderer = meshFilter.GetComponent<Renderer>();

                if (renderer == null)
                {
                    continue;
                }

                MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();

                if (meshCollider == null)
                {
                    meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                }

                meshCollider.sharedMesh = meshFilter.sharedMesh;
                Rigidbody attachedRigidbody = meshFilter.GetComponentInParent<Rigidbody>();
                meshCollider.convex = attachedRigidbody != null && !attachedRigidbody.isKinematic;
                meshCollider.isTrigger = false;
                meshCollider.enabled = true;

                CopyScanSurfaceInfoFromParents(meshFilter.gameObject);
            }
        }

        private void DisableRuntimeBoxColliders(GameObject root, BoxCollider colliderToKeep)
        {
            if (!UseMeshCollidersInsteadOfBoxColliders || root == null)
            {
                return;
            }

            BoxCollider[] boxColliders = root.GetComponentsInChildren<BoxCollider>(true);

            for (int i = 0; i < boxColliders.Length; i++)
            {
                BoxCollider boxCollider = boxColliders[i];

                if (boxCollider == null || boxCollider == colliderToKeep)
                {
                    continue;
                }

                if (IsGeneratedHelperObject(boxCollider.transform))
                {
                    boxCollider.enabled = false;
                    continue;
                }

                if (boxCollider.isTrigger)
                {
                    continue;
                }

                if (!HasUsableMeshColliderNearby(boxCollider.transform, root.transform))
                {
                    continue;
                }

                boxCollider.enabled = false;
            }
        }

        private bool HasUsableMeshColliderNearby(Transform target, Transform root)
        {
            if (target == null)
            {
                return false;
            }

            MeshCollider ownMeshCollider = target.GetComponent<MeshCollider>();

            if (ownMeshCollider != null && ownMeshCollider.enabled && ownMeshCollider.sharedMesh != null)
            {
                return true;
            }

            MeshCollider[] childMeshColliders = target.GetComponentsInChildren<MeshCollider>(true);

            for (int i = 0; i < childMeshColliders.Length; i++)
            {
                MeshCollider meshCollider = childMeshColliders[i];

                if (meshCollider != null && meshCollider.enabled && meshCollider.sharedMesh != null)
                {
                    return true;
                }
            }

            Transform current = target.parent;

            while (current != null)
            {
                MeshCollider parentMeshCollider = current.GetComponent<MeshCollider>();

                if (parentMeshCollider != null && parentMeshCollider.enabled && parentMeshCollider.sharedMesh != null)
                {
                    return true;
                }

                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return false;
        }

        private void DisableGeneratedHelperColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider = colliders[i];

                if (targetCollider == null)
                {
                    continue;
                }

                if (IsGeneratedHelperObject(targetCollider.transform))
                {
                    targetCollider.enabled = false;
                }
            }
        }

        private bool IsGeneratedHelperObject(Transform target)
        {
            Transform current = target;

            while (current != null && current != transform)
            {
                string objectName = current.name;

                if (ContainsIgnoreCase(objectName, PlayerSpawnPointPrefix) ||
                    ContainsIgnoreCase(objectName, ItemSpawnPointPrefix) ||
                    ContainsIgnoreCase(objectName, "DoorPoint") ||
                    ContainsIgnoreCase(objectName, "TempPortal"))
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
                   !string.IsNullOrEmpty(value) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CopyScanSurfaceInfoFromParents(GameObject target)
        {
            if (target == null || target.GetComponent<ScanSurfaceInfo>() != null)
            {
                return;
            }

            ScanSurfaceInfo parentInfo = target.GetComponentInParent<ScanSurfaceInfo>();

            if (parentInfo == null)
            {
                return;
            }

            ScanSurfaceInfo copiedInfo = target.AddComponent<ScanSurfaceInfo>();
            copiedInfo.surfaceType = parentInfo.surfaceType;
        }

        // 아이템 위치가 플레이어 시작 위치와 너무 가까운지 검사한다.
        private bool IsTooCloseToPlayerSpawn(Vector3 itemPosition)
        {
            if (MinItemDistanceFromPlayerSpawn <= 0f)
            {
                return false;
            }

            for (int i = 0; i < playerSpawnedPositions.Count; i++)
            {
                float distance = Vector3.Distance(itemPosition, playerSpawnedPositions[i]);

                if (distance < MinItemDistanceFromPlayerSpawn)
                {
                    return true;
                }
            }

            return false;
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
                InstantiateDoorPrefab(prefab, exit, ConnectedDoorPositionOffset, ConnectedDoorRotationOffset);
            }
        }

        // 막는 문은 빨간 바닥/TempPortal의 회전 전체를 쓰면 바닥처럼 누워버릴 수 있다.
        // 그래서 Y축 방향만 사용하고, 높이는 월드 Y 기준으로 올려서 세운다.
        private void GetBlockDoorPose(Transform exit, out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            Quaternion yawOnlyRotation = Quaternion.Euler(0f, exit.eulerAngles.y, 0f);

            Vector3 horizontalOffset = yawOnlyRotation * new Vector3(BlockDoorPositionOffset.x, 0f, BlockDoorPositionOffset.z);
            Vector3 verticalOffset = Vector3.up * BlockDoorPositionOffset.y;

            spawnPosition = exit.position + horizontalOffset + verticalOffset;
            spawnRotation = yawOnlyRotation * Quaternion.Euler(BlockDoorRotationOffset);
        }

        // 남은 빨간 출구를 InsteadDoor 하나로 막을 때 사용하는 전용 생성 함수다.
        private GameObject InstantiateBlockDoorPrefab(GameObject prefab, Transform exit)
        {
            if (prefab == null || exit == null)
            {
                return null;
            }

            GetBlockDoorPose(exit, out Vector3 spawnPosition, out Quaternion spawnRotation);

            GameObject createdDoor = Instantiate(prefab, spawnPosition, spawnRotation, transform);
            ApplyGeneratedLightOptimization(createdDoor);
            return createdDoor;
        }

        // DoorPoint 기준으로 문/막힌 벽 프리팹을 생성한다.
        private GameObject InstantiateDoorPrefab(GameObject prefab, Transform exit, Vector3 localPositionOffset, Vector3 rotationOffset)
        {
            if (prefab == null || exit == null)
            {
                return null;
            }

            Vector3 spawnPosition = exit.TransformPoint(localPositionOffset);
            Quaternion spawnRotation = exit.rotation * Quaternion.Euler(rotationOffset);

            GameObject createdDoor = Instantiate(prefab, spawnPosition, spawnRotation, transform);
            ApplyGeneratedLightOptimization(createdDoor);
            return createdDoor;
        }

        // 생성된 오브젝트 안에 들어있는 Light 그림자를 꺼서 URP shadow atlas 경고를 막는다.
        private void ApplyGeneratedLightOptimization(GameObject root)
        {
            if (!OptimizeGeneratedLightShadows || root == null)
            {
                return;
            }

            Light[] lights = root.GetComponentsInChildren<Light>(true);

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null)
                {
                    continue;
                }

                if (DisableGeneratedLightShadows)
                {
                    lights[i].shadows = LightShadows.None;
                }
            }
        }

        // 막는 문 프리팹이 비어있을 때 임시 검은 벽 큐브를 생성한다.
        private GameObject CreateFallbackBlockDoor(Transform exit)
        {
            if (!CreateFallbackBlockDoorWhenPrefabMissing || exit == null)
            {
                return null;
            }

            GetBlockDoorPose(exit, out Vector3 spawnPosition, out Quaternion spawnRotation);

            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Generated_BlockDoor_Fallback";
            blocker.transform.SetParent(transform, true);
            blocker.transform.position = spawnPosition;
            blocker.transform.rotation = spawnRotation;
            blocker.transform.localScale = FallbackBlockDoorLocalScale;

            Renderer renderer = blocker.GetComponent<Renderer>();
            if (renderer != null && FallbackBlockDoorMaterial != null)
            {
                renderer.sharedMaterial = FallbackBlockDoorMaterial;
            }

            ApplyGeneratedLightOptimization(blocker);
            return blocker;
        }

        // 생성된 맵의 Renderer를 꺼서 인게임에서 맵 본체가 직접 보이지 않게 한다.
        private void ApplyRuntimeVisualState()
        {
            if (!HideGeneratedVisualsInGame)
            {
                return;
            }

            if (HideGeneratedVisualsOnlyInPlayMode && !Application.isPlaying)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                renderers[i].enabled = false;
            }
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
            HashSet<Transform> blockedExits = new HashSet<Transform>();

            for (int cellIndex = 0; cellIndex < generatedCells.Count; cellIndex++)
            {
                Cell cell = generatedCells[cellIndex];

                if (cell == null || cell.Exits == null)
                {
                    continue;
                }

                for (int exitIndex = 0; exitIndex < cell.Exits.Length; exitIndex++)
                {
                    GameObject exitObject = cell.Exits[exitIndex];

                    if (exitObject == null)
                    {
                        continue;
                    }

                    BlockSingleRemainingExit(exitObject.transform, blockedExits);
                }
            }

            BlockExtraRemainingExitObjectsByName(blockedExits);
        }

        // 출구 하나를 막는 문으로 바꾸고 기존 출구 표시 오브젝트를 삭제한다.
        private void BlockSingleRemainingExit(Transform exitTransform, HashSet<Transform> blockedExits)
        {
            if (exitTransform == null)
            {
                return;
            }

            if (blockedExits != null && blockedExits.Contains(exitTransform))
            {
                return;
            }

            if (IsExitConsumed(exitTransform))
            {
                if (blockedExits != null)
                {
                    blockedExits.Add(exitTransform);
                }

                return;
            }

            if (blockedExits != null)
            {
                blockedExits.Add(exitTransform);
            }

            // activeInHierarchy를 검사하지 않는다.
            // 프리팹에서 DoorPoint가 비활성화되어 있어도 남은 출구라면 무조건 막아야 한다.
            if (InsteadDoor != null)
            {
                InstantiateBlockDoorPrefab(InsteadDoor, exitTransform);
            }
            else
            {
                CreateFallbackBlockDoor(exitTransform);
            }

            DestroyExitObject(exitTransform);
        }

        // Cell.Exits 배열에 빠져있는 TempPortal/DoorPoint 같은 남은 출구도 찾아서 막는다.
        private void BlockExtraRemainingExitObjectsByName(HashSet<Transform> blockedExits)
        {
            if (!BlockExtraExitObjectsByName || ExtraExitObjectNameKeywords == null || ExtraExitObjectNameKeywords.Length == 0)
            {
                return;
            }

            for (int cellIndex = 0; cellIndex < generatedCells.Count; cellIndex++)
            {
                Cell cell = generatedCells[cellIndex];

                if (cell == null)
                {
                    continue;
                }

                Transform[] children = cell.GetComponentsInChildren<Transform>(true);

                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    Transform child = children[childIndex];

                    if (child == null || child == cell.transform)
                    {
                        continue;
                    }

                    if (blockedExits != null && blockedExits.Contains(child))
                    {
                        continue;
                    }

                    if (!IsExtraExitObjectName(child.name))
                    {
                        continue;
                    }

                    BlockSingleRemainingExit(child, blockedExits);
                }
            }
        }

        // 이름이 남은 출구 오브젝트 키워드에 해당하는지 확인한다.
        private bool IsExtraExitObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || ExtraExitObjectNameKeywords == null)
            {
                return false;
            }

            for (int i = 0; i < ExtraExitObjectNameKeywords.Length; i++)
            {
                string keyword = ExtraExitObjectNameKeywords[i];

                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                if (objectName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        // 연결, 시작방 연결, 탈출구 생성에 이미 사용한 출구를 기록한다.
        private void MarkExitAsConsumed(Transform exitTransform)
        {
            if (exitTransform == null)
            {
                return;
            }

            AddConsumedTransformWithChildren(exitTransform);

            Transform parent = exitTransform.parent;

            // DoorPoint와 TempPortal이 부모/자식 구조로 섞여 있는 프리팹을 대비한다.
            if (parent != null && parent != transform && IsExtraExitObjectName(parent.name))
            {
                AddConsumedTransformWithChildren(parent);
            }
        }

        // 대상 Transform과 그 자식들을 사용 완료 목록에 넣는다.
        private void AddConsumedTransformWithChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                {
                    consumedExitTransforms.Add(children[i]);
                }
            }
        }

        // 이 출구가 이미 연결/탈출구 처리에 사용된 출구인지 확인한다.
        private bool IsExitConsumed(Transform exitTransform)
        {
            Transform current = exitTransform;

            while (current != null)
            {
                if (consumedExitTransforms.Contains(current))
                {
                    return true;
                }

                if (current == transform)
                {
                    break;
                }

                current = current.parent;
            }

            return false;
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
        private void DestroyExitObject(Transform exit)
        {
            if (exit == null)
            {
                return;
            }

            MarkExitAsConsumed(exit);

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
            consumedExitTransforms.Clear();
            startRoomPositions.Clear();
            spawnedCountByPrefabName.Clear();
            playerSpawnedCells.Clear();
            playerSpawnedPositions.Clear();
            exitDoorOwnerCell = null;
            exitDoorPosition = Vector3.zero;
            hasExitDoorPosition = false;
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
