using System.Collections.Generic;
using UnityEngine;

// 시설형 절차적 맵 생성기이다.
// 시작방 여러 개, 1층/2층 소켓, 방 직접 연결, 복도 브릿지, 탈출구/아이템 배치를 처리한다.
public class ProceduralFacilityGenerator : MonoBehaviour
{
    [Header("Generate")]
    // 게임 시작 시 자동 생성할지 정한다.
    [SerializeField] private bool generateOnStart = true;

    // 생성 전 이전 결과물을 삭제할지 정한다.
    [SerializeField] private bool clearBeforeGenerate = true;

    [Header("Seed")]
    // 고정 시드를 사용할지 정한다.
    [SerializeField] private bool useFixedSeed = true;

    // 고정 시드 값이다.
    [SerializeField] private int fixedSeed = 12345;

    // 마지막 생성에 사용된 시드이다.
    [SerializeField] private int lastGeneratedSeed = 0;

    [Header("Player / Start Rooms")]
    // 플레이어 수이다. 이 수만큼 시작방을 만든다.
    [SerializeField] private int playerCount = 4;

    // 시작방이 배치될 원형 반지름이다.
    [SerializeField] private float startRoomPlacementRadius = 28f;

    // 시작방끼리 최소 거리이다.
    [SerializeField] private float minStartRoomDistance = 12f;

    [Header("Room Count")]
    // 시작방과 복도까지 포함한 목표 룸 개수이다.
    [SerializeField] private int targetRoomCount = 24;

    // 한 방 배치 시 최대 시도 횟수이다.
    [SerializeField] private int maxPlacementAttemptsPerStep = 50;

    [Header("Prefabs - Start")]
    // 시작방 프리팹 목록이다.
    [SerializeField] private FacilityRoom[] startRoomPrefabs;

    [Header("Prefabs - Rooms")]
    // 작은방 프리팹 목록이다.
    [SerializeField] private FacilityRoom[] smallRoomPrefabs;

    // 일반방 프리팹 목록이다.
    [SerializeField] private FacilityRoom[] normalRoomPrefabs;

    // 큰방 프리팹 목록이다.
    [SerializeField] private FacilityRoom[] largeRoomPrefabs;

    // 특수방 프리팹 목록이다.
    [SerializeField] private FacilityRoom[] specialRoomPrefabs;

    [Header("Prefabs - Corridor / Stair")]
    // 복도 프리팹 목록이다.
    [SerializeField] private FacilityRoom[] corridorPrefabs;

    // 계단 또는 2층 연결 방 프리팹 목록이다.
    [SerializeField] private FacilityRoom[] stairPrefabs;

    [Header("Door Prefabs")]
    // 남은 소켓을 막을 문 프리팹이다.
    [SerializeField] private GameObject blockedDoorPrefab;

    // 탈출구 문 프리팹이다.
    [SerializeField] private GameObject exitDoorPrefab;

    [Header("Item Prefabs")]
    // 탈출 아이템 프리팹 목록이다.
    [SerializeField] private GameObject[] escapeItemPrefabs;

    // 생성할 탈출 아이템 개수이다.
    [SerializeField] private int escapeItemCount = 4;

    [Header("Connection Rules")]
    // 방끼리 직접 연결할 확률이다.
    [Range(0f, 1f)]
    [SerializeField] private float directRoomConnectionChance = 0.45f;

    // 계단/2층 연결 방을 뽑을 확률이다.
    [Range(0f, 1f)]
    [SerializeField] private float stairRoomChance = 0.12f;

    [Header("Bridge Connections")]
    // 서로 다른 가지를 복도로 이어줄 시도 개수이다.
    [SerializeField] private int bridgeConnectionCount = 3;

    // 브릿지 연결을 시도할 최대 소켓 거리이다.
    [SerializeField] private float maxBridgeSocketDistance = 16f;

    // 브릿지 복도 끝 소켓과 대상 소켓이 이 거리 이내면 연결 성공으로 본다.
    [SerializeField] private float bridgeSocketSnapTolerance = 0.55f;

    [Header("Exit / Escape Item Rules")]
    // 탈출 아이템이 시작방에서 최소 몇 단계 이상 떨어져야 하는지 정한다.
    [SerializeField] private int minEscapeItemDepthFromStart = 3;

    // 탈출 아이템이 시작방에서 최소 몇 미터 이상 떨어져야 하는지 정한다.
    [SerializeField] private float minEscapeItemDistanceFromStart = 15f;

    [Header("Overlap")]
    // Bounds 검사 시 살짝 줄일 값이다.
    [SerializeField] private float boundsShrinkAmount = 0.35f;

    // 문 연결부에서 허용할 얇은 겹침 깊이이다.
    [SerializeField] private float maxAllowedConnectionOverlapDepth = 1.0f;

    [Header("Scan / Visibility")]
    // 생성된 맵에 RevealSurface 레이어를 자동 적용할지 정한다.
    [SerializeField] private bool applyScanLayerAutomatically = true;

