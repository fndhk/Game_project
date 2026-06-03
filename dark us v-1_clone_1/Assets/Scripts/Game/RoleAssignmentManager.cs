using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

// 게임 시작 시 플레이어 역할을 정하고 Photon 방 속성으로 동기화한다.
public class RoleAssignmentManager : MonoBehaviourPunCallbacks
{
    public const string ImposterActorRoomPropertyKey = "imposterActorNumber";
    public const string ImposterActorsRoomPropertyKey = "imposterActorNumbers";

    private const char ImposterActorSeparator = ',';
    private const int TwoImposterPlayerThreshold = 8;

    [Header("플레이어 목록")]
    // 역할을 배정할 플레이어들을 Inspector에서 넣는다.
    public PlayerCombatTarget[] players;

    [Header("Photon 역할 동기화")]
    // Photon 방에서 시작한 게임이면 방 속성에 저장된 도플갱어 번호로 자기 역할을 정한다.
    public bool usePhotonRoomRoles = true;

    [Header("자동 시작")]
    // Start에서 자동으로 역할 배정을 할지 정한다.
    public bool assignRolesOnStart = true;

    private bool hasAssignedPhotonRole;
    private float nextPhotonRoleRetryTime;

    // 게임 시작 시 자동 배정을 실행한다.
    private void Start()
    {
        GameLoopManager.EnsureExists();

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

        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("RoleAssignmentManager: players가 비어 있음.");
            return;
        }

        List<PlayerCombatTarget> validPlayers = new List<PlayerCombatTarget>();
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
            {
                continue;
            }

