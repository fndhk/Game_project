using System.Collections.Generic;
using UnityEngine;

public class DotVisionRenderer : MonoBehaviour
{
    [Header("References")]
    public PlayerRevealTrail revealTrail;
    public ProximitySensor localProximitySensor;
    public GameObject dotPrefab;
    public Transform dotParent;

    [Header("Runtime")]
    public int renderedCount;

    private readonly List<GameObject> spawnedDots = new List<GameObject>();

    private void Update()
    {
        if (revealTrail == null || dotPrefab == null)
        {
            return;
        }

        while (renderedCount < revealTrail.myDots.Count)
        {
            RevealDot dot = revealTrail.myDots[renderedCount];
            Transform parent = dotParent != null ? dotParent : transform;

            GameObject obj = Instantiate(dotPrefab, dot.worldPos, Quaternion.identity, parent);
            obj.transform.localScale = Vector3.one * dot.size;

            // 점은 보이기만 해야 하므로 충돌/물리 제거
            Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in colliders)
            {
                col.enabled = false;
            }

            Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>(true);
            foreach (Rigidbody rb in rigidbodies)
            {
                Destroy(rb);
            }

            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer != -1)
            {
                obj.layer = ignoreRaycastLayer;
            }

            RevealDotVisual visual = obj.GetComponent<RevealDotVisual>();
            if (visual != null)
            {
                visual.Initialize(localProximitySensor);
            }

            spawnedDots.Add(obj);
            renderedCount++;
        }
    }

    public void ClearAllDots()
    {
        foreach (GameObject dot in spawnedDots)
        {
            if (dot != null)
            {
                Destroy(dot);
            }
        }

        spawnedDots.Clear();
        renderedCount = 0;
    }
}