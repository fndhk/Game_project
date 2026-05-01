// E키를 길게 눌러 진행되는 상호작용이 필요할 때 사용하는 선택 인터페이스이다.
// 기존 IPlayerInteractable은 그대로 유지하고, 필요한 오브젝트만 이 인터페이스를 추가로 구현한다.
public interface IPlayerHoldInteractable : IPlayerInteractable
{
    // 길게 누르기 상호작용을 시작한다.
    bool BeginHold(PlayerObjectiveInteractor interactor);

    // 길게 누르고 있는 동안 매 프레임 호출된다.
    // false를 반환하면 현재 길게 누르기 상태를 종료한다.
    bool UpdateHold(PlayerObjectiveInteractor interactor, float deltaTime);

    // 키를 떼거나 시야에서 벗어났을 때 상호작용을 취소한다.
    void CancelHold(PlayerObjectiveInteractor interactor);
}
