using UnityEngine;
using TMPro;

// 플레이어의 간단한 2칸 인벤토리를 관리한다.
// 1번, 2번 키로 슬롯을 선택하고,
// 아이템 획득/소모/표시를 담당한다.
public class PlayerInventory : MonoBehaviour
{
    [System.Serializable]
    public class ItemSlot
    {
        // 슬롯에 들어있는 아이템 종류이다.
        public ItemType itemType = ItemType.None;

        // 해당 아이템의 사용 가능 횟수 또는 개수이다.
        public int amount = 0;
    }

    [Header("Slots")]
    // 현재는 2칸 인벤토리만 사용한다.
    public ItemSlot[] slots = new ItemSlot[2]
    {
        new ItemSlot(),
        new ItemSlot()
    };

    [Header("Input")]
    // 1번 슬롯 선택 키이다.
    public KeyCode slot1Key = KeyCode.Alpha1;

    // 2번 슬롯 선택 키이다.
    public KeyCode slot2Key = KeyCode.Alpha2;

    [Header("Optional UI")]
    // 1번 슬롯 텍스트이다. 비워도 기능은 동작한다.
    public TMP_Text slot1Text;

    // 2번 슬롯 텍스트이다. 비워도 기능은 동작한다.
    public TMP_Text slot2Text;

    // 비어 있는 슬롯에 표시할 문구이다.
    public string emptySlotLabel = "-";

    // 현재 선택된 슬롯 번호이다.
    // 0이면 1번 슬롯, 1이면 2번 슬롯이다.
    [SerializeField] private int selectedSlotIndex = 0;

    // 외부에서 현재 선택 슬롯을 읽을 수 있게 한다.
    public int SelectedSlotIndex => selectedSlotIndex;

    private void Awake()
    {
        // 슬롯 배열이 비정상이어도 2칸으로 보정한다.
        ValidateSlots();

        // 시작 시 UI를 한 번 갱신한다.
        RefreshSlotUi();
    }

    private void Update()
    {
        // 1, 2번 슬롯 선택 입력을 처리한다.
        HandleSlotInput();
    }

    // 슬롯 배열을 안전하게 2칸으로 보정한다.
    private void ValidateSlots()
    {
        if (slots == null || slots.Length != 2)
        {
            slots = new ItemSlot[2];
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = new ItemSlot();
            }

            if (slots[i].amount <= 0)
            {
                slots[i].itemType = ItemType.None;
                slots[i].amount = 0;
            }
        }

        selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slots.Length - 1);
    }

    // 1번, 2번 입력으로 선택 슬롯을 바꾼다.
    private void HandleSlotInput()
    {
        if (Input.GetKeyDown(slot1Key))
        {
            SelectSlot(0);
            return;
        }

        if (Input.GetKeyDown(slot2Key))
        {
            SelectSlot(1);
            return;
        }
    }

    // 특정 슬롯을 선택한다.
    public void SelectSlot(int slotIndex)
    {
        ValidateSlots();

        selectedSlotIndex = Mathf.Clamp(slotIndex, 0, slots.Length - 1);
        RefreshSlotUi();
    }

    // 현재 선택된 슬롯의 아이템 타입을 반환한다.
    public ItemType GetSelectedItemType()
    {
        ValidateSlots();

        ItemSlot slot = slots[selectedSlotIndex];
        return slot.amount > 0 ? slot.itemType : ItemType.None;
    }

    // 현재 선택된 슬롯의 아이템 개수를 반환한다.
    public int GetSelectedItemAmount()
    {
        ValidateSlots();

        return slots[selectedSlotIndex].amount;
    }

    // 아이템을 인벤토리에 넣는다.
    public bool TryAddItem(ItemType itemType, int amount)
    {
        ValidateSlots();

        if (itemType == ItemType.None)
        {
            return false;
        }

        if (amount <= 0)
        {
            return false;
        }

        // 같은 아이템이 이미 있으면 그 슬롯에 개수를 더한다.
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].itemType == itemType && slots[i].amount > 0)
            {
                slots[i].amount += amount;
                RefreshSlotUi();
                return true;
            }
        }

        // 비어 있는 슬롯을 찾아 새 아이템을 넣는다.
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].itemType == ItemType.None || slots[i].amount <= 0)
            {
                slots[i].itemType = itemType;
                slots[i].amount = amount;
                RefreshSlotUi();
                return true;
            }
        }

        // 2칸이 모두 차 있으면 획득 실패이다.
        return false;
    }

    // 현재 선택된 아이템을 amount만큼 소모한다.
    public bool ConsumeSelectedItem(int amount)
    {
        ValidateSlots();

        if (amount <= 0)
        {
            return false;
        }

        ItemSlot slot = slots[selectedSlotIndex];

        if (slot.itemType == ItemType.None || slot.amount <= 0)
        {
            return false;
        }

        slot.amount -= amount;

        if (slot.amount <= 0)
        {
            slot.itemType = ItemType.None;
            slot.amount = 0;
        }

        RefreshSlotUi();
        return true;
    }

    // 아이템 이름을 UI 표시용 문자열로 바꾼다.
    public string GetItemDisplayName(ItemType itemType)
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
                return emptySlotLabel;
        }
    }

    // 슬롯 UI를 갱신한다.
    public void RefreshSlotUi()
    {
        ValidateSlots();

        SetSlotText(slot1Text, 0);
        SetSlotText(slot2Text, 1);
    }

    // 슬롯 하나의 텍스트를 갱신한다.
    private void SetSlotText(TMP_Text targetText, int slotIndex)
    {
        if (targetText == null)
        {
            return;
        }

        ItemSlot slot = slots[slotIndex];

        string prefix = slotIndex == 0 ? "[1] " : "[2] ";

        if (slot.itemType == ItemType.None || slot.amount <= 0)
        {
            targetText.text = prefix + emptySlotLabel;
            return;
        }

        targetText.text = prefix + GetItemDisplayName(slot.itemType) + " x" + slot.amount;
    }
}