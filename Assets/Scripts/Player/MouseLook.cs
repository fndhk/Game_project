using UnityEngine;

// 1인칭 시점에서 마우스로 시야를 회전시키는 스크립트임
public class MouseLook : MonoBehaviour
{
    // 좌우 회전을 담당할 플레이어 본체 Transform임
    public Transform playerBody;

    // 마우스 감도 설정값임
    public float mouseSensitivity = 200f;

    // 위아래 회전값을 따로 저장해서 카메라 상하 시야에 사용함
    private float xRotation = 0f;

    void Start()
    {
        // 게임 시작할 때 마우스 포인터를 화면 중앙에 고정함
        Cursor.lockState = CursorLockMode.Locked;

        // 1인칭 게임에서는 마우스 포인터를 숨겨서 화면 중앙 고정처럼 보이게 함
        Cursor.visible = false;

        // 시작할 때 카메라의 상하 회전을 정면으로 초기화함
        xRotation = 0f;
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        // playerBody가 연결되어 있으면 플레이어 좌우 회전도 정면으로 초기화함
        if (playerBody != null)
        {
            playerBody.rotation = Quaternion.Euler(0f, playerBody.rotation.eulerAngles.y, 0f);
        }
    }

    void Update()
    {
        // 마우스 좌우 이동량을 받아서 좌우 회전에 사용함
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;

        // 마우스 상하 이동량을 받아서 상하 회전에 사용함
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // 상하 회전은 xRotation 변수로 누적해서 관리함
        xRotation -= mouseY;

        // 상하 회전 각도가 너무 과하게 꺾이지 않게 제한함
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        // 카메라는 상하 회전만 적용하게 함
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 플레이어 본체는 좌우 회전만 적용하게 함
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}