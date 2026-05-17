using UnityEngine;
using UnityEngine.SceneManagement;

public class GameAudioManager : MonoBehaviour
{
    private const float SfxOutputMultiplier = 0.42f;
    private const float UiHoverCooldown = 0.055f;
    private const string MenuMusicPath = "Audio/BGM/MainMenu";
    private const string InGameMusicPath = "Audio/BGM/InGame";
    private const string UiHoverPath = "Audio/UI/UI_Hover";
    private const string UiClickPath = "Audio/UI/UI_Click";
    private const string ColorSelectPath = "Audio/UI/Color_Select";
    private const string InteractPath = "Audio/Game/Interact";
    private const string PickupPath = "Audio/Game/Pickup";
    private const string ItemDropPath = "Audio/Game/Item_Drop";
    private const string GameOverCitizenPath = "Audio/Game/GameOver_Citizens";
    private const string GameOverKillerPath = "Audio/Game/GameOver_Killer";

    private static GameAudioManager instance;

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private string currentMusicKey;
    private float lastUiHoverAt = -100f;

    private AudioClip menuMusicClip;
    private AudioClip inGameMusicClip;
    private AudioClip uiHoverClip;
    private AudioClip uiClickClip;
    private AudioClip colorSelectClip;
    private AudioClip interactClip;
    private AudioClip pickupClip;
    private AudioClip itemDropClip;
    private AudioClip gameOverCitizenClip;
    private AudioClip gameOverKillerClip;
    private AudioClip cameraUseClip;
    private AudioClip knifeUseClip;
    private AudioClip medkitUseClip;
    private AudioClip genericItemUseClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void ApplyVolumes()
    {
        EnsureInstance();

        if (instance.musicSource != null)
        {
            instance.musicSource.volume = SettingsManager.BgmVolume;
        }

        if (instance.sfxSource != null)
        {
            instance.sfxSource.volume = SettingsManager.SfxVolume * SfxOutputMultiplier;
        }
    }

    public static void PlayUiHover()
    {
        GameAudioManager manager = Instance;
        if (Time.unscaledTime - manager.lastUiHoverAt < UiHoverCooldown)
        {
            return;
        }

        manager.lastUiHoverAt = Time.unscaledTime;
        PlaySfx(manager.uiHoverClip, 0.38f);
    }

    public static void PlayUiClick()
    {
        PlaySfx(Instance.uiClickClip, 0.42f);
    }

    public static void PlayColorSelect()
    {
        PlaySfx(Instance.colorSelectClip, 0.48f);
    }

    public static void PlayInteract()
    {
        PlaySfx(Instance.interactClip, 0.46f);
    }

    public static void PlayPickup()
    {
        PlaySfx(Instance.pickupClip, 0.44f);
    }

    public static void PlayItemDrop()
    {
        PlaySfx(Instance.itemDropClip, 0.42f);
    }

    public static void PlayItemUse(ItemType itemType)
    {
        AudioClip clip;
        switch (itemType)
        {
            case ItemType.Camera:
                clip = Instance.cameraUseClip;
                break;

            case ItemType.Knife:
                clip = Instance.knifeUseClip;
                break;

            case ItemType.Medkit:
                clip = Instance.medkitUseClip;
                break;

            default:
                clip = Instance.genericItemUseClip;
                break;
        }

        PlaySfx(clip, 0.48f);
    }

    public static void PlayGameOver(bool citizensWon)
    {
        PlaySfx(citizensWon ? Instance.gameOverCitizenClip : Instance.gameOverKillerClip, 0.62f);
    }

