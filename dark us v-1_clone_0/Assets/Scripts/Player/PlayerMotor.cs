using UnityEngine;

// 이 오브젝트에는 CharacterController가 반드시 붙어 있어야 하게 만듦
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Move")]
    // 평소 기본 이동 속도 설정값임
    public float moveSpeed = 4f;

    // 우클릭 스캔 중일 때 이동 속도에 곱해지는 배율값임
    // 1에 가까울수록 평소 속도와 비슷해짐
    public float scanMoveMultiplier = 1.0f;

    [Header("Gravity")]
    // 아래로 떨어지게 만드는 중력 값임
    public float gravity = -20f;

    // 실제 이동 처리를 담당하는 CharacterController를 저장함
    private CharacterController controller;

    // 중력 속도를 따로 저장해서 매 프레임 누적시킴
    private Vector3 velocity;

    // 우클릭 스캔 중인지 확인하기 위해 PlayerRevealTrail을 참조함
    private PlayerRevealTrail revealTrail;

    void Awake()
    {
        // 같은 오브젝트에 붙어 있는 CharacterController를 가져옴
        controller = GetComponent<CharacterController>();

        // 같은 오브젝트에 붙어 있는 PlayerRevealTrail을 가져옴
        revealTrail = GetComponent<PlayerRevealTrail>();
    }

    void Update()
    {
        // A,D 입력값을 받아서 좌우 이동값으로 사용함
        float h = Input.GetAxisRaw("Horizontal");

        // W,S 입력값을 받아서 앞뒤 이동값으로 사용함
        float v = Input.GetAxisRaw("Vertical");

        // 플레이어가 바라보는 방향 기준으로 이동 방향을 계산함
        Vector3 move = (transform.right * h + transform.forward * v).normalized;

        // 기본 이동 속도를 현재 속도로 먼저 넣어 둠
        float currentSpeed = moveSpeed;

        // 우클릭 스캔 중이면 이동 속도를 조금 줄여 줌
        if (revealTrail != null && revealTrail.IsScanning)
        {
            currentSpeed *= scanMoveMultiplier;
        }

        // 계산된 방향과 속도로 플레이어를 이동시킴
        controller.Move(move * currentSpeed * Time.deltaTime);

        // 바닥에 닿아 있고 아래 속도가 남아 있으면 살짝 바닥에 붙여 줌
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        // 중력값을 계속 누적해서 아래 방향 속도를 만듦
        velocity.y += gravity * Time.deltaTime;

        // 누적된 중력 속도를 실제 이동에 적용함
        controller.Move(velocity * Time.deltaTime);
    }
}