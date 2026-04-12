using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; // 상단에 반드시 추가

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    void Update()
    {
        if (!IsOwner) return;

        // New Input System 방식의 간단한 키 입력 처리
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y = 1;
            if (Keyboard.current.sKey.isPressed) input.y = -1;
            if (Keyboard.current.aKey.isPressed) input.x = -1;
            if (Keyboard.current.dKey.isPressed) input.x = 1;
        }

        Vector3 move = new Vector3(input.x, 0, input.y);
        transform.position += move * moveSpeed * Time.deltaTime;
    }

    public override void OnNetworkSpawn()
    {
        if (GetComponent<Renderer>() != null)
        {
            GetComponent<Renderer>().material.color = IsOwner ? Color.red : Color.blue;
        }
    }
}