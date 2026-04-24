using System.Collections.Generic;
using UnityEngine;

// 절차적 시설 맵에서 하나의 방/복도/계단 프리팹 정보를 관리한다.
public class FacilityRoom : MonoBehaviour
{
    [Header("Room Info")]
    // 방 종류이다.
    public FacilityRoomType roomType = FacilityRoomType.NormalRoom;

    // 방 크기이다. LargeRoom끼리 직접 연결되는 것을 막을 때 사용한다.
    public FacilityRoomSize roomSize = FacilityRoomSize.Normal;

    [Header("Room Rules")]
    // 이 방이 탈출구 문을 받을 수 있는지 정한다.
    public bool canReceiveExitDoor = true;

    // 이 방에 탈출 아이템이 배치될 수 있는지 정한다.
    public bool canReceiveEscapeItem = true;

    [Header("Sockets")]
    // 연결 소켓 목록이다.
    public FacilitySocket[] sockets;

    [Header("Placement Bounds")]
    // 겹침 검사에 사용할 Bounds Collider이다.
    public BoxCollider placementBoundsCollider;

    [Header("Points")]
    // 아이템 배치 위치 목록이다.
    public FacilityItemPoint[] itemPoints;

    // 탈출구 배치 위치 목록이다.
    public FacilityExitPoint[] exitPoints;

    // 플레이어 스폰 위치 목록이다.
    public FacilitySpawnPoint[] spawnPoints;

    [Header("Scan Layer")]
    // Bounds 오브젝트는 RevealSurface 적용에서 제외할지 정한다.
    public bool excludePlacementBoundsFromScanLayer = true;

    [Header("Runtime Info")]
    // 시작방에서 몇 단계 떨어져 있는지 저장한다.
    public int runtimeDepthFromStart = 0;

    // 가장 가까운 시작방과의 월드 거리이다.
    public float runtimeNearestStartDistance = 0f;

    // 런타임 초기화이다.
    public void InitializeRuntime()
    {
        RefreshCachedChildrenIfNeeded();

        if (sockets != null)
        {
            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i] != null)
                {
                    sockets[i].ResetRuntimeState();
                }
            }
        }

        if (itemPoints != null)
        {
            for (int i = 0; i < itemPoints.Length; i++)
            {
                if (itemPoints[i] != null)
                {
                    itemPoints[i].ResetRuntimeState();
                }
            }
        }

        if (exitPoints != null)
        {
            for (int i = 0; i < exitPoints.Length; i++)
            {
                if (exitPoints[i] != null)
                {
                    exitPoints[i].ResetRuntimeState();
                }
            }
        }

        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] != null)
                {
                    spawnPoints[i].ResetRuntimeState();
                }
            }
        }
    }

    // 배열이 비어 있으면 자식에서 자동으로 찾는다.
    public void RefreshCachedChildrenIfNeeded()
    {
        if (sockets == null || sockets.Length == 0)
        {
            sockets = GetComponentsInChildren<FacilitySocket>(true);
        }

        if (itemPoints == null || itemPoints.Length == 0)
        {
            itemPoints = GetComponentsInChildren<FacilityItemPoint>(true);
        }

        if (exitPoints == null || exitPoints.Length == 0)
        {
            exitPoints = GetComponentsInChildren<FacilityExitPoint>(true);
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            spawnPoints = GetComponentsInChildren<FacilitySpawnPoint>(true);
        }
    }

    // 사용 가능한 소켓 목록을 반환한다.
    public List<FacilitySocket> GetAvailableSockets()
    {
        RefreshCachedChildrenIfNeeded();

        List<FacilitySocket> result = new List<FacilitySocket>();

        if (sockets == null)
        {
            return result;
        }

        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] != null && sockets[i].IsAvailable)
            {
                result.Add(sockets[i]);
            }
        }

        return result;
    }

    // 특정 층에서 사용 가능한 소켓 목록을 반환한다.
    public List<FacilitySocket> GetAvailableSocketsByFloor(int floorIndex)
    {
        RefreshCachedChildrenIfNeeded();

        List<FacilitySocket> result = new List<FacilitySocket>();

        if (sockets == null)
        {
            return result;
        }

        for (int i = 0; i < sockets.Length; i++)
        {
            FacilitySocket socket = sockets[i];

            if (socket == null)
            {
                continue;
            }

            if (!socket.IsAvailable)
            {
                continue;
            }

            if (socket.floorIndex != floorIndex)
            {
                continue;
            }

            result.Add(socket);
        }

        return result;
    }

    // 겹침 검사에 사용할 월드 Bounds를 반환한다.
    public Bounds GetPlacementBounds()
    {
        if (placementBoundsCollider != null)
        {
            return placementBoundsCollider.bounds;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        bool hasAny = false;
        Bounds combined = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider current = colliders[i];

            if (current == null)
            {
                continue;
            }

            if (!hasAny)
            {
                combined = current.bounds;
                hasAny = true;
            }
            else
            {
                combined.Encapsulate(current.bounds);
            }
        }

        return combined;
    }

    // 생성된 방 전체에 스캔 레이어를 적용한다.
    public void ApplyScanLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        int layerIndex = LayerMask.NameToLayer(layerName);

        if (layerIndex < 0)
        {
            Debug.LogWarning("FacilityRoom: layer not found = " + layerName);
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child == null)
            {
                continue;
            }

            if (excludePlacementBoundsFromScanLayer && placementBoundsCollider != null)
            {
                if (child == placementBoundsCollider.transform)
                {
                    continue;
                }
            }

            child.gameObject.layer = layerIndex;
        }
    }

    // Renderer만 켜거나 끈다. Collider는 유지한다.
    public void SetRenderersVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    // Inspector에서 붙였을 때 자동으로 자식들을 찾아준다.
    private void Reset()
    {
        sockets = GetComponentsInChildren<FacilitySocket>(true);
        itemPoints = GetComponentsInChildren<FacilityItemPoint>(true);
        exitPoints = GetComponentsInChildren<FacilityExitPoint>(true);
        spawnPoints = GetComponentsInChildren<FacilitySpawnPoint>(true);

        BoxCollider[] boxes = GetComponentsInChildren<BoxCollider>(true);

        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i] != null && boxes[i].isTrigger)
            {
                placementBoundsCollider = boxes[i];
                break;
            }
        }
    }
}