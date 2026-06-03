using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ArtNotes.UndergroundLaboratoryGenerator;

public static class TutorialSceneMaterializer
{
    private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

    [MenuItem("Tools/Dark Us/Materialize Tutorial Scene")]
    public static void MaterializeTutorialSceneFromMenu()
    {
        MaterializeTutorialScene();
    }

    public static void MaterializeTutorialScene()
    {
        var scene = EditorSceneManager.OpenScene(TutorialScenePath);
        TutorialSceneController controller = Object.FindFirstObjectByType<TutorialSceneController>();

        if (controller == null)
        {
            GameObject controllerObject = new GameObject("TutorialSceneController");
            controller = controllerObject.AddComponent<TutorialSceneController>();
        }

        SerializedObject serialized = new SerializedObject(controller);
        SetObject(serialized, "commonRoomPrefab", Load<Cell>("Assets/ArtNotes/Underground Laboratory Generator/Prefabs/Rooms/Room 1.prefab"));
        SetObject(serialized, "citizenRoomPrefab", Load<Cell>("Assets/ArtNotes/Underground Laboratory Generator/Prefabs/Rooms/Room 2.prefab"));
        SetObject(serialized, "doppelgangerRoomPrefab", Load<Cell>("Assets/ArtNotes/Underground Laboratory Generator/Prefabs/Rooms/Room 3.prefab"));
        SetObject(serialized, "corridorPrefab", Load<Cell>("Assets/ArtNotes/Underground Laboratory Generator/Prefabs/Coridors/Coridor 1.prefab"));
        SetObject(serialized, "doorPrefab", Load<GameObject>("Assets/ArtNotes/Underground Laboratory Generator/Prefabs/Doors/Door 1.prefab"));
        SetObject(serialized, "computerPrefab", Load<GameObject>("Assets/ArtNotes/Underground Laboratory Generator/Prefabs/Decorations/Table cmplx.prefab"));
        SetObject(serialized, "tablePrefab", Load<GameObject>("Assets/ArtNotes/Underground Laboratory Generator/Prefabs/Decorations/Table 1.prefab"));
        SetObject(serialized, "cameraPickupPrefab", Load<GameObject>("Assets/Prefabs/Items/CameraPickup.prefab"));
        SetObject(serialized, "dotSourcePrefab", Load<GameObject>("Assets/Prefabs/InstancedDotSource.prefab"));
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[TutorialSceneMaterializer] Tutorial scene prefab references assigned.");
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogWarning("[TutorialSceneMaterializer] Missing asset: " + path);
        }

        return asset;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning("[TutorialSceneMaterializer] Missing property: " + propertyName);
            return;
        }

        property.objectReferenceValue = value;
    }
}
