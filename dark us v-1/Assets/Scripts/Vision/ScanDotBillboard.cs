using UnityEngine;

// Quad 점이 항상 카메라를 향하도록 만드는 스크립트이다.
public class ScanDotBillboard : MonoBehaviour
{
    // 바라볼 카메라를 저장한다.
    private Camera targetCamera;

    private void Start()
    {
        // 시작 시 메인 카메라를 찾는다.
        targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        // 카메라가 없으면 동작하지 않는다.
        if (targetCamera == null)
        {
            return;
        }

        // 점이 카메라를 향하게 만든다.
        transform.forward = targetCamera.transform.forward;
    }
}