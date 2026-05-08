using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PunScenePlayerSync : MonoBehaviour, IOnEventCallback
{
    private const byte TransformEventCode = 42;

    public float sendInterval = 0.05f;
    public string remoteAvatarRootName = "ScanOnlyCharacterAvatar";
    public float avatarHeadLocalHeight = 1.77f;

    private readonly Dictionary<int, Transform> remotePlayers = new Dictionary<int, Transform>();
    private Transform eyeTransform;
    private float nextSendTime;

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
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
        Transform remote = GetOrCreateRemotePlayer(photonEvent.Sender);
        remote.SetPositionAndRotation(position, rotation);
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

    private Transform GetOrCreateRemotePlayer(int actorNumber)
    {
        if (remotePlayers.TryGetValue(actorNumber, out Transform remote) && remote != null)
        {
            return remote;
        }

        GameObject remoteObject = new GameObject("RemotePlayer_" + actorNumber);
        remoteObject.name = "RemotePlayer_" + actorNumber;
        remoteObject.transform.localScale = Vector3.one;

        PlayerScanAvatar scanAvatar = remoteObject.AddComponent<PlayerScanAvatar>();
        scanAvatar.avatarRootName = remoteAvatarRootName;
        scanAvatar.hideRenderers = true;
        scanAvatar.hideAttachedBodyRenderers = true;
        scanAvatar.disableSelfScanWhenScannerIsHere = false;
        scanAvatar.RebuildAvatar();

        remotePlayers[actorNumber] = remoteObject.transform;
        return remoteObject.transform;
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