    // 스캔 대상 레이어 이름이다.
    [SerializeField] private string scanLayerName = "RevealSurface";

    // 생성 후 Renderer를 숨길지 정한다.
    [SerializeField] private bool hideRenderersAfterGenerate = false;

    [Header("Hierarchy")]
    // 생성된 맵을 담는 루트이다.
    [SerializeField] private Transform generatedRoot;

    [Header("Debug")]
    // 배치 실패 로그를 출력할지 정한다.
    [SerializeField] private bool debugPlacementFailures = true;

    // 생성된 방 목록이다.
    private readonly List<FacilityRoom> generatedRooms = new List<FacilityRoom>();

    // 시작방 목록이다.
    private readonly List<FacilityRoom> startRooms = new List<FacilityRoom>();

    // 아직 열린 소켓 목록이다.
    private readonly List<FacilitySocket> openSockets = new List<FacilitySocket>();

    // 생성된 별도 오브젝트 목록이다.
    private readonly List<GameObject> generatedLooseObjects = new List<GameObject>();

    // 마지막 실패 이유이다.
    private string lastFailReason = "";

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Facility")]
    public void Generate()
    {
        ClampSettings();

        if (clearBeforeGenerate)
        {
            ClearGenerated();
        }

        EnsureGeneratedRoot();

        lastGeneratedSeed = useFixedSeed ? fixedSeed : Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(lastGeneratedSeed);

        bool startSuccess = GenerateStartRooms();

        if (!startSuccess)
        {
            Debug.LogError("ProceduralFacilityGenerator: failed to generate start rooms.");
            return;
        }

        ConnectStartRoomsWithCorridors();

        ExpandMainLayout();

        BuildBridgeConnections();

        PlaceExitDoor();

        PlaceEscapeItems();

        BlockRemainingSockets();

        ApplyRendererVisibility();

        Debug.Log("ProceduralFacilityGenerator: generated rooms = " + generatedRooms.Count + ", seed = " + lastGeneratedSeed);
    }

    [ContextMenu("Clear Facility")]
    public void ClearGenerated()
    {
        generatedRooms.Clear();
        startRooms.Clear();
        openSockets.Clear();
        generatedLooseObjects.Clear();

        if (generatedRoot == null)
        {
            Transform oldRoot = transform.Find("GeneratedFacility");

            if (oldRoot != null)
            {
                generatedRoot = oldRoot;
            }
        }

        if (generatedRoot != null)
        {
            GameObject rootObject = generatedRoot.gameObject;
            generatedRoot = null;
            DestroyObjectSafe(rootObject);
        }
    }

