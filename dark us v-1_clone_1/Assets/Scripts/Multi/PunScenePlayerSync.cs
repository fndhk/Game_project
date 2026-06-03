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
    private PlayerVoiceChat localVoiceChat;
    private PlayerCombatTarget localCombatTarget;
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
        ConfigureWorldAudioSync();
        EnsureLocalVoiceChat();
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
        ConfigureWorldAudioSync();
        EnsureLocalVoiceChat();
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
        UpdateRemotePlayers();

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null || Time.time < nextSendTime || IsLocalPlayerDead())
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
        if (photonEvent.Code != TransformEventCode)
        {
            return;
        }

        if (PhotonNetwork.LocalPlayer != null && photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber)
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

    private bool IsLocalPlayerDead()
    {
        if (localCombatTarget == null)
        {
            localCombatTarget = GetComponent<PlayerCombatTarget>();
        }

        return localCombatTarget != null && localCombatTarget.isDead;
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
        EnsureRemoteScanFallbackColliders(remoteObject);

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

    private void EnsureRemoteScanFallbackColliders(GameObject remoteObject)
    {
        if (remoteObject == null || remoteObject.transform.Find("RemoteScanFallback") != null)
        {
            return;
        }

        const int scanLayer = 7;
        GameObject root = new GameObject("RemoteScanFallback");
        root.layer = scanLayer;
        root.transform.SetParent(remoteObject.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        CreateRemoteScanCapsule(root.transform, "Body", new Vector3(0f, 0.9f, 0f), 0.28f, 1.55f, scanLayer);
        CreateRemoteScanSphere(root.transform, "Head", new Vector3(0f, 1.65f, 0f), 0.22f, scanLayer);
    }

    private void CreateRemoteScanCapsule(Transform parent, string objectName, Vector3 center, float radius, float height, int layer)
    {
        GameObject colliderObject = new GameObject(objectName);
        colliderObject.layer = layer;
        colliderObject.transform.SetParent(parent, false);
        CapsuleCollider capsule = colliderObject.AddComponent<CapsuleCollider>();
        capsule.center = center;
        capsule.radius = radius;
        capsule.height = height;
        capsule.direction = 1;
        capsule.isTrigger = false;
        ScanSurfaceInfo surfaceInfo = colliderObject.AddComponent<ScanSurfaceInfo>();
        surfaceInfo.surfaceType = ScanSurfaceType.PlayerBody;
    }

    private void CreateRemoteScanSphere(Transform parent, string objectName, Vector3 center, float radius, int layer)
    {
        GameObject colliderObject = new GameObject(objectName);
        colliderObject.layer = layer;
        colliderObject.transform.SetParent(parent, false);
        SphereCollider sphere = colliderObject.AddComponent<SphereCollider>();
        sphere.center = center;
        sphere.radius = radius;
        sphere.isTrigger = false;
        ScanSurfaceInfo surfaceInfo = colliderObject.AddComponent<ScanSurfaceInfo>();
        surfaceInfo.surfaceType = ScanSurfaceType.PlayerBody;
    }

    private void UpdateRemoteRole(int actorNumber, RemotePlayerState state)
    {
        if (state == null || state.combatTarget == null)
        {
            return;
        }

        state.combatTarget.role = RoleAssignmentManager.IsActorImposter(actorNumber) ? PlayerRole.Killer : PlayerRole.Citizen;
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
        localVoiceChat.voiceEnabled = true;
        localVoiceChat.showLocalMicHud = true;
    }

    private void AddRemoteFootsteps(GameObject remoteObject)
    {
        if (remoteObject == null)
        {
            return;
        }

        PlayerFootstepAudio footsteps = remoteObject.GetComponent<PlayerFootstepAudio>();
        if (footsteps != null)
        {
            footsteps.enabled = false;
        }
    }

    private void ConfigureWorldAudioSync()
    {
        PunWorldAudioSync worldAudioSync = PunWorldAudioSync.EnsureExists();
        worldAudioSync.minDistance = remoteFootstepMinDistance;
        worldAudioSync.maxDistance = remoteFootstepMaxDistance;
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
