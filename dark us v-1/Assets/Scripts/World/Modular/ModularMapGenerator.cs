using System.Collections.Generic;
using UnityEngine;

// 직접 만든 방 프리팹들을 문 위치 기준으로 이어 붙여 맵을 생성하는 스크립트이다.
// 방 조각은 사람이 만들고, 전체 배치만 랜덤으로 조립하는 방식이다.
public class ModularMapGenerator : MonoBehaviour
{
    [Header("Generate")]
    // 게임 시작 시 자동으로 생성할지 정한다.
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

    [Header("Room Count")]
    // 시작방을 포함한 목표 방 개수이다.
    [SerializeField] private int targetRoomCount = 8;

    // 방 하나를 배치할 때 시도할 최대 횟수이다.
    [SerializeField] private int maxPlacementAttemptsPerRoom = 40;

    [Header("Room Prefabs")]
    // 시작 방 프리팹이다.
    [SerializeField] private ModularRoom startRoomPrefab;

    // 일반 방 / 복도 프리팹 목록이다.
    [SerializeField] private ModularRoom[] roomPrefabs;

    [Header("Door Prefabs")]
    // 남는 문을 막을 때 사용할 프리팹이다.
    [SerializeField] private GameObject blockedDoorPrefab;

    [Header("Generated Root")]
    // 생성된 맵을 담을 부모 Transform이다.
    [SerializeField] private Transform generatedRoot;

    [Header("Overlap")]
    // Bounds끼리 닿는 정도를 약간 허용하기 위해 줄이는 값이다.
    [SerializeField] private float boundsShrinkAmount = 0.3f;

    // 문끼리 붙는 부분에서 허용할 최대 수평 겹침 깊이이다.
    // 이 값보다 얇은 겹침은 문 연결 때문에 생긴 정상 접촉으로 본다.
    [SerializeField] private float maxAllowedConnectionOverlapDepth = 0.65f;

    // 문을 맞춘 뒤 새 방을 기존 방 바깥쪽으로 아주 살짝 밀어낼 거리이다.
    // 너무 크면 문 사이에 틈이 보이므로 작게 둔다.
    [SerializeField] private float doorSeparationOffset = 0.04f;

    [Header("Debug")]
    // 배치 실패 이유를 Console에 출력할지 정한다.
    [SerializeField] private bool debugPlacementFailures = true;

    [Header("Scan Setup")]
    // 생성된 방에 스캔 레이어를 자동 적용할지 정한다.
    [SerializeField] private bool applyScanLayerAutomatically = true;

    // 스캔 대상 레이어 이름이다.
    [SerializeField] private string scanLayerName = "RevealSurface";

    [Header("Runtime Visibility")]
    // 생성 후 Renderer를 끌지 정한다.
    // 테스트 중에는 false, 실제 어두운 게임에서는 true가 좋다.
    [SerializeField] private bool hideRenderersAfterGenerate = false;

    // 생성된 방 목록이다.
    private readonly List<ModularRoom> generatedRooms = new List<ModularRoom>();

    // 생성된 막힌 문 오브젝트 목록이다.
    // Renderer를 자동으로 끌 때 사용한다.
    private readonly List<GameObject> generatedBlockedDoors = new List<GameObject>();

    // 아직 연결되지 않은 문 목록이다.
    private readonly List<ModularDoorPoint> openDoorPoints = new List<ModularDoorPoint>();

    // 마지막 배치 실패 이유를 저장한다.
    private string lastPlacementFailReason = "";

    // 시작 시 자동 생성 옵션을 처리한다.
    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    // Inspector 우클릭 메뉴에서 맵을 생성할 수 있게 한다.
    [ContextMenu("Generate Modular Map")]
    public void Generate()
    {
        // 안전값을 보정한다.
        ClampSettings();

        // 기존 결과물을 삭제한다.
        if (clearBeforeGenerate)
        {
            ClearGenerated();
        }

        // 생성 루트를 준비한다.
        EnsureGeneratedRoot();

        // 시드를 결정한다.
        lastGeneratedSeed = useFixedSeed ? fixedSeed : Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(lastGeneratedSeed);

        // 시작 방이 없으면 생성할 수 없다.
        if (startRoomPrefab == null)
        {
            Debug.LogError("ModularMapGenerator: startRoomPrefab is missing.");
            return;
        }

        // 시작 방을 생성한다.
        ModularRoom startRoom = SpawnStartRoom();

        if (startRoom == null)
        {
            Debug.LogError("ModularMapGenerator: failed to spawn start room.");
            return;
        }

        // 목표 방 개수까지 반복해서 방을 붙인다.
        while (generatedRooms.Count < targetRoomCount && openDoorPoints.Count > 0)
        {
            bool placed = TryPlaceOneRoom();

            // 더 이상 배치할 수 없으면 중단한다.
            if (!placed)
            {
                if (debugPlacementFailures)
                {
                    Debug.LogWarning(
                        "ModularMapGenerator: placement stopped. Generated count = "
                        + generatedRooms.Count
                        + " / reason = "
                        + lastPlacementFailReason
                    );
                }

                break;
            }
        }

        // 남은 문을 막힌 문으로 처리한다.
        BlockRemainingDoors();

        // 생성 후 Renderer 상태를 적용한다.
        ApplyRendererVisibility();

        // 최종 생성 결과를 출력한다.
        Debug.Log("ModularMapGenerator: generated rooms = " + generatedRooms.Count + ", seed = " + lastGeneratedSeed);
    }

