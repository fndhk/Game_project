using System.Collections;
using UnityEngine;

// Terminal 복구 후 열리는 최종 탈출문이다.
public class EmergencyExitDoor : MonoBehaviour, IPlayerInteractable
{
    [Header("Exit State")]
    // 시작 시 잠겨 있는지 정한다.
    public bool startLocked = true;

    // 탈출구가 열리면 자동으로 문을 열지 정한다.
    public bool openAutomaticallyWhenUnlocked = false;

    [Header("Door Visual")]
    // 실제로 움직일 문 루트이다. 비어 있으면 자기 자신을 움직인다.
    public Transform doorRoot;

    // 문이 열릴 때 이동할 로컬 위치 오프셋이다.
    public Vector3 openLocalPositionOffset = new Vector3(0f, 3f, 0f);

    // 문이 열리는 시간이다.
    public float openDuration = 0.8f;

    // 열렸을 때 충돌체를 끌지 정한다.
    public bool disableCollidersWhenOpened = true;

    // 현재 잠금 상태이다.
    [SerializeField] private bool locked = true;

    // 현재 열림 상태이다.
    [SerializeField] private bool opened = false;

    // 문 닫힌 위치이다.
    private Vector3 closedLocalPosition;

    public bool IsOpen => opened;

    // 초기 상태를 준비한다.
    private void Awake()
    {
        if (doorRoot == null)
        {
            doorRoot = transform;
        }

        closedLocalPosition = doorRoot.localPosition;
        locked = startLocked;
    }

    // 매니저에 탈출문을 등록한다.
    private void Start()
    {
        if (LabObjectiveManager.Instance != null)
        {
            LabObjectiveManager.Instance.RegisterExitDoor(this);
        }
    }

    // 문을 잠금 해제한다.
    public void UnlockDoor()
    {
        locked = false;

        if (openAutomaticallyWhenUnlocked)
        {
            OpenDoor();
        }
    }

    public void ResetDoorState(bool lockedState)
    {
        StopAllCoroutines();

        locked = lockedState;
        opened = false;

        if (doorRoot == null)
        {
            doorRoot = transform;
        }

        doorRoot.localPosition = closedLocalPosition;

        Collider[] childColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; i++)
        {
            if (childColliders[i] != null)
            {
                childColliders[i].enabled = true;
            }
        }
    }

    // 상호작용 문구를 반환한다.
    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        if (locked)
        {
            return T("Exit Locked");
        }

        if (opened)
        {
            PlayerCombatTarget target = interactor != null ? interactor.GetComponentInParent<PlayerCombatTarget>() : null;
            if (target != null && target.role == PlayerRole.Citizen && !target.isDead)
            {
                return "[E] " + T("Escape");
            }

            return T("Escape Route Open");
        }

        return "[E] " + T("Open Exit");
    }

    // 탈출문은 잠겨 있어도 상태 확인 문구를 보여준다.
    public bool CanInteract(PlayerObjectiveInteractor interactor)
    {
        return true;
    }

    // 잠금 해제 후 E를 누르면 문을 연다.
    public void Interact(PlayerObjectiveInteractor interactor)
    {
        if (locked)
        {
            return;
        }

        if (opened)
        {
            PlayerCombatTarget target = interactor != null ? interactor.GetComponentInParent<PlayerCombatTarget>() : null;
            if (target != null && target.role == PlayerRole.Citizen && !target.isDead)
            {
                GameLoopManager.EnsureExists().ReportLocalEscape(target);
            }
            return;
        }

        OpenDoor();
    }

    // 문 열림을 시작한다.
    public void OpenDoor()
    {
        if (opened)
        {
            return;
        }

        opened = true;
        StartCoroutine(OpenDoorRoutine());
    }

    // 문을 부드럽게 연다.
    private IEnumerator OpenDoorRoutine()
    {
        if (doorRoot == null)
        {
            yield break;
        }

        Vector3 start = doorRoot.localPosition;
        Vector3 target = closedLocalPosition + openLocalPositionOffset;
        float elapsed = 0f;

        while (elapsed < openDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = openDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / openDuration);
            doorRoot.localPosition = Vector3.Lerp(start, target, ratio);
            yield return null;
        }

        doorRoot.localPosition = target;

        if (disableCollidersWhenOpened)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();

            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }
    }

    private string T(string key)
    {
        return InGameLocalization.Text(key);
    }
}
