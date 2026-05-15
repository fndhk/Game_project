using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PunScenePlayerSync : MonoBehaviour, IOnEventCallback
{
    private const byte TransformEventCode = 42;

    public float sendInterval = 0.025f;
    public string remoteAvatarRootName = "ScanOnlyCharacterAvatar";
    public float avatarHeadLocalHeight = 1.77f;
    public float remoteFollowSharpness = 32f;
    public float remoteSnapDistance = 3.5f;
    public float remoteFootstepMinDistance = 1.5f;
    public float remoteFootstepMaxDistance = 14f;

    private readonly Dictionary<int, RemotePlayerState> remotePlayers = new Dictionary<int, RemotePlayerState>();
    private Transform eyeTransform;
    private PlayerFootstepAudio localFootstepTemplate;
    private PlayerVoiceChat localVoiceChat;
    private float nextSendTime;

    private class RemotePlayerState
    {
        public Transform root;
        public PlayerCombatTarget combatTarget;
        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public bool hasTarget;
    }

    private void Awake()
    {
        PunWorldAudioSync.EnsureExists();
        EnsureLocalVoiceChat();
        localFootstepTemplate = FindObjectOfType<PlayerFootstepAudio>();
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
        PunWorldAudioSync.EnsureExists();
        EnsureLocalVoiceChat();
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
        UpdateRemotePlayers();

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null || Time.time < nextSendTime)
        {
            return;
        }

        Transform senderTransform = GetEyeTransform();
        nextSendTime = Time.time + sendInterval;
        object[] payload =
        {
            senderTransform.position.x,
            senderTransform.position.y,
            senderTransform.position.z,
            transform.eulerAngles.y
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };

        PhotonNetwork.RaiseEvent(TransformEventCode, payload, options, SendOptions.SendUnreliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != TransformEventCode || photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return;
        }

        object[] payload = photonEvent.CustomData as object[];
        if (payload == null || payload.Length < 4)
        {
            return;
        }

        Vector3 eyePosition = new Vector3(ToFloat(payload[0]), ToFloat(payload[1]), ToFloat(payload[2]));
        Vector3 position = eyePosition - Vector3.up * avatarHeadLocalHeight;
        Quaternion rotation = Quaternion.Euler(0f, ToFloat(payload[3]), 0f);
        RemotePlayerState remote = GetOrCreateRemotePlayer(photonEvent.Sender);
        remote.targetPosition = position;
        remote.targetRotation = rotation;
        remote.hasTarget = true;
        UpdateRemoteRole(photonEvent.Sender, remote);
    }

    private void UpdateRemotePlayers()
    {
        if (remotePlayers.Count == 0)
        {
            return;
        }

        float follow = 1f - Mathf.Exp(-remoteFollowSharpness * Time.deltaTime);

        foreach (KeyValuePair<int, RemotePlayerState> pair in remotePlayers)
        {
            RemotePlayerState state = pair.Value;
            if (state == null || state.root == null || !state.hasTarget)
            {
                continue;
            }

            float distance = Vector3.Distance(state.root.position, state.targetPosition);
            if (distance >= remoteSnapDistance)
            {
                state.root.SetPositionAndRotation(state.targetPosition, state.targetRotation);
                continue;
            }

            state.root.position = Vector3.Lerp(state.root.position, state.targetPosition, follow);
            state.root.rotation = Quaternion.Slerp(state.root.rotation, state.targetRotation, follow);
        }
    }

    private Transform GetEyeTransform()
    {
        if (eyeTransform != null)
        {
            return eyeTransform;
        }

        Camera childCamera = GetComponentInChildren<Camera>(true);
        eyeTransform = childCamera != null ? childCamera.transform : transform;
        return eyeTransform;
    }

    private RemotePlayerState GetOrCreateRemotePlayer(int actorNumber)
    {
        if (remotePlayers.TryGetValue(actorNumber, out RemotePlayerState remote) && remote != null && remote.root != null)
        {
            return remote;
        }

        GameObject remoteObject = new GameObject("RemotePlayer_" + actorNumber);
        remoteObject.name = "RemotePlayer_" + actorNumber;
        remoteObject.transform.localScale = Vector3.one;

        PlayerVisibleAvatar visibleAvatar = remoteObject.AddComponent<PlayerVisibleAvatar>();
        visibleAvatar.hideWhenLocalScannerOwner = false;
        visibleAvatar.hideRenderers = true;
        visibleAvatar.hideCollidersWhenHidden = false;
        visibleAvatar.addScanColliders = true;
        visibleAvatar.RebuildAvatar();

        PlayerCombatTarget combatTarget = remoteObject.AddComponent<PlayerCombatTarget>();
        combatTarget.isRemoteProxy = true;
        combatTarget.photonActorNumber = actorNumber;
        Transform visualRoot = remoteObject.transform.Find(visibleAvatar.visualRootName);
        combatTarget.bodyVisualRoot = visualRoot != null ? visualRoot.gameObject : null;

        RemotePlayerState state = new RemotePlayerState
        {
            root = remoteObject.transform,
            combatTarget = combatTarget,
            targetPosition = remoteObject.transform.position,
            targetRotation = remoteObject.transform.rotation
        };

        UpdateRemoteRole(actorNumber, state);
        remotePlayers[actorNumber] = state;
        AddRemoteFootsteps(remoteObject);
        return state;
    }

    private void UpdateRemoteRole(int actorNumber, RemotePlayerState state)
    {
        if (state == null || state.combatTarget == null)
        {
            return;
        }

        int imposterActor = RoleAssignmentManager.GetPhotonImposterActor();
        state.combatTarget.role = actorNumber == imposterActor ? PlayerRole.Killer : PlayerRole.Citizen;
    }

    private void EnsureLocalVoiceChat()
    {
        if (localVoiceChat != null)
        {
            return;
        }

        localVoiceChat = GetComponent<PlayerVoiceChat>();
        if (localVoiceChat == null)
        {
            localVoiceChat = gameObject.AddComponent<PlayerVoiceChat>();
        }

        localVoiceChat.spatialBlend = 1f;
        localVoiceChat.minDistance = 1.2f;
        localVoiceChat.maxDistance = 9f;
        localVoiceChat.showLocalMicHud = true;
    }

    private void AddRemoteFootsteps(GameObject remoteObject)
    {
        if (remoteObject == null)
        {
            return;
        }

        if (localFootstepTemplate == null)
        {
            localFootstepTemplate = FindObjectOfType<PlayerFootstepAudio>();
        }

        if (localFootstepTemplate == null)
        {
            return;
        }

        AudioSource source = remoteObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = remoteObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = remoteFootstepMinDistance;
        source.maxDistance = remoteFootstepMaxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;

        PlayerFootstepAudio footsteps = remoteObject.GetComponent<PlayerFootstepAudio>();
        if (footsteps == null)
        {
            footsteps = remoteObject.AddComponent<PlayerFootstepAudio>();
        }

        footsteps.playerRoot = remoteObject.transform;
        footsteps.playerMotor = null;
        footsteps.characterController = null;
        footsteps.groundMask = localFootstepTemplate.groundMask;
        footsteps.groundCheckDistance = Mathf.Max(0.75f, localFootstepTemplate.groundCheckDistance);
        footsteps.useGroundRaycastFallback = true;
        footsteps.commonClips = localFootstepTemplate.commonClips;
        footsteps.walkClips = localFootstepTemplate.walkClips;
        footsteps.sprintClips = localFootstepTemplate.sprintClips;
        footsteps.crouchClips = localFootstepTemplate.crouchClips;
        footsteps.minimumMoveSpeed = localFootstepTemplate.minimumMoveSpeed;
        footsteps.walkStepDistance = localFootstepTemplate.walkStepDistance;
        footsteps.sprintStepDistance = localFootstepTemplate.sprintStepDistance;
        footsteps.crouchStepDistance = localFootstepTemplate.crouchStepDistance;
        footsteps.walkVolume = Mathf.Max(localFootstepTemplate.walkVolume, 0.95f);
        footsteps.sprintVolume = Mathf.Max(localFootstepTemplate.sprintVolume, 1.1f);
        footsteps.crouchVolume = Mathf.Max(localFootstepTemplate.crouchVolume, 0.6f);
        footsteps.minPitch = localFootstepTemplate.minPitch;
        footsteps.maxPitch = localFootstepTemplate.maxPitch;

        System.Reflection.FieldInfo broadcastField = typeof(PlayerFootstepAudio).GetField("broadcastFootstepsToNetwork");
        if (broadcastField != null)
        {
            broadcastField.SetValue(footsteps, false);
        }
    }

    private float ToFloat(object value)
    {
        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        return 0f;
    }
}
