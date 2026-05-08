#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// 아이템 프리팹을 에디터에서 자동으로 만들어주는 도구이다.
// Tools/Siu/Create Item Prefabs 메뉴를 누르면 Camera, Knife, Medkit 프리팹이 생성된다.
public static class ItemPrefabBuilder
{
    private const string PrefabFolderPath = "Assets/Prefabs/Items";
    private const string MaterialFolderPath = "Assets/Materials/Items";
    private const string MeshFolderPath = "Assets/Meshes/Items";

    [MenuItem("Tools/Siu/Create Item Prefabs")]
    public static void CreateItemPrefabs()
    {
        EnsureFolders();

        Material darkMetal = CreateMaterial("Item_DarkMetal_Mat", new Color(0.08f, 0.085f, 0.09f, 1f), 0.65f, 0.35f);
        Material blackRubber = CreateMaterial("Item_BlackRubber_Mat", new Color(0.015f, 0.015f, 0.018f, 1f), 0.15f, 0.22f);
        Material bladeMetal = CreateMaterial("Item_BladeMetal_Mat", new Color(0.58f, 0.60f, 0.62f, 1f), 0.85f, 0.45f);
        Material glass = CreateMaterial("Item_DarkGlass_Mat", new Color(0.02f, 0.055f, 0.075f, 1f), 0.1f, 0.75f);
        Material medkitRed = CreateMaterial("Item_MedkitRed_Mat", new Color(0.55f, 0.03f, 0.025f, 1f), 0.2f, 0.35f);
        Material medkitWhite = CreateMaterial("Item_MedkitWhite_Mat", new Color(0.82f, 0.78f, 0.68f, 1f), 0.2f, 0.28f);
        Material warningYellow = CreateMaterial("Item_WarningYellow_Mat", new Color(0.95f, 0.72f, 0.12f, 1f), 0.25f, 0.4f);

        CreateCameraPrefab(darkMetal, blackRubber, glass, warningYellow);
        CreateKnifePrefab(bladeMetal, blackRubber, darkMetal);
        CreateMedkitPrefab(medkitRed, medkitWhite, darkMetal);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ItemPrefabBuilder] Item prefabs created in " + PrefabFolderPath);
    }

    private static void CreateCameraPrefab(Material darkMetal, Material blackRubber, Material glass, Material warningYellow)
    {
        GameObject root = CreateRoot("CameraPickup", ItemType.Camera, 2, ScanSurfaceType.Metal);
        Transform visual = CreateVisualRoot(root.transform);

        // 본체는 납작한 스캐너 카메라 형태로 만든다.
        CreateCube("Body", visual, new Vector3(0f, 0.42f, 0f), Vector3.zero, new Vector3(0.62f, 0.32f, 0.28f), darkMetal);
        CreateCube("TopPlate", visual, new Vector3(0f, 0.61f, 0.015f), Vector3.zero, new Vector3(0.48f, 0.055f, 0.20f), darkMetal);
        CreateCube("SideGrip_Left", visual, new Vector3(-0.37f, 0.41f, 0f), Vector3.zero, new Vector3(0.08f, 0.30f, 0.22f), blackRubber);
        CreateCube("SideGrip_Right", visual, new Vector3(0.37f, 0.41f, 0f), Vector3.zero, new Vector3(0.08f, 0.30f, 0.22f), blackRubber);

        // 전면 렌즈는 원통 여러 겹으로 만들어서 박스처럼 보이지 않게 한다.
        CreateCylinder("LensOuterRing", visual, new Vector3(0f, 0.43f, -0.18f), new Vector3(90f, 0f, 0f), new Vector3(0.31f, 0.08f, 0.31f), darkMetal);
        CreateCylinder("LensGlass", visual, new Vector3(0f, 0.43f, -0.235f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.035f, 0.22f), glass);
        CreateCylinder("SmallSensor_Left", visual, new Vector3(-0.22f, 0.50f, -0.17f), new Vector3(90f, 0f, 0f), new Vector3(0.07f, 0.035f, 0.07f), glass);
        CreateCylinder("SmallSensor_Right", visual, new Vector3(0.22f, 0.50f, -0.17f), new Vector3(90f, 0f, 0f), new Vector3(0.07f, 0.035f, 0.07f), glass);

        // 손잡이와 버튼 디테일을 추가한다.
        CreateCube("Handle", visual, new Vector3(0f, 0.18f, 0.08f), new Vector3(-7f, 0f, 0f), new Vector3(0.22f, 0.34f, 0.14f), blackRubber);
        CreateCube("Button_01", visual, new Vector3(-0.14f, 0.65f, -0.04f), Vector3.zero, new Vector3(0.10f, 0.025f, 0.06f), warningYellow);
        CreateCube("Button_02", visual, new Vector3(0.02f, 0.65f, -0.04f), Vector3.zero, new Vector3(0.10f, 0.025f, 0.06f), blackRubber);
        CreateCube("Button_03", visual, new Vector3(0.18f, 0.65f, -0.04f), Vector3.zero, new Vector3(0.10f, 0.025f, 0.06f), blackRubber);
        CreateCube("Vent_01", visual, new Vector3(-0.20f, 0.34f, -0.155f), Vector3.zero, new Vector3(0.13f, 0.018f, 0.018f), blackRubber);
        CreateCube("Vent_02", visual, new Vector3(-0.20f, 0.40f, -0.155f), Vector3.zero, new Vector3(0.13f, 0.018f, 0.018f), blackRubber);
        CreateCube("Vent_03", visual, new Vector3(-0.20f, 0.46f, -0.155f), Vector3.zero, new Vector3(0.13f, 0.018f, 0.018f), blackRubber);

        SavePrefabAndCleanup(root, "CameraPickup.prefab");
    }

    private static void CreateKnifePrefab(Material bladeMetal, Material blackRubber, Material darkMetal)
    {
        GameObject root = CreateRoot("KnifePickup", ItemType.Knife, 1, ScanSurfaceType.Metal);
        Transform visual = CreateVisualRoot(root.transform);

        // 칼은 커스텀 삼각 프리즘 메쉬로 날을 만들어 실루엣이 칼처럼 보이게 한다.
        Mesh bladeMesh = CreateKnifeBladeMesh();
        GameObject blade = new GameObject("Blade");
        blade.transform.SetParent(visual, false);
        blade.transform.localPosition = new Vector3(0.18f, 0.12f, 0f);
        blade.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        MeshFilter bladeFilter = blade.AddComponent<MeshFilter>();
        bladeFilter.sharedMesh = bladeMesh;
        MeshRenderer bladeRenderer = blade.AddComponent<MeshRenderer>();
        bladeRenderer.sharedMaterial = bladeMetal;
        MeshCollider bladeCollider = blade.AddComponent<MeshCollider>();
        bladeCollider.sharedMesh = bladeMesh;
        bladeCollider.convex = true;

        // 손잡이는 고무 그립과 금속 가드를 나누어 만든다.
        CreateCube("Guard", visual, new Vector3(0.08f, 0.12f, 0f), Vector3.zero, new Vector3(0.07f, 0.16f, 0.34f), darkMetal);
        CreateCube("Handle", visual, new Vector3(-0.38f, 0.12f, 0f), Vector3.zero, new Vector3(0.82f, 0.13f, 0.20f), blackRubber);
        CreateCube("HandlePommel", visual, new Vector3(-0.83f, 0.12f, 0f), Vector3.zero, new Vector3(0.08f, 0.15f, 0.23f), darkMetal);

        // 손잡이 홈을 여러 개 넣어서 단순 막대처럼 보이지 않게 한다.
        for (int i = 0; i < 5; i++)
        {
            float x = -0.67f + i * 0.13f;
            CreateCube("GripGroove_" + i, visual, new Vector3(x, 0.19f, 0f), new Vector3(0f, 0f, 16f), new Vector3(0.025f, 0.035f, 0.22f), darkMetal);
        }

        SavePrefabAndCleanup(root, "KnifePickup.prefab");
    }

    private static void CreateMedkitPrefab(Material medkitRed, Material medkitWhite, Material darkMetal)
    {
        GameObject root = CreateRoot("MedkitPickup", ItemType.Medkit, 1, ScanSurfaceType.Metal);
        Transform visual = CreateVisualRoot(root.transform);

        // 구급상자는 하드 케이스 느낌으로 만든다.
        CreateCube("CaseBody", visual, new Vector3(0f, 0.28f, 0f), Vector3.zero, new Vector3(0.72f, 0.42f, 0.32f), medkitRed);
        CreateCube("CaseFrontPanel", visual, new Vector3(0f, 0.28f, -0.17f), Vector3.zero, new Vector3(0.62f, 0.31f, 0.025f), medkitWhite);
        CreateCube("CaseLidSeam", visual, new Vector3(0f, 0.50f, -0.181f), Vector3.zero, new Vector3(0.66f, 0.025f, 0.025f), darkMetal);

        // 십자 마크는 얇은 큐브 두 개로 만든다.
        CreateCube("CrossVertical", visual, new Vector3(0f, 0.28f, -0.19f), Vector3.zero, new Vector3(0.085f, 0.23f, 0.018f), medkitRed);
        CreateCube("CrossHorizontal", visual, new Vector3(0f, 0.28f, -0.195f), Vector3.zero, new Vector3(0.25f, 0.075f, 0.018f), medkitRed);

        // 손잡이, 잠금장치, 모서리 보호대를 추가한다.
        CreateCube("HandleTop", visual, new Vector3(0f, 0.57f, 0f), Vector3.zero, new Vector3(0.34f, 0.055f, 0.13f), darkMetal);
        CreateCube("HandleLeftPost", visual, new Vector3(-0.17f, 0.51f, 0f), Vector3.zero, new Vector3(0.045f, 0.13f, 0.09f), darkMetal);
        CreateCube("HandleRightPost", visual, new Vector3(0.17f, 0.51f, 0f), Vector3.zero, new Vector3(0.045f, 0.13f, 0.09f), darkMetal);
        CreateCube("LatchLeft", visual, new Vector3(-0.25f, 0.48f, -0.19f), Vector3.zero, new Vector3(0.09f, 0.08f, 0.035f), darkMetal);
        CreateCube("LatchRight", visual, new Vector3(0.25f, 0.48f, -0.19f), Vector3.zero, new Vector3(0.09f, 0.08f, 0.035f), darkMetal);

        CreateCube("CornerTopLeft", visual, new Vector3(-0.37f, 0.50f, -0.16f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.04f), darkMetal);
        CreateCube("CornerTopRight", visual, new Vector3(0.37f, 0.50f, -0.16f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.04f), darkMetal);
        CreateCube("CornerBottomLeft", visual, new Vector3(-0.37f, 0.06f, -0.16f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.04f), darkMetal);
        CreateCube("CornerBottomRight", visual, new Vector3(0.37f, 0.06f, -0.16f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.04f), darkMetal);

        SavePrefabAndCleanup(root, "MedkitPickup.prefab");
    }

    private static GameObject CreateRoot(string rootName, ItemType itemType, int amount, ScanSurfaceType scanSurfaceType)
    {
        GameObject root = new GameObject(rootName);

        // 월드 아이템 스크립트를 붙여서 E키로 주울 수 있게 한다.
        WorldItemPickup pickup = root.AddComponent<WorldItemPickup>();
        pickup.itemType = itemType;
        pickup.amount = Mathf.Max(1, amount);
        pickup.hideAfterPickup = true;
        pickup.destroyAfterPickup = false;

        // 스캔 점 색상 구분용 정보이다.
        ScanSurfaceInfo surfaceInfo = root.AddComponent<ScanSurfaceInfo>();
        surfaceInfo.surfaceType = scanSurfaceType;

        return root;
    }

    private static Transform CreateVisualRoot(Transform parent)
    {
        GameObject visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(parent, false);
        return visualRoot.transform;
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.Euler(localEulerAngles);
        cube.transform.localScale = localScale;
        AssignMaterial(cube, material);
        return cube;
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localRotation = Quaternion.Euler(localEulerAngles);
        cylinder.transform.localScale = localScale;
        AssignMaterial(cylinder, material);
        return cylinder;
    }

    private static void AssignMaterial(GameObject target, Material material)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static Mesh CreateKnifeBladeMesh()
    {
        string meshPath = MeshFolderPath + "/KnifeBlade_Mesh.asset";
        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        if (existingMesh != null)
        {
            return existingMesh;
        }

        float length = 1.05f;
        float halfWidth = 0.10f;
        float halfThickness = 0.018f;

        Vector3[] vertices =
        {
            new Vector3(0f, halfThickness, -halfWidth),
            new Vector3(0f, halfThickness, halfWidth),
            new Vector3(length, halfThickness, 0f),
            new Vector3(0f, -halfThickness, -halfWidth),
            new Vector3(0f, -halfThickness, halfWidth),
            new Vector3(length, -halfThickness, 0f)
        };

        int[] triangles =
        {
            0, 1, 2,
            5, 4, 3,
            0, 2, 5,
            0, 5, 3,
            1, 4, 5,
            1, 5, 2,
            0, 3, 4,
            0, 4, 1
        };

        Mesh mesh = new Mesh();
        mesh.name = "KnifeBlade_Mesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        AssetDatabase.CreateAsset(mesh, meshPath);
        return mesh;
    }

    private static Material CreateMaterial(string materialName, Color color, float metallic, float smoothness)
    {
        string materialPath = MaterialFolderPath + "/" + materialName + ".mat";
        Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (existingMaterial != null)
        {
            SetMaterialValues(existingMaterial, color, metallic, smoothness);
            EditorUtility.SetDirty(existingMaterial);
            return existingMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = materialName;
        SetMaterialValues(material, color, metallic, smoothness);

        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static void SetMaterialValues(Material material, Color color, float metallic, float smoothness)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        }
    }

    private static void SavePrefabAndCleanup(GameObject root, string prefabFileName)
    {
        string prefabPath = PrefabFolderPath + "/" + prefabFileName;

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Prefabs");
        EnsureFolder("Assets/Prefabs", "Items");
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Items");
        EnsureFolder("Assets", "Meshes");
        EnsureFolder("Assets/Meshes", "Items");
    }

    private static void EnsureFolder(string parentPath, string folderName)
    {
        string targetPath = Path.Combine(parentPath, folderName).Replace("\\", "/");

        if (!AssetDatabase.IsValidFolder(targetPath))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }
}
#endif
