using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using POpusCodec.Enums;
using UnityEngine;

public class DarkUsPunVoiceClient : PunVoiceClient
{
    protected override Speaker InstantiateSpeakerForRemoteVoice(int playerId, byte voiceId, object userData)
    {
        return InstantiateSpeakerPrefab(gameObject, true);
    }
}

// Photon Voice 2 기반 음성채팅 어댑터이다.
// 기존 UI/HUD 호출부가 쓰는 정적 API는 유지하고, 실제 마이크 송수신은 Photon Voice가 처리한다.
public class PlayerVoiceChat : MonoBehaviour
{
    private static readonly Dictionary<int, Speaker> speakersByActor = new Dictionary<int, Speaker>();
    private static PlayerVoiceChat localVoiceChat;
    private static PunVoiceClient registeredSpeakerEventClient;
    private static bool localVoiceMuted;
    private static GameObject speakerPrefab;
    private static float lastMuteToggleTime = -100f;

    [Header("Input")]
    public KeyCode pushToTalkKey = KeyCode.V;
    public KeyCode muteToggleKey = KeyCode.B;
    public bool voiceEnabled = true;
    public bool enableLocalMonitor = false;

    [Header("Capture")]
    public string microphoneDeviceName = "";
    public int sampleRate = 16000;
    public int microphoneBufferSeconds = 1;
    public int chunkSampleCount = 720;
    public float silenceThreshold = 0.008f;
    public float voiceHangoverSeconds = 0.22f;

    [Header("Playback")]
    public float remoteVoiceVolume = 1f;
    public float spatialBlend = 1f;
    public float minDistance = 1.2f;
    public float maxDistance = 9f;

    [Header("Debug")]
    public bool showLocalMicHud = true;

    private Recorder recorder;
    private WebRtcAudioDsp audioDsp;
    private PunVoiceClient voiceClient;
    private int lastRecorderActorNumber = -1;
    private bool recorderAddedToVoiceClient;
    private bool muteKeyWasDown;

    private void Awake()
    {
        EnsurePhotonVoiceSetup();
    }

    private void OnEnable()
    {
        localVoiceChat = this;
        EnsurePhotonVoiceSetup();
    }

    private void OnDisable()
    {
        if (voiceClient != null && recorder != null && recorderAddedToVoiceClient)
        {
            voiceClient.RemoveRecorder(recorder);
            recorderAddedToVoiceClient = false;
        }

        if (localVoiceChat == this)
        {
            localVoiceChat = null;
        }
    }

    private void Update()
    {
        EnsurePhotonVoiceSetup();
        ApplySavedVoiceVolume();
        HandleMuteToggle();
        UpdateRecorderState();
        RefreshAllSpeakerAudioSources();
    }

    private void EnsurePhotonVoiceSetup()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        voiceClient = GetOrCreateVoiceClient();
        RegisterSpeakerEvents(voiceClient);
        EnsureSpeakerPrefab(voiceClient);
        EnsureRecorder();

