using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public static class UiEventSystemUtility
{
    public static EventSystem EnsureSingle(GameObject context)
    {
        Scene contextScene = context != null ? context.scene : SceneManager.GetActiveScene();
        EventSystem[] systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include);
        EventSystem primary = FindPrimary(systems, contextScene);

        if (primary == null)
        {
            primary = CreateEventSystem(contextScene);
        }
        else if (!Activate(primary))
        {
            primary = CreateEventSystem(contextScene);
        }

        EnsureInputModule(primary);

        for (int i = 0; i < systems.Length; i++)
        {
            EventSystem system = systems[i];
            if (system != null && system != primary && system.gameObject.scene.IsValid())
            {
                system.gameObject.SetActive(false);
            }
        }

        primary.UpdateModules();
        return primary;
    }

    private static EventSystem CreateEventSystem(Scene targetScene)
    {
        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        if (targetScene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(eventSystemObject, targetScene);
        }

        return eventSystemObject.GetComponent<EventSystem>();
    }

    private static bool Activate(EventSystem system)
    {
        if (system == null)
        {
            return false;
        }

        system.gameObject.SetActive(true);
        system.enabled = true;
        return system.gameObject.activeInHierarchy;
    }

    private static void EnsureInputModule(EventSystem system)
    {
        BaseInputModule[] modules = system.GetComponents<BaseInputModule>();
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] != null && modules[i].enabled)
            {
                return;
            }
        }

        if (modules.Length > 0 && modules[0] != null)
        {
            modules[0].enabled = true;
            return;
        }

        system.gameObject.AddComponent<StandaloneInputModule>();
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
        return system != null && system.enabled && system.gameObject.scene.IsValid() && system.gameObject.activeInHierarchy;
    }
}
