using UnityEngine;

// 이 스크립트는 CharacterController의 실제 이동 속도를 읽어서
// Animator의 speed 파라미터에 넣어주는 역할을 한다.
// Idle / Walk / Run 상태 전환을 자동으로 만들기 위해 사용한다.
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animator 설정")]
    // Animator에서 사용할 속도 파라미터 이름이다.
    // 지금 네 Animator에는 소문자 speed로 만들었으니 그대로 맞춘다.
    public string speedParameterName = "speed";

    [Header("참조")]
    // 실제 이동 속도를 읽어올 CharacterController이다.
    // 보통 플레이어 루트에 붙어 있다.
    public CharacterController characterController;

    // 이동 속도 기준값을 읽기 위한 PlayerMotor이다.
    // sprintSpeed를 기준으로 0~1 범위 속도를 계산할 때 사용한다.
    public PlayerMotor playerMotor;

    [Header("보간")]
    // speed 값이 너무 딱딱 바뀌지 않게 부드럽게 만드는 시간이다.
    public float speedDampTime = 0.08f;

    // Animator를 저장하는 변수이다.
    private Animator animator;

    // speed 파라미터 해시값이다.
    private int speedHash;

    // 시작 전에 필요한 참조를 자동으로 가져온다.
    private void Awake()
    {
        // 같은 오브젝트의 Animator를 가져온다.
        animator = GetComponent<Animator>();

        // 직접 지정하지 않았으면 부모 쪽에서 CharacterController를 찾는다.
        if (characterController == null)
        {
            characterController = GetComponentInParent<CharacterController>();
        }

        // 직접 지정하지 않았으면 부모 쪽에서 PlayerMotor를 찾는다.
        if (playerMotor == null)
        {
            playerMotor = GetComponentInParent<PlayerMotor>();
        }

        // 문자열 대신 해시를 써서 조금 더 안정적으로 접근한다.
        speedHash = Animator.StringToHash(speedParameterName);
    }

    // 이동이 끝난 뒤의 실제 속도를 읽는 편이 더 자연스러우므로 LateUpdate를 사용한다.
    private void LateUpdate()
    {
        // Animator가 없으면 더 진행하지 않는다.
        if (animator == null)
        {
            return;
        }

        // CharacterController가 없으면 속도를 읽을 수 없으므로 중단한다.
        if (characterController == null)
        {
            return;
        }

        // 현재 월드 기준 속도를 가져온다.
        Vector3 worldVelocity = characterController.velocity;

        // 수직 속도는 빼고, 바닥 위에서의 이동 속도만 사용한다.
        Vector3 planarVelocity = new Vector3(worldVelocity.x, 0f, worldVelocity.z);

        // 현재 실제 수평 이동 속도를 계산한다.
        float currentPlanarSpeed = planarVelocity.magnitude;

        // 기준 최대 속도는 기본값 1로 두고,
        // PlayerMotor가 있으면 sprintSpeed를 최대 속도로 사용한다.
        float maxReferenceSpeed = 1f;

        if (playerMotor != null)
        {
            maxReferenceSpeed = Mathf.Max(0.01f, playerMotor.sprintSpeed);
        }

        // 현재 속도를 0~1 범위로 정규화한다.
        // 멈춤은 0, 걷기는 중간값, 달리기는 1에 가깝게 된다.
        float normalizedSpeed = Mathf.Clamp01(currentPlanarSpeed / maxReferenceSpeed);

        // Animator의 speed 파라미터에 부드럽게 반영한다.
        animator.SetFloat(speedHash, normalizedSpeed, speedDampTime, Time.deltaTime);
    }
}