    // Inspector 우클릭 메뉴에서 생성된 맵을 삭제할 수 있게 한다.
    [ContextMenu("Clear Modular Map")]
    public void ClearGenerated()
    {
        // 내부 목록을 비운다.
        generatedRooms.Clear();
        generatedBlockedDoors.Clear();
        openDoorPoints.Clear();

        // 생성 루트가 없으면 현재 Transform 아래의 GeneratedModularMap을 찾아본다.
        if (generatedRoot == null)
        {
            Transform oldRoot = transform.Find("GeneratedModularMap");

            if (oldRoot != null)
            {
                generatedRoot = oldRoot;
            }
        }

        // 생성 루트가 있으면 삭제한다.
        if (generatedRoot != null)
        {
            GameObject rootObject = generatedRoot.gameObject;
            generatedRoot = null;

            DestroyObjectSafe(rootObject);
        }
    }

    // Inspector 값들을 안전한 범위로 보정한다.
    private void ClampSettings()
    {
        // 방 개수는 최소 1개 이상이어야 한다.
        targetRoomCount = Mathf.Max(1, targetRoomCount);

        // 배치 시도 횟수는 최소 1회 이상이어야 한다.
        maxPlacementAttemptsPerRoom = Mathf.Max(1, maxPlacementAttemptsPerRoom);

        // Bounds 축소 값은 음수가 되면 안 된다.
        boundsShrinkAmount = Mathf.Max(0f, boundsShrinkAmount);

        // 허용 겹침 깊이는 음수가 되면 안 된다.
        maxAllowedConnectionOverlapDepth = Mathf.Max(0f, maxAllowedConnectionOverlapDepth);

        // 문 분리 거리는 음수가 되면 안 된다.
        doorSeparationOffset = Mathf.Max(0f, doorSeparationOffset);
    }

