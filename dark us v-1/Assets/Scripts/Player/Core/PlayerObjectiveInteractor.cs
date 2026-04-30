using UnityEngine;
using TMPro;

// 플레이어 시점에서 E키 상호작용을 처리한다.
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

    // 매 프레임 바라보는 대상과 입력을 처리한다.
    private void Update()
    {
        UpdateCurrentTarget();
        HandleInteractInput();
    }

    // 화면 중앙 레이캐스트로 상호작용 대상을 찾는다.
    private void UpdateCurrentTarget()
    {
        currentTarget = null;

        if (playerCamera == null)
        {
            SetPrompt(string.Empty);
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            SetPrompt(string.Empty);
            return;
        }

        currentTarget = FindInteractable(hit.collider);

        if (currentTarget == null || !currentTarget.CanInteract(this))
        {
            SetPrompt(string.Empty);
            return;
        }

        SetPrompt(currentTarget.GetPrompt(this));
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

        if (Input.GetKeyDown(interactKey))
        {
            currentTarget.Interact(this);
            UpdateCurrentTarget();
        }
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