        if (voiceClient != null && recorder != null)
        {
            voiceClient.PrimaryRecorder = recorder;

            if (!recorderAddedToVoiceClient)
            {
                recorderAddedToVoiceClient = voiceClient.AddRecorder(recorder);
            }
        }
    }

    private void EnsureRecorder()
    {
        if (recorder == null)
        {
            recorder = GetComponent<Recorder>();
            if (recorder == null)
            {
                recorder = gameObject.AddComponent<Recorder>();
            }
        }

        recorder.SourceType = Recorder.InputSourceType.Microphone;
        recorder.MicrophoneType = Recorder.MicType.Photon;
        recorder.SamplingRate = ToPhotonSamplingRate(sampleRate);
        recorder.FrameDuration = OpusCodec.FrameDuration.Frame20ms;
        recorder.Bitrate = 30000;
        recorder.VoiceDetection = true;
        recorder.VoiceDetectionThreshold = Mathf.Clamp(silenceThreshold, 0.001f, 0.2f);
        recorder.VoiceDetectionDelayMs = Mathf.RoundToInt(Mathf.Clamp(voiceHangoverSeconds, 0.05f, 2f) * 1000f);
        recorder.RecordWhenJoined = true;
        recorder.DebugEchoMode = enableLocalMonitor;
        recorder.ReliableMode = false;

        if (PhotonNetwork.LocalPlayer != null)
        {
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            if (lastRecorderActorNumber != actorNumber)
            {
                recorder.UserData = actorNumber;
                lastRecorderActorNumber = actorNumber;
            }
        }

        if (audioDsp == null)
        {
            audioDsp = GetComponent<WebRtcAudioDsp>();
            if (audioDsp == null)
            {
                audioDsp = gameObject.AddComponent<WebRtcAudioDsp>();
            }
        }
    }

    private void UpdateRecorderState()
    {
        if (recorder == null)
        {
            return;
        }

        bool canTransmit = PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null && voiceEnabled && !localVoiceMuted;
        recorder.TransmitEnabled = canTransmit;
        recorder.RecordingEnabled = canTransmit;
    }

    private void HandleMuteToggle()
    {
        bool isMuteKeyDown = Input.GetKey(GameInputBindings.MicMute);
        if (isMuteKeyDown && !muteKeyWasDown)
        {
            localVoiceMuted = !localVoiceMuted;
            voiceEnabled = !localVoiceMuted;
            lastMuteToggleTime = Time.unscaledTime;
        }

        muteKeyWasDown = isMuteKeyDown;
    }

    private static void RegisterSpeakerEvents(PunVoiceClient client)
    {
        if (client == null || registeredSpeakerEventClient == client)
        {
            return;
        }

        if (registeredSpeakerEventClient != null)
        {
            registeredSpeakerEventClient.SpeakerLinked -= OnSpeakerLinked;
        }

        client.SpeakerLinked += OnSpeakerLinked;
        registeredSpeakerEventClient = client;
    }

    private static void OnSpeakerLinked(Speaker speaker)
    {
        if (speaker == null || speaker.RemoteVoice == null)
        {
            return;
        }

        int actorNumber = GetActorNumberFromSpeaker(speaker);
        speakersByActor[actorNumber] = speaker;
        speaker.OnRemoteVoiceRemoveAction += removedSpeaker =>
        {
            if (removedSpeaker != null &&
                removedSpeaker.RemoteVoice != null &&
                speakersByActor.TryGetValue(GetActorNumberFromSpeaker(removedSpeaker), out Speaker current) &&
                current == removedSpeaker)
            {
                speakersByActor.Remove(GetActorNumberFromSpeaker(removedSpeaker));
            }
        };

        ConfigureSpeakerAudioSource(speaker);
    }

    private static PunVoiceClient GetOrCreateVoiceClient()
    {
        PunVoiceClient existingClient = Object.FindFirstObjectByType<PunVoiceClient>();
        if (existingClient != null)
        {
            return existingClient;
        }

        GameObject clientObject = new GameObject("DarkUsPunVoiceClient");
        return clientObject.AddComponent<DarkUsPunVoiceClient>();
    }

    private static void EnsureSpeakerPrefab(PunVoiceClient client)
    {
        if (client == null)
        {
            return;
        }

        if (speakerPrefab == null)
        {
            speakerPrefab = new GameObject("DarkUsPhotonVoiceSpeakerPrefab");
            speakerPrefab.hideFlags = HideFlags.HideAndDontSave;

            AudioSource source = speakerPrefab.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            speakerPrefab.AddComponent<Speaker>();
        }

        client.SpeakerPrefab = speakerPrefab;
    }

    private void RefreshAllSpeakerAudioSources()
    {
        List<int> invalidActors = null;

        foreach (KeyValuePair<int, Speaker> pair in speakersByActor)
        {
            if (pair.Value == null)
            {
                invalidActors ??= new List<int>();
                invalidActors.Add(pair.Key);
                continue;
            }

            ConfigureSpeakerAudioSource(pair.Value);
        }

        if (invalidActors == null)
        {
            return;
        }

        for (int i = 0; i < invalidActors.Count; i++)
        {
            speakersByActor.Remove(invalidActors[i]);
        }
    }

    private static void ConfigureSpeakerAudioSource(Speaker speaker)
    {
        if (speaker == null)
        {
            return;
        }

        PlayerVoiceChat activeVoice = localVoiceChat;
        AudioSource source = speaker.GetComponent<AudioSource>();
        if (source == null)
        {
            source = speaker.gameObject.AddComponent<AudioSource>();
        }

        int actorNumber = GetActorNumberFromSpeaker(speaker);
        source.volume = GetSavedVoiceVolume(actorNumber);
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;
        source.spatialBlend = activeVoice != null ? activeVoice.spatialBlend : 0f;
        source.minDistance = activeVoice != null ? activeVoice.minDistance : 1.2f;
        source.maxDistance = activeVoice != null ? activeVoice.maxDistance : 9f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, CreateVoiceRolloff(source.minDistance, source.maxDistance));

        if (source.spatialBlend > 0.01f && actorNumber > 0)
        {
            Transform remoteTransform = FindRemoteActorTransform(actorNumber);
            if (remoteTransform != null && !speaker.transform.IsChildOf(remoteTransform))
            {
                speaker.transform.SetParent(remoteTransform, false);
                speaker.transform.localPosition = Vector3.up * 1.65f;
            }
        }
        else if (activeVoice != null && !speaker.transform.IsChildOf(activeVoice.transform))
        {
            speaker.transform.SetParent(activeVoice.transform, false);
            speaker.transform.localPosition = Vector3.zero;
        }
    }

    private static AnimationCurve CreateVoiceRolloff(float minDistance, float maxDistance)
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(Mathf.Max(0.01f, minDistance), 1f),
            new Keyframe(Mathf.Max(minDistance + 0.01f, maxDistance), 0f)
        );
    }

    private static Transform FindRemoteActorTransform(int actorNumber)
    {
        if (actorNumber <= 0)
        {
            return null;
        }

        GameObject remoteObject = GameObject.Find("RemotePlayer_" + actorNumber);
        return remoteObject != null ? remoteObject.transform : null;
    }

    private static int GetActorNumberFromSpeaker(Speaker speaker)
    {
        if (speaker == null || speaker.RemoteVoice == null)
        {
            return 0;
        }

        object userData = speaker.RemoteVoice.VoiceInfo.UserData;
        if (userData is int intValue)
        {
            return intValue;
        }

        if (userData is short shortValue)
        {
            return shortValue;
        }

        if (userData is byte byteValue)
        {
            return byteValue;
        }

        if (userData is string text && int.TryParse(text, out int parsedValue))
        {
            return parsedValue;
        }

        return speaker.RemoteVoice.PlayerId;
    }

    public static bool IsActorSpeaking(int actorNumber)
    {
        if (actorNumber <= 0)
        {
            return false;
        }

        if (PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.ActorNumber == actorNumber &&
            localVoiceChat != null &&
            localVoiceChat.recorder != null)
        {
            return !localVoiceMuted && localVoiceChat.recorder.IsCurrentlyTransmitting;
        }

        return speakersByActor.TryGetValue(actorNumber, out Speaker speaker) &&
               speaker != null &&
               speaker.IsPlaying;
    }

    public static bool IsLocalMuted()
    {
        return localVoiceMuted;
    }

    public static float LastMuteToggleTime
    {
        get { return lastMuteToggleTime; }
    }

    public static void ApplySavedVoiceVolumeToAll()
    {
        foreach (Speaker speaker in speakersByActor.Values)
        {
            ConfigureSpeakerAudioSource(speaker);
        }

        if (localVoiceChat != null)
        {
            localVoiceChat.ApplySavedVoiceVolume();
        }
    }

    private void ApplySavedVoiceVolume()
    {
        int actorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        remoteVoiceVolume = GetSavedVoiceVolume(actorNumber);
    }

    private static float GetSavedVoiceVolume(int actorNumber)
    {
        string actorKey = "setting_voice_volume_actor_" + actorNumber;
        if (actorNumber > 0 && PlayerPrefs.HasKey(actorKey))
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(actorKey, 1f), 0f, 2f);
        }

        string clientKey = "setting_voice_volume_client_" + Mathf.Max(0, actorNumber - 1);
        if (PlayerPrefs.HasKey(clientKey))
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(clientKey, 1f), 0f, 2f);
        }

        return Mathf.Clamp(PlayerPrefs.GetFloat("setting_voice_volume", 1f), 0f, 2f);
    }

    private static SamplingRate ToPhotonSamplingRate(int requestedSampleRate)
    {
        if (requestedSampleRate <= 8000)
        {
            return SamplingRate.Sampling08000;
        }

        if (requestedSampleRate <= 12000)
        {
            return SamplingRate.Sampling12000;
        }

        if (requestedSampleRate <= 16000)
        {
            return SamplingRate.Sampling16000;
        }

        if (requestedSampleRate <= 24000)
        {
            return SamplingRate.Sampling24000;
        }

        return SamplingRate.Sampling48000;
    }

    private void OnGUI()
    {
        if (!showLocalMicHud || !Application.isPlaying || !PhotonNetwork.InRoom || recorder == null)
        {
            return;
        }

        float level = recorder.LevelMeter != null ? recorder.LevelMeter.CurrentAvgAmp : 0f;
        string state = voiceEnabled && !localVoiceMuted
            ? (recorder.IsCurrentlyTransmitting ? "MIC TRANSMITTING" : "MIC OPEN")
            : "MIC MUTED";
        GUI.Label(new Rect(18f, Screen.height - 42f, 260f, 24f), state + "  " + level.ToString("0.00"));
    }
}
