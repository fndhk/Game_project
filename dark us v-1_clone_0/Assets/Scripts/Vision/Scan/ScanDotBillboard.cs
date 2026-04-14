using UnityEngine;

// 점이 항상 카메라를 향하게 만든다.
public class ScanDotBillboard : MonoBehaviour
{
    // 따라볼 카메라를 저장한다.
    private Camera targetCamera;

    private void Start()
    {
        // 시작할 때 메인 카메라를 찾는다.
        targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        // 카메라가 없으면 종료한다.
        if (targetCamera == null)
        {
            return;
        }

        // 점이 카메라 방향을 바라보게 만든다.
        transform.forward = targetCamera.transform.forward;
    }
}