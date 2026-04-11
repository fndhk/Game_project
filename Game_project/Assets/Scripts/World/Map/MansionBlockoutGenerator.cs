using UnityEngine;

public class MansionBlockoutGenerator : MonoBehaviour
{
    [Header("Generate")]
    public bool generateOnStart = true;
    public bool clearChildrenBeforeGenerate = true;

    [Header("Layout")]
    public Vector3 center = Vector3.zero;
    public float wallHeight = 4f;
    public float wallThickness = 0.4f;

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Mansion")]
    public void Generate()
    {
        if (clearChildrenBeforeGenerate)
        {
            ClearChildren();
        }

        CreateGround();
        CreateRoom("EntranceHall", center + new Vector3(0f, 0f, 0f), new Vector2(14f, 12f));
        CreateRoom("DiningRoom", center + new Vector3(16f, 0f, 0f), new Vector2(10f, 8f));
        CreateRoom("Kitchen", center + new Vector3(28f, 0f, 0f), new Vector2(10f, 7f));
        CreateRoom("Lounge", center + new Vector3(-16f, 0f, 0f), new Vector2(10f, 8f));
        CreateRoom("Library", center + new Vector3(-28f, 0f, 0f), new Vector2(10f, 8f));
        CreateRoom("Storage", center + new Vector3(-28f, 0f, -12f), new Vector2(8f, 6f));
        CreateCorridor("MainCorridor", center + new Vector3(0f, 0f, -12f), new Vector2(38f, 4f));
        CreateRoom("BackYardExit", center + new Vector3(28f, 0f, -12f), new Vector2(8f, 6f));

        CreateUpperFloorMarker("UpperFloor_Corridor", center + new Vector3(0f, 4.5f, 14f), new Vector2(24f, 4f));
        CreateUpperFloorMarker("MasterRoom", center + new Vector3(12f, 4.5f, 14f), new Vector2(10f, 8f));
        CreateUpperFloorMarker("ChildRoom", center + new Vector3(-10f, 4.5f, 14f), new Vector2(8f, 7f));
        CreateUpperFloorMarker("PrayerRoom", center + new Vector3(-22f, 4.5f, 14f), new Vector2(8f, 6f));

        CreateFurniture();
        CreateStairMarker();
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Mansion_Ground";
        ground.transform.SetParent(transform);
        ground.transform.position = center;
        ground.transform.localScale = new Vector3(12f, 1f, 12f);
        ground.layer = LayerMask.NameToLayer("RevealSurface") >= 0 ? LayerMask.NameToLayer("RevealSurface") : 0;
    }

    private void CreateRoom(string roomName, Vector3 roomCenter, Vector2 size)
    {
        GameObject root = new GameObject(roomName);
        root.transform.SetParent(transform);
        root.transform.position = roomCenter;

        CreateFloor(root.transform, roomCenter, size);
        CreateWalls(root.transform, roomCenter, size);
    }

    private void CreateUpperFloorMarker(string roomName, Vector3 roomCenter, Vector2 size)
    {
        GameObject root = new GameObject(roomName);
        root.transform.SetParent(transform);
        root.transform.position = roomCenter;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = roomName + "_Floor";
        floor.transform.SetParent(root.transform);
        floor.transform.position = roomCenter;
        floor.transform.localScale = new Vector3(size.x, 0.3f, size.y);

        CreateWalls(root.transform, roomCenter, size);
    }

    private void CreateCorridor(string corridorName, Vector3 corridorCenter, Vector2 size)
    {
        CreateRoom(corridorName, corridorCenter, size);
    }

    private void CreateFloor(Transform parent, Vector3 roomCenter, Vector2 size)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(parent);
        floor.transform.position = roomCenter;
        floor.transform.localScale = new Vector3(size.x, 0.3f, size.y);
        floor.layer = LayerMask.NameToLayer("RevealSurface") >= 0 ? LayerMask.NameToLayer("RevealSurface") : 0;
    }

    private void CreateWalls(Transform parent, Vector3 roomCenter, Vector2 size)
    {
        CreateWall(parent, roomCenter + new Vector3(0f, wallHeight * 0.5f, size.y * 0.5f), new Vector3(size.x, wallHeight, wallThickness));
        CreateWall(parent, roomCenter + new Vector3(0f, wallHeight * 0.5f, -size.y * 0.5f), new Vector3(size.x, wallHeight, wallThickness));
        CreateWall(parent, roomCenter + new Vector3(size.x * 0.5f, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, size.y));
        CreateWall(parent, roomCenter + new Vector3(-size.x * 0.5f, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, size.y));
    }

    private void CreateWall(Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    private void CreateFurniture()
    {
        CreateFurnitureBlock("Dining_Table", center + new Vector3(16f, 0.8f, 0f), new Vector3(5f, 1.2f, 2f));
        CreateFurnitureBlock("Kitchen_Counter", center + new Vector3(28f, 0.9f, 1.8f), new Vector3(5f, 1.4f, 1f));
        CreateFurnitureBlock("Lounge_Sofa", center + new Vector3(-16f, 0.7f, -1f), new Vector3(3.5f, 1f, 1.5f));
        CreateFurnitureBlock("Library_Shelf_A", center + new Vector3(-29f, 1.4f, -1.5f), new Vector3(1f, 2.8f, 5f));
        CreateFurnitureBlock("Library_Shelf_B", center + new Vector3(-26f, 1.4f, 1.5f), new Vector3(1f, 2.8f, 5f));
    }

    private void CreateStairMarker()
    {
        GameObject stairs = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stairs.name = "Main_StairMarker";
        stairs.transform.SetParent(transform);
        stairs.transform.position = center + new Vector3(0f, 1.2f, 4f);
        stairs.transform.localScale = new Vector3(4f, 2.4f, 6f);
    }

    private void CreateFurnitureBlock(string objName, Vector3 position, Vector3 scale)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = objName;
        obj.transform.SetParent(transform);
        obj.transform.position = position;
        obj.transform.localScale = scale;
    }
}
