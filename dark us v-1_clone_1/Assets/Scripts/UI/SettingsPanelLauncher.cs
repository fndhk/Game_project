using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class SettingsPanelLauncher
{
    private const string PrefabPath = "UI/SettingsPanel";
    private static SettingsUIController instance;
    private static int lastEscapeCloseFrame = -1;

    public static bool IsOpen => instance != null && instance.IsOpen;
    public static bool IsCapturingKey => instance != null && instance.IsCapturingKey;
    public static bool ClosedByEscapeThisFrame => Time.frameCount == lastEscapeCloseFrame;

    public static void Show()
    {
        EnsureInstance();
        if (instance == null)
        {
            Debug.LogWarning("Settings panel could not be created.");
            return;
        }

        EnsureEventSystem();
        lastEscapeCloseFrame = -1;
        instance.Show();
    }

    public static void Hide()
    {
        if (instance != null)
        {
            instance.HideWithoutNotify();
        }
    }

    public static void Toggle()
    {
        if (IsOpen)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public static void DestroyInstance()
    {
        if (instance != null)
        {
            GameObject root = instance.transform.root != null ? instance.transform.root.gameObject : instance.gameObject;
            if (Application.isPlaying)
            {
                Object.Destroy(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }
        }

        instance = null;
        lastEscapeCloseFrame = -1;
    }

    public static void MarkEscapeCloseFrame()
    {
        lastEscapeCloseFrame = Time.frameCount;
    }

    public static void TickEscapeCloseFrame()
    {
        // Kept for existing callers. ClosedByEscapeThisFrame is computed from Time.frameCount.
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        EnsureEventSystem();
        Canvas canvas = EnsureCanvas();
        if (canvas == null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        GameObject panelObject = prefab != null
            ? Object.Instantiate(prefab, canvas.transform, false)
            : CreateFallbackPanel(canvas.transform);

        panelObject.name = "SettingsPanel";
        Object.DontDestroyOnLoad(canvas.gameObject);
        instance = panelObject.GetComponent<SettingsUIController>();
        if (instance == null)
        {
            instance = panelObject.AddComponent<SettingsUIController>();
        }
    }

    private static Canvas EnsureCanvas()
    {
        GameObject canvasObject = GameObject.Find("SettingsCanvas");
        Canvas canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            canvasObject.SetActive(true);
            canvas.enabled = true;
            EnsureCanvasComponents(canvasObject);
            return canvas;
        }

        canvasObject = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60000;
        EnsureCanvasComponents(canvasObject);
        Object.DontDestroyOnLoad(canvasObject);
        return canvas;
    }

    private static void EnsureCanvasComponents(GameObject canvasObject)
    {
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = canvasObject.AddComponent<GraphicRaycaster>();
        }

        raycaster.enabled = true;
    }

    private static GameObject CreateFallbackPanel(Transform parent)
    {
        GameObject panelObject = new GameObject("SettingsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(SettingsUIController));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        panelObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);
        return panelObject;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
