#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Scanner Battery 아이템 프리팹을 자동으로 만들어주는 Unity Editor 전용 스크립트이다.
// 이 파일은 반드시 Assets/Scripts/Editor 폴더 안에 넣어야 한다.
public static class ScannerBatteryPrefabBuilder
{
    private const string PrefabFolderPath = "Assets/Prefabs/Items";
    private const string MaterialFolderPath = "Assets/Materials/Items";
    private const string PrefabPath = PrefabFolderPath + "/ScannerBatteryPickup.prefab";

    [MenuItem("Tools/Siu/Create Scanner Battery Prefab")]
    public static void CreateScannerBatteryPrefab()
    {
        CreateFolders();

        Material darkMetal = CreateMaterial("M_Item_Battery_DarkMetal", new Color(0.045f, 0.047f, 0.05f, 1f), false);
        Material batteryCell = CreateMaterial("M_Item_Battery_Cell", new Color(0.13f, 0.14f, 0.15f, 1f), false);
        Material rubber = CreateMaterial("M_Item_Battery_Rubber", new Color(0.015f, 0.015f, 0.017f, 1f), false);
        Material contact = CreateMaterial("M_Item_Battery_ContactGray", new Color(0.36f, 0.36f, 0.34f, 1f), false);
        Material indicator = CreateMaterial("M_Item_Battery_IndicatorGray", new Color(0.70f, 0.74f, 0.76f, 1f), true);
        Material label = CreateMaterial("M_Item_Battery_LabelGray", new Color(0.62f, 0.62f, 0.58f, 1f), false);

        GameObject root = new GameObject("ScannerBatteryPickup");
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // 나중에 스캔했을 때 아이템 공통 점으로 찍히도록 설정한다.
        ScanSurfaceInfo scanSurfaceInfo = root.AddComponent<ScanSurfaceInfo>();
        scanSurfaceInfo.surfaceType = ScanSurfaceType.Item;

        // 상호작용 Raycast가 잡을 단일 충돌체이다.
        BoxCollider rootCollider = root.AddComponent<BoxCollider>();
        rootCollider.isTrigger = false;
        rootCollider.center = new Vector3(0f, 0.16f, 0f);
        rootCollider.size = new Vector3(0.9f, 0.42f, 0.42f);

        // E키로 주울 수 있는 월드 아이템 컴포넌트이다.
        WorldItemPickup pickup = root.AddComponent<WorldItemPickup>();
        pickup.itemType = ItemType.ScannerBattery;
        pickup.amount = 1;
        pickup.hideAfterPickup = true;
        pickup.destroyAfterPickup = false;

        // 아래 받침 프레임이다.
        CreateCubeChild(root, "LowerFrame", new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.78f, 0.12f, 0.32f), darkMetal);

        // 좌우 보호 프레임이다.
        CreateCubeChild(root, "LeftEndCap", new Vector3(-0.43f, 0.17f, 0f), Vector3.zero, new Vector3(0.08f, 0.26f, 0.36f), darkMetal);
        CreateCubeChild(root, "RightEndCap", new Vector3(0.43f, 0.17f, 0f), Vector3.zero, new Vector3(0.08f, 0.26f, 0.36f), darkMetal);

        // 실제 배터리 셀처럼 보이는 원통 두 개이다.
        CreateCylinderChild(root, "BatteryCell_A", new Vector3(0f, 0.20f, -0.09f), new Vector3(0f, 0f, 90f), new Vector3(0.075f, 0.36f, 0.075f), batteryCell);
        CreateCylinderChild(root, "BatteryCell_B", new Vector3(0f, 0.20f, 0.09f), new Vector3(0f, 0f, 90f), new Vector3(0.075f, 0.36f, 0.075f), batteryCell);

        // 양쪽 전극 접점이다.
        CreateCylinderChild(root, "PositiveContact_A", new Vector3(0.38f, 0.20f, -0.09f), new Vector3(0f, 0f, 90f), new Vector3(0.082f, 0.014f, 0.082f), contact);
        CreateCylinderChild(root, "PositiveContact_B", new Vector3(0.38f, 0.20f, 0.09f), new Vector3(0f, 0f, 90f), new Vector3(0.082f, 0.014f, 0.082f), contact);
        CreateCylinderChild(root, "NegativeContact_A", new Vector3(-0.38f, 0.20f, -0.09f), new Vector3(0f, 0f, 90f), new Vector3(0.082f, 0.014f, 0.082f), contact);
        CreateCylinderChild(root, "NegativeContact_B", new Vector3(-0.38f, 0.20f, 0.09f), new Vector3(0f, 0f, 90f), new Vector3(0.082f, 0.014f, 0.082f), contact);

        // 위쪽 손잡이이다.
        CreateCubeChild(root, "HandleLeftSupport", new Vector3(-0.24f, 0.36f, 0f), Vector3.zero, new Vector3(0.06f, 0.16f, 0.08f), rubber);
        CreateCubeChild(root, "HandleRightSupport", new Vector3(0.24f, 0.36f, 0f), Vector3.zero, new Vector3(0.06f, 0.16f, 0.08f), rubber);
        CreateCubeChild(root, "TopHandle", new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(0.55f, 0.08f, 0.1f), rubber);

