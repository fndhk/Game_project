using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoleRevealIntro : MonoBehaviour
{
    private const float IntroDuration = 3.4f;

    private static bool hasShownForScene;
    private static RoleRevealIntro activeInstance;

    private CanvasGroup canvasGroup;
    private TMP_Text roleText;
    private TMP_Text titleText;
    private TMP_Text hintText;
    private Image pulseRing;
    private RectTransform scanLine;
    private AudioSource audioSource;
    private PlayerRole revealedRole = PlayerRole.Citizen;
    private readonly Dictionary<Behaviour, bool> lockedBehaviours = new Dictionary<Behaviour, bool>();

    public static bool IsShowing => activeInstance != null;

    public static void ShowWhenReady()
    {
        if (IsMenuScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (activeInstance != null || hasShownForScene)
        {
            return;
        }

        GameObject root = new GameObject("Role Reveal Intro");
        DontDestroyOnLoad(root);
        activeInstance = root.AddComponent<RoleRevealIntro>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void HookSceneReset()
    {
        SceneManager.sceneLoaded -= ResetForScene;
        SceneManager.sceneLoaded += ResetForScene;
    }

    private static void ResetForScene(Scene scene, LoadSceneMode mode)
    {
        if (IsMenuScene(scene.name))
        {
            hasShownForScene = false;

            if (activeInstance != null)
            {
                Destroy(activeInstance.gameObject);
            }

            return;
        }

        hasShownForScene = false;
    }

    private static bool IsMenuScene(string sceneName)
    {
        return sceneName == "LobbyScene" ||
               sceneName == "LobbyScene 1" ||
               sceneName == "CreateRoomLobbyScene" ||
               sceneName == "PublicRoomListScene";
    }

    private void Awake()
    {
        activeInstance = this;
        BuildUi();
        LockPlayerInput();
        StartCoroutine(RevealRoutine());
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
        }

        RestorePlayerInput();
    }

    private IEnumerator RevealRoutine()
    {
        while (RoleAssignmentManager.IsWaitingForPhotonRole())
        {
            yield return null;
        }

        PlayerCombatTarget localTarget = FindLocalCombatTarget();
        if (localTarget != null)
        {
            revealedRole = localTarget.role;
        }

        bool isImposter = revealedRole == PlayerRole.Killer;
        roleText.text = InGameLocalization.RoleName(revealedRole);
        roleText.color = isImposter ? new Color(1f, 0.28f, 0.20f, 1f) : new Color(0.58f, 0.95f, 1f, 1f);
        hintText.text = isImposter
            ? InGameLocalization.Text("Objective Kill Crew")
            : InGameLocalization.Text("Objective Find Computers");

        PlayRevealSound(isImposter);

        float elapsed = 0f;
        while (elapsed < IntroDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / IntroDuration);
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.5f));
            float exit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.78f) / 0.22f));

            canvasGroup.alpha = Mathf.Clamp01(reveal - exit);
            roleText.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.04f, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
            pulseRing.color = isImposter
                ? new Color(1f, 0.20f, 0.12f, Mathf.Lerp(0.06f, 0.22f, Mathf.PingPong(Time.unscaledTime * 1.1f, 1f)))
                : new Color(0.45f, 0.95f, 1f, Mathf.Lerp(0.06f, 0.22f, Mathf.PingPong(Time.unscaledTime * 1.1f, 1f)));
            pulseRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.18f, Mathf.PingPong(Time.unscaledTime * 0.7f, 1f));
            scanLine.anchoredPosition = new Vector2(Mathf.Lerp(-360f, 360f, Mathf.PingPong(Time.unscaledTime * 0.5f, 1f)), 0f);

            yield return null;
        }

        hasShownForScene = true;
        RoundTimer.ResetTimer();
        Destroy(gameObject);
    }

    private void LockPlayerInput()
    {
        lockedBehaviours.Clear();
        AddLockTargets(FindObjectsOfType<MouseLook>(true));
        AddLockTargets(FindObjectsOfType<PlayerMotor>(true));
        AddLockTargets(FindObjectsOfType<PlayerObjectiveInteractor>(true));
        AddLockTargets(FindObjectsOfType<PlayerInventory>(true));
        AddLockTargets(FindObjectsOfType<PlayerItemUser>(true));
        AddLockTargets(FindObjectsOfType<LidarSpotScanner>(true));
    }

    private void AddLockTargets<T>(T[] targets) where T : Behaviour
    {
        for (int i = 0; i < targets.Length; i++)
        {
            T target = targets[i];
            if (target == null || ReferenceEquals(target, this))
            {
                continue;
            }

            lockedBehaviours[target] = target.enabled;
            target.enabled = false;
        }
    }

    private void RestorePlayerInput()
    {
        foreach (KeyValuePair<Behaviour, bool> pair in lockedBehaviours)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        lockedBehaviours.Clear();
    }

    private PlayerCombatTarget FindLocalCombatTarget()
    {
        PlayerCombatTarget[] targets = FindObjectsOfType<PlayerCombatTarget>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
            {
                continue;
            }

            PhotonView photonView = targets[i].GetComponent<PhotonView>();
            if (photonView != null && photonView.IsMine)
            {
                return targets[i];
            }
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null && targets[i].gameObject.activeInHierarchy)
            {
                return targets[i];
            }
        }

        return targets.Length > 0 ? targets[0] : null;
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32100;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        RectTransform root = canvas.GetComponent<RectTransform>();
        Stretch(root);

        Image blackout = CreateImage("Blackout", root, new Color(0f, 0f, 0f, 0.96f));
        Stretch(blackout.rectTransform);

        pulseRing = CreateImage("Pulse Ring", root, new Color(0.45f, 0.95f, 1f, 0.12f));
        pulseRing.sprite = CreateRingSprite();
        pulseRing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        pulseRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        pulseRing.rectTransform.sizeDelta = new Vector2(620f, 620f);
        pulseRing.rectTransform.anchoredPosition = Vector2.zero;

        scanLine = CreateImage("Scan Line", root, new Color(0.65f, 0.95f, 1f, 0.18f)).rectTransform;
        scanLine.anchorMin = new Vector2(0.5f, 0.5f);
        scanLine.anchorMax = new Vector2(0.5f, 0.5f);
        scanLine.sizeDelta = new Vector2(3f, 520f);

        titleText = CreateText("Title", root, InGameLocalization.Text("Role"), 26f, TextAlignmentOptions.Center);
        titleText.color = new Color(0.80f, 0.88f, 0.88f, 0.82f);
        titleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        titleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        titleText.rectTransform.sizeDelta = new Vector2(640f, 42f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 110f);

        roleText = CreateText("Role", root, string.Empty, 72f, TextAlignmentOptions.Center);
        roleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        roleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        roleText.rectTransform.sizeDelta = new Vector2(900f, 110f);
        roleText.rectTransform.anchoredPosition = Vector2.zero;

        hintText = CreateText("Hint", root, string.Empty, 18f, TextAlignmentOptions.Center);
        hintText.color = new Color(0.95f, 0.74f, 0.28f, 0.82f);
        hintText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        hintText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        hintText.rectTransform.sizeDelta = new Vector2(760f, 36f);
        hintText.rectTransform.anchoredPosition = new Vector2(0f, -94f);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0.45f;
    }

    private void PlayRevealSound(bool isImposter)
    {
        audioSource.clip = CreateToneClip(isImposter ? 92f : 220f, isImposter ? 0.9f : 0.72f);
        audioSource.Play();
    }

    private static AudioClip CreateToneClip(float baseFrequency, float duration)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            float tone = Mathf.Sin(t * baseFrequency * Mathf.PI * 2f) * 0.55f;
            tone += Mathf.Sin(t * baseFrequency * 2.01f * Mathf.PI * 2f) * 0.18f;
            samples[i] = tone * envelope;
        }

        AudioClip clip = AudioClip.Create("Runtime Role Reveal Tone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(Shadow));
        textObject.transform.SetParent(parent, false);

        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        LocalizedTmpFontProvider.Apply(tmp);

        Shadow shadow = textObject.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return tmp;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.InverseLerp(49f, 51f, distance) * (1f - Mathf.InverseLerp(56f, 59f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
