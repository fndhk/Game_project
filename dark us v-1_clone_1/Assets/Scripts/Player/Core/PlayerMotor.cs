using UnityEngine;

// 이 스크립트는 플레이어의 기본 이동, 달리기, 웅크리기,
// 점프, 중력, 카메라 높이, 카메라 시야각(FOV)을 담당한다.
// 체력과 스테미나는 PlayerStats에서 따로 관리한다.
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("참조")]
    // 플레이어의 눈 역할을 하는 카메라 Transform이다.
    public Transform playerCamera;

    // 체력과 스테미나를 관리하는 PlayerStats이다.
    public PlayerStats playerStats;

    [Header("이동 속도")]
    // 기본 이동 속도이다.
    public float walkSpeed = 1.8f;

    // Shift를 누르고 앞으로 갈 때의 속도이다.
    public float sprintSpeed = 2.7f;

    // Ctrl을 누르고 있을 때의 속도이다.
    public float crouchSpeed = 0.85f;

    // 이동이 시작될 때 목표 속도까지 붙는 속도이다.
    public float acceleration = 10f;

    // 이동을 멈출 때 감속되는 속도이다.
    public float deceleration = 12f;
    
    [Header("달리기 탈진")]
    // 스태미나가 0이 되었을 때 다시 달릴 수 있게 풀어줄 회복 비율이다.
    // 예를 들어 0.25면 현재 체력 기준 최대 스태미나의 25%까지 회복해야 다시 달릴 수 있다.
    public float sprintRecoverUnlockPercent = 0.25f;

    // 현재 탈진 상태인지 저장하는 값이다.
    private bool isSprintExhausted = false;

    [Header("점프 / 중력")]
    // 기본 걷기 상태에서의 점프 높이이다.
    public float walkJumpHeight = 0.9f;

    // 달리기 상태에서의 점프 높이이다.
    public float sprintJumpHeight = 1.05f;

    // 웅크린 상태에서의 점프 높이이다.
    public float crouchJumpHeight = 0.55f;

    // 아래로 끌어당기는 힘이다.
    public float gravity = -20f;

    [Header("카메라 시점")]
    // 서 있을 때 카메라의 로컬 높이이다.
    public float standingCameraHeight = 1.62f;

    // 웅크렸을 때 카메라의 로컬 높이이다.
    public float crouchingCameraHeight = 1.02f;

    // 카메라 높이가 바뀔 때 부드럽게 변하는 속도이다.
    public float cameraHeightSmooth = 10f;

    // 기본 시야각이다.
    public float normalFov = 75f;

    // 달릴 때 살짝 넓어지는 시야각이다.
    public float sprintFov = 78f;

    // 시야각이 바뀔 때 부드럽게 변하는 속도이다.
    public float fovSmooth = 6f;

    [Header("캐릭터 높이")]
    // 서 있을 때 CharacterController 높이이다.
    public float standingControllerHeight = 1.8f;

    // 웅크렸을 때 CharacterController 높이이다.
    public float crouchingControllerHeight = 1.15f;

    // 컨트롤러 높이가 부드럽게 바뀌는 속도이다.
    public float controllerHeightSmooth = 10f;

    // CharacterController를 저장하는 변수이다.
    private CharacterController controller;

    // Camera 컴포넌트를 저장하는 변수이다.
    private Camera cameraComponent;

    // 현재 수평 이동 속도를 저장하는 변수이다.
    private Vector3 horizontalVelocity;

    // 수직 방향 속도를 저장하는 변수이다.
    private float verticalVelocity;

    // 현재 웅크리고 있는지 저장하는 변수이다.
    private bool isCrouching = false;

    // 현재 바닥에 닿아 있는지 저장하는 변수이다.
    private bool isGrounded = false;

    // 시작할 때 필요한 컴포넌트를 가져오고 초기값을 맞춘다.
    private void Start()
    {
        // 같은 오브젝트에 붙어 있는 CharacterController를 가져온다.
        controller = GetComponent<CharacterController>();

        // 카메라가 직접 지정되지 않았다면 Main Camera를 찾아 연결한다.
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        // PlayerStats가 직접 지정되지 않았다면 자동으로 가져온다.
        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        // 카메라가 연결되어 있으면 Camera 컴포넌트도 가져온다.
        if (playerCamera != null)
        {
            cameraComponent = playerCamera.GetComponent<Camera>();
        }

        // 시작 시 높이를 서 있는 값으로 맞춘다.
        controller.height = standingControllerHeight;

        // 컨트롤러 중심도 높이에 맞게 설정한다.
        controller.center = new Vector3(0f, standingControllerHeight / 2f, 0f);

        // 카메라가 있으면 시작 위치를 눈높이에 맞춘다.
        if (playerCamera != null)
        {
            Vector3 cameraLocalPos = playerCamera.localPosition;
            cameraLocalPos.y = standingCameraHeight;
            playerCamera.localPosition = cameraLocalPos;
        }

        // 카메라가 있으면 시작 시야각을 기본값으로 맞춘다.
        if (cameraComponent != null)
        {
            cameraComponent.fieldOfView = normalFov;
        }
    }

    // 매 프레임 입력, 이동, 점프, 중력, 시점 처리를 진행한다.
    private void Update()
    {
        // 현재 스태미나를 기준으로 탈진 상태를 먼저 갱신한다.
        UpdateSprintExhaustedState();

        // 현재 바닥 상태를 먼저 갱신한다.
        UpdateGroundedState();

        // 웅크리기 입력을 먼저 처리한다.
        HandleCrouchInput();

        // 점프 입력을 처리한다.
        HandleJump();

        // 중력을 처리한다.
        HandleGravity();

        // 수평 이동과 스테미나 소모를 처리한다.
        HandleHorizontalMovement();

        // 캐릭터 높이와 카메라 높이, 시야각을 갱신한다.
        UpdateBodyAndCamera();
    }

    // 현재 바닥에 닿아 있는지 갱신하는 함수이다.
    private void UpdateGroundedState()
    {
        // CharacterController의 grounded 상태를 그대로 저장한다.
        isGrounded = controller.isGrounded;
    }

    // Ctrl 입력을 확인해서 웅크리기 상태를 바꾸는 함수이다.
    private void HandleCrouchInput()
    {
        // 좌/우 Ctrl 중 하나라도 누르고 있으면 웅크린다.
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            isCrouching = true;
        }
        else
        {
            // Ctrl을 떼면 다시 서 있는 상태로 바꾼다.
            isCrouching = false;
        }
    }

    // 현재 스태미나 값에 따라 탈진 상태를 갱신하는 함수이다.
    private void UpdateSprintExhaustedState()
    {
        // PlayerStats가 없으면 탈진 상태를 쓸 수 없으므로 false로 둔다.
        if (playerStats == null)
        {
            isSprintExhausted = false;
            return;
        }

        // 체력이 0이면 달릴 수 없으므로 탈진 상태로 둔다.
        if (playerStats.currentHealth <= 0f)
        {
            isSprintExhausted = true;
            return;
        }

        // 스태미나가 거의 0이면 탈진 상태로 진입시킨다.
        if (playerStats.currentStamina <= 0.01f)
        {
            isSprintExhausted = true;
        }

        // 현재 체력 기준 최대 스태미나의 몇 퍼센트까지 회복하면 다시 달릴지 계산한다.
        float recoverUnlockValue = playerStats.currentHealth * sprintRecoverUnlockPercent;

        // 이미 탈진 상태이고, 충분히 회복했다면 탈진 상태를 해제한다.
        if (isSprintExhausted && playerStats.currentStamina >= recoverUnlockValue)
        {
            isSprintExhausted = false;
        }
    }

    // 현재 실제로 달릴 수 있는 상태인지 반환하는 함수이다.
    private bool IsTryingToSprint()
    {
        // 앞쪽 입력을 받는지 확인한다.
        float z = Input.GetAxisRaw("Vertical");

        // PlayerStats가 없으면 기본적으로 달리기 가능으로 본다.
        // PlayerStats가 있으면 스태미나가 남아 있고 탈진 상태가 아닐 때만 달리기 가능하다.
        bool hasSprintResource = playerStats == null || (playerStats.CanSprint() && !isSprintExhausted);

        // 조건을 모두 만족하면 달리기 상태로 본다.
        return
            !isCrouching &&
            z > 0f &&
            hasSprintResource &&
            (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
    }

    // 현재 상태에 맞는 점프 높이를 반환하는 함수이다.
    private float GetCurrentJumpHeight()
    {
        // 웅크린 상태면 가장 낮은 점프를 사용한다.
        if (isCrouching)
        {
            return crouchJumpHeight;
        }

        // 달리기 상태면 기본보다 약간 더 높은 점프를 사용한다.
        if (IsTryingToSprint())
        {
            return sprintJumpHeight;
        }

        // 나머지는 기본 점프를 사용한다.
        return walkJumpHeight;
    }

    // Space 입력으로 점프를 처리하는 함수이다.
    private void HandleJump()
    {
        // 바닥에 서 있고 Space를 누른 순간에만 점프를 시작한다.
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            // 현재 상태에 맞는 점프 높이를 가져온다.
            float jumpHeight = GetCurrentJumpHeight();

            // 점프 공식으로 위쪽 초기 속도를 만든다.
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    // WASD 이동, Shift 달리기, 스테미나 소모/회복을 처리하는 함수이다.
    private void HandleHorizontalMovement()
    {
        // 좌우 이동 입력을 받는다.
        float x = Input.GetAxisRaw("Horizontal");

        // 앞뒤 이동 입력을 받는다.
        float z = Input.GetAxisRaw("Vertical");

        // 플레이어 기준의 이동 방향을 만든다.
        Vector3 inputDirection = transform.right * x + transform.forward * z;

        // 대각선 이동이 더 빨라지지 않도록 길이를 1 이하로 보정한다.
        inputDirection = Vector3.ClampMagnitude(inputDirection, 1f);

        // 기본 목표 속도는 걷기 속도로 시작한다.
        float targetSpeed = walkSpeed;

        // 현재 달리기 상태인지 저장한다.
        bool isSprinting = false;

        // 웅크리고 있으면 웅크리기 속도를 사용한다.
        if (isCrouching)
        {
            targetSpeed = crouchSpeed;
        }
        else
        {
            // 실제로 달리기 가능한 상태면 달리기 속도를 사용한다.
            if (IsTryingToSprint())
            {
                targetSpeed = sprintSpeed;
                isSprinting = true;
            }
        }

        // PlayerStats가 있으면 달리기 중에는 스테미나만 소모하고,
        // 달리지 않으면 스테미나만 회복한다.
        if (playerStats != null)
        {
            // 실제 이동 입력이 있고 실제로 달릴 때만 스테미나를 소모한다.
            if (isSprinting && inputDirection.sqrMagnitude > 0.0001f)
            {
                playerStats.DrainStaminaForSprint(Time.deltaTime);
            }
            else
            {
                // 달리지 않으면 스테미나를 회복한다.
                playerStats.RecoverStamina(Time.deltaTime);
            }
            // 방금 소모/회복된 값을 기준으로 탈진 상태를 한 번 더 갱신한다.
            UpdateSprintExhaustedState();
        }

        // 최종 수평 목표 속도를 만든다.
        Vector3 targetHorizontalVelocity = inputDirection * targetSpeed;

        // 입력이 있을 때는 더 부드럽게 가속하고, 없으면 감속한다.
        float moveRate = inputDirection.sqrMagnitude > 0.0001f ? acceleration : deceleration;

        // 현재 속도를 목표 속도 쪽으로 천천히 보낸다.
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetHorizontalVelocity,
            moveRate * Time.deltaTime
        );

        // 수평 이동과 수직 이동을 합쳐 최종 이동 벡터를 만든다.
        Vector3 finalMove = new Vector3(
            horizontalVelocity.x,
            verticalVelocity,
            horizontalVelocity.z
        );

        // CharacterController로 실제 이동을 적용한다.
        controller.Move(finalMove * Time.deltaTime);
    }

    // 플레이어가 바닥에 붙어 있도록 중력을 처리하는 함수이다.
    private void HandleGravity()
    {
        // 바닥에 닿아 있고 아래 방향 속도가 있다면 살짝 바닥 쪽으로 고정한다.
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // 중력을 계속 누적해서 자연스럽게 떨어지게 만든다.
        verticalVelocity += gravity * Time.deltaTime;
    }

    // 컨트롤러 높이, 카메라 높이, 시야각을 부드럽게 갱신하는 함수이다.
    private void UpdateBodyAndCamera()
    {
        // 현재 상태에 따라 목표 컨트롤러 높이를 정한다.
        float targetControllerHeight = isCrouching ? crouchingControllerHeight : standingControllerHeight;

        // 컨트롤러 높이를 부드럽게 변경한다.
        controller.height = Mathf.Lerp(
            controller.height,
            targetControllerHeight,
            controllerHeightSmooth * Time.deltaTime
        );

        // 높이에 맞게 컨트롤러 중심도 같이 갱신한다.
        controller.center = Vector3.Lerp(
            controller.center,
            new Vector3(0f, controller.height / 2f, 0f),
            controllerHeightSmooth * Time.deltaTime
        );

        // 카메라가 있다면 눈높이도 상태에 맞게 바꾼다.
        if (playerCamera != null)
        {
            // 서 있는 높이와 웅크린 높이 중 현재 목표를 정한다.
            float targetCameraHeight = isCrouching ? crouchingCameraHeight : standingCameraHeight;

            // 현재 카메라 위치를 가져온다.
            Vector3 cameraLocalPos = playerCamera.localPosition;

            // y값만 부드럽게 변경해서 눈높이가 자연스럽게 움직이도록 한다.
            cameraLocalPos.y = Mathf.Lerp(
                cameraLocalPos.y,
                targetCameraHeight,
                cameraHeightSmooth * Time.deltaTime
            );

            // 변경한 위치를 다시 적용한다.
            playerCamera.localPosition = cameraLocalPos;
        }

        // 카메라가 있다면 상태에 따라 시야각도 조금 바꿔준다.
        if (cameraComponent != null)
        {
            // 기본 목표 시야각은 normalFov이다.
            float targetFov = normalFov;

            // 달리는 중이면 살짝 넓어진 시야각을 사용한다.
            if (IsTryingToSprint())
            {
                targetFov = sprintFov;
            }

            // 시야각을 부드럽게 바꿔서 갑작스럽지 않게 만든다.
            cameraComponent.fieldOfView = Mathf.Lerp(
                cameraComponent.fieldOfView,
                targetFov,
                fovSmooth * Time.deltaTime
            );
        }
    }
}