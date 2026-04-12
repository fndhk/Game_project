using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 PlayerRevealTrail이 만든 점 데이터만 읽어서
// 실제 점 프리팹을 씬에 하나씩 생성하는 역할을 한다.
// 핵심은 "현재까지 추가된 데이터만" 순서대로 그리는 것이다.
public class DotVisionRenderer : MonoBehaviour
{
    [Header("References")]
    // 점 데이터를 가지고 있는 PlayerRevealTrail 참조이다.
    public PlayerRevealTrail revealTrail;

    // 실제로 생성할 점 프리팹이다.
    public GameObject dotPrefab;

    // 생성된 점들을 모아 둘 부모 Transform이다.
    // 비워 두면 이 스크립트가 붙은 오브젝트 아래에 생성된다.
    public Transform dotParent;

    [Header("Dot Visual")]
    // 점 색을 강제로 적용할지 정한다.
    public bool forceDotColor = true;

    // 점 색상이다.
    public Color dotColor = Color.white;

    [Header("Runtime")]
    // 현재까지 실제 오브젝트로 생성한 점 개수이다.
    public int renderedCount = 0;

    // 생성한 점 오브젝트들을 저장하는 리스트이다.
    private readonly List<GameObject> spawnedDots = new List<GameObject>();

    // 매 프레임 새 점 데이터가 생겼는지 확인한다.
    private void Update()
    {
        // 필수 참조가 없으면 종료한다.
        if (revealTrail == null || dotPrefab == null)
        {
            return;
        }

        // 데이터가 완전히 비워졌으면 생성한 오브젝트도 같이 정리한다.
        if (revealTrail.myDots.Count == 0 && renderedCount > 0)
        {
            ClearAllDots();
            return;
        }

        // 혹시 렌더 카운트가 데이터보다 커졌으면 전체를 다시 맞춘다.
        if (renderedCount > revealTrail.myDots.Count)
        {
            ClearAllDots();
            return;
        }

        // 아직 실제 오브젝트로 만들지 않은 점만 순서대로 생성한다.
        while (renderedCount < revealTrail.myDots.Count)
        {
            // 이번에 생성할 점 데이터를 가져온다.
            RevealDot dotData = revealTrail.myDots[renderedCount];

            // 부모를 정한다.
            Transform parent = dotParent != null ? dotParent : transform;

            // 프리팹을 실제 위치에 생성한다.
            GameObject dotObject = Instantiate(dotPrefab, dotData.worldPos, Quaternion.identity, parent);

            // 점 크기를 데이터에 맞춘다.
            dotObject.transform.localScale = Vector3.one * dotData.size;

            // 충돌과 물리를 제거해서 점이 게임플레이에 영향을 주지 않게 한다.
            DisablePhysics(dotObject);

            // 색상을 적용한다.
            ApplyDotColor(dotObject);

            // Ignore Raycast 레이어가 있으면 그 레이어로 바꿔서 다시 스캔 대상이 되지 않게 한다.
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer != -1)
            {
                SetLayerRecursively(dotObject, ignoreRaycastLayer);
            }

            // 생성 목록에 추가한다.
            spawnedDots.Add(dotObject);

            // 렌더된 개수를 증가시킨다.
            renderedCount++;
        }
    }

    // 점 오브젝트의 충돌과 물리를 제거하는 함수이다.
    private void DisablePhysics(GameObject obj)
    {
        // 자식까지 포함한 모든 Collider를 찾는다.
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // 자식까지 포함한 모든 Rigidbody를 찾는다.
        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rigidbodies)
        {
            Destroy(rb);
        }
    }

    // 점 프리팹에 색상을 적용하는 함수이다.
    private void ApplyDotColor(GameObject obj)
    {
        // 강제 색상 적용을 사용하지 않으면 종료한다.
        if (!forceDotColor)
        {
            return;
        }

        // SpriteRenderer가 있으면 직접 색을 바꾼다.
        SpriteRenderer[] spriteRenderers = obj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in spriteRenderers)
        {
            sr.color = dotColor;
        }

        // 일반 Renderer에는 MaterialPropertyBlock으로 색을 적용한다.
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            // SpriteRenderer는 이미 위에서 처리했으므로 건너뛴다.
            if (renderer is SpriteRenderer)
            {
                continue;
            }

            // 공유 머티리얼이 없으면 건너뛴다.
            if (renderer.sharedMaterial == null)
            {
                continue;
            }

            // 프로퍼티 블록을 만든다.
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            // 기본 색상 프로퍼티가 있으면 적용한다.
            if (renderer.sharedMaterial.HasProperty("_Color"))
            {
                block.SetColor("_Color", dotColor);
            }

            // URP 기본 색상 프로퍼티가 있으면 적용한다.
            if (renderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                block.SetColor("_BaseColor", dotColor);
            }

            // 적용한 값을 다시 넣는다.
            renderer.SetPropertyBlock(block);
        }
    }

    // 자식까지 포함해서 레이어를 재귀적으로 바꾸는 함수이다.
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        for (int i = 0; i < obj.transform.childCount; i++)
        {
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
        }
    }

    // 현재 생성된 점을 전부 삭제하는 함수이다.
    public void ClearAllDots()
    {
        // 저장된 점 오브젝트를 전부 제거한다.
        foreach (GameObject dot in spawnedDots)
        {
            if (dot != null)
            {
                Destroy(dot);
            }
        }

        // 리스트를 비운다.
        spawnedDots.Clear();

        // 렌더 카운트도 0으로 되돌린다.
        renderedCount = 0;
    }
}
