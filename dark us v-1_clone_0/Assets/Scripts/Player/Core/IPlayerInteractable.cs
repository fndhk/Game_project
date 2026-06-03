using UnityEngine;

// 플레이어가 E키로 상호작용할 수 있는 오브젝트가 구현하는 인터페이스이다.
public interface IPlayerInteractable
{
    // 화면에 표시할 상호작용 문구를 반환한다.
    string GetPrompt(PlayerObjectiveInteractor interactor);

    // 지금 상호작용이 가능한 상태인지 반환한다.
    bool CanInteract(PlayerObjectiveInteractor interactor);

    // 실제 상호작용을 실행한다.
    void Interact(PlayerObjectiveInteractor interactor);
}