        // 상태 표시 패널과 발광 게이지이다.
        CreateCubeChild(root, "StatusPanel", new Vector3(0f, 0.30f, -0.185f), new Vector3(12f, 0f, 0f), new Vector3(0.36f, 0.035f, 0.045f), darkMetal);
        CreateCubeChild(root, "GlowGauge", new Vector3(0f, 0.325f, -0.215f), new Vector3(12f, 0f, 0f), new Vector3(0.30f, 0.012f, 0.018f), indicator);

        // 작은 충전 잔량 표시등이다.
        CreateSphereChild(root, "ChargeLight_01", new Vector3(-0.15f, 0.345f, -0.205f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), indicator);
        CreateSphereChild(root, "ChargeLight_02", new Vector3(0f, 0.345f, -0.205f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), indicator);
        CreateSphereChild(root, "ChargeLight_03", new Vector3(0.15f, 0.345f, -0.205f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), indicator);

        // 경고 라벨 느낌의 사선 스트라이프이다.
        CreateCubeChild(root, "WarningStripe_01", new Vector3(-0.22f, 0.145f, -0.18f), new Vector3(0f, 0f, 28f), new Vector3(0.13f, 0.015f, 0.035f), label);
        CreateCubeChild(root, "WarningStripe_02", new Vector3(0f, 0.145f, -0.18f), new Vector3(0f, 0f, 28f), new Vector3(0.13f, 0.015f, 0.035f), label);
        CreateCubeChild(root, "WarningStripe_03", new Vector3(0.22f, 0.145f, -0.18f), new Vector3(0f, 0f, 28f), new Vector3(0.13f, 0.015f, 0.035f), label);

        // 앞쪽 충전 포트이다.
        CreateCylinderChild(root, "ChargePortOuter", new Vector3(0.46f, 0.17f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.08f, 0.018f, 0.08f), darkMetal);
        CreateCylinderChild(root, "ChargePortInner", new Vector3(0.485f, 0.17f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.045f, 0.01f, 0.045f), indicator);

        // 옆면 나사 디테일이다.
        CreateSphereChild(root, "Screw_01", new Vector3(-0.37f, 0.305f, -0.16f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), contact);
        CreateSphereChild(root, "Screw_02", new Vector3(0.37f, 0.305f, -0.16f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), contact);
        CreateSphereChild(root, "Screw_03", new Vector3(-0.37f, 0.075f, 0.16f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), contact);
        CreateSphereChild(root, "Screw_04", new Vector3(0.37f, 0.075f, 0.16f), Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f), contact);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[ScannerBatteryPrefabBuilder] Created prefab: " + PrefabPath);
    }

    private static void CreateFolders()
    {
        CreateFolderIfMissing("Assets", "Prefabs");
        CreateFolderIfMissing("Assets/Prefabs", "Items");
        CreateFolderIfMissing("Assets", "Materials");
        CreateFolderIfMissing("Assets/Materials", "Items");
    }

    private static void CreateFolderIfMissing(string parentPath, string folderName)
    {
        string fullPath = parentPath + "/" + folderName;

        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parentPath, folderName);
        }
    }

    private static Material CreateMaterial(string materialName, Color color, bool useEmission)
    {
        string materialPath = MaterialFolderPath + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }

        SetMaterialColor(material, color);

        if (useEmission)
        {
            EnableEmission(material, color * 1.8f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
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
    }

    private static void EnableEmission(Material material, Color emissionColor)
    {
        if (material == null)
        {
            return;
        }

        material.EnableKeyword("_EMISSION");

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emissionColor);
        }
    }

    private static GameObject CreateCubeChild(GameObject parent, string name, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Material material)
    {
        return CreatePrimitiveChild(parent, PrimitiveType.Cube, name, localPosition, localEulerAngles, localScale, material);
    }

    private static GameObject CreateCylinderChild(GameObject parent, string name, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Material material)
    {
        return CreatePrimitiveChild(parent, PrimitiveType.Cylinder, name, localPosition, localEulerAngles, localScale, material);
    }

    private static GameObject CreateSphereChild(GameObject parent, string name, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Material material)
    {
        return CreatePrimitiveChild(parent, PrimitiveType.Sphere, name, localPosition, localEulerAngles, localScale, material);
    }

    private static GameObject CreatePrimitiveChild(GameObject parent, PrimitiveType primitiveType, string name, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, Material material)
    {
        GameObject child = GameObject.CreatePrimitive(primitiveType);
        child.name = name;
        child.transform.SetParent(parent.transform, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.Euler(localEulerAngles);
        child.transform.localScale = localScale;

        Collider childCollider = child.GetComponent<Collider>();

        if (childCollider != null)
        {
            Object.DestroyImmediate(childCollider);
        }

        Renderer renderer = child.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return child;
    }
}
#endif
