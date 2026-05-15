using UnityEngine;

// SkinnedMeshRenderer의 현재 포즈를 MeshCollider에 반영해서 스캔 점이 애니메이션 실루엣에 찍히게 한다.
[RequireComponent(typeof(SkinnedMeshRenderer))]
public class SkinnedScanCollider : MonoBehaviour
{
    public ScanSurfaceType surfaceType = ScanSurfaceType.PlayerBody;
    public float bakeInterval = 0.05f;

    private SkinnedMeshRenderer skinnedRenderer;
    private MeshCollider meshCollider;
    private Mesh bakedMesh;
    private float nextBakeTime;

    private void Awake()
    {
        skinnedRenderer = GetComponent<SkinnedMeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }

        meshCollider.convex = false;
        meshCollider.enabled = true;

        ScanSurfaceInfo surfaceInfo = GetComponent<ScanSurfaceInfo>();
        if (surfaceInfo == null)
        {
            surfaceInfo = gameObject.AddComponent<ScanSurfaceInfo>();
        }

        surfaceInfo.surfaceType = surfaceType;
        bakedMesh = new Mesh();
        bakedMesh.name = name + "_BakedScanMesh";
        BakeNow();
    }

    private void LateUpdate()
    {
        if (Time.time < nextBakeTime)
        {
            return;
        }

        nextBakeTime = Time.time + Mathf.Max(0.01f, bakeInterval);
        BakeNow();
    }

    private void BakeNow()
    {
        if (skinnedRenderer == null || meshCollider == null || bakedMesh == null)
        {
            return;
        }

        skinnedRenderer.BakeMesh(bakedMesh);
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = bakedMesh;
    }

    private void OnDestroy()
    {
        if (bakedMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(bakedMesh);
        }
        else
        {
            DestroyImmediate(bakedMesh);
        }
    }
}
