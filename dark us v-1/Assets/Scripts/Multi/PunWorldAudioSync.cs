using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PunWorldAudioSync : MonoBehaviour, IOnEventCallback
{
    private const byte WorldAudioEventCode = 44;

    private enum SoundKind : byte
    {
        ScanPulse = 1,
        ComputerStart = 2,
        Footstep = 3
    }

    private static PunWorldAudioSync instance;

    public float minDistance = 1.5f;
    public float maxDistance = 14f;

    private Transform cachedAudioListenerTransform;
    private float nextAudioListenerSearchTime;

    public static PunWorldAudioSync EnsureExists()
    {
        if (instance != null)
        {
            return instance;
        }

        PunWorldAudioSync existing = Object.FindFirstObjectByType<PunWorldAudioSync>();
        if (existing != null)
        {
            instance = existing;
            return existing;
        }

        GameObject syncObject = new GameObject("PunWorldAudioSync");
        instance = syncObject.AddComponent<PunWorldAudioSync>();
        DontDestroyOnLoad(syncObject);
        return instance;
    }

    public static void RaiseScanPulse(Vector3 position, float volume)
    {
        Raise(SoundKind.ScanPulse, position, volume, SendOptions.SendUnreliable);
    }

    public static void RaiseComputerStart(Vector3 position, float volume)
    {
        Raise(SoundKind.ComputerStart, position, volume, SendOptions.SendReliable);
    }

    public static void RaiseFootstep(Vector3 position, float volume)
    {
        Raise(SoundKind.Footstep, position, volume, SendOptions.SendUnreliable);
    }

    private static void Raise(SoundKind kind, Vector3 position, float volume, SendOptions sendOptions)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return;
        }

        EnsureExists();
        object[] payload =
        {
            (byte)kind,
            position.x,
            position.y,
            position.z,
            Mathf.Clamp(volume, 0.01f, 2.5f)
        };

        PhotonNetwork.RaiseEvent(
            WorldAudioEventCode,
            payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            sendOptions
        );
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);

        if (instance == this)
        {
            instance = null;
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != WorldAudioEventCode)
        {
            return;
        }

        if (PhotonNetwork.LocalPlayer != null && photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return;
        }

        object[] payload = photonEvent.CustomData as object[];
        if (payload == null || payload.Length < 5)
        {
            return;
        }

        SoundKind kind = (SoundKind)ToInt(payload[0]);
        Vector3 position = new Vector3(ToFloat(payload[1]), ToFloat(payload[2]), ToFloat(payload[3]));
        float volume = Mathf.Clamp(ToFloat(payload[4]), 0.01f, 2.5f);

        if (!IsWithinAudibleRange(position))
        {
            return;
        }

        AudioClip clip = ResolveClip(kind);

        if (clip == null)
        {
            return;
        }

        PlaySpatialOneShot(clip, position, volume);
    }

    private AudioClip ResolveClip(SoundKind kind)
    {
        switch (kind)
        {
            case SoundKind.ScanPulse:
                LidarSpotScanner scanner = Object.FindFirstObjectByType<LidarSpotScanner>();
                if (scanner == null)
                {
                    return null;
                }

                AudioSource scanSource = GetPrivateField<AudioSource>(scanner, "scanPulseSource");
                return scanSource != null ? scanSource.clip : null;

            case SoundKind.ComputerStart:
                ObjectiveComputer computer = Object.FindFirstObjectByType<ObjectiveComputer>();
                if (computer == null)
                {
                    return null;
                }

                if (computer.startAudioSource != null && computer.startAudioSource.clip != null)
                {
                    return computer.startAudioSource.clip;
                }

                return computer.loopAudioSource != null ? computer.loopAudioSource.clip : null;

            case SoundKind.Footstep:
                PlayerFootstepAudio footsteps = Object.FindFirstObjectByType<PlayerFootstepAudio>();
                return footsteps != null ? GetFootstepClip(footsteps) : null;

            default:
                return null;
        }
    }

    private AudioClip GetFootstepClip(PlayerFootstepAudio footsteps)
    {
        AudioClip[] clips = footsteps.walkClips;

        if (clips == null || clips.Length == 0)
        {
            clips = footsteps.commonClips;
        }

        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        return clips[Random.Range(0, clips.Length)];
    }

    private T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        if (target == null)
        {
            return null;
        }

        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );

        return field != null ? field.GetValue(target) as T : null;
    }

    private void PlaySpatialOneShot(AudioClip clip, Vector3 position, float volume)
    {
        GameObject audioObject = new GameObject("RemoteWorldAudio_" + clip.name);
        audioObject.transform.position = position;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.Play();

        Destroy(audioObject, clip.length + 0.25f);
    }

    private bool IsWithinAudibleRange(Vector3 position)
    {
        Transform listenerTransform = GetAudioListenerTransform();
        if (listenerTransform == null)
        {
            return true;
        }

        float cutoffDistance = Mathf.Max(0.1f, maxDistance);
        return (listenerTransform.position - position).sqrMagnitude <= cutoffDistance * cutoffDistance;
    }

    private Transform GetAudioListenerTransform()
    {
        if (cachedAudioListenerTransform != null && cachedAudioListenerTransform.gameObject.activeInHierarchy)
        {
            return cachedAudioListenerTransform;
        }

        if (Time.unscaledTime < nextAudioListenerSearchTime)
        {
            return cachedAudioListenerTransform;
        }

        nextAudioListenerSearchTime = Time.unscaledTime + 0.5f;
        AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
        if (listener != null)
        {
            cachedAudioListenerTransform = listener.transform;
            return cachedAudioListenerTransform;
        }

        Camera mainCamera = Camera.main;
        cachedAudioListenerTransform = mainCamera != null ? mainCamera.transform : null;
        return cachedAudioListenerTransform;
    }

    private int ToInt(object value)
    {
        if (value is byte byteValue)
        {
            return byteValue;
        }

        if (value is short shortValue)
        {
            return shortValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return 0;
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

        if (value is int intValue)
        {
            return intValue;
        }

        return 0f;
    }
}
