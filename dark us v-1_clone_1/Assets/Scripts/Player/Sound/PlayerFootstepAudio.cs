using UnityEngine;

// 이 스크립트는 플레이어가 실제로 이동한 거리를 기준으로
// 발소리를 자동 재생한다.
// PlayerMotor를 직접 수정하지 않고 별도 컴포넌트로 붙여서 사용한다.
[RequireComponent(typeof(AudioSource))]
public class PlayerFootstepAudio : MonoBehaviour
{
    [Header("참조")]
    // 실제로 움직이는 플레이어 루트 Transform이다.
    public Transform playerRoot;

    // 플레이어의 이동 속도 정보를 읽기 위한 PlayerMotor이다.
    public PlayerMotor playerMotor;

    // 바닥 판정과 높이 정보를 읽기 위한 CharacterController이다.
    public CharacterController characterController;

    [Header("바닥 판정")]
    // CharacterController.isGrounded가 애매할 때 보조로 사용할 레이어 마스크이다.
    public LayerMask groundMask = ~0;

    // 아래쪽으로 바닥을 확인할 거리이다.
    public float groundCheckDistance = 0.35f;

    // 필요할 때 레이캐스트 바닥 판정을 같이 사용할지 정한다.
    public bool useGroundRaycastFallback = true;

    [Header("발소리 클립")]
    // 공용 발소리 배열이다.
    // 지금처럼 Grass 사운드만 있을 때 여기에 전부 넣으면 된다.
    public AudioClip[] commonClips;

    // 걷기 전용 발소리 배열이다.
    // 비워두면 commonClips를 대신 사용한다.
    public AudioClip[] walkClips;

    // 달리기 전용 발소리 배열이다.
    // 비워두면 commonClips를 대신 사용한다.
    public AudioClip[] sprintClips;

    // 웅크리기 전용 발소리 배열이다.
    // 비워두면 commonClips를 대신 사용한다.
    public AudioClip[] crouchClips;

    [Header("이동 판정")]
    // 너무 작은 흔들림에는 발소리가 나지 않게 막는 최소 속도이다.
    public float minimumMoveSpeed = 0.15f;

    [Header("걸음 간격")]
    // 걷기 상태에서 발소리가 한 번 날 때 필요한 이동 거리이다.
    public float walkStepDistance = 0.85f;

    // 달리기 상태에서 발소리가 한 번 날 때 필요한 이동 거리이다.
    public float sprintStepDistance = 0.95f;

    // 웅크리기 상태에서 발소리가 한 번 날 때 필요한 이동 거리이다.
    public float crouchStepDistance = 0.60f;

    [Header("볼륨")]
    // 걷기 볼륨 배수이다.
    public float walkVolume = 0.90f;

    // 달리기 볼륨 배수이다.
    public float sprintVolume = 1.00f;

    // 웅크리기 볼륨 배수이다.
    public float crouchVolume = 0.55f;

    [Header("피치 랜덤")]
    // 발소리가 매번 완전히 똑같지 않게 살짝 바꿔줄 최소 피치이다.
    public float minPitch = 0.96f;

    // 발소리가 매번 완전히 똑같지 않게 살짝 바꿔줄 최대 피치이다.
    public float maxPitch = 1.04f;

    // 이 오브젝트의 AudioSource이다.
    private AudioSource audioSource;

    // 이전 프레임의 플레이어 위치이다.
    private Vector3 lastRootPosition;

    // 현재까지 누적된 이동 거리이다.
    private float accumulatedDistance = 0f;

    // 직전에 재생한 클립 인덱스이다.
    private int lastPlayedClipIndex = -1;

    // 현재 이동 상태를 구분하기 위한 열거형이다.
    private enum FootstepState
    {
        Walk,
        Sprint,
        Crouch
    }

