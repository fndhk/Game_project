using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerVoiceChat : NetworkBehaviour
{
    private const string VoiceToServerMessage = "DarkUsVoiceToServer";
    private const string VoiceToClientMessage = "DarkUsVoiceToClient";

    private static readonly Dictionary<ulong, PlayerVoiceChat> playersByOwnerId = new Dictionary<ulong, PlayerVoiceChat>();
    private static NetworkManager registeredNetworkManager;
    private static bool handlersRegistered;

    [Header("Input")]
    public KeyCode pushToTalkKey = KeyCode.V;
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

    private void Awake()
    {
        ApplySavedVoiceVolume();

        playbackSource = GetComponent<AudioSource>();
        if (playbackSource == null)
        {
            playbackSource = gameObject.AddComponent<AudioSource>();
        }

        playbackSource.playOnAwake = false;
        playbackSource.loop = false;
        playbackSource.spatialBlend = spatialBlend;
        playbackSource.minDistance = minDistance;
        playbackSource.maxDistance = maxDistance;
        playbackSource.rolloffMode = AudioRolloffMode.Logarithmic;
    }

    public override void OnNetworkSpawn()
    {
        playersByOwnerId[OwnerClientId] = this;
        ApplySavedVoiceVolume();
        RegisterVoiceHandlers();
    }

    public override void OnNetworkDespawn()
    {
        if (playersByOwnerId.TryGetValue(OwnerClientId, out PlayerVoiceChat current) && current == this)
        {
            playersByOwnerId.Remove(OwnerClientId);
        }

        StopMicrophone();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        StopMicrophone();
    }

    private void Update()
    {
        ApplySavedVoiceVolume();

        if (!IsOwner)
        {
            return;
        }

        if (!voiceEnabled)
        {
            StopMicrophone();
            return;
        }

        if (Input.GetKey(pushToTalkKey))
        {
            if (!isCapturing)
            {
                StartMicrophone();
            }

            CaptureAndSendAvailableChunks();
            return;
        }

        isTalking = false;
        inputLevel = 0f;
        StopMicrophone();
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
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
        {
            return;
        }

        if (enableLocalMonitor)
        {
            PlayVoiceChunk(samples, sampleCount);
        }

        using FastBufferWriter writer = new FastBufferWriter(sizeof(ulong) + sizeof(int) + sizeof(int) + sampleCount * sizeof(short), Allocator.Temp);
        WriteVoicePayload(writer, OwnerClientId, sampleRate, samples, sampleCount);

        if (IsServer)
        {
            RelayVoicePayload(OwnerClientId, sampleRate, samples, sampleCount);
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            VoiceToServerMessage,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.UnreliableSequenced
        );
    }

    private static void RegisterVoiceHandlers()
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
        {
            return;
        }

        if (handlersRegistered && registeredNetworkManager == NetworkManager.Singleton)
        {
            return;
        }

        registeredNetworkManager = NetworkManager.Singleton;
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(VoiceToServerMessage, OnVoiceToServerMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(VoiceToClientMessage, OnVoiceToClientMessage);
        handlersRegistered = true;
    }

    private static void OnVoiceToServerMessage(ulong senderClientId, FastBufferReader reader)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        ReadVoicePayload(reader, out ulong ownerClientId, out int payloadSampleRate, out float[] samples);

        if (ownerClientId != senderClientId)
        {
            ownerClientId = senderClientId;
        }

        RelayVoicePayload(ownerClientId, payloadSampleRate, samples, samples.Length);
    }

    private static void OnVoiceToClientMessage(ulong senderClientId, FastBufferReader reader)
    {
        ReadVoicePayload(reader, out ulong ownerClientId, out int payloadSampleRate, out float[] samples);

        if (NetworkManager.Singleton != null && ownerClientId == NetworkManager.Singleton.LocalClientId)
        {
            return;
        }

        if (!playersByOwnerId.TryGetValue(ownerClientId, out PlayerVoiceChat voiceChat) || voiceChat == null)
        {
            return;
        }

        voiceChat.sampleRate = payloadSampleRate;
        voiceChat.PlayVoiceChunk(samples, samples.Length);
    }

    private static void RelayVoicePayload(ulong ownerClientId, int payloadSampleRate, float[] samples, int sampleCount)
    {
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.CustomMessagingManager == null)
        {
            return;
        }

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == ownerClientId)
            {
                continue;
            }

            using FastBufferWriter writer = new FastBufferWriter(sizeof(ulong) + sizeof(int) + sizeof(int) + sampleCount * sizeof(short), Allocator.Temp);
            WriteVoicePayload(writer, ownerClientId, payloadSampleRate, samples, sampleCount);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                VoiceToClientMessage,
                clientId,
                writer,
                NetworkDelivery.UnreliableSequenced
            );
        }
    }

    private static void WriteVoicePayload(FastBufferWriter writer, ulong ownerClientId, int payloadSampleRate, float[] samples, int sampleCount)
    {
        writer.WriteValueSafe(ownerClientId);
        writer.WriteValueSafe(payloadSampleRate);
        writer.WriteValueSafe(sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            short encodedSample = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * short.MaxValue), short.MinValue, short.MaxValue);
            writer.WriteValueSafe(encodedSample);
        }
    }

    private static void ReadVoicePayload(FastBufferReader reader, out ulong ownerClientId, out int payloadSampleRate, out float[] samples)
    {
        reader.ReadValueSafe(out ownerClientId);
        reader.ReadValueSafe(out payloadSampleRate);
        reader.ReadValueSafe(out int sampleCount);

        sampleCount = Mathf.Clamp(sampleCount, 1, 4096);
        samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            reader.ReadValueSafe(out short encodedSample);
            samples[i] = encodedSample / (float)short.MaxValue;
        }
    }

    private void PlayVoiceChunk(float[] samples, int sampleCount)
    {
        if (playbackSource == null || sampleCount <= 0)
        {
            return;
        }

        AudioClip clip = AudioClip.Create("VoiceChunk", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        playbackSource.PlayOneShot(clip, remoteVoiceVolume);
        Destroy(clip, Mathf.Max(0.5f, sampleCount / (float)sampleRate + 0.25f));
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
        remoteVoiceVolume = GetSavedVoiceVolume(OwnerClientId);
    }

    private static float GetSavedVoiceVolume(ulong ownerClientId)
    {
        string clientKey = "setting_voice_volume_client_" + ownerClientId;
        if (PlayerPrefs.HasKey(clientKey))
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(clientKey, 1f), 0f, 2f);
        }

        string actorFallbackKey = "setting_voice_volume_actor_" + (ownerClientId + 1UL);
        if (PlayerPrefs.HasKey(actorFallbackKey))
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(actorFallbackKey, 1f), 0f, 2f);
        }

        return Mathf.Clamp(PlayerPrefs.GetFloat("setting_voice_volume", 1f), 0f, 2f);
    }

    private void OnGUI()
    {
        if (!showLocalMicHud || !IsOwner)
        {
            return;
        }

        string state = isTalking ? "MIC TRANSMITTING" : "HOLD V TO TALK";
        GUI.Label(new Rect(18f, Screen.height - 42f, 260f, 24f), state + "  " + inputLevel.ToString("0.00"));
    }
}
