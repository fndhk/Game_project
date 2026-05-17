using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class UiEventSystemUtility
{
    public static EventSystem EnsureSingle(GameObject context)
    {
        Scene contextScene = context != null ? context.scene : default(Scene);
        EventSystem[] systems = Resources.FindObjectsOfTypeAll<EventSystem>();
        EventSystem primary = FindPrimary(systems, contextScene);

        if (primary == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return eventSystemObject.GetComponent<EventSystem>();
        }

        primary.gameObject.SetActive(true);
        if (primary.GetComponent<StandaloneInputModule>() == null)
        {
            primary.gameObject.AddComponent<StandaloneInputModule>();
        }

        EventSystem.current = primary;
        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system != null && system != primary && system.gameObject.scene.IsValid())
            {
                system.gameObject.SetActive(false);
            }
        }

        return primary;
    }

    private static EventSystem FindPrimary(EventSystem[] systems, Scene contextScene)
    {
        EventSystem current = EventSystem.current;
        if (IsUsable(current) && (!contextScene.IsValid() || current.gameObject.scene == contextScene))
        {
            return current;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (IsUsable(system) && contextScene.IsValid() && system.gameObject.scene == contextScene)
            {
                return system;
            }
        }

        if (IsUsable(current))
        {
            return current;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system != null && system.gameObject.scene.IsValid())
            {
                return system;
            }
        }

        return null;
    }

    private static bool IsUsable(EventSystem system)
    {
        return system != null && system.gameObject.scene.IsValid() && system.gameObject.activeSelf;
    }
}