            players[i].SetRole(PlayerRole.Citizen);
            validPlayers.Add(players[i]);
        }

        if (validPlayers.Count <= 0)
        {
            Debug.LogWarning("RoleAssignmentManager: 배정 가능한 플레이어가 없음.");
            return;
        }

        int killerCount = GetLimitedImposterCount(validPlayers.Count);
        for (int i = 0; i < killerCount; i++)
        {
            int index = Random.Range(0, validPlayers.Count);
            validPlayers[index].SetRole(PlayerRole.Killer);
            validPlayers.RemoveAt(index);
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                Debug.Log(players[i].name + " => " + players[i].role);
            }
        }
    }

    private void Update()
    {
        if (!usePhotonRoomRoles || !assignRolesOnStart || !PhotonNetwork.InRoom || hasAssignedPhotonRole)
        {
            return;
        }

        if (Time.time < nextPhotonRoleRetryTime)
        {
            return;
        }

        nextPhotonRoleRetryTime = Time.time + 0.25f;
        AssignLocalPhotonRole();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!usePhotonRoomRoles || propertiesThatChanged == null)
        {
            return;
        }

        if (!propertiesThatChanged.ContainsKey(ImposterActorsRoomPropertyKey) &&
            !propertiesThatChanged.ContainsKey(ImposterActorRoomPropertyKey))
        {
            return;
        }

        AssignLocalPhotonRole();
    }

    public static int SelectNewPhotonImposterActor()
    {
        int[] actors = SelectNewPhotonImposterActors();
        return actors.Length > 0 ? actors[0] : -1;
    }

    public static int[] SelectNewPhotonImposterActors()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            return new int[0];
        }

        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.PlayerList == null || PhotonNetwork.PlayerList.Length == 0)
        {
            return new int[0];
        }

        int[] imposterActors = PickPhotonImposterActors();
        SetPhotonImposterActors(imposterActors);

        Debug.Log("New Photon imposter actors = " + SerializeActorList(imposterActors));
        return imposterActors;
    }

    public static int EnsurePhotonImposterActor()
    {
        int[] actors = EnsurePhotonImposterActors();
        return actors.Length > 0 ? actors[0] : -1;
    }

    public static int[] EnsurePhotonImposterActors()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            return new int[0];
        }

        int[] existingActors = GetPhotonImposterActors();
        if (HasRequiredPhotonImposterActors(existingActors))
        {
            return existingActors;
        }

        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.PlayerList == null || PhotonNetwork.PlayerList.Length == 0)
        {
            return existingActors;
        }

        int[] imposterActors = PickPhotonImposterActors();
        SetPhotonImposterActors(imposterActors);

        Debug.Log("Photon imposter actors = " + SerializeActorList(imposterActors));
        return imposterActors;
    }

    public static int GetPhotonImposterActor()
    {
        int[] actors = GetPhotonImposterActors();
        return actors.Length > 0 ? actors[0] : -1;
    }

    public static int[] GetPhotonImposterActors()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.CustomProperties == null)
        {
            return new int[0];
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ImposterActorsRoomPropertyKey, out object actorsValue))
        {
            int[] actors = ParseActorList(actorsValue);
            if (actors.Length > 0)
            {
                return actors;
            }
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ImposterActorRoomPropertyKey, out object actorValue))
        {
            return new int[0];
        }

        return ParseActorList(actorValue);
    }

    public static bool IsActorImposter(int actorNumber)
    {
        if (actorNumber <= 0)
        {
            return false;
        }

        return ContainsActor(GetPhotonImposterActors(), actorNumber);
    }

    public static int GetRequiredImposterCount(int playerCount)
    {
        return Mathf.Max(1, playerCount >= TwoImposterPlayerThreshold ? 2 : 1);
    }

    public static bool ArePhotonImposterActorsReady(int[] expectedActors)
    {
        int[] currentActors = GetPhotonImposterActors();
        if (!HasRequiredPhotonImposterActors(currentActors))
        {
            return false;
        }

        if (expectedActors == null || expectedActors.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < expectedActors.Length; i++)
        {
            if (expectedActors[i] > 0 && !ContainsActor(currentActors, expectedActors[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsWaitingForPhotonRole()
    {
        if (!PhotonNetwork.InRoom)
        {
            return false;
        }

        int[] imposterActors = GetPhotonImposterActors();
        if (!HasRequiredPhotonImposterActors(imposterActors) && PhotonNetwork.IsMasterClient)
        {
            imposterActors = EnsurePhotonImposterActors();
        }

        return !HasRequiredPhotonImposterActors(imposterActors);
    }

    private void AssignLocalPhotonRole()
    {
        int[] imposterActors = EnsurePhotonImposterActors();
        if (!HasRequiredPhotonImposterActors(imposterActors))
        {
            Debug.Log("Photon imposter actors are not ready yet.");
            return;
        }

        PlayerRole localRole = PhotonNetwork.LocalPlayer != null &&
                               ContainsActor(imposterActors, PhotonNetwork.LocalPlayer.ActorNumber)
            ? PlayerRole.Killer
            : PlayerRole.Citizen;

        AssignPhotonRolesToTargets(imposterActors, localRole);
        hasAssignedPhotonRole = true;
        Debug.Log("Local Photon role = " + localRole);
    }

    public static PlayerRole GetLocalPhotonRole(PlayerRole fallbackRole = PlayerRole.Citizen)
    {
        int[] imposterActors = EnsurePhotonImposterActors();
        if (!HasRequiredPhotonImposterActors(imposterActors) || PhotonNetwork.LocalPlayer == null)
        {
            return fallbackRole;
        }

        return ContainsActor(imposterActors, PhotonNetwork.LocalPlayer.ActorNumber) ? PlayerRole.Killer : PlayerRole.Citizen;
    }

    private void AssignPhotonRolesToTargets(int[] imposterActors, PlayerRole localFallbackRole)
    {
        bool assignedAny = false;

        if (players != null)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null)
                {
                    continue;
                }

                players[i].SetRole(GetRoleForTarget(players[i], imposterActors, localFallbackRole));
                assignedAny = true;
            }
        }

        if (assignedAny)
        {
            return;
        }

        PlayerCombatTarget[] sceneTargets = Object.FindObjectsByType<PlayerCombatTarget>(FindObjectsInactive.Include);
        for (int i = 0; i < sceneTargets.Length; i++)
        {
            if (sceneTargets[i] != null)
            {
                sceneTargets[i].SetRole(GetRoleForTarget(sceneTargets[i], imposterActors, localFallbackRole));
            }
        }
    }

    private static PlayerRole GetRoleForTarget(PlayerCombatTarget target, int[] imposterActors, PlayerRole fallbackRole)
    {
        if (target == null || imposterActors == null || imposterActors.Length <= 0)
        {
            return fallbackRole;
        }

        int actorNumber = target.GetActorNumber();
        if (actorNumber <= 0)
        {
            return fallbackRole;
        }

        return ContainsActor(imposterActors, actorNumber) ? PlayerRole.Killer : PlayerRole.Citizen;
    }

    private static int[] PickPhotonImposterActors()
    {
        List<int> actorNumbers = new List<int>();

        if (PhotonNetwork.PlayerList != null)
        {
            for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
            {
                if (PhotonNetwork.PlayerList[i] != null)
                {
                    actorNumbers.Add(PhotonNetwork.PlayerList[i].ActorNumber);
                }
            }
        }

        if (actorNumbers.Count <= 0)
        {
            return new int[0];
        }

        int imposterCount = GetLimitedImposterCount(actorNumbers.Count);
        int[] selectedActors = new int[imposterCount];

        for (int i = 0; i < imposterCount; i++)
        {
            int swapIndex = Random.Range(i, actorNumbers.Count);
            int temp = actorNumbers[i];
            actorNumbers[i] = actorNumbers[swapIndex];
            actorNumbers[swapIndex] = temp;
            selectedActors[i] = actorNumbers[i];
        }

        return selectedActors;
    }

    private static int GetLimitedImposterCount(int playerCount)
    {
        if (playerCount <= 1)
        {
            return Mathf.Max(1, playerCount);
        }

        return Mathf.Clamp(GetRequiredImposterCount(playerCount), 1, playerCount - 1);
    }

    private static bool HasRequiredPhotonImposterActors(int[] actors)
    {
        if (!PhotonNetwork.InRoom)
        {
            return false;
        }

        int playerCount = GetPhotonPlayerCount();
        if (playerCount <= 0)
        {
            return false;
        }

        int requiredCount = GetLimitedImposterCount(playerCount);
        return CountValidUniqueActors(actors) >= requiredCount;
    }

    private static int GetPhotonPlayerCount()
    {
        if (PhotonNetwork.PlayerList != null && PhotonNetwork.PlayerList.Length > 0)
        {
            return PhotonNetwork.PlayerList.Length;
        }

        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.PlayerCount > 0)
        {
            return PhotonNetwork.CurrentRoom.PlayerCount;
        }

        return 0;
    }

    private static int CountValidUniqueActors(int[] actors)
    {
        if (actors == null || actors.Length == 0)
        {
            return 0;
        }

        List<int> uniqueActors = new List<int>();
        for (int i = 0; i < actors.Length; i++)
        {
            int actorNumber = actors[i];
            if (actorNumber <= 0 || !IsActorInCurrentRoom(actorNumber) || ContainsActor(uniqueActors, actorNumber))
            {
                continue;
            }

            uniqueActors.Add(actorNumber);
        }

        return uniqueActors.Count;
    }

    private static void SetPhotonImposterActors(int[] imposterActors)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || imposterActors == null || imposterActors.Length == 0)
        {
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { ImposterActorsRoomPropertyKey, SerializeActorList(imposterActors) },
            { ImposterActorRoomPropertyKey, imposterActors[0] }
        });
    }

    private static string SerializeActorList(int[] actors)
    {
        if (actors == null || actors.Length == 0)
        {
            return string.Empty;
        }

        string result = string.Empty;
        for (int i = 0; i < actors.Length; i++)
        {
            if (actors[i] <= 0)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(result))
            {
                result += ImposterActorSeparator;
            }

            result += actors[i].ToString();
        }

        return result;
    }

    private static int[] ParseActorList(object value)
    {
        if (value == null)
        {
            return new int[0];
        }

        if (value is int[] intArray)
        {
            return FilterPositiveActors(intArray);
        }

        if (value is object[] objectArray)
        {
            List<int> actors = new List<int>();
            for (int i = 0; i < objectArray.Length; i++)
            {
                int actorNumber = ToActorNumber(objectArray[i]);
                if (actorNumber > 0 && !ContainsActor(actors, actorNumber))
                {
                    actors.Add(actorNumber);
                }
            }

            return actors.ToArray();
        }

        if (value is string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new int[0];
            }

            string[] parts = text.Split(ImposterActorSeparator);
            List<int> actors = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i], out int actorNumber) && actorNumber > 0 && !ContainsActor(actors, actorNumber))
                {
                    actors.Add(actorNumber);
                }
            }

            return actors.ToArray();
        }

        int singleActor = ToActorNumber(value);
        return singleActor > 0 ? new[] { singleActor } : new int[0];
    }

    private static int[] FilterPositiveActors(int[] source)
    {
        if (source == null || source.Length == 0)
        {
            return new int[0];
        }

        List<int> actors = new List<int>();
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] > 0 && !ContainsActor(actors, source[i]))
            {
                actors.Add(source[i]);
            }
        }

        return actors.ToArray();
    }

    private static int ToActorNumber(object value)
    {
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

        if (value is long longValue)
        {
            return longValue > int.MaxValue ? -1 : (int)longValue;
        }

        return -1;
    }

    private static bool ContainsActor(int[] actors, int actorNumber)
    {
        if (actors == null || actorNumber <= 0)
        {
            return false;
        }

        for (int i = 0; i < actors.Length; i++)
        {
            if (actors[i] == actorNumber)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsActor(List<int> actors, int actorNumber)
    {
        if (actors == null || actorNumber <= 0)
        {
            return false;
        }

        for (int i = 0; i < actors.Count; i++)
        {
            if (actors[i] == actorNumber)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActorInCurrentRoom(int actorNumber)
    {
        if (actorNumber <= 0 || PhotonNetwork.PlayerList == null)
        {
            return false;
        }

        for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++)
        {
            if (PhotonNetwork.PlayerList[i] != null && PhotonNetwork.PlayerList[i].ActorNumber == actorNumber)
            {
                return true;
            }
        }

        return false;
    }

    private void AssignRoleToListedPlayers(PlayerRole role)
    {
        if (players == null || players.Length == 0)
        {
            PlayerCombatTarget localTarget = Object.FindAnyObjectByType<PlayerCombatTarget>();
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
