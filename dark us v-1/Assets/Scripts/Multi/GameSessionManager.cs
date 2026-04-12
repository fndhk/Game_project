using UnityEngine;
using Unity.Netcode;
using System;

public class GameSessionManager : NetworkBehaviour
{
    [SerializeField] private int minPlayers = 4;
    [SerializeField] private int maxPlayers = 12;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += CheckPlayerCount;
        }
    }

    private void CheckPlayerCount(ulong clientId)
    {
        int currentPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;

        if (currentPlayers >= minPlayers)
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        // 게임 루틴 시작 및 씬 전환 로직
        Debug.Log("최소 인원 충족: 게임을 시작합니다.");
    }
}