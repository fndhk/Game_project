using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class GameSessionManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private int minPlayers = 4;
    [SerializeField] private int maxPlayers = 12;

    private void Start()
    {
        CheckPlayerCount();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        CheckPlayerCount();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        CheckPlayerCount();
    }

    private void CheckPlayerCount()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        int currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        int requiredPlayers = Mathf.Clamp(minPlayers, 1, Mathf.Max(1, maxPlayers));

        if (currentPlayers >= requiredPlayers)
        {
            Debug.Log("Photon room has enough players.");
        }
    }
}
