using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

// 라운드 승패, 사망, 탈출, 목표 복구 동기화를 담당한다.
public class GameLoopManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte ComputerRestoredEventCode = 70;
    private const byte PlayerDiedEventCode = 71;
    private const byte PlayerEscapedEventCode = 72;
    private const byte GameOverEventCode = 73;
    private const byte ComputerSabotagedEventCode = 74;
    private const string MapSeedPropertyKey = "mapSeed";
    private const string ReadyPropertyKey = "ready";
    private const string StartSignalPropertyKey = "gameStarting";
    private const string RoomVisiblePrefsKey = "dark_us_room_is_visible";

    public static GameLoopManager Instance { get; private set; }

    [Header("Round Rules")]
    public int requiredEscapedCitizens = 1;
    public bool killerWinsOnTimerExpired = true;
    public bool killerWinsWhenNoCitizenCanEscape = true;

    [Header("Round End Flow")]
    public bool autoReturnToRoomLobby = true;
    public bool showInGameResultOverlay = false;
    public string roomLobbySceneName = "CreateRoomLobbyScene";
    public float returnToLobbyDelay = 3f;

    [Header("State")]
    [SerializeField] private bool gameOver;
    [SerializeField] private bool citizensWon;
    [SerializeField] private string gameOverReason = "";

    private readonly HashSet<int> deadActors = new HashSet<int>();
    private readonly HashSet<int> escapedActors = new HashSet<int>();
    private readonly Dictionary<int, ObjectiveComputer> computersById = new Dictionary<int, ObjectiveComputer>();
    private PlayerCombatTarget localTarget;
    private bool isReturningToLobby;
    private float returnToLobbyAt;

    public bool IsGameOver => gameOver;
    public bool CitizensWon => citizensWon;
    public string GameOverReason => gameOverReason;

    public static GameLoopManager EnsureExists()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameLoopManager existing = Object.FindAnyObjectByType<GameLoopManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("GameLoopManager");
        return managerObject.AddComponent<GameLoopManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnEnable()
    {
        base.OnEnable();
    }

    public override void OnDisable()
    {
        base.OnDisable();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        RoundTimer.ResetTimer();
        RefreshLocalTarget();
        RebuildComputerIndex();
    }

    private void Update()
    {
        if (gameOver)
        {
            UpdateGameOverReturn();
            return;
        }

        if (GameplayStartupGate.IsBlocked)
        {
            return;
        }

        if (killerWinsOnTimerExpired && RoundTimer.RemainingSeconds <= 0f)
        {
            TriggerGameOver(false, "Time Expired", true);
            return;
        }

        EvaluateKillerWinCondition();
    }

    public void RebuildComputerIndex()
    {
        computersById.Clear();

        ObjectiveComputer[] computers = Object.FindObjectsByType<ObjectiveComputer>(FindObjectsInactive.Include);
        for (int i = 0; i < computers.Length; i++)
        {
            if (computers[i] == null || computers[i].NetworkObjectiveId < 0)
            {
                continue;
            }

            computersById[computers[i].NetworkObjectiveId] = computers[i];
        }
    }

    public void ReportComputerRestored(ObjectiveComputer computer)
    {
        if (computer == null || computer.NetworkObjectiveId < 0)
        {
            return;
        }

        RebuildComputerIndex();

        object[] payload =
        {
            computer.NetworkObjectiveId
        };

        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };

        PhotonNetwork.RaiseEvent(ComputerRestoredEventCode, payload, options, SendOptions.SendReliable);
    }

    public void ReportComputerSabotaged(ObjectiveComputer computer)
    {
        if (computer == null || computer.NetworkObjectiveId < 0)
        {
            return;
        }

        RebuildComputerIndex();

        object[] payload =
        {
            computer.NetworkObjectiveId
        };

        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };

        PhotonNetwork.RaiseEvent(ComputerSabotagedEventCode, payload, options, SendOptions.SendReliable);
    }

    public void ReportPlayerDeath(int actorNumber)
    {
        if (actorNumber <= 0)
        {
            actorNumber = GetLocalActorNumber();
        }

        if (actorNumber <= 0 || !deadActors.Add(actorNumber))
        {
            EvaluateKillerWinCondition();
            return;
        }

        object[] payload = { actorNumber };
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.RaiseEvent(PlayerDiedEventCode, payload, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }
        EvaluateKillerWinCondition();
    }

    public void ReportLocalEscape(PlayerCombatTarget target)
    {
        if (gameOver || target == null || target.isDead || target.role != PlayerRole.Citizen)
        {
            return;
        }

        int actorNumber = target.GetActorNumber();
        if (actorNumber <= 0)
        {
            actorNumber = GetLocalActorNumber();
        }

        if (actorNumber <= 0)
        {
            TriggerGameOver(true, "Citizens Escaped", true);
            return;
        }

        escapedActors.Add(actorNumber);
        object[] payload = { actorNumber };
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.RaiseEvent(PlayerEscapedEventCode, payload, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }
        EvaluateCitizenWinCondition();
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent == null)
        {
            return;
        }

        switch (photonEvent.Code)
        {
            case ComputerRestoredEventCode:
                ApplyComputerRestoredEvent(photonEvent.CustomData as object[]);
                break;

            case PlayerDiedEventCode:
                ApplyPlayerDiedEvent(photonEvent.CustomData as object[]);
                break;

            case PlayerEscapedEventCode:
                ApplyPlayerEscapedEvent(photonEvent.CustomData as object[]);
                break;

            case GameOverEventCode:
                ApplyGameOverEvent(photonEvent.CustomData as object[]);
                break;

            case ComputerSabotagedEventCode:
                ApplyComputerSabotagedEvent(photonEvent.CustomData as object[]);
                break;
        }
    }

    private void ApplyComputerRestoredEvent(object[] payload)
    {
        if (payload == null || payload.Length <= 0)
        {
            return;
        }

        int objectiveId = ToInt(payload[0], -1);
        if (objectiveId < 0)
        {
            return;
        }

        if (!computersById.TryGetValue(objectiveId, out ObjectiveComputer computer) || computer == null)
        {
            RebuildComputerIndex();
            computersById.TryGetValue(objectiveId, out computer);
        }

        if (computer != null)
        {
            computer.ApplyRestoredFromNetwork();
        }
    }

    private void ApplyComputerSabotagedEvent(object[] payload)
    {
        if (payload == null || payload.Length <= 0)
        {
            return;
        }

        int objectiveId = ToInt(payload[0], -1);
        if (objectiveId < 0)
        {
            return;
        }

        if (!computersById.TryGetValue(objectiveId, out ObjectiveComputer computer) || computer == null)
        {
            RebuildComputerIndex();
            computersById.TryGetValue(objectiveId, out computer);
        }

        if (computer != null)
        {
            computer.ApplySabotagedFromNetwork();
        }
    }

    private void ApplyPlayerDiedEvent(object[] payload)
    {
        if (payload == null || payload.Length <= 0)
        {
            return;
        }

        int actorNumber = ToInt(payload[0], -1);
        if (actorNumber <= 0)
        {
            return;
        }

        deadActors.Add(actorNumber);
        ApplyDeathToActorObjects(actorNumber);
        EvaluateKillerWinCondition();
    }

    private void ApplyPlayerEscapedEvent(object[] payload)
    {
        if (payload == null || payload.Length <= 0)
        {
            return;
        }

        int actorNumber = ToInt(payload[0], -1);
        if (actorNumber <= 0)
        {
            return;
        }

        escapedActors.Add(actorNumber);
        EvaluateCitizenWinCondition();
    }

    private void ApplyGameOverEvent(object[] payload)
    {
        if (payload == null || payload.Length < 2)
        {
            return;
        }

        bool didCitizensWin = ToInt(payload[0], 0) != 0;
        string reason = payload[1] as string;
        TriggerGameOver(didCitizensWin, string.IsNullOrWhiteSpace(reason) ? "Round Complete" : reason, false);
    }

    private void ApplyDeathToActorObjects(int actorNumber)
    {
        PlayerCombatTarget[] targets = Object.FindObjectsByType<PlayerCombatTarget>(FindObjectsInactive.Include);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null && targets[i].GetActorNumber() == actorNumber)
            {
                targets[i].ApplyDeathFromNetwork();
            }
        }
    }

    private void EvaluateCitizenWinCondition()
    {
        int escapedCitizens = 0;
        Player[] players = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList : null;

        if (players != null && players.Length > 0)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null && !RoleAssignmentManager.IsActorImposter(players[i].ActorNumber) && escapedActors.Contains(players[i].ActorNumber))
                {
                    escapedCitizens++;
                }
            }
        }
        else if (escapedActors.Count > 0)
        {
            escapedCitizens = escapedActors.Count;
        }

        if (escapedCitizens >= Mathf.Max(1, requiredEscapedCitizens))
        {
            TriggerGameOver(true, "Citizens Escaped", true);
        }
    }

    private void EvaluateKillerWinCondition()
    {
        if (gameOver || !killerWinsWhenNoCitizenCanEscape)
        {
            return;
        }

        Player[] players = PhotonNetwork.InRoom ? PhotonNetwork.PlayerList : null;

        if (players == null || players.Length == 0)
        {
            RefreshLocalTarget();
            if (localTarget != null && localTarget.role == PlayerRole.Citizen && localTarget.isDead)
            {
                TriggerGameOver(false, "All Citizens Down", true);
            }
            return;
        }

        int livingCitizens = 0;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || RoleAssignmentManager.IsActorImposter(players[i].ActorNumber))
            {
                continue;
            }

            int actor = players[i].ActorNumber;
            if (!deadActors.Contains(actor) && !escapedActors.Contains(actor))
            {
                livingCitizens++;
            }
        }

        if (livingCitizens <= 0)
        {
            TriggerGameOver(false, "All Citizens Down", true);
        }
    }

    private void TriggerGameOver(bool didCitizensWin, string reason, bool broadcast)
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        citizensWon = didCitizensWin;
        gameOverReason = reason;

        if (broadcast && PhotonNetwork.InRoom)
        {
            object[] payload =
            {
                didCitizensWin ? 1 : 0,
                reason
            };

            PhotonNetwork.RaiseEvent(GameOverEventCode, payload, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }

        Debug.Log("Game Over: " + (citizensWon ? "Citizens Win" : "Killer Wins") + " / " + gameOverReason);
        GameAudioManager.PlayGameOver(citizensWon);
        DarkScanLoadingScreen.ForceHideImmediate();
        RoleRevealIntro.CancelPending();
        VictoryScreen.Show(citizensWon, gameOverReason, returnToLobbyDelay);
        ScheduleReturnToLobby();
    }

    private void ScheduleReturnToLobby()
    {
        if (!autoReturnToRoomLobby || isReturningToLobby)
        {
            return;
        }

        isReturningToLobby = true;
        returnToLobbyAt = Time.unscaledTime + Mathf.Max(0.1f, returnToLobbyDelay);
        ResetLocalReadyState();
    }

    private void UpdateGameOverReturn()
    {
        if (!isReturningToLobby || Time.unscaledTime < returnToLobbyAt)
        {
            return;
        }

        ReturnToRoomLobby();
    }

    private void ReturnToRoomLobby()
    {
        isReturningToLobby = false;
        ResetLocalReadyState();
        DarkScanLoadingScreen.ForceHideImmediate();

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.AutomaticallySyncScene = true;

            if (PhotonNetwork.IsMasterClient)
            {
                ResetRoomForLobby();
                PhotonNetwork.LoadLevel(roomLobbySceneName);
            }

            return;
        }

        SceneManager.LoadScene(roomLobbySceneName);
    }

    private void ResetLocalReadyState()
    {
        if (PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { ReadyPropertyKey, false }
        });
    }

    private void ResetRoomForLobby()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        PhotonNetwork.CurrentRoom.IsOpen = true;
        PhotonNetwork.CurrentRoom.IsVisible = PlayerPrefs.GetInt(RoomVisiblePrefsKey, 0) == 1;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { StartSignalPropertyKey, false },
            { MapSeedPropertyKey, Random.Range(1, int.MaxValue) },
            { RoleAssignmentManager.ImposterActorsRoomPropertyKey, null },
            { RoleAssignmentManager.ImposterActorRoomPropertyKey, null }
        });
        PhotonNetwork.SendAllOutgoingCommands();
    }

    private void RefreshLocalTarget()
    {
        if (localTarget != null)
        {
            return;
        }

        PlayerCombatTarget[] targets = Object.FindObjectsByType<PlayerCombatTarget>(FindObjectsInactive.Include);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null && !targets[i].isRemoteProxy)
            {
                localTarget = targets[i];
                return;
            }
        }
    }

    private int GetLocalActorNumber()
    {
        return PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1;
    }

    private int ToInt(object value, int fallback)
    {
        if (value is int intValue)
        {
            return intValue;
        }

        if (value is short shortValue)
        {
            return shortValue;
        }

        if (value is byte byteValue)
        {
            return byteValue;
        }

        return fallback;
    }

    private void OnGUI()
    {
        if (!showInGameResultOverlay || !gameOver)
        {
            return;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.055f, 26f, 54f)),
            fontStyle = FontStyle.Bold
        };

        style.normal.textColor = citizensWon ? new Color(0.55f, 0.95f, 1f, 1f) : new Color(1f, 0.28f, 0.22f, 1f);

        string title = citizensWon ? InGameLocalization.Text("Citizens Win") : InGameLocalization.Text("Killer Wins");
        string reason = InGameLocalization.Text(gameOverReason);
        GUI.Label(new Rect(0f, Screen.height * 0.34f, Screen.width, 120f), title + "\n" + reason, style);
    }
}
