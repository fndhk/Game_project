using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 이 스크립트는 게임 시작 시 플레이어들 중 한 명을 랜덤으로 킬러로 정하고,
// 나머지는 시민으로 배정한다.
public class RoleAssignmentManager : MonoBehaviourPunCallbacks
{
    public const string ImposterActorRoomPropertyKey = "imposterActorNumber";

    [Header("플레이어 목록")]
    // 역할을 배정할 플레이어들을 Inspector에서 넣는다.
    public PlayerCombatTarget[] players;

    [Header("Photon 역할 동기화")]
    // Photon 방에서 시작한 게임이면 방 속성에 저장된 임포스터 번호로 자기 역할을 정한다.
    public bool usePhotonRoomRoles = true;

    [Header("자동 시작")]
    // Start에서 자동으로 역할 배정을 할지 정한다.
    public bool assignRolesOnStart = true;

    // 게임 시작 시 자동 배정을 실행한다.
    private void Start()
    {
        // 자동 시작이 켜져 있으면 역할 배정을 실행한다.
        if (assignRolesOnStart)
        {
            AssignRoles();
        }
    }

    // 플레이어들에게 역할을 랜덤 배정하는 함수이다.
    public void AssignRoles()
    {
        if (usePhotonRoomRoles && PhotonNetwork.InRoom)
        {
            AssignLocalPhotonRole();
            return;
        }

        // 플레이어 목록이 비어 있으면 종료한다.
        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("RoleAssignmentManager: players가 비어 있음.");
            return;
        }

        // 먼저 전부 시민으로 초기화한다.
        for (int i = 0; i < players.Length; i++)
        {
            // 비어 있지 않은 플레이어만 처리한다.
            if (players[i] != null)
            {
                players[i].SetRole(PlayerRole.Citizen);
            }
        }

        // 랜덤으로 킬러 1명을 고른다.
        int killerIndex = Random.Range(0, players.Length);

        // 해당 플레이어를 킬러로 설정한다.
        if (players[killerIndex] != null)
        {
            players[killerIndex].SetRole(PlayerRole.Killer);
        }

        // 콘솔에 결과를 출력한다.
        for (int i = 0; i < players.Length; i++)
        {
            // 비어 있지 않은 플레이어만 출력한다.
            if (players[i] != null)
            {
                Debug.Log(players[i].name + " => " + players[i].role);
            }
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!usePhotonRoomRoles || propertiesThatChanged == null || !propertiesThatChanged.ContainsKey(ImposterActorRoomPropertyKey))
        {
            return;
        }

        AssignLocalPhotonRole();
    }

    public static int EnsurePhotonImposterActor()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            return -1;
        }

        int existingActor = GetPhotonImposterActor();
        if (existingActor > 0)
        {
            return existingActor;
        }

        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.PlayerList == null || PhotonNetwork.PlayerList.Length == 0)
        {
            return -1;
        }

        int index = Random.Range(0, PhotonNetwork.PlayerList.Length);
        int imposterActor = PhotonNetwork.PlayerList[index].ActorNumber;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { ImposterActorRoomPropertyKey, imposterActor }
        });

        Debug.Log("Photon imposter actor = " + imposterActor);
        return imposterActor;
    }

    public static int GetPhotonImposterActor()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
        {
            return -1;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ImposterActorRoomPropertyKey, out object value))
        {
            return -1;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is byte byteValue)
        {
            return byteValue;
        }

        if (value is short shortValue)
        {
            return shortValue;
        }

        return -1;
    }

    public static bool IsWaitingForPhotonRole()
    {
        return PhotonNetwork.InRoom && GetPhotonImposterActor() <= 0;
    }

    private void AssignLocalPhotonRole()
    {
        int imposterActor = EnsurePhotonImposterActor();
        if (imposterActor <= 0)
        {
            Debug.Log("Photon imposter actor is not ready yet.");
            return;
        }

        PlayerRole localRole = PhotonNetwork.LocalPlayer != null &&
                               PhotonNetwork.LocalPlayer.ActorNumber == imposterActor
            ? PlayerRole.Killer
            : PlayerRole.Citizen;

        AssignRoleToListedPlayers(localRole);
        Debug.Log("Local Photon role = " + localRole);
    }

    private void AssignRoleToListedPlayers(PlayerRole role)
    {
        if (players == null || players.Length == 0)
        {
            PlayerCombatTarget localTarget = FindObjectOfType<PlayerCombatTarget>();
            if (localTarget != null)
            {
                localTarget.SetRole(role);
            }
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].SetRole(role);
            }
        }
    }
}
