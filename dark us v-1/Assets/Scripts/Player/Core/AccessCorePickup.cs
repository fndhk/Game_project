using UnityEngine;

// 플레이어가 주워서 Terminal에 넣는 연구소 탈출 아이템이다.
public class AccessCorePickup : MonoBehaviour, IPlayerInteractable
{
    [Header("Pickup")]
    // 한 번 주웠을 때 증가할 Access Core 개수이다.
    public int coreAmount = 1;

    // 주운 뒤 오브젝트를 비활성화할지 정한다.
    public bool hideAfterPickup = true;

    // 이미 획득되었는지 저장한다.
    private bool collected = false;

    // 상호작용 문구를 반환한다.
    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        return "[E] Take Access Core";
    }

    // 아직 먹지 않은 상태면 상호작용 가능하다.
    public bool CanInteract(PlayerObjectiveInteractor interactor)
    {
        return !collected;
    }

    // Access Core를 획득한다.
    public void Interact(PlayerObjectiveInteractor interactor)
    {
        if (collected)
        {
            return;
        }

        collected = true;

        if (LabObjectiveManager.Instance != null)
        {
            LabObjectiveManager.Instance.CollectCore(coreAmount);
        }

        if (hideAfterPickup)
        {
            gameObject.SetActive(false);
        }
    }
}
