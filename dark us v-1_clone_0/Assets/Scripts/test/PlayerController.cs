using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviourPunCallbacks
{
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        PhotonView view = GetComponent<PhotonView>();
        if (view != null && !view.IsMine)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y = 1f;
            if (Keyboard.current.sKey.isPressed) input.y = -1f;
            if (Keyboard.current.aKey.isPressed) input.x = -1f;
            if (Keyboard.current.dKey.isPressed) input.x = 1f;
        }

        Vector3 move = new Vector3(input.x, 0f, input.y);
        transform.position += move * moveSpeed * Time.deltaTime;
    }

    private void Start()
    {
        Renderer targetRenderer = GetComponent<Renderer>();
        if (targetRenderer == null)
        {
            return;
        }

        PhotonView view = GetComponent<PhotonView>();
        targetRenderer.material.color = view == null || view.IsMine ? Color.red : Color.blue;
    }
}
