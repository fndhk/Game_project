using UnityEngine;

namespace ArtNotes.UndergroundLaboratoryGenerator
{
    // 방의 종류를 구분한다.
    public enum FacilityCellType
    {
        StartRoom,
        SmallRoom,
        BigRoom,
        Corridor,
        StairRoom,
        VerticalCorridor,
        MultiFloorRoom
    }

    // 방의 크기 분류를 구분한다.
    public enum FacilityCellSize
    {
        Small,
        Big,
        Corridor
    }

    // 이 프리팹이 몇 층에서 생성될 수 있는지 정한다.
    public enum FacilityFloorRule
    {
        AnyFloor,
        FirstFloorOnly,
        SecondFloorOnly
    }

    [RequireComponent(typeof(BoxCollider))]
    public class Cell : MonoBehaviour
    {
        [Header("Original Generator")]
        [HideInInspector]
        public BoxCollider TriggerBox;

        [Tooltip("방과 방을 연결하는 DoorPoint 오브젝트 목록")]
        public GameObject[] Exits;

        [Header("Generation Setting")]
        [Tooltip("방, 복도, 계단방, 시작방 같은 타입")]
        public FacilityCellType CellType = FacilityCellType.SmallRoom;

        [Tooltip("큰방-큰방 직접 연결 금지에 사용할 크기 분류")]
        public FacilityCellSize CellSize = FacilityCellSize.Small;

        [Tooltip("이 프리팹이 생성 가능한 층")]
        public FacilityFloorRule FloorRule = FacilityFloorRule.AnyFloor;

        [Tooltip("계단방, 1층-2층 연결 복도, 1+2층 방이면 체크")]
        public bool IsVerticalConnector = false;

        [Tooltip("이 프리팹의 최대 생성 개수. 0 이하면 제한 없음")]
        public int MaxSpawnCount = 0;

        [Tooltip("랜덤 선택 가중치. 높을수록 더 자주 선택됨")]
        [Min(1)]
        public int SpawnWeight = 1;

        // 방처럼 취급되는 타입인지 확인한다.
        public bool IsRoomLike
        {
            get
            {
                return CellType == FacilityCellType.SmallRoom ||
                       CellType == FacilityCellType.BigRoom ||
                       CellType == FacilityCellType.MultiFloorRoom ||
                       CellType == FacilityCellType.StairRoom;
            }
        }

        // 복도 타입인지 확인한다.
        public bool IsCorridorLike
        {
            get
            {
                return CellType == FacilityCellType.Corridor ||
                       CellType == FacilityCellType.VerticalCorridor;
            }
        }

        private void Awake()
        {
            CacheTriggerBox();
        }

        private void OnValidate()
        {
            CacheTriggerBox();
        }

        // BoxCollider를 캐싱하고 방 겹침 검사 전용 Trigger로 만든다.
        public void CacheTriggerBox()
        {
            TriggerBox = GetComponent<BoxCollider>();

            if (TriggerBox != null)
            {
                TriggerBox.isTrigger = true;
            }
        }
    }
}