using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerVoiceChat : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte VoiceEventCode = 43;

    private static readonly Dictionary<int, AudioSource> remotePlaybackSources = new Dictionary<int, AudioSource>();
    private static readonly Dictionary<int, float> lastVoiceReceivedTimeByActor = new Dictionary<int, float>();
    private static PlayerVoiceChat localVoiceChat;
    private static bool localVoiceMuted;
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
    public int chunkSampleCount = 1024;
    public float silenceThreshold = 0.015f;

    [Header("Playback")]
    public float remoteVoiceVolume = 1f;
    public float spatialBlend = 1f;
    public float minDistance = 1.5f;
    public float maxDistance = 18f;

    [Header("Debug")]
    public bool showLocalMicHud = true;

    private AudioSource playbackSource;
    private AudioClip microphoneClip;
    private float[] microphoneBuffer;
    private float[] chunkBuffer;
    private int lastMicrophonePosition;
    private bool isCapturing;
    private bool isTalking;
    private float inputLevel;
    private bool muteKeyWasDown;

    private void Awake()
    {
        playbackSource = GetOrCreatePlaybackSource(gameObject);
        ConfigurePlaybackSource(playbackSource);
        ApplySavedVoiceVolume();
    }

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
        if (IsLocalVoiceOwner())
        {
            localVoiceChat = this;
        }
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
        StopMicrophone();

        if (localVoiceChat == this)
        {
            localVoiceChat = null;
        }
    }

    private void OnDestroy()
    {
        StopMicrophone();
    }

    private void Update()
    {
        ApplySavedVoiceVolume();
        HandleMuteToggle();
        voiceEnabled = !localVoiceMuted;

        if (!CanCaptureLocalVoice())
        {
            StopMicrophone();
            return;
        }

        if (!voiceEnabled)
        {
            StopMicrophone();
            return;
        }

        if (!isCapturing)
        {
            StartMicrophone();
        }

        CaptureAndSendAvailableChunks();
    }

    private void HandleMuteToggle()
    {
        bool isMuteKeyDown = Input.GetKey(muteToggleKey);
        if (isMuteKeyDown && !muteKeyWasDown)
        {
            localVoiceMuted = !localVoiceMuted;
            voiceEnabled = !localVoiceMuted;
            lastMuteToggleTime = Time.unscaledTime;

            if (!voiceEnabled)
            {
                StopMicrophone();
            }
        }

        muteKeyWasDown = isMuteKeyDown;
    }

    private bool CanCaptureLocalVoice()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return false;
        }

        return IsLocalVoiceOwner();
    }

    private bool IsLocalVoiceOwner()
    {
        PhotonView view = GetComponent<PhotonView>();
        return view == null || view.IsMine;
    }

    private void StartMicrophone()
    {
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone device found.");
            return;
        }

        string deviceName = string.IsNullOrWhiteSpace(microphoneDeviceName) ? null : microphoneDeviceName;
        microphoneClip = Microphone.Start(deviceName, true, microphoneBufferSeconds, sampleRate);
        lastMicrophonePosition = 0;
        microphoneBuffer = new float[microphoneClip.samples];
        chunkBuffer = new float[chunkSampleCount];
        isCapturing = true;
    }

    private void StopMicrophone()
    {
        if (!isCapturing)
        {
            return;
        }

        string deviceName = string.IsNullOrWhiteSpace(microphoneDeviceName) ? null : microphoneDeviceName;
        Microphone.End(deviceName);
        microphoneClip = null;
        isCapturing = false;
        isTalking = false;
        inputLevel = 0f;
    }

    private void CaptureAndSendAvailableChunks()
    {
        if (microphoneClip == null)
        {
            return;
        }

        int currentPosition = Microphone.GetPosition(string.IsNullOrWhiteSpace(microphoneDeviceName) ? null : microphoneDeviceName);
        if (currentPosition < 0)
        {
            return;
        }

        int availableSamples = currentPosition - lastMicrophonePosition;
        if (availableSamples < 0)
        {
            availableSamples += microphoneClip.samples;
        }

        if (availableSamples < chunkSampleCount)
        {
            return;
        }

        microphoneClip.GetData(microphoneBuffer, 0);

        while (availableSamples >= chunkSampleCount)
        {
            float sum = 0f;
            for (int i = 0; i < chunkSampleCount; i++)
            {
                float sample = microphoneBuffer[(lastMicrophonePosition + i) % microphoneClip.samples];
                chunkBuffer[i] = sample;
                sum += Mathf.Abs(sample);
            }

            inputLevel = sum / chunkSampleCount;
            isTalking = inputLevel >= silenceThreshold;

            if (isTalking || enableLocalMonitor)
            {
                SendVoiceChunk(chunkBuffer, chunkSampleCount);
            }

            lastMicrophonePosition = (lastMicrophonePosition + chunkSampleCount) % microphoneClip.samples;
            availableSamples -= chunkSampleCount;
        }
    }

    private void SendVoiceChunk(float[] samples, int sampleCount)
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (enableLocalMonitor)
        {
            PlayVoiceChunk(samples, sampleCount, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        object[] payload =
        {
            sampleRate,
            EncodeSamples(samples, sampleCount)
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.Others
        };

        PhotonNetwork.RaiseEvent(VoiceEventCode, payload, options, SendOptions.SendUnreliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != VoiceEventCode)
        {
            return;
        }

        if (PhotonNetwork.LocalPlayer != null && photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return;
        }

        object[] payload = photonEvent.CustomData as object[];
        if (payload == null || payload.Length < 2)
        {
            return;
        }

        int payloadSampleRate = ToInt(payload[0], sampleRate);
        byte[] encodedSamples = payload[1] as byte[];
        if (encodedSamples == null || encodedSamples.Length <= 0)
        {
            return;
        }

        PlayerVoiceChat receiver = localVoiceChat != null ? localVoiceChat : this;
        receiver.sampleRate = payloadSampleRate;
        lastVoiceReceivedTimeByActor[photonEvent.Sender] = Time.unscaledTime;
        receiver.PlayVoiceChunk(DecodeSamples(encodedSamples), encodedSamples.Length / 2, photonEvent.Sender);
    }

    public static bool IsActorSpeaking(int actorNumber)
    {
        if (actorNumber <= 0)
        {
            return false;
        }

        if (PhotonNetwork.LocalPlayer != null &&
            PhotonNetwork.LocalPlayer.ActorNumber == actorNumber &&
            localVoiceChat != null)
        {
            return !localVoiceMuted && localVoiceChat.isTalking;
        }

        return lastVoiceReceivedTimeByActor.TryGetValue(actorNumber, out float lastVoiceTime) &&
               Time.unscaledTime - lastVoiceTime <= 0.45f;
    }

    public static bool IsLocalMuted()
    {
        return localVoiceMuted;
    }

    public static float LastMuteToggleTime
    {
        get { return lastMuteToggleTime; }
    }

    private byte[] EncodeSamples(float[] samples, int sampleCount)
    {
        sampleCount = Mathf.Clamp(sampleCount, 1, samples.Length);
        byte[] encodedSamples = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            short encodedSample = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            encodedSamples[i * 2] = (byte)(encodedSample & 0xFF);
            encodedSamples[i * 2 + 1] = (byte)((encodedSample >> 8) & 0xFF);
        }

        return encodedSamples;
    }

    private float[] DecodeSamples(byte[] encodedSamples)
    {
        int sampleCount = encodedSamples.Length / 2;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short encodedSample = (short)(encodedSamples[i * 2] | (encodedSamples[i * 2 + 1] << 8));
            samples[i] = encodedSample / (float)short.MaxValue;
        }

        return samples;
    }

    private void PlayVoiceChunk(float[] samples, int sampleCount, int actorNumber)
    {
        if (samples == null || sampleCount <= 0)
        {
            return;
        }

        AudioSource targetSource = GetPlaybackSourceForActor(actorNumber);
        if (targetSource == null)
        {
            return;
        }

        AudioClip clip = AudioClip.Create("PhotonVoiceChunk", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        targetSource.PlayOneShot(clip, GetSavedVoiceVolume(actorNumber));
        Destroy(clip, Mathf.Max(0.5f, sampleCount / (float)sampleRate + 0.25f));
    }

    private AudioSource GetPlaybackSourceForActor(int actorNumber)
    {
        if (actorNumber > 0 &&
            remotePlaybackSources.TryGetValue(actorNumber, out AudioSource cachedSource) &&
            cachedSource != null)
        {
            return cachedSource;
        }

        Transform remoteTransform = FindRemoteActorTransform(actorNumber);
        GameObject sourceObject = remoteTransform != null ? remoteTransform.gameObject : gameObject;
        AudioSource source = GetOrCreatePlaybackSource(sourceObject);
        ConfigurePlaybackSource(source);

        if (actorNumber > 0)
        {
            remotePlaybackSources[actorNumber] = source;
        }

        return source;
    }

    private Transform FindRemoteActorTransform(int actorNumber)
    {
        if (actorNumber <= 0)
        {
            return null;
        }

        GameObject remoteObject = GameObject.Find("RemotePlayer_" + actorNumber);
        return remoteObject != null ? remoteObject.transform : null;
    }

    private AudioSource GetOrCreatePlaybackSource(GameObject sourceObject)
    {
        AudioSource source = sourceObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = sourceObject.AddComponent<AudioSource>();
        }

        return source;
    }

    private void ConfigurePlaybackSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    public static void ApplySavedVoiceVolumeToAll()
    {
        PlayerVoiceChat[] voiceChats = FindObjectsOfType<PlayerVoiceChat>(true);
        for (int i = 0; i < voiceChats.Length; i++)
        {
            voiceChats[i].ApplySavedVoiceVolume();
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
        if (!showLocalMicHud || !CanCaptureLocalVoice())
        {
            return;
        }

        string state = voiceEnabled ? (isTalking ? "MIC TRANSMITTING" : "MIC OPEN") : "MIC MUTED";
        GUI.Label(new Rect(18f, Screen.height - 42f, 260f, 24f), state + "  " + inputLevel.ToString("0.00"));
    }
}
