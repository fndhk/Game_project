using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DarkUiRuntimeStyler : MonoBehaviour
{
    private static DarkUiRuntimeStyler instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureInstance();
        ApplyLoadedScenes();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DarkUiRuntimeStyler styler = EnsureInstance();
        styler.StartCoroutine(styler.ApplyDelayed(scene));
    }

    private static DarkUiRuntimeStyler EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject obj = new GameObject("DarkUiRuntimeStyler");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<DarkUiRuntimeStyler>();
        return instance;
    }

    private static void ApplyLoadedScenes()
    {
        DarkUiRuntimeStyler styler = EnsureInstance();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                styler.StartCoroutine(styler.ApplyDelayed(scene));
            }
        }
    }

    private IEnumerator ApplyDelayed(Scene scene)
    {
        yield return null;
        yield return null;
        ApplyScene(scene);
    }

    private static void ApplyScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        if (scene.name == "LobbyScene" || scene.name == "LobbyScene 1")
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
            {
                DarkUiSkin.ApplyToHierarchy(roots[i].transform);
            }
        }
    }
}