    private static GameAudioManager Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject audioObject = new GameObject("GameAudioManager");
        DontDestroyOnLoad(audioObject);
        instance = audioObject.AddComponent<GameAudioManager>();
        instance.Initialize();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance == null)
        {
            return;
        }

        if (IsMenuScene(scene.name))
        {
            instance.PlayMusic("menu", instance.menuMusicClip);
            return;
        }

        if (scene.name == "labor")
        {
            instance.PlayMusic("ingame", instance.inGameMusicClip);
            return;
        }

        instance.StopMusic();
    }

    private static bool IsMenuScene(string sceneName)
    {
        return sceneName == "LobbyScene" ||
               sceneName == "SettingsScene" ||
               sceneName == "CreateRoomLobbyScene" ||
               sceneName == "PublicRoomListScene";
    }

    private static void PlaySfx(AudioClip clip, float volumeScale)
    {
        EnsureInstance();
        if (clip == null || instance.sfxSource == null)
        {
            return;
        }

        instance.sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private void Initialize()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;

        LoadClips();
        ApplyVolumes();
    }

    private void LoadClips()
    {
        menuMusicClip = LoadClip(MenuMusicPath) ?? CreateAmbientLoop("Fallback Menu BGM", 52f, 78f, 117f, 7.5f, 0.15f);
        inGameMusicClip = LoadClip(InGameMusicPath) ?? CreateAmbientLoop("Fallback InGame BGM", 41f, 61f, 92f, 8f, 0.13f);
        uiHoverClip = LoadClip(UiHoverPath) ?? CreateSoftNoiseHit("Fallback UI Hover", 0.09f, 0.045f, 0f, 0.75f, 22f, 0.07f);
        uiClickClip = LoadClip(UiClickPath) ?? CreateMuffledImpact("Fallback UI Click", 0.13f, 0.065f, 95f, 0.28f, 20f);
        colorSelectClip = LoadClip(ColorSelectPath) ?? CreateSoftNoiseHit("Fallback Color Select", 0.18f, 0.065f, 185f, 0.36f, 11f, 0.1f);
        interactClip = LoadClip(InteractPath) ?? CreateMuffledImpact("Fallback Interact", 0.16f, 0.07f, 120f, 0.34f, 13f);
        pickupClip = LoadClip(PickupPath) ?? CreateSoftNoiseHit("Fallback Pickup", 0.16f, 0.06f, 210f, 0.42f, 10f, 0.11f);
        itemDropClip = LoadClip(ItemDropPath) ?? CreateMuffledImpact("Fallback Item Drop", 0.2f, 0.075f, 82f, 0.32f, 9f);
        gameOverCitizenClip = LoadClip(GameOverCitizenPath) ?? CreateLowRumble("Fallback Citizens Win", 0.95f, 0.11f, 84f, 122f, 0.18f);
        gameOverKillerClip = LoadClip(GameOverKillerPath) ?? CreateLowRumble("Fallback Killer Wins", 1.05f, 0.12f, 66f, 41f, 0.22f);
        cameraUseClip = LoadClip("Audio/Items/Item_CameraUse") ?? CreateSoftNoiseHit("Fallback Camera Use", 0.2f, 0.07f, 0f, 0.85f, 8f, 0.13f);
        knifeUseClip = LoadClip("Audio/Items/Item_KnifeUse") ?? CreateSoftNoiseHit("Fallback Knife Use", 0.17f, 0.065f, 140f, 0.72f, 12f, 0.16f);
        medkitUseClip = LoadClip("Audio/Items/Item_MedkitUse") ?? CreateSoftNoiseHit("Fallback Medkit Use", 0.22f, 0.06f, 170f, 0.5f, 7f, 0.09f);
        genericItemUseClip = CreateMuffledImpact("Fallback Item Use", 0.15f, 0.06f, 110f, 0.3f, 12f);
    }

    private AudioClip LoadClip(string resourcesPath)
    {
        return Resources.Load<AudioClip>(resourcesPath);
    }

    private void PlayMusic(string key, AudioClip clip)
    {
        if (clip == null || musicSource == null)
        {
            return;
        }

        if (currentMusicKey == key && musicSource.isPlaying)
        {
            return;
        }

        currentMusicKey = key;
        musicSource.clip = clip;
        musicSource.volume = SettingsManager.BgmVolume;
        musicSource.Play();
    }

    private void StopMusic()
    {
        currentMusicKey = string.Empty;
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    private static AudioClip CreateSoftNoiseHit(string name, float duration, float amplitude, float bodyFrequency, float noiseAmount, float decayRate, float filterSpeed)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        float filteredNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = i / (float)(sampleCount - 1);
            float time = i / (float)sampleRate;
            float rawNoise = Noise(i) * 2f - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, rawNoise, Mathf.Clamp01(filterSpeed));
            float fadeIn = Mathf.Clamp01(normalized / 0.025f);
            float envelope = fadeIn * Mathf.Exp(-decayRate * normalized);
            float body = bodyFrequency > 0f ? Mathf.Sin(2f * Mathf.PI * bodyFrequency * time) * 0.32f : 0f;
            samples[i] = (filteredNoise * noiseAmount + body) * envelope * amplitude;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateMuffledImpact(string name, float duration, float amplitude, float bodyFrequency, float noiseAmount, float decayRate)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        float filteredNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = i / (float)(sampleCount - 1);
            float time = i / (float)sampleRate;
            float rawNoise = Noise(i + 917) * 2f - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, rawNoise, 0.055f);
            float bodyEnvelope = Mathf.Exp(-decayRate * normalized);
            float noiseEnvelope = Mathf.Exp(-(decayRate * 1.35f) * normalized);
            float body = Mathf.Sin(2f * Mathf.PI * bodyFrequency * time) * bodyEnvelope;
            float tail = Mathf.Sin(2f * Mathf.PI * bodyFrequency * 0.5f * time) * bodyEnvelope * 0.38f;
            samples[i] = (body * 0.72f + tail + filteredNoise * noiseAmount * noiseEnvelope) * amplitude;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateLowRumble(string name, float duration, float amplitude, float startFrequency, float endFrequency, float noiseAmount)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        float filteredNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = i / (float)(sampleCount - 1);
            float time = i / (float)sampleRate;
            float frequency = Mathf.Lerp(startFrequency, endFrequency, normalized);
            float rawNoise = Noise(i + 1907) * 2f - 1f;
            filteredNoise = Mathf.Lerp(filteredNoise, rawNoise, 0.025f);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized / 0.18f));
            float fall = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalized - 0.72f) / 0.28f));
            float envelope = rise * fall;
            float pulse = Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.56f;
            float sub = Mathf.Sin(2f * Mathf.PI * frequency * 0.5f * time) * 0.42f;
            samples[i] = (pulse + sub + filteredNoise * noiseAmount) * envelope * amplitude;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float Noise(int sampleIndex)
    {
        float value = Mathf.Sin(sampleIndex * 12.9898f + 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private static AudioClip CreateAmbientLoop(string name, float lowFrequency, float midFrequency, float highFrequency, float duration, float amplitude)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float slowPulse = 0.65f + Mathf.Sin(2f * Mathf.PI * 0.09f * t) * 0.16f;
            float sample =
                Mathf.Sin(2f * Mathf.PI * lowFrequency * t) * 0.44f +
                Mathf.Sin(2f * Mathf.PI * midFrequency * t) * 0.25f +
                Mathf.Sin(2f * Mathf.PI * highFrequency * t) * 0.12f;

            samples[i] = sample * slowPulse * amplitude;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
