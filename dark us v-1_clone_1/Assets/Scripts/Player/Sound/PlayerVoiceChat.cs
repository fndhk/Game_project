using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PlayerVoiceChat : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte VoiceEventCode = 43;
    private const int VoiceChannels = 1;
    private const float RemoteBufferDelaySeconds = 0.20f;
    private const float RemoteBufferMaxSeconds = 0.95f;

    private static readonly Dictionary<int, AudioSource> remotePlaybackSources = new Dictionary<int, AudioSource>();
    private static readonly Dictionary<int, RemoteVoicePlayback> remotePlaybackStates = new Dictionary<int, RemoteVoicePlayback>();
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
    public int sampleRate = 24000;
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

    private AudioSource playbackSource;
    private AudioClip microphoneClip;
    private float[] microphoneBuffer;
    private float[] chunkBuffer;
    private int lastMicrophonePosition;
    private bool isCapturing;
    private bool isTalking;
    private float inputLevel;
    private bool muteKeyWasDown;
    private float lastAudibleInputTime = -100f;

    private class RemoteVoicePlayback
    {
        private readonly Queue<float> queuedSamples = new Queue<float>();
        private readonly object sampleLock = new object();

        private AudioSource source;
        private AudioClip streamClip;
        private int streamSampleRate;
        private int maxQueuedSamples;
        private int bufferedStartSamples;
        private float outputGain = 1f;
        private float lastOutputSample;

        public void Configure(AudioSource targetSource, int sampleRate, float gain)
        {
            source = targetSource;
            streamSampleRate = Mathf.Max(8000, sampleRate);
            maxQueuedSamples = Mathf.RoundToInt(streamSampleRate * RemoteBufferMaxSeconds);
            bufferedStartSamples = Mathf.RoundToInt(streamSampleRate * RemoteBufferDelaySeconds);
            outputGain = gain;

            if (streamClip == null || streamClip.frequency != streamSampleRate)
            {
                if (source != null)
                {
                    source.Stop();
                }

                streamClip = AudioClip.Create("PhotonVoiceStream", streamSampleRate, VoiceChannels, streamSampleRate, true, OnAudioRead);

                if (source != null)
                {
                    source.clip = streamClip;
                    source.loop = true;
                }

                Clear();
            }

            if (source != null && source.clip != streamClip)
            {
                source.clip = streamClip;
                source.loop = true;
            }
        }

        public void Enqueue(float[] samples, int sampleCount)
        {
            if (samples == null || sampleCount <= 0)
            {
                return;
            }

            lock (sampleLock)
            {
                while (queuedSamples.Count + sampleCount > maxQueuedSamples && queuedSamples.Count > 0)
                {
                    queuedSamples.Dequeue();
                }

                int clampedCount = Mathf.Min(sampleCount, samples.Length);
                for (int i = 0; i < clampedCount; i++)
                {
                    queuedSamples.Enqueue(samples[i]);
                }

                if (source != null && !source.isPlaying && queuedSamples.Count >= bufferedStartSamples)
                {
                    source.Play();
                }
            }
        }

        private void Clear()
        {
            lock (sampleLock)
            {
                queuedSamples.Clear();
            }
        }

        private void OnAudioRead(float[] data)
        {
            lock (sampleLock)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (queuedSamples.Count > 0)
                    {
                        lastOutputSample = queuedSamples.Dequeue() * outputGain;
                    }
                    else
                    {
                        lastOutputSample *= 0.94f;
                    }

                    data[i] = lastOutputSample;
                }
            }
        }
    }

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
        bool isMuteKeyDown = Input.GetKey(GameInputBindings.MicMute);
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
        lastAudibleInputTime = -100f;
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
            if (inputLevel >= silenceThreshold)
            {
                lastAudibleInputTime = Time.unscaledTime;
            }

            isTalking = Time.unscaledTime - lastAudibleInputTime <= voiceHangoverSeconds;

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
        lastVoiceReceivedTimeByActor[photonEvent.Sender] = Time.unscaledTime;
        receiver.PlayVoiceChunk(DecodeSamples(encodedSamples), encodedSamples.Length / 2, photonEvent.Sender, payloadSampleRate);
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
        PlayVoiceChunk(samples, sampleCount, actorNumber, sampleRate);
    }

    private void PlayVoiceChunk(float[] samples, int sampleCount, int actorNumber, int playbackSampleRate)
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

        if (!remotePlaybackStates.TryGetValue(actorNumber, out RemoteVoicePlayback playbackState) || playbackState == null)
        {
            playbackState = new RemoteVoicePlayback();
            remotePlaybackStates[actorNumber] = playbackState;
        }

        playbackState.Configure(targetSource, playbackSampleRate, GetSavedVoiceVolume(actorNumber));
        playbackState.Enqueue(samples, sampleCount);
    }

    private AudioSource GetPlaybackSourceForActor(int actorNumber)
    {
        Transform remoteTransform = FindRemoteActorTransform(actorNumber);
        if (actorNumber > 0 && remoteTransform == null && spatialBlend > 0.01f)
        {
            return null;
        }

        AudioSource cachedSource = null;
        if (actorNumber > 0)
        {
            remotePlaybackSources.TryGetValue(actorNumber, out cachedSource);
        }

        if (actorNumber > 0 && cachedSource != null && remoteTransform != null && !cachedSource.transform.IsChildOf(remoteTransform))
        {
            remotePlaybackSources.Remove(actorNumber);
            remotePlaybackStates.Remove(actorNumber);
            cachedSource = null;
        }

        if (cachedSource != null)
        {
            ConfigurePlaybackSource(cachedSource);
            return cachedSource;
        }

        GameObject sourceObject = remoteTransform != null
            ? GetOrCreateVoicePlaybackObject(remoteTransform)
            : GetOrCreateLobbyVoicePlaybackObject(actorNumber);
        AudioSource source = GetOrCreatePlaybackSource(sourceObject);
        ConfigurePlaybackSource(source);

        if (actorNumber > 0)
        {
            remotePlaybackSources[actorNumber] = source;
        }

        return source;
    }

    private GameObject GetOrCreateLobbyVoicePlaybackObject(int actorNumber)
    {
        string objectName = "LobbyVoicePlayback_" + Mathf.Max(0, actorNumber);
        Transform existing = transform.Find(objectName);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject voiceObject = new GameObject(objectName);
        voiceObject.transform.SetParent(transform, false);
        voiceObject.transform.localPosition = Vector3.zero;
        return voiceObject;
    }

    private GameObject GetOrCreateVoicePlaybackObject(Transform actorTransform)
    {
        Transform existing = actorTransform.Find("VoicePlayback");
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject voiceObject = new GameObject("VoicePlayback");
        voiceObject.transform.SetParent(actorTransform, false);
        voiceObject.transform.localPosition = Vector3.up * 1.65f;
        return voiceObject;
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
        source.dopplerLevel = 0f;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Custom;
        AnimationCurve rolloff = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(Mathf.Max(0.01f, minDistance), 1f),
            new Keyframe(Mathf.Max(minDistance + 0.01f, maxDistance), 0f)
        );
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloff);
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