    // 시작 전에 필요한 참조를 자동으로 잡는다.
    private void Awake()
    {
        // 같은 오브젝트의 AudioSource를 가져온다.
        audioSource = GetComponent<AudioSource>();

        // playerRoot가 비어 있으면 부모 오브젝트를 우선 사용한다.
        if (playerRoot == null)
        {
            if (transform.parent != null)
            {
                playerRoot = transform.parent;
            }
            else
            {
                playerRoot = transform;
            }
        }

        // PlayerMotor가 비어 있으면 playerRoot에서 찾아 연결한다.
        if (playerMotor == null && playerRoot != null)
        {
            playerMotor = playerRoot.GetComponent<PlayerMotor>();
        }

        // CharacterController가 비어 있으면 playerRoot에서 찾아 연결한다.
        if (characterController == null && playerRoot != null)
        {
            characterController = playerRoot.GetComponent<CharacterController>();
        }

        // AudioSource 기본값을 발소리용으로 정리한다.
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 1f;
            audioSource.dopplerLevel = 0f;
        }
    }

    // 활성화될 때 현재 위치를 기준점으로 저장한다.
    private void OnEnable()
    {
        if (playerRoot != null)
        {
            lastRootPosition = playerRoot.position;
        }

        // 누적 이동 거리와 마지막 재생 인덱스를 초기화한다.
        accumulatedDistance = 0f;
        lastPlayedClipIndex = -1;
    }

    // 매 프레임 실제 이동량을 읽어서 발소리를 처리한다.
    private void Update()
    {
        // 참조가 없으면 더 진행하지 않는다.
        if (playerRoot == null || audioSource == null)
        {
            return;
        }

        // 현재 위치를 가져온다.
        Vector3 currentPosition = playerRoot.position;

        // 수직 이동은 제외하고 수평 이동만 계산한다.
        Vector3 horizontalDelta = currentPosition - lastRootPosition;
        horizontalDelta.y = 0f;

        // 이번 프레임 수평 이동 거리와 속도를 계산한다.
        float movedDistance = horizontalDelta.magnitude;
        float moveSpeed = movedDistance / Mathf.Max(Time.deltaTime, 0.0001f);

        // 다음 프레임 계산을 위해 현재 위치를 저장한다.
        lastRootPosition = currentPosition;

        // 바닥에 없거나 너무 느리면 발소리 누적을 끊는다.
        if (!IsGrounded() || moveSpeed < minimumMoveSpeed)
        {
            accumulatedDistance = 0f;
            return;
        }

        // 현재 상태를 걷기/달리기/웅크리기로 판정한다.
        FootstepState currentState = GetCurrentFootstepState(moveSpeed);

        // 이동 거리를 누적한다.
        accumulatedDistance += movedDistance;

        // 현재 상태에 필요한 걸음 거리 기준을 가져온다.
        float requiredStepDistance = GetRequiredStepDistance(currentState);

        // 누적 거리가 기준을 넘으면 발소리를 한 번 재생한다.
        if (accumulatedDistance >= requiredStepDistance)
        {
            accumulatedDistance -= requiredStepDistance;
            PlayFootstep(currentState);
        }
    }

    // 현재 바닥에 닿아 있는지 확인한다.
    private bool IsGrounded()
    {
        // CharacterController가 있으면 우선 그 값을 사용한다.
        if (characterController != null && characterController.enabled)
        {
            if (characterController.isGrounded)
            {
                return true;
            }
        }

        // 보조 레이캐스트를 쓰지 않으면 여기서 false를 반환한다.
        if (!useGroundRaycastFallback || playerRoot == null)
        {
            return false;
        }

        // 플레이어 중심보다 살짝 위에서 아래로 레이캐스트를 쏜다.
        Vector3 rayOrigin = playerRoot.position + Vector3.up * 0.1f;

        // 지정한 레이어를 기준으로 바닥 여부를 확인한다.
        return Physics.Raycast(
            rayOrigin,
            Vector3.down,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    // 현재 이동 속도와 높이를 기준으로 상태를 판정한다.
    private FootstepState GetCurrentFootstepState(float moveSpeed)
    {
        // PlayerMotor나 CharacterController가 없으면 기본은 걷기로 본다.
        if (playerMotor == null || characterController == null)
        {
            return FootstepState.Walk;
        }

        // 현재 컨트롤러 높이가 웅크린 높이에 가까우면 웅크리기로 본다.
        float crouchMiddleHeight =
            (playerMotor.standingControllerHeight + playerMotor.crouchingControllerHeight) * 0.5f;

        if (characterController.height <= crouchMiddleHeight)
        {
            return FootstepState.Crouch;
        }

        // 이동 속도가 걷기보다 충분히 빠르면 달리기로 본다.
        float sprintThreshold = Mathf.Lerp(playerMotor.walkSpeed, playerMotor.sprintSpeed, 0.55f);

        if (moveSpeed >= sprintThreshold)
        {
            return FootstepState.Sprint;
        }

        // 그 외는 걷기로 본다.
        return FootstepState.Walk;
    }

    // 현재 상태에 맞는 걸음 거리 기준을 반환한다.
    private float GetRequiredStepDistance(FootstepState state)
    {
        // 상태별로 다른 간격을 돌려준다.
        switch (state)
        {
            case FootstepState.Sprint:
                return sprintStepDistance;

            case FootstepState.Crouch:
                return crouchStepDistance;

            default:
                return walkStepDistance;
        }
    }

    // 현재 상태에 맞는 볼륨 배수를 반환한다.
    private float GetVolume(FootstepState state)
    {
        // 상태별로 다른 볼륨을 돌려준다.
        switch (state)
        {
            case FootstepState.Sprint:
                return sprintVolume;

            case FootstepState.Crouch:
                return crouchVolume;

            default:
                return walkVolume;
        }
    }

    // 현재 상태에 맞는 발소리 배열을 반환한다.
    private AudioClip[] GetClipArray(FootstepState state)
    {
        // 상태별 전용 배열이 있으면 우선 사용한다.
        switch (state)
        {
            case FootstepState.Sprint:
                if (sprintClips != null && sprintClips.Length > 0)
                {
                    return sprintClips;
                }
                break;

            case FootstepState.Crouch:
                if (crouchClips != null && crouchClips.Length > 0)
                {
                    return crouchClips;
                }
                break;

            default:
                if (walkClips != null && walkClips.Length > 0)
                {
                    return walkClips;
                }
                break;
        }

        // 전용 배열이 비어 있으면 공용 배열을 사용한다.
        return commonClips;
    }

    // 발소리를 실제로 한 번 재생한다.
    private void PlayFootstep(FootstepState state)
    {
        // 상태에 맞는 클립 배열을 가져온다.
        AudioClip[] clips = GetClipArray(state);

        // 배열이 비어 있으면 재생하지 않는다.
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        // 랜덤 인덱스를 뽑는다.
        int clipIndex = Random.Range(0, clips.Length);

        // 클립이 2개 이상이면 직전과 같은 소리는 피하려고 한 칸 밀어준다.
        if (clips.Length > 1 && clipIndex == lastPlayedClipIndex)
        {
            clipIndex = (clipIndex + 1) % clips.Length;
        }

        // 최종 재생할 클립을 가져온다.
        AudioClip selectedClip = clips[clipIndex];

        // 직전 인덱스를 저장한다.
        lastPlayedClipIndex = clipIndex;

        // 피치를 조금 랜덤하게 바꿔서 반복감을 줄인다.
        audioSource.pitch = Random.Range(minPitch, maxPitch);

        // 현재 상태에 맞는 볼륨으로 One Shot 재생한다.
        audioSource.PlayOneShot(selectedClip, GetVolume(state));
    }
}