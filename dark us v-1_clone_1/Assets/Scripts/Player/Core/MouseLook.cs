using UnityEngine;

// 이 스크립트는 1인칭 시점에서
// 카메라 시점에 따라 플레이어가 움직이도록 만드는 마우스 시점 제어 스크립트이다.
// 좌우 회전은 플레이어 몸체가 담당하고,
// 위아래 회전은 카메라가 담당한다.
public class MouseLook : MonoBehaviour
{
    [Header("참조")]
    // 위아래 회전을 적용할 카메라 Transform이다.
    // 보통 Main Camera를 연결하면 된다.
    public Transform playerCamera;

    [Header("마우스 감도")]
    // 좌우 회전 감도이다.
    // 너무 빠르지 않게 적당히 낮춘 값으로 시작한다.
    public float mouseSensitivityX = 100f;

    // 위아래 회전 감도이다.
    // 좌우와 비슷하거나 조금 낮게 두면 자연스럽다.
    public float mouseSensitivityY = 95f;

    [Header("위아래 시야 제한")]
    // 위쪽으로 볼 수 있는 최대 각도이다.
    public float maxLookUpAngle = 75f;

    // 아래쪽으로 볼 수 있는 최대 각도이다.
    public float maxLookDownAngle = -75f;

    // 현재 위아래 회전값을 저장하는 변수이다.
    private float pitch = 0f;

    // 시작할 때 카메라 연결과 마우스 잠금을 처리한다.
    private void Start()
    {
        // 카메라가 직접 연결되지 않았으면 Main Camera를 자동으로 찾는다.
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        // 마우스 커서를 화면 중앙에 고정해서 FPS처럼 조작되게 만든다.
        Cursor.lockState = CursorLockMode.Locked;

        // 마우스 커서를 보이지 않게 만든다.
        Cursor.visible = false;
    }

    // 매 프레임 마우스 입력을 받아 회전을 적용한다.
    private void Update()
    {
        // 카메라가 없으면 더 진행하지 않는다.
        if (playerCamera == null)
        {
            return;
        }

        // 마우스 좌우 입력값을 가져온다.
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;

        // 마우스 위아래 입력값을 가져온다.
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;

        // 위아래 회전값은 누적해서 관리한다.
        // 마우스를 위로 올리면 화면이 위를 보도록 부호를 반대로 준다.
        pitch -= mouseY;

        // 위아래 각도가 너무 많이 꺾이지 않도록 제한한다.
        pitch = Mathf.Clamp(pitch, maxLookDownAngle, maxLookUpAngle);

        // 플레이어 몸체는 좌우 회전만 담당한다.
        // 이 회전이 곧 이동 방향 기준이 되므로
        // PlayerMotor가 transform.forward, transform.right를 사용하면
        // 카메라 시점에 따라 움직이게 된다.
        transform.Rotate(Vector3.up * mouseX);

        // 카메라는 위아래 회전만 적용한다.
        playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}