    // 생성 루트가 없으면 만든다.
    private void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("GeneratedModularMap");
        rootObject.transform.SetParent(transform, false);
        generatedRoot = rootObject.transform;
    }

    // 시작 방을 중앙에 생성한다.
    private ModularRoom SpawnStartRoom()
    {
        // 시작 방을 현재 생성기 위치에 만든다.
        ModularRoom startRoom = Instantiate(
            startRoomPrefab,
            transform.position,
            transform.rotation,
            generatedRoot
        );

        // 이름을 정리한다.
        startRoom.name = startRoomPrefab.name + "_Generated_Start";

        // 방 런타임 상태를 초기화한다.
        startRoom.InitializeRuntime();

        // 스캔 레이어를 적용한다.
        ApplyScanSetup(startRoom);

        // 생성 목록에 추가한다.
        generatedRooms.Add(startRoom);

        // 열린 문 목록을 갱신한다.
        AddAvailableDoorsFromRoom(startRoom);

        return startRoom;
    }

    // 방 하나를 랜덤 문에 붙여 배치한다.
    private bool TryPlaceOneRoom()
    {
        // 방 프리팹이 없으면 실패한다.
        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            lastPlacementFailReason = "roomPrefabs is empty.";
            Debug.LogError("ModularMapGenerator: roomPrefabs is empty.");
            return false;
        }

        // 여러 번 시도해서 배치 가능한 방을 찾는다.
        for (int attempt = 0; attempt < maxPlacementAttemptsPerRoom; attempt++)
        {
            // 열린 문이 없으면 실패한다.
            if (openDoorPoints.Count == 0)
            {
                lastPlacementFailReason = "no open door points.";
                return false;
            }

            // 연결 대상 문을 고른다.
            ModularDoorPoint targetDoor = GetRandomOpenDoor();

            if (targetDoor == null)
            {
                lastPlacementFailReason = "targetDoor is null.";
                continue;
            }

            // 랜덤 방 프리팹을 고른다.
            ModularRoom selectedPrefab = GetRandomRoomPrefab();

            if (selectedPrefab == null)
            {
                lastPlacementFailReason = "selectedPrefab is null.";
                continue;
            }

            // 후보 방을 임시로 생성한다.
            ModularRoom candidateRoom = Instantiate(
                selectedPrefab,
                Vector3.zero,
                Quaternion.identity,
                generatedRoot
            );

            candidateRoom.name = selectedPrefab.name + "_Generated";

            // 후보 방 상태를 초기화한다.
            candidateRoom.InitializeRuntime();

            // 후보 방에서 연결할 문을 고른다.
            ModularDoorPoint candidateDoor = GetRandomAvailableDoor(candidateRoom);

            if (candidateDoor == null)
            {
                lastPlacementFailReason = "candidateDoor is null. prefab = " + selectedPrefab.name;
                DestroyObjectSafe(candidateRoom.gameObject);
                continue;
            }

            // 두 문이 서로 마주보게 후보 방을 이동/회전시킨다.
            AlignRoomToDoor(candidateRoom, candidateDoor, targetDoor);

            // 방을 이동/회전시킨 직후 Unity 물리 엔진의 Transform 정보를 강제로 갱신한다.
            // 이걸 하지 않으면 Collider.bounds가 이전 위치 기준으로 읽혀서 겹침 판정이 잘못될 수 있다.
            Physics.SyncTransforms();

            // 겹치면 후보 방을 삭제하고 다시 시도한다.
            if (IsRoomOverlapping(candidateRoom, targetDoor))
            {
                lastPlacementFailReason =
                    "overlap rejected. prefab = "
                    + selectedPrefab.name
                    + ", attempt = "
                    + attempt;

                DestroyObjectSafe(candidateRoom.gameObject);
                continue;
            }

            // 연결 성공 상태를 기록한다.
            targetDoor.MarkConnected();
            candidateDoor.MarkConnected();

            // 열린 문 목록에서 대상 문을 제거한다.
            openDoorPoints.Remove(targetDoor);

            // 스캔 레이어를 적용한다.
            ApplyScanSetup(candidateRoom);

            // 생성 목록에 후보 방을 추가한다.
            generatedRooms.Add(candidateRoom);

            // 새 방의 남은 문을 열린 문 목록에 추가한다.
            AddAvailableDoorsFromRoom(candidateRoom);

            // 마지막 실패 이유를 비운다.
            lastPlacementFailReason = "";

            return true;
        }

        return false;
    }

    // 후보 방의 특정 문을 기존 열린 문에 맞춰 배치한다.
    private void AlignRoomToDoor(ModularRoom candidateRoom, ModularDoorPoint candidateDoor, ModularDoorPoint targetDoor)
    {
        // 대상 문과 후보 문이 마주보도록 목표 회전을 만든다.
        Quaternion desiredDoorRotation = Quaternion.LookRotation(-targetDoor.transform.forward, targetDoor.transform.up);

        // 후보 문이 후보 방 루트 기준으로 어떤 회전을 가지고 있는지 계산한다.
        Quaternion candidateDoorLocalRotation =
            Quaternion.Inverse(candidateRoom.transform.rotation) * candidateDoor.transform.rotation;

        // 후보 방의 최종 회전을 계산한다.
        Quaternion finalRoomRotation = desiredDoorRotation * Quaternion.Inverse(candidateDoorLocalRotation);

        // 회전을 먼저 적용한다.
        candidateRoom.transform.rotation = finalRoomRotation;

        // 회전 후 후보 문의 위치가 바뀌었으므로 위치 차이를 계산한다.
        Vector3 positionOffset = targetDoor.transform.position - candidateDoor.transform.position;

        // 후보 방을 대상 문 위치로 이동시킨다.
        candidateRoom.transform.position += positionOffset;

        // 문끼리 완전히 같은 좌표에 있을 때 Bounds가 닿아서 실패하는 문제를 줄이기 위해 아주 살짝 바깥으로 민다.
        if (doorSeparationOffset > 0f)
        {
            candidateRoom.transform.position += targetDoor.transform.forward * doorSeparationOffset;
        }

        // 후보 방의 이동/회전 결과를 물리 엔진에 즉시 반영한다.
        // Collider.bounds를 바로 읽기 전에 최신 위치가 반영되도록 하기 위한 처리이다.
        Physics.SyncTransforms();
    }

    // 후보 방이 기존 방들과 겹치는지 검사한다.
    private bool IsRoomOverlapping(ModularRoom candidateRoom, ModularDoorPoint targetDoor)
    {
         // 겹침 검사 직전에 물리 Transform을 강제로 갱신한다.
        // Instantiate 후 이동/회전한 Collider의 bounds가 최신 값으로 계산되게 한다.
        Physics.SyncTransforms();
        
        // 후보 방의 Bounds를 가져온다.
        Bounds candidateBounds = GetShrunkBounds(candidateRoom.GetOverlapBounds());

        // 이 후보 방이 붙으려는 기존 방을 찾는다.
        ModularRoom targetRoom = GetRoomFromDoor(targetDoor);

        // 기존 생성된 방들과 비교한다.
        for (int i = 0; i < generatedRooms.Count; i++)
        {
            ModularRoom existingRoom = generatedRooms[i];

            if (existingRoom == null)
            {
                continue;
            }

            Bounds existingBounds = GetShrunkBounds(existingRoom.GetOverlapBounds());

            // Bounds가 아예 안 겹치면 통과한다.
            if (!candidateBounds.Intersects(existingBounds))
            {
                continue;
            }

            // 연결 대상 방과의 얇은 접촉은 정상 연결로 허용한다.
            if (existingRoom == targetRoom)
            {
                if (IsAcceptableConnectionOverlap(candidateBounds, existingBounds))
                {
                    continue;
                }
            }

            // 그 외의 겹침은 배치 실패로 본다.
            return true;
        }

        return false;
    }

    // 문 연결 지점에서 생기는 얇은 겹침인지 확인한다.
    private bool IsAcceptableConnectionOverlap(Bounds candidateBounds, Bounds existingBounds)
    {
        // 각 축에서 실제 겹친 깊이를 계산한다.
        Vector3 overlapDepth = GetBoundsOverlapDepth(candidateBounds, existingBounds);

        // X나 Z 중 하나라도 아주 얇게만 겹치면 문 연결로 인한 정상 접촉으로 본다.
        bool thinOnX = overlapDepth.x <= maxAllowedConnectionOverlapDepth;
        bool thinOnZ = overlapDepth.z <= maxAllowedConnectionOverlapDepth;

        // 수평 방향 중 한 축만 얇게 겹치는 상태는 방이 문으로 맞닿은 상황에 가깝다.
        if (thinOnX || thinOnZ)
        {
            return true;
        }

        // 둘 다 깊게 겹치면 실제 겹침으로 본다.
        return false;
    }

    // 두 Bounds가 각 축에서 얼마나 겹쳤는지 계산한다.
    private Vector3 GetBoundsOverlapDepth(Bounds a, Bounds b)
    {
        // X축 겹침 깊이를 계산한다.
        float overlapX = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);

        // Y축 겹침 깊이를 계산한다.
        float overlapY = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);

        // Z축 겹침 깊이를 계산한다.
        float overlapZ = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);

        // 음수가 나오지 않게 보정한다.
        overlapX = Mathf.Max(0f, overlapX);
        overlapY = Mathf.Max(0f, overlapY);
        overlapZ = Mathf.Max(0f, overlapZ);

        return new Vector3(overlapX, overlapY, overlapZ);
    }

    // DoorPoint가 속한 ModularRoom을 찾는다.
    private ModularRoom GetRoomFromDoor(ModularDoorPoint door)
    {
        if (door == null)
        {
            return null;
        }

        return door.GetComponentInParent<ModularRoom>();
    }

    // Bounds를 살짝 줄여서 문 주변의 아주 작은 접촉을 허용한다.
    private Bounds GetShrunkBounds(Bounds sourceBounds)
    {
        Vector3 shrink = Vector3.one * boundsShrinkAmount;
        Vector3 newSize = sourceBounds.size - shrink;

        newSize.x = Mathf.Max(0.01f, newSize.x);
        newSize.y = Mathf.Max(0.01f, newSize.y);
        newSize.z = Mathf.Max(0.01f, newSize.z);

        return new Bounds(sourceBounds.center, newSize);
    }

    // 방 하나의 사용 가능한 문들을 열린 문 목록에 추가한다.
    private void AddAvailableDoorsFromRoom(ModularRoom room)
    {
        if (room == null)
        {
            return;
        }

        List<ModularDoorPoint> availableDoors = room.GetAvailableDoorPoints();

        for (int i = 0; i < availableDoors.Count; i++)
        {
            ModularDoorPoint door = availableDoors[i];

            if (door == null)
            {
                continue;
            }

            if (!openDoorPoints.Contains(door))
            {
                openDoorPoints.Add(door);
            }
        }
    }

    // 열린 문 목록에서 랜덤 문을 가져온다.
    private ModularDoorPoint GetRandomOpenDoor()
    {
        // 잘못된 문을 먼저 정리한다.
        CleanupOpenDoorList();

        if (openDoorPoints.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, openDoorPoints.Count);
        return openDoorPoints[randomIndex];
    }

    // 열린 문 목록에서 이미 쓸 수 없는 문을 제거한다.
    private void CleanupOpenDoorList()
    {
        for (int i = openDoorPoints.Count - 1; i >= 0; i--)
        {
            ModularDoorPoint door = openDoorPoints[i];

            if (door == null || !door.IsAvailable)
            {
                openDoorPoints.RemoveAt(i);
            }
        }
    }

    // 랜덤 방 프리팹을 가져온다.
    private ModularRoom GetRandomRoomPrefab()
    {
        if (roomPrefabs == null || roomPrefabs.Length == 0)
        {
            return null;
        }

        // null이 아닌 프리팹만 임시로 모은다.
        List<ModularRoom> validPrefabs = new List<ModularRoom>();

        for (int i = 0; i < roomPrefabs.Length; i++)
        {
            if (roomPrefabs[i] != null)
            {
                validPrefabs.Add(roomPrefabs[i]);
            }
        }

        if (validPrefabs.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, validPrefabs.Count);
        return validPrefabs[randomIndex];
    }

    // 방 안에서 랜덤으로 사용 가능한 문을 가져온다.
    private ModularDoorPoint GetRandomAvailableDoor(ModularRoom room)
    {
        if (room == null)
        {
            return null;
        }

        List<ModularDoorPoint> availableDoors = room.GetAvailableDoorPoints();

        if (availableDoors.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, availableDoors.Count);
        return availableDoors[randomIndex];
    }

    // 남아 있는 열린 문들을 막힌 문 프리팹으로 막는다.
    private void BlockRemainingDoors()
    {
        CleanupOpenDoorList();

        for (int i = 0; i < openDoorPoints.Count; i++)
        {
            ModularDoorPoint door = openDoorPoints[i];

            if (door == null)
            {
                continue;
            }

            if (blockedDoorPrefab != null)
            {
                GameObject blockedDoor = Instantiate(
                    blockedDoorPrefab,
                    door.transform.position,
                    door.transform.rotation,
                    generatedRoot
                );

                blockedDoor.name = blockedDoorPrefab.name + "_Generated_Blocked";

                // 막힌 문도 스캔 대상 레이어를 적용한다.
                ApplyScanLayerToGameObject(blockedDoor);

                // 나중에 Renderer를 자동으로 끄기 위해 생성된 막힌 문을 기록한다.
                generatedBlockedDoors.Add(blockedDoor);
            }

            door.MarkBlocked();
        }

        openDoorPoints.Clear();
    }

    // 생성된 방의 스캔 세팅을 적용한다.
    private void ApplyScanSetup(ModularRoom room)
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

    // 일반 GameObject에 스캔 레이어를 적용한다.
    private void ApplyScanLayerToGameObject(GameObject target)
    {
        if (!applyScanLayerAutomatically)
        {
            return;
        }

        if (target == null)
        {
            return;
        }

        int layerIndex = LayerMask.NameToLayer(scanLayerName);

        if (layerIndex < 0)
        {
            Debug.LogWarning("ModularMapGenerator: layer not found = " + scanLayerName);
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

    // 생성 후 Renderer 보이기 상태를 적용한다.
    private void ApplyRendererVisibility()
    {
        // Hide Renderers After Generate가 켜져 있으면 false, 꺼져 있으면 true이다.
        bool visible = !hideRenderersAfterGenerate;

        // 생성된 방들의 Renderer를 켜거나 끈다.
        for (int i = 0; i < generatedRooms.Count; i++)
        {
            if (generatedRooms[i] != null)
            {
                generatedRooms[i].SetRenderersVisible(visible);
            }
        }

        // 생성된 막힌 문들의 Renderer도 켜거나 끈다.
        for (int i = 0; i < generatedBlockedDoors.Count; i++)
        {
            GameObject blockedDoor = generatedBlockedDoors[i];

            if (blockedDoor == null)
            {
                continue;
            }

            SetRenderersVisible(blockedDoor, visible);
        }
    }
    
    // 특정 오브젝트와 모든 자식 Renderer를 켜거나 끈다.
    // Collider는 건드리지 않기 때문에 스캔과 충돌은 그대로 유지된다.
    private void SetRenderersVisible(GameObject targetObject, bool visible)
    {
        if (targetObject == null)
        {
            return;
        }

        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    // 에디터/플레이 상태에 맞게 오브젝트를 안전하게 삭제한다.
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