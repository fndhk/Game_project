using UnityEngine;

// 월드에 떨어져 있는 아이템이다.
// PlayerObjectiveInteractor가 E키로 이 컴포넌트와 상호작용한다.
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

    // 이미 주웠는지 저장한다.
    private bool collected = false;

    // 상호작용 문구를 반환한다.
    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        return "[E] Take " + GetItemDisplayName() + " x" + Mathf.Max(1, amount);
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
            Debug.Log("Inventory is full.");
            return;
        }

        collected = true;

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

    // 아이템 표시 이름을 반환한다.
    private string GetItemDisplayName()
    {
        switch (itemType)
        {
            case ItemType.Camera:
                return "Camera";

            case ItemType.Knife:
                return "Knife";

            case ItemType.Medkit:
                return "Medkit";

            default:
                return "Item";
        }
    }
}