using UnityEngine;

public class ForestBlockoutGenerator : MonoBehaviour
{
    [Header("Generate")]
    public bool generateOnStart = true;
    public bool clearChildrenBeforeGenerate = true;

    [Header("Map Size")]
    public Vector2 mapSize = new Vector2(80f, 80f);
    public int treeCount = 70;
    public int rockCount = 22;
    public int logCount = 12;

    [Header("Layout")]
    public Vector3 center = Vector3.zero;

    private void Start()
    {
        if (generateOnStart)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Forest")]
    public void Generate()
    {
        if (clearChildrenBeforeGenerate)
        {
            ClearChildren();
        }

        CreateGround();
        CreateCentralClearing();
        CreateRegionMarkers();
        ScatterTrees();
        ScatterRocks();
        ScatterLogs();
        CreateExitArea();
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
        ground.name = "Forest_Ground";
        ground.transform.SetParent(transform);
        ground.transform.position = center;
        ground.transform.localScale = new Vector3(mapSize.x / 10f, 1f, mapSize.y / 10f);
        ground.layer = LayerMask.NameToLayer("RevealSurface") >= 0 ? LayerMask.NameToLayer("RevealSurface") : 0;
    }

    private void CreateCentralClearing()
    {
        GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stump.name = "Central_Stump";
        stump.transform.SetParent(transform);
        stump.transform.position = center + new Vector3(0f, 0.75f, 0f);
        stump.transform.localScale = new Vector3(2.2f, 0.75f, 2.2f);

        for (int i = 0; i < 3; i++)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cube);
            log.name = $"Central_Log_{i}";
            log.transform.SetParent(transform);
            log.transform.position = center + Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(6f, 0.5f, 0f);
            log.transform.rotation = Quaternion.Euler(0f, i * 50f, 15f);
            log.transform.localScale = new Vector3(4.2f, 0.6f, 0.6f);
        }
    }

    private void CreateRegionMarkers()
    {
        CreateMarker("Camp", center + new Vector3(-22f, 0f, 22f), new Vector3(6f, 2f, 6f));
        CreateMarker("Swamp", center + new Vector3(24f, 0f, 22f), new Vector3(12f, 0.2f, 10f));
        CreateMarker("WatchTower_Base", center + new Vector3(28f, 0f, 0f), new Vector3(4f, 5f, 4f));
        CreateMarker("Trap_Area", center + new Vector3(-25f, 0f, -14f), new Vector3(10f, 1f, 8f));
        CreateMarker("Warehouse", center + new Vector3(4f, 0f, -26f), new Vector3(9f, 4f, 7f));
        CreateMarker("Stone_Graves", center + new Vector3(26f, 0f, -24f), new Vector3(12f, 1f, 12f));
    }

    private void ScatterTrees()
    {
        for (int i = 0; i < treeCount; i++)
        {
            Vector3 pos = RandomInsideMap();
            if (Vector3.Distance(pos, center) < 10f) continue;

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = $"Tree_{i}";
            trunk.transform.SetParent(transform);
            float height = Random.Range(3.5f, 6.5f);
            trunk.transform.position = pos + new Vector3(0f, height * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(Random.Range(0.3f, 0.6f), height * 0.5f, Random.Range(0.3f, 0.6f));

            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = $"TreeTop_{i}";
            crown.transform.SetParent(trunk.transform);
            crown.transform.localPosition = new Vector3(0f, height * 0.65f, 0f);
            crown.transform.localScale = Vector3.one * Random.Range(2f, 3.5f);
        }
    }

    private void ScatterRocks()
    {
        for (int i = 0; i < rockCount; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock_{i}";
            rock.transform.SetParent(transform);
            rock.transform.position = RandomInsideMap() + new Vector3(0f, Random.Range(0.4f, 1.2f), 0f);
            rock.transform.rotation = Random.rotation;
            rock.transform.localScale = new Vector3(Random.Range(1.2f, 3f), Random.Range(0.8f, 2f), Random.Range(1.2f, 3f));
        }
    }

    private void ScatterLogs()
    {
        for (int i = 0; i < logCount; i++)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cube);
            log.name = $"FallenLog_{i}";
            log.transform.SetParent(transform);
            log.transform.position = RandomInsideMap() + new Vector3(0f, 0.45f, 0f);
            log.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 180f), Random.Range(-10f, 10f));
            log.transform.localScale = new Vector3(Random.Range(3f, 7f), 0.6f, 0.7f);
        }
    }

    private void CreateExitArea()
    {
        GameObject exitGate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        exitGate.name = "Forest_ExitGate";
        exitGate.transform.SetParent(transform);
        exitGate.transform.position = center + new Vector3(4f, 2.5f, -34f);
        exitGate.transform.localScale = new Vector3(6f, 5f, 0.5f);
    }

    private void CreateMarker(string markerName, Vector3 position, Vector3 scale)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = markerName;
        marker.transform.SetParent(transform);
        marker.transform.position = position + new Vector3(0f, scale.y * 0.5f, 0f);
        marker.transform.localScale = scale;
    }

    private Vector3 RandomInsideMap()
    {
        float x = Random.Range(-mapSize.x * 0.5f, mapSize.x * 0.5f);
        float z = Random.Range(-mapSize.y * 0.5f, mapSize.y * 0.5f);
        return center + new Vector3(x, 0f, z);
    }
}
