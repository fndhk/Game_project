using UnityEngine;
using TMPro;

// 플레이어 시점에서 E키 상호작용을 처리한다.
// 기존 클릭 상호작용과 새 길게 누르기 상호작용을 모두 지원한다.
public class PlayerObjectiveInteractor : MonoBehaviour
{
    [Header("References")]
    // 기준 카메라이다. 비어 있으면 자식 카메라 또는 Camera.main을 찾는다.
    public Camera playerCamera;

    // 화면 중앙 아래에 표시할 상호작용 문구이다.
    public TMP_Text promptText;

    [Header("Interaction")]
    // 상호작용 가능한 최대 거리이다.
    public float interactDistance = 2.4f;

    // 상호작용 가능한 레이어이다. 기본값은 모든 레이어이다.
    public LayerMask interactMask = ~0;

    // 상호작용 키이다.
    public KeyCode interactKey = KeyCode.E;

    // 현재 바라보고 있는 상호작용 대상이다.
    private IPlayerInteractable currentTarget;

    // 현재 길게 누르기 중인 대상이다.
    private IPlayerHoldInteractable currentHoldTarget;

    // 현재 길게 누르기 상호작용 중인지 저장한다.
    private bool isHoldingInteraction = false;

    // 필요한 참조를 자동으로 찾는다.
    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    // 비활성화될 때 진행 중인 길게 누르기 상호작용을 취소한다.
    private void OnDisable()
    {
        CancelActiveHold();
        SetPrompt(string.Empty);
    }

    // 매 프레임 바라보는 대상과 입력을 처리한다.
    private void Update()
    {
        UpdateCurrentTarget();
        HandleInteractInput();
        RefreshCurrentPrompt();
    }

    // 화면 중앙 레이캐스트로 상호작용 대상을 찾는다.
    private void UpdateCurrentTarget()
    {
        IPlayerInteractable previousTarget = currentTarget;
        currentTarget = null;

        if (playerCamera == null)
        {
            CancelActiveHold();
            SetPrompt(string.Empty);
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            CancelActiveHold();
            SetPrompt(string.Empty);
            return;
        }

        currentTarget = FindInteractable(hit.collider);

        if (currentTarget == null || !currentTarget.CanInteract(this))
        {
            CancelActiveHold();
            SetPrompt(string.Empty);
            currentTarget = null;
            return;
        }

        // 길게 누르는 중에 다른 대상을 보게 되면 기존 상호작용을 취소한다.
        if (isHoldingInteraction && previousTarget != currentTarget)
        {
            CancelActiveHold();
        }
    }

    // Collider의 부모 방향에서 IPlayerInteractable 구현체를 찾는다.
    private IPlayerInteractable FindInteractable(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlayerInteractable interactable)
            {
                return interactable;
            }
        }

        return null;
    }

    // E키 입력을 처리한다.
    private void HandleInteractInput()
    {
        if (currentTarget == null)
        {
            return;
        }

        // 길게 누르기 상호작용을 지원하는 대상이면 별도 흐름으로 처리한다.
        IPlayerHoldInteractable holdTarget = currentTarget as IPlayerHoldInteractable;

        if (holdTarget != null)
        {
            HandleHoldInteractInput(holdTarget);
            return;
        }

        // 일반 상호작용은 기존처럼 E키를 한 번 누르면 실행한다.
        if (Input.GetKeyDown(interactKey))
        {
            currentTarget.Interact(this);
            UpdateCurrentTarget();
        }
    }

    // 길게 누르기 상호작용 입력을 처리한다.
    private void HandleHoldInteractInput(IPlayerHoldInteractable holdTarget)
    {
        if (holdTarget == null)
        {
            return;
        }

        // E키를 처음 누르면 길게 누르기 상호작용을 시작한다.
        if (Input.GetKeyDown(interactKey))
        {
            if (holdTarget.CanInteract(this) && holdTarget.BeginHold(this))
            {
                currentHoldTarget = holdTarget;
                isHoldingInteraction = true;
            }
        }

        // E키를 누르고 있는 동안 진행도를 갱신한다.
        if (isHoldingInteraction && currentHoldTarget != null && Input.GetKey(interactKey))
        {
            bool shouldContinue = currentHoldTarget.UpdateHold(this, Time.deltaTime);

            if (!shouldContinue)
            {
                ClearHoldStateOnly();
                UpdateCurrentTarget();
            }
        }

        // E키를 떼면 진행 중인 상호작용을 취소한다.
        if (isHoldingInteraction && Input.GetKeyUp(interactKey))
        {
            CancelActiveHold();
            UpdateCurrentTarget();
        }
    }

    // 현재 대상의 안내 문구를 갱신한다.
    private void RefreshCurrentPrompt()
    {
        if (currentTarget == null)
        {
            return;
        }

        if (!currentTarget.CanInteract(this))
        {
            SetPrompt(string.Empty);
            return;
        }

        SetPrompt(currentTarget.GetPrompt(this));
    }

    // 진행 중인 길게 누르기 상호작용을 취소한다.
    private void CancelActiveHold()
    {
        if (isHoldingInteraction && currentHoldTarget != null)
        {
            currentHoldTarget.CancelHold(this);
        }

        ClearHoldStateOnly();
    }

    // 길게 누르기 상태 변수만 초기화한다.
    private void ClearHoldStateOnly()
    {
        currentHoldTarget = null;
        isHoldingInteraction = false;
    }

    // 상호작용 안내 문구를 표시한다.
    private void SetPrompt(string message)
    {
        if (promptText != null)
        {
            promptText.text = message;
            promptText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
            return;
        }

        if (LabObjectiveManager.Instance != null)
        {
            LabObjectiveManager.Instance.SetPromptText(message);
        }
    }
}
