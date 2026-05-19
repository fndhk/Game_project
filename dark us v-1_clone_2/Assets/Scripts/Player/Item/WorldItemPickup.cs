using UnityEngine;

// 월드에 떨어져 있는 아이템이다.
// PlayerObjectiveInteractor가 아이템 줍기 키로 이 컴포넌트와 상호작용한다.
public class WorldItemPickup : MonoBehaviour, IPlayerInteractable
{
    [Header("Item")]
    // 이 오브젝트가 줄 아이템 종류이다.
    public ItemType itemType = ItemType.Camera;

    // 획득 시 들어갈 개수 또는 사용 횟수이다.
    // Camera는 2, Knife는 1, Medkit은 1 추천.
    public int amount = 1;

    [Header("Pickup")]
    // 주운 뒤 오브젝트를 숨길지 정한다.
    public bool hideAfterPickup = true;

    // 주운 뒤 오브젝트를 완전히 삭제할지 정한다.
    public bool destroyAfterPickup = false;

    [Header("Scan Dot Cleanup")]
    // 주웠을 때 이미 찍혀 있던 아이템 점도 같이 지울지 정한다.
    public bool removeScanDotsAfterPickup = true;

    // 아이템 점을 지울 최소 반경이다.
    public float scanDotRemoveRadius = 0.85f;

    // Collider/Renderer 크기를 기준으로 실제 제거 반경을 자동 보정한다.
    public bool useObjectBoundsForDotCleanup = true;

    // Bounds 기준 반경에 추가로 더할 여유값이다.
    public float scanDotRemovePadding = 0.18f;

    // 켜면 Item 색상 그룹 점만 지운다. 주변 바닥/벽 점이 같이 사라지는 것을 막는다.
    public bool onlyRemoveItemColorDots = true;

    // 점 렌더러 참조이다. 비워두면 씬에서 자동으로 찾는다.
    public InstancedScanDotRenderer[] scanDotRenderers;

    // 이미 주웠는지 저장한다.
    private bool collected = false;

    // 상호작용 문구를 반환한다.
    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        return "[F] " + InGameLocalization.Text("Take") + " " + GetItemDisplayName() + " x" + Mathf.Max(1, amount);
    }

    // 아직 먹지 않았으면 상호작용 가능하다.
    public bool CanInteract(PlayerObjectiveInteractor interactor)
    {
        return !collected;
    }

    // 실제 아이템 획득 처리이다.
    public void Interact(PlayerObjectiveInteractor interactor)
    {
        if (collected)
        {
            return;
        }

        if (interactor == null)
        {
            return;
        }

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();

        if (inventory == null)
        {
            inventory = interactor.GetComponentInParent<PlayerInventory>();
        }

        if (inventory == null)
        {
            Debug.LogWarning("WorldItemPickup: PlayerInventory를 찾지 못함.");
            return;
        }

        bool added = inventory.TryAddItem(itemType, Mathf.Max(1, amount));

        if (!added)
        {
            Debug.Log(InGameLocalization.Text("Inventory is full."));
            return;
        }

        collected = true;

        // 오브젝트를 숨기거나 삭제하기 전에, 이미 생성된 스캔 점을 먼저 제거한다.
        RemoveExistingScanDots();

        if (destroyAfterPickup)
        {
            Destroy(gameObject);
            return;
        }

        if (hideAfterPickup)
        {
            gameObject.SetActive(false);
        }
    }

    // 아이템 주변에 이미 찍힌 스캔 점을 제거한다.
    private void RemoveExistingScanDots()
    {
        if (!removeScanDotsAfterPickup)
        {
            return;
        }

        if (scanDotRenderers == null || scanDotRenderers.Length <= 0)
        {
            scanDotRenderers = Object.FindObjectsByType<InstancedScanDotRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        if (scanDotRenderers == null || scanDotRenderers.Length <= 0)
        {
            return;
        }

        Vector3 center = transform.position;
        float radius = Mathf.Max(0.01f, scanDotRemoveRadius);

        if (useObjectBoundsForDotCleanup && TryGetObjectBounds(out Bounds bounds))
        {
            center = bounds.center;
            radius = Mathf.Max(radius, bounds.extents.magnitude + Mathf.Max(0f, scanDotRemovePadding));
        }

        for (int i = 0; i < scanDotRenderers.Length; i++)
        {
            if (scanDotRenderers[i] == null)
            {
                continue;
            }

            if (onlyRemoveItemColorDots)
            {
                scanDotRenderers[i].RemoveDotsInSphere(center, radius, ScanDotColorGroup.Item);
            }
            else
            {
                scanDotRenderers[i].RemoveDotsInSphere(center, radius);
            }
        }
    }

    // Collider 또는 Renderer를 기준으로 아이템 전체 Bounds를 계산한다.
    private bool TryGetObjectBounds(out Bounds resultBounds)
    {
        bool hasBounds = false;
        resultBounds = new Bounds(transform.position, Vector3.zero);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                resultBounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                resultBounds.Encapsulate(colliders[i].bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                resultBounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                resultBounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }

    // 아이템 표시 이름을 반환한다.
    private string GetItemDisplayName()
    {
        switch (itemType)
        {
            case ItemType.Camera:
                return InGameLocalization.ItemName(itemType);

            case ItemType.Knife:
                return InGameLocalization.ItemName(itemType);

            case ItemType.Medkit:
                return InGameLocalization.ItemName(itemType);

            default:
                return InGameLocalization.ItemName(itemType);
        }
    }
}