    private void ClampSettings()
    {
        playerCount = Mathf.Clamp(playerCount, 1, 12);
        targetRoomCount = Mathf.Max(playerCount, targetRoomCount);
        maxPlacementAttemptsPerStep = Mathf.Max(1, maxPlacementAttemptsPerStep);
        startRoomPlacementRadius = Mathf.Max(1f, startRoomPlacementRadius);
        minStartRoomDistance = Mathf.Max(0f, minStartRoomDistance);
        bridgeConnectionCount = Mathf.Max(0, bridgeConnectionCount);
        maxBridgeSocketDistance = Mathf.Max(1f, maxBridgeSocketDistance);
        bridgeSocketSnapTolerance = Mathf.Max(0.05f, bridgeSocketSnapTolerance);
        boundsShrinkAmount = Mathf.Max(0f, boundsShrinkAmount);
        maxAllowedConnectionOverlapDepth = Mathf.Max(0f, maxAllowedConnectionOverlapDepth);
        escapeItemCount = Mathf.Max(0, escapeItemCount);
    }

    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("GeneratedFacility");
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
    }

    private bool GenerateStartRooms()
    {
        if (startRoomPrefabs == null || startRoomPrefabs.Length == 0)
        {
            lastFailReason = "startRoomPrefabs is empty.";
            return false;
        }

        Vector3 center = transform.position;

        for (int i = 0; i < playerCount; i++)
        {
            FacilityRoom prefab = GetRandomRoomFromArray(startRoomPrefabs);

            if (prefab == null)
            {
                lastFailReason = "start room prefab is null.";
                return false;
            }

            float angle = (360f / playerCount) * i;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 position = center + direction * startRoomPlacementRadius;

            if (!IsFarEnoughFromOtherStartRooms(position))
            {
                position = FindAlternativeStartPosition(center, i);
            }

            FacilityRoom room = Instantiate(prefab, position, Quaternion.identity, generatedRoot);
            room.name = prefab.name + "_Generated_Start_" + (i + 1);
            room.InitializeRuntime();
            room.runtimeDepthFromStart = 0;
            room.runtimeNearestStartDistance = 0f;

            RotatePrimarySocketToward(room, center);

            Physics.SyncTransforms();

            if (IsRoomOverlapping(room, null, null))
            {
                DestroyObjectSafe(room.gameObject);
                lastFailReason = "start room overlap.";
                return false;
            }

            ApplyRoomSetup(room);

            generatedRooms.Add(room);
            startRooms.Add(room);
            AddAvailableSocketsFromRoom(room);
        }

        return true;
    }

    private Vector3 FindAlternativeStartPosition(Vector3 center, int index)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            float randomAngle = Random.Range(0f, 360f);
            float randomRadius = Random.Range(startRoomPlacementRadius * 0.85f, startRoomPlacementRadius * 1.25f);

            Vector3 direction = Quaternion.Euler(0f, randomAngle, 0f) * Vector3.forward;
            Vector3 candidate = center + direction * randomRadius;

            if (IsFarEnoughFromOtherStartRooms(candidate))
            {
                return candidate;
            }
        }

        float fallbackAngle = (360f / Mathf.Max(1, playerCount)) * index;
        Vector3 fallbackDirection = Quaternion.Euler(0f, fallbackAngle, 0f) * Vector3.forward;
        return center + fallbackDirection * startRoomPlacementRadius;
    }

    private bool IsFarEnoughFromOtherStartRooms(Vector3 position)
    {
        for (int i = 0; i < startRooms.Count; i++)
        {
            if (startRooms[i] == null)
            {
                continue;
            }

            float distance = Vector3.Distance(position, startRooms[i].transform.position);

            if (distance < minStartRoomDistance)
            {
                return false;
            }
        }

        return true;
    }

    private void RotatePrimarySocketToward(FacilityRoom room, Vector3 targetPosition)
    {
        if (room == null)
        {
            return;
        }

        room.RefreshCachedChildrenIfNeeded();

        if (room.sockets == null || room.sockets.Length == 0 || room.sockets[0] == null)
        {
            return;
        }

        FacilitySocket primarySocket = room.sockets[0];

        Vector3 direction = targetPosition - primarySocket.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion desiredSocketRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        Quaternion socketLocalRotation = Quaternion.Inverse(room.transform.rotation) * primarySocket.transform.rotation;
        Quaternion finalRoomRotation = desiredSocketRotation * Quaternion.Inverse(socketLocalRotation);

        room.transform.rotation = finalRoomRotation;
    }

    private void ConnectStartRoomsWithCorridors()
    {
        List<FacilitySocket> startSockets = new List<FacilitySocket>();

        for (int i = 0; i < openSockets.Count; i++)
        {
            FacilitySocket socket = openSockets[i];

            if (socket == null || !socket.IsAvailable)
            {
                continue;
            }

            FacilityRoom room = GetRoomFromSocket(socket);

            if (room != null && room.roomType == FacilityRoomType.StartRoom)
            {
                startSockets.Add(socket);
            }
        }

        for (int i = 0; i < startSockets.Count; i++)
        {
            FacilitySocket socket = startSockets[i];

            if (socket == null || !socket.IsAvailable)
            {
                continue;
            }

            FacilityRoom corridorPrefab = GetRandomRoomFromArray(corridorPrefabs);

            if (corridorPrefab == null)
            {
                continue;
            }

            TryPlaceSpecificPrefabAtSocket(socket, corridorPrefab);
        }
    }

    private void ExpandMainLayout()
    {
        int safety = targetRoomCount * maxPlacementAttemptsPerStep;

        while (generatedRooms.Count < targetRoomCount && safety > 0)
        {
            safety--;

            CleanupOpenSockets();

            if (openSockets.Count == 0)
            {
                break;
            }

            FacilitySocket sourceSocket = openSockets[Random.Range(0, openSockets.Count)];

            if (sourceSocket == null || !sourceSocket.IsAvailable)
            {
                continue;
            }

            bool placed = TryPlaceRandomRoomAtSocket(sourceSocket);

            if (!placed && debugPlacementFailures)
            {
                Debug.LogWarning("ProceduralFacilityGenerator: placement failed / " + lastFailReason);
            }
        }
    }

    private bool TryPlaceRandomRoomAtSocket(FacilitySocket sourceSocket)
    {
        FacilityRoom sourceRoom = GetRoomFromSocket(sourceSocket);

        if (sourceRoom == null)
        {
            lastFailReason = "source room is null.";
            return false;
        }

        for (int attempt = 0; attempt < maxPlacementAttemptsPerStep; attempt++)
        {
            FacilityRoom prefab = PickCandidatePrefabForSource(sourceRoom);

            if (prefab == null)
            {
                lastFailReason = "candidate prefab is null.";
                continue;
            }

            bool placed = TryPlaceSpecificPrefabAtSocket(sourceSocket, prefab);

            if (placed)
            {
                return true;
            }
        }

        return false;
    }

    private FacilityRoom PickCandidatePrefabForSource(FacilityRoom sourceRoom)
    {
        if (sourceRoom == null)
        {
            return null;
        }

        if (sourceRoom.roomType == FacilityRoomType.StartRoom)
        {
            return GetRandomRoomFromArray(corridorPrefabs);
        }

        float roll = Random.value;

        if (roll < stairRoomChance)
        {
            FacilityRoom stair = GetRandomRoomFromArray(stairPrefabs);

            if (stair != null)
            {
                return stair;
            }
        }

        bool chooseDirectRoom = roll < directRoomConnectionChance;

        if (sourceRoom.roomType == FacilityRoomType.Corridor)
        {
            chooseDirectRoom = true;
        }

        if (!chooseDirectRoom)
        {
            FacilityRoom corridor = GetRandomRoomFromArray(corridorPrefabs);

            if (corridor != null)
            {
                return corridor;
            }
        }

        return GetRandomWeightedNormalRoom();
    }

    private FacilityRoom GetRandomWeightedNormalRoom()
    {
        List<FacilityRoom> candidates = new List<FacilityRoom>();

        AddArrayToList(candidates, smallRoomPrefabs);
        AddArrayToList(candidates, normalRoomPrefabs);
        AddArrayToList(candidates, normalRoomPrefabs);
        AddArrayToList(candidates, largeRoomPrefabs);
        AddArrayToList(candidates, specialRoomPrefabs);

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private bool TryPlaceSpecificPrefabAtSocket(FacilitySocket sourceSocket, FacilityRoom prefab)
    {
        if (sourceSocket == null || prefab == null)
        {
            return false;
        }

        FacilityRoom sourceRoom = GetRoomFromSocket(sourceSocket);

        if (sourceRoom == null)
        {
            return false;
        }

        FacilityRoom candidateRoom = Instantiate(prefab, Vector3.zero, Quaternion.identity, generatedRoot);
        candidateRoom.name = prefab.name + "_Generated";
        candidateRoom.InitializeRuntime();

        FacilitySocket candidateSocket = GetRandomCompatibleSocket(candidateRoom, sourceSocket);

        if (candidateSocket == null)
        {
            lastFailReason = "candidate socket not found. prefab = " + prefab.name;
            DestroyObjectSafe(candidateRoom.gameObject);
            return false;
        }

        AlignRoomToSocket(candidateRoom, candidateSocket, sourceSocket);

        Physics.SyncTransforms();

        if (!IsConnectionAllowed(sourceRoom, candidateRoom, sourceSocket, candidateSocket))
        {
            lastFailReason = "connection rule rejected. prefab = " + prefab.name;
            DestroyObjectSafe(candidateRoom.gameObject);
            return false;
        }

        if (IsRoomOverlapping(candidateRoom, sourceRoom, null))
        {
            lastFailReason = "overlap rejected. prefab = " + prefab.name;
            DestroyObjectSafe(candidateRoom.gameObject);
            return false;
        }

        sourceSocket.MarkConnected();
        candidateSocket.MarkConnected();
        openSockets.Remove(sourceSocket);

        candidateRoom.runtimeDepthFromStart = sourceRoom.runtimeDepthFromStart + 1;
        candidateRoom.runtimeNearestStartDistance = GetNearestStartDistance(candidateRoom.transform.position);

        ApplyRoomSetup(candidateRoom);

        generatedRooms.Add(candidateRoom);
        AddAvailableSocketsFromRoom(candidateRoom);

        return true;
    }

    private FacilitySocket GetRandomCompatibleSocket(FacilityRoom candidateRoom, FacilitySocket sourceSocket)
    {
        if (candidateRoom == null || sourceSocket == null)
        {
            return null;
        }

        List<FacilitySocket> compatibleSockets = candidateRoom.GetAvailableSocketsByFloor(sourceSocket.floorIndex);

        if (compatibleSockets.Count == 0)
        {
            return null;
        }

        return compatibleSockets[Random.Range(0, compatibleSockets.Count)];
    }

    private void AlignRoomToSocket(FacilityRoom candidateRoom, FacilitySocket candidateSocket, FacilitySocket sourceSocket)
    {
        Quaternion desiredSocketRotation = Quaternion.LookRotation(-sourceSocket.transform.forward, sourceSocket.transform.up);
        Quaternion candidateSocketLocalRotation = Quaternion.Inverse(candidateRoom.transform.rotation) * candidateSocket.transform.rotation;
        Quaternion finalRoomRotation = desiredSocketRotation * Quaternion.Inverse(candidateSocketLocalRotation);

        candidateRoom.transform.rotation = finalRoomRotation;

        Vector3 positionOffset = sourceSocket.transform.position - candidateSocket.transform.position;
        candidateRoom.transform.position += positionOffset;

        Physics.SyncTransforms();
    }

    private bool IsConnectionAllowed(FacilityRoom sourceRoom, FacilityRoom candidateRoom, FacilitySocket sourceSocket, FacilitySocket candidateSocket)
    {
        if (sourceRoom == null || candidateRoom == null || sourceSocket == null || candidateSocket == null)
        {
            return false;
        }

        if (sourceSocket.floorIndex != candidateSocket.floorIndex)
        {
            return false;
        }

        if (sourceRoom.roomType == FacilityRoomType.StartRoom && candidateRoom.roomType != FacilityRoomType.Corridor)
        {
            return false;
        }

        bool sourceIsRoom = sourceRoom.roomType != FacilityRoomType.Corridor && sourceRoom.roomType != FacilityRoomType.StartRoom;
        bool candidateIsRoom = candidateRoom.roomType != FacilityRoomType.Corridor && candidateRoom.roomType != FacilityRoomType.StartRoom;

        if (sourceIsRoom && candidateIsRoom)
        {
            if (!sourceSocket.canConnectRoom || !candidateSocket.canConnectRoom)
            {
                return false;
            }

            if (sourceRoom.roomSize == FacilityRoomSize.Large && candidateRoom.roomSize == FacilityRoomSize.Large)
            {
                return false;
            }
        }

        if (candidateRoom.roomType == FacilityRoomType.Corridor)
        {
            if (!sourceSocket.canConnectCorridor)
            {
                return false;
            }
        }

        return true;
    }

    private void BuildBridgeConnections()
    {
        for (int i = 0; i < bridgeConnectionCount; i++)
        {
            CleanupOpenSockets();

            bool madeBridge = TryBuildOneBridgeConnection();

            if (!madeBridge && debugPlacementFailures)
            {
                Debug.LogWarning("ProceduralFacilityGenerator: bridge failed / " + lastFailReason);
            }
        }
    }

    private bool TryBuildOneBridgeConnection()
    {
        if (corridorPrefabs == null || corridorPrefabs.Length == 0)
        {
            lastFailReason = "bridge corridor prefabs empty.";
            return false;
        }

        List<FacilitySocketPair> pairs = FindBridgeSocketPairs();

        if (pairs.Count == 0)
        {
            lastFailReason = "no bridge socket pairs.";
            return false;
        }

        FacilitySocketPair pair = pairs[Random.Range(0, pairs.Count)];

        for (int attempt = 0; attempt < maxPlacementAttemptsPerStep; attempt++)
        {
            FacilityRoom corridorPrefab = GetRandomRoomFromArray(corridorPrefabs);

            if (corridorPrefab == null)
            {
                continue;
            }

            bool success = TryPlaceBridgeCorridor(pair.socketA, pair.socketB, corridorPrefab);

            if (success)
            {
                return true;
            }
        }

        return false;
    }

    private struct FacilitySocketPair
    {
        public FacilitySocket socketA;
        public FacilitySocket socketB;
    }

    private List<FacilitySocketPair> FindBridgeSocketPairs()
    {
        List<FacilitySocketPair> result = new List<FacilitySocketPair>();

        for (int i = 0; i < openSockets.Count; i++)
        {
            FacilitySocket a = openSockets[i];

            if (a == null || !a.IsAvailable)
            {
                continue;
            }

            FacilityRoom roomA = GetRoomFromSocket(a);

            if (roomA == null)
            {
                continue;
            }

            for (int j = i + 1; j < openSockets.Count; j++)
            {
                FacilitySocket b = openSockets[j];

                if (b == null || !b.IsAvailable)
                {
                    continue;
                }

                FacilityRoom roomB = GetRoomFromSocket(b);

                if (roomB == null || roomA == roomB)
                {
                    continue;
                }

                if (a.floorIndex != b.floorIndex)
                {
                    continue;
                }

                float distance = Vector3.Distance(a.transform.position, b.transform.position);

                if (distance > maxBridgeSocketDistance)
                {
                    continue;
                }

                float facingDot = Vector3.Dot(a.transform.forward, b.transform.forward);

                if (facingDot > -0.35f)
                {
                    continue;
                }

                FacilitySocketPair pair = new FacilitySocketPair();
                pair.socketA = a;
                pair.socketB = b;
                result.Add(pair);
            }
        }

        return result;
    }

    private bool TryPlaceBridgeCorridor(FacilitySocket socketA, FacilitySocket socketB, FacilityRoom corridorPrefab)
    {
        if (socketA == null || socketB == null || corridorPrefab == null)
        {
            return false;
        }

        FacilityRoom roomA = GetRoomFromSocket(socketA);
        FacilityRoom roomB = GetRoomFromSocket(socketB);

        if (roomA == null || roomB == null)
        {
            return false;
        }

        FacilityRoom corridor = Instantiate(corridorPrefab, Vector3.zero, Quaternion.identity, generatedRoot);
        corridor.name = corridorPrefab.name + "_Generated_Bridge";
        corridor.InitializeRuntime();

        FacilitySocket corridorStartSocket = GetRandomCompatibleSocket(corridor, socketA);

        if (corridorStartSocket == null)
        {
            DestroyObjectSafe(corridor.gameObject);
            return false;
        }

        AlignRoomToSocket(corridor, corridorStartSocket, socketA);

        Physics.SyncTransforms();

        FacilitySocket corridorEndSocket = FindClosestAvailableSocket(corridor, socketB);

        if (corridorEndSocket == null)
        {
            DestroyObjectSafe(corridor.gameObject);
            lastFailReason = "bridge end socket not found.";
            return false;
        }

        float endDistance = Vector3.Distance(corridorEndSocket.transform.position, socketB.transform.position);

        if (endDistance > bridgeSocketSnapTolerance)
        {
            DestroyObjectSafe(corridor.gameObject);
            lastFailReason = "bridge length mismatch. distance = " + endDistance;
            return false;
        }

        HashSet<FacilityRoom> allowedOverlapRooms = new HashSet<FacilityRoom>();
        allowedOverlapRooms.Add(roomA);
        allowedOverlapRooms.Add(roomB);

        if (IsRoomOverlapping(corridor, allowedOverlapRooms))
        {
            DestroyObjectSafe(corridor.gameObject);
            lastFailReason = "bridge overlap rejected.";
            return false;
        }

        socketA.MarkConnected();
        socketB.MarkConnected();
        corridorStartSocket.MarkConnected();
        corridorEndSocket.MarkConnected();

        openSockets.Remove(socketA);
        openSockets.Remove(socketB);

        corridor.runtimeDepthFromStart = Mathf.Min(roomA.runtimeDepthFromStart, roomB.runtimeDepthFromStart) + 1;
        corridor.runtimeNearestStartDistance = GetNearestStartDistance(corridor.transform.position);

        ApplyRoomSetup(corridor);

        generatedRooms.Add(corridor);
        AddAvailableSocketsFromRoom(corridor);

        return true;
    }

    private FacilitySocket FindClosestAvailableSocket(FacilityRoom room, FacilitySocket targetSocket)
    {
        if (room == null || targetSocket == null)
        {
            return null;
        }

        List<FacilitySocket> sockets = room.GetAvailableSocketsByFloor(targetSocket.floorIndex);

        FacilitySocket best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < sockets.Count; i++)
        {
            FacilitySocket socket = sockets[i];

            if (socket == null)
            {
                continue;
            }

            float distance = Vector3.Distance(socket.transform.position, targetSocket.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = socket;
            }
        }

        return best;
    }

    private void PlaceExitDoor()
    {
        if (exitDoorPrefab == null)
        {
            Debug.LogWarning("ProceduralFacilityGenerator: exitDoorPrefab is missing.");
            return;
        }

        List<FacilityExitPointCandidate> candidates = new List<FacilityExitPointCandidate>();

        for (int i = 0; i < generatedRooms.Count; i++)
        {
            FacilityRoom room = generatedRooms[i];

            if (room == null)
            {
                continue;
            }

            if (!room.canReceiveExitDoor)
            {
                continue;
            }

            if (room.roomType == FacilityRoomType.StartRoom || room.roomType == FacilityRoomType.Corridor)
            {
                continue;
            }

            if (room.exitPoints == null || room.exitPoints.Length == 0)
            {
                continue;
            }

            for (int j = 0; j < room.exitPoints.Length; j++)
            {
                FacilityExitPoint point = room.exitPoints[j];

                if (point == null || point.isOccupied)
                {
                    continue;
                }

                FacilityExitPointCandidate candidate = new FacilityExitPointCandidate();
                candidate.room = room;
                candidate.point = point;
                candidate.score = room.runtimeDepthFromStart * 10f + room.runtimeNearestStartDistance;
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("ProceduralFacilityGenerator: no exit point candidates.");
            return;
        }

        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        FacilityExitPointCandidate selected = candidates[0];

        GameObject exitDoor = Instantiate(
            exitDoorPrefab,
            selected.point.transform.position,
            selected.point.transform.rotation,
            generatedRoot
        );

        exitDoor.name = exitDoorPrefab.name + "_Generated_ExitDoor";
        selected.point.isOccupied = true;

        ApplyScanLayerToGameObject(exitDoor);
        SetSurfaceInfoRecursively(exitDoor, ScanSurfaceType.ExitDoor);

        generatedLooseObjects.Add(exitDoor);
    }

    private struct FacilityExitPointCandidate
    {
        public FacilityRoom room;
        public FacilityExitPoint point;
        public float score;
    }

    private void PlaceEscapeItems()
    {
        if (escapeItemPrefabs == null || escapeItemPrefabs.Length == 0 || escapeItemCount <= 0)
        {
            return;
        }

        List<FacilityItemPointCandidate> candidates = new List<FacilityItemPointCandidate>();

        for (int i = 0; i < generatedRooms.Count; i++)
        {
            FacilityRoom room = generatedRooms[i];

            if (room == null)
            {
                continue;
            }

            if (!room.canReceiveEscapeItem)
            {
                continue;
            }

            if (room.roomType == FacilityRoomType.StartRoom || room.roomType == FacilityRoomType.Corridor)
            {
                continue;
            }

            if (room.runtimeDepthFromStart < minEscapeItemDepthFromStart)
            {
                continue;
            }

            if (room.runtimeNearestStartDistance < minEscapeItemDistanceFromStart)
            {
                continue;
            }

            if (room.itemPoints == null || room.itemPoints.Length == 0)
            {
                continue;
            }

            for (int j = 0; j < room.itemPoints.Length; j++)
            {
                FacilityItemPoint point = room.itemPoints[j];

                if (point == null || point.isOccupied)
                {
                    continue;
                }

                if (point.allowedKind != FacilityItemKind.Any && point.allowedKind != FacilityItemKind.EscapeItem)
                {
                    continue;
                }

                FacilityItemPointCandidate candidate = new FacilityItemPointCandidate();
                candidate.room = room;
                candidate.point = point;
                candidates.Add(candidate);
            }
        }

        Shuffle(candidates);

        int placedCount = 0;
        HashSet<FacilityRoom> usedRooms = new HashSet<FacilityRoom>();

        for (int i = 0; i < candidates.Count && placedCount < escapeItemCount; i++)
        {
            FacilityItemPointCandidate candidate = candidates[i];

            if (candidate.room == null || candidate.point == null)
            {
                continue;
            }

            if (usedRooms.Contains(candidate.room))
            {
                continue;
            }

            GameObject itemPrefab = GetRandomGameObjectPrefab(escapeItemPrefabs);

            if (itemPrefab == null)
            {
                continue;
            }

            GameObject item = Instantiate(
                itemPrefab,
                candidate.point.transform.position,
                candidate.point.transform.rotation,
                generatedRoot
            );

            item.name = itemPrefab.name + "_Generated_EscapeItem";
            candidate.point.isOccupied = true;
            usedRooms.Add(candidate.room);

            ApplyScanLayerToGameObject(item);
            SetSurfaceInfoRecursively(item, ScanSurfaceType.EscapeItem);

            generatedLooseObjects.Add(item);
            placedCount++;
        }

        if (placedCount < escapeItemCount)
        {
            Debug.LogWarning("ProceduralFacilityGenerator: escape items placed less than requested. placed = " + placedCount);
        }
    }

    private struct FacilityItemPointCandidate
    {
        public FacilityRoom room;
        public FacilityItemPoint point;
    }

    private void BlockRemainingSockets()
    {
        CleanupOpenSockets();

        for (int i = 0; i < openSockets.Count; i++)
        {
            FacilitySocket socket = openSockets[i];

            if (socket == null || !socket.IsAvailable)
            {
                continue;
            }

            if (blockedDoorPrefab != null)
            {
                GameObject blockedDoor = Instantiate(
                    blockedDoorPrefab,
                    socket.transform.position,
                    socket.transform.rotation,
                    generatedRoot
                );

                blockedDoor.name = blockedDoorPrefab.name + "_Generated_Blocked";

                ApplyScanLayerToGameObject(blockedDoor);
                SetSurfaceInfoRecursively(blockedDoor, ScanSurfaceType.Default);

                generatedLooseObjects.Add(blockedDoor);
            }

            socket.MarkBlocked();
        }

        openSockets.Clear();
    }

    private bool IsRoomOverlapping(FacilityRoom candidateRoom, FacilityRoom allowedRoomA, FacilityRoom allowedRoomB)
    {
        HashSet<FacilityRoom> allowedRooms = new HashSet<FacilityRoom>();

        if (allowedRoomA != null)
        {
            allowedRooms.Add(allowedRoomA);
        }

        if (allowedRoomB != null)
        {
            allowedRooms.Add(allowedRoomB);
        }

        return IsRoomOverlapping(candidateRoom, allowedRooms);
    }

    private bool IsRoomOverlapping(FacilityRoom candidateRoom, HashSet<FacilityRoom> allowedRooms)
    {
        if (candidateRoom == null)
        {
            return true;
        }

        Physics.SyncTransforms();

        Bounds candidateBounds = GetShrunkBounds(candidateRoom.GetPlacementBounds());

        for (int i = 0; i < generatedRooms.Count; i++)
        {
            FacilityRoom existingRoom = generatedRooms[i];

            if (existingRoom == null)
            {
                continue;
            }

            Bounds existingBounds = GetShrunkBounds(existingRoom.GetPlacementBounds());

            if (!candidateBounds.Intersects(existingBounds))
            {
                continue;
            }

            if (allowedRooms != null && allowedRooms.Contains(existingRoom))
            {
                if (IsAcceptableConnectionOverlap(candidateBounds, existingBounds))
                {
                    continue;
                }
            }

            return true;
        }

        return false;
    }

    private Bounds GetShrunkBounds(Bounds sourceBounds)
    {
        Vector3 shrink = Vector3.one * boundsShrinkAmount;
        Vector3 newSize = sourceBounds.size - shrink;

        newSize.x = Mathf.Max(0.01f, newSize.x);
        newSize.y = Mathf.Max(0.01f, newSize.y);
        newSize.z = Mathf.Max(0.01f, newSize.z);

        return new Bounds(sourceBounds.center, newSize);
    }

    private bool IsAcceptableConnectionOverlap(Bounds a, Bounds b)
    {
        Vector3 overlapDepth = GetBoundsOverlapDepth(a, b);

        bool thinOnX = overlapDepth.x <= maxAllowedConnectionOverlapDepth;
        bool thinOnZ = overlapDepth.z <= maxAllowedConnectionOverlapDepth;

        return thinOnX || thinOnZ;
    }

    private Vector3 GetBoundsOverlapDepth(Bounds a, Bounds b)
    {
        float overlapX = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
        float overlapY = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
        float overlapZ = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);

        return new Vector3(
            Mathf.Max(0f, overlapX),
            Mathf.Max(0f, overlapY),
            Mathf.Max(0f, overlapZ)
        );
    }

    private void AddAvailableSocketsFromRoom(FacilityRoom room)
    {
        if (room == null)
        {
            return;
        }

        List<FacilitySocket> sockets = room.GetAvailableSockets();

        for (int i = 0; i < sockets.Count; i++)
        {
            FacilitySocket socket = sockets[i];

            if (socket == null)
            {
                continue;
            }

            if (!openSockets.Contains(socket))
            {
                openSockets.Add(socket);
            }
        }
    }

    private void CleanupOpenSockets()
    {
        for (int i = openSockets.Count - 1; i >= 0; i--)
        {
            FacilitySocket socket = openSockets[i];

            if (socket == null || !socket.IsAvailable)
            {
                openSockets.RemoveAt(i);
            }
        }
    }

    private FacilityRoom GetRoomFromSocket(FacilitySocket socket)
    {
        if (socket == null)
        {
            return null;
        }

        return socket.GetComponentInParent<FacilityRoom>();
    }

    private float GetNearestStartDistance(Vector3 position)
    {
        float nearest = float.MaxValue;

        for (int i = 0; i < startRooms.Count; i++)
        {
            FacilityRoom startRoom = startRooms[i];

            if (startRoom == null)
            {
                continue;
            }

            float distance = Vector3.Distance(position, startRoom.transform.position);

            if (distance < nearest)
            {
                nearest = distance;
            }
        }

        if (nearest == float.MaxValue)
        {
            nearest = 0f;
        }

        return nearest;
    }

    private void ApplyRoomSetup(FacilityRoom room)
    {
        if (room == null)
        {
            return;
        }

        if (applyScanLayerAutomatically)
        {
            room.ApplyScanLayer(scanLayerName);
        }
    }

    private void ApplyRendererVisibility()
    {
        bool visible = !hideRenderersAfterGenerate;

        for (int i = 0; i < generatedRooms.Count; i++)
        {
            if (generatedRooms[i] != null)
            {
                generatedRooms[i].SetRenderersVisible(visible);
            }
        }

        for (int i = 0; i < generatedLooseObjects.Count; i++)
        {
            SetRenderersVisible(generatedLooseObjects[i], visible);
        }
    }

    private void SetRenderersVisible(GameObject target, bool visible)
    {
        if (target == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    private void ApplyScanLayerToGameObject(GameObject target)
    {
        if (!applyScanLayerAutomatically || target == null)
        {
            return;
        }

        int layerIndex = LayerMask.NameToLayer(scanLayerName);

        if (layerIndex < 0)
        {
            Debug.LogWarning("ProceduralFacilityGenerator: layer not found = " + scanLayerName);
            return;
        }

        Transform[] children = target.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null)
            {
                children[i].gameObject.layer = layerIndex;
            }
        }
    }

    private void SetSurfaceInfoRecursively(GameObject target, ScanSurfaceType surfaceType)
    {
        if (target == null)
        {
            return;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);

        if (colliders == null || colliders.Length == 0)
        {
            ScanSurfaceInfo rootInfo = target.GetComponent<ScanSurfaceInfo>();

            if (rootInfo == null)
            {
                rootInfo = target.AddComponent<ScanSurfaceInfo>();
            }

            rootInfo.surfaceType = surfaceType;
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];

            if (collider == null)
            {
                continue;
            }

            ScanSurfaceInfo info = collider.GetComponent<ScanSurfaceInfo>();

            if (info == null)
            {
                info = collider.gameObject.AddComponent<ScanSurfaceInfo>();
            }

            info.surfaceType = surfaceType;
        }
    }

    private FacilityRoom GetRandomRoomFromArray(FacilityRoom[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        List<FacilityRoom> valid = new List<FacilityRoom>();

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                valid.Add(prefabs[i]);
            }
        }

        if (valid.Count == 0)
        {
            return null;
        }

        return valid[Random.Range(0, valid.Count)];
    }

    private GameObject GetRandomGameObjectPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        List<GameObject> valid = new List<GameObject>();

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                valid.Add(prefabs[i]);
            }
        }

        if (valid.Count == 0)
        {
            return null;
        }

        return valid[Random.Range(0, valid.Count)];
    }

    private void AddArrayToList(List<FacilityRoom> list, FacilityRoom[] array)
    {
        if (list == null || array == null)
        {
            return;
        }

        for (int i = 0; i < array.Length; i++)
        {
            if (array[i] != null)
            {
                list.Add(array[i]);
            }
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        if (list == null)
        {
            return;
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private void DestroyObjectSafe(GameObject target)
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