// 방의 종류를 구분하기 위한 enum이다.
// 1차 버전에서는 크게 많이 쓰지 않지만, 나중에 방 비율과 목표방 배치에 사용한다.
public enum ModularRoomType
{
    StartRoom,
    NormalRoom,
    Corridor,
    HubRoom,
    ObjectiveRoom,
    ExitRoom
}