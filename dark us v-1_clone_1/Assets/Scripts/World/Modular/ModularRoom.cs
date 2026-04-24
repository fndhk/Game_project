using System.Collections.Generic;
using UnityEngine;

// 하나의 방 프리팹을 관리하는 스크립트이다.
// 방 루트 오브젝트에 붙이고, DoorPoint와 Bounds Collider를 연결한다.
public class ModularRoom : MonoBehaviour
{
    [Header("Room Info")]
    // 이 방의 종류이다.
    [SerializeField] private ModularRoomType roomType = ModularRoomType.NormalRoom;

    [Header("Door Points")]
    // 방 안에 있는 문 연결 지점들이다.
    [SerializeField] private ModularDoorPoint[] doorPoints;

    [Header("Overlap Bounds")]
    // 방끼리 겹치는지 검사할 때 사용하는 Collider이다.
    // 실제 플레이어 충돌용 Collider가 아니라 생성기 전용 범위이다.
    [SerializeField] private BoxCollider overlapBoundsCollider;

    [Header("Scan Layer Setup")]
    // 스캔 레이어 자동 적용 시 Bounds Collider는 제외할지 여부이다.
    [SerializeField] private bool excludeOverlapBoundsFromScanLayer = true;

    // 방 타입을 외부에서 읽기 위한 프로퍼티이다.
    public ModularRoomType RoomType
    {
        get
        {
            return roomType;
        }
    }

    // 현재 방의 모든 문 목록을 반환한다.
    public ModularDoorPoint[] DoorPoints
    {
        get
        {
            return doorPoints;
        }
    }

    // 현재 방의 겹침 검사 Collider를 반환한다.
    public BoxCollider OverlapBoundsCollider
    {
        get
        {
            return overlapBoundsCollider;
        }
    }

    // 생성기에서 방을 만든 직후 호출하는 초기화 함수이다.
    public void InitializeRuntime()
    {
        // 문 목록이 비어 있으면 자식에서 자동으로 찾는다.
        RefreshDoorPointsIfNeeded();

        // 모든 문 상태를 초기화한다.
        ResetDoorStates();
    }

    // 문 목록이 비어 있을 때 자식에서 자동으로 찾는다.
    public void RefreshDoorPointsIfNeeded()
    {
        // 이미 문 목록이 있으면 그대로 사용한다.
        if (doorPoints != null && doorPoints.Length > 0)
        {
            return;
        }

        // 자식 오브젝트에서 ModularDoorPoint를 찾는다.
        doorPoints = GetComponentsInChildren<ModularDoorPoint>(true);
    }

    // 모든 문 상태를 초기화한다.
    public void ResetDoorStates()
    {
        // 문 목록이 없으면 종료한다.
        if (doorPoints == null)
        {
            return;
        }

        // 모든 문을 사용 가능 상태로 초기화한다.
        for (int i = 0; i < doorPoints.Length; i++)
        {
            if (doorPoints[i] != null)
            {
                doorPoints[i].ResetRuntimeState();
            }
        }
    }

    // 사용 가능한 문 목록을 반환한다.
    public List<ModularDoorPoint> GetAvailableDoorPoints()
    {
        // 결과를 담을 리스트이다.
        List<ModularDoorPoint> availableDoors = new List<ModularDoorPoint>();

        // 문 목록이 비어 있으면 자동으로 찾는다.
        RefreshDoorPointsIfNeeded();

        // 사용할 수 있는 문만 모은다.
        for (int i = 0; i < doorPoints.Length; i++)
        {
            ModularDoorPoint currentDoor = doorPoints[i];

            if (currentDoor == null)
            {
                continue;
            }

            if (currentDoor.IsAvailable)
            {
                availableDoors.Add(currentDoor);
            }
        }

        return availableDoors;
    }

    // 겹침 검사에 사용할 월드 Bounds를 반환한다.
    public Bounds GetOverlapBounds()
    {
        // 전용 BoxCollider가 있으면 그 Bounds를 사용한다.
        if (overlapBoundsCollider != null)
        {
            return overlapBoundsCollider.bounds;
        }

        // 전용 Bounds가 없으면 전체 Collider 기준으로 대체한다.
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        bool hasAnyCollider = false;
        Bounds combinedBounds = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider currentCollider = colliders[i];

            if (currentCollider == null)
            {
                continue;
            }

            if (!hasAnyCollider)
            {
                combinedBounds = currentCollider.bounds;
                hasAnyCollider = true;
            }
            else
            {
                combinedBounds.Encapsulate(currentCollider.bounds);
            }
        }

        // Collider가 하나도 없으면 Renderer 기준으로 대체한다.
        if (!hasAnyCollider)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer currentRenderer = renderers[i];

                if (currentRenderer == null)
                {
                    continue;
                }

                if (!hasAnyCollider)
                {
                    combinedBounds = currentRenderer.bounds;
                    hasAnyCollider = true;
                }
                else
                {
                    combinedBounds.Encapsulate(currentRenderer.bounds);
                }
            }
        }

        return combinedBounds;
    }

    // 생성된 방의 스캔 대상 레이어를 자동으로 맞춘다.
    public void ApplyScanLayer(string layerName)
    {
        // 레이어 이름이 비어 있으면 종료한다.
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        // 레이어 번호를 찾는다.
        int layerIndex = LayerMask.NameToLayer(layerName);

        // 레이어가 없으면 종료한다.
        if (layerIndex < 0)
        {
            Debug.LogWarning("ModularRoom: layer not found = " + layerName);
            return;
        }

        // 모든 자식 Transform을 가져온다.
        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child == null)
            {
                continue;
            }

            // 겹침 검사 Bounds는 스캔 레이어에서 제외한다.
            if (excludeOverlapBoundsFromScanLayer && overlapBoundsCollider != null)
            {
                if (child == overlapBoundsCollider.transform)
                {
                    continue;
                }
            }

            // 실제 벽, 바닥, 오브젝트 쪽에 스캔 레이어를 적용한다.
            child.gameObject.layer = layerIndex;
        }
    }

    // 생성된 방의 Renderer를 켜거나 끈다.
    public void SetRenderersVisible(bool visible)
    {
        // 자식 포함 모든 Renderer를 가져온다.
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        // Renderer만 켜고 끄고, Collider는 건드리지 않는다.
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible;
            }
        }
    }

    // Inspector에서 자동 세팅할 때 사용할 함수이다.
    private void Reset()
    {
        // 자식에서 문 지점을 자동으로 찾는다.
        doorPoints = GetComponentsInChildren<ModularDoorPoint>(true);

        // 자식에서 첫 번째 BoxCollider를 Bounds 후보로 잡는다.
        BoxCollider[] boxColliders = GetComponentsInChildren<BoxCollider>(true);

        for (int i = 0; i < boxColliders.Length; i++)
        {
            if (boxColliders[i] != null && boxColliders[i].isTrigger)
            {
                overlapBoundsCollider = boxColliders[i];
                break;
            }
        }
    }
}