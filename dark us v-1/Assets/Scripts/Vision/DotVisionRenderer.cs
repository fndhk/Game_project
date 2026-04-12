using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 PlayerRevealTrail에 저장된 점 데이터를 읽어서
// 실제 프리팹을 생성하고 카메라를 향하게 만든다.
// 기본 설정은 점이 사라지지 않도록 fadeWithLifetime를 false로 둔다.
public class DotVisionRenderer : MonoBehaviour
{
    [Header("References")]
    // 점 데이터를 가지고 있는 PlayerRevealTrail 참조이다.
    public PlayerRevealTrail revealTrail;

    // 실제로 생성할 점 프리팹이다.
    public GameObject dotPrefab;

    // 생성된 점들을 담아둘 부모 Transform이다.
    // 반드시 씬 루트의 DotContainer를 넣는 것을 추천한다.
    public Transform dotParent;

    // 점이 바라볼 카메라이다.
    public Camera targetCamera;

    [Header("Dot Visual")]
    // 점 색을 강제로 적용할지 정하는 값이다.
    public bool forceDotColor = true;

    // 점 색상이다.
    public Color dotColor = Color.white;

    // 점이 항상 카메라를 바라보게 할지 정한다.
    public bool faceCamera = true;

    // 수명에 따라 서서히 사라지게 할지 정한다.
    // 지금 효과는 누적 유지가 목적이므로 기본값은 false가 맞다.
    public bool fadeWithLifetime = false;

    // 생성 직후 알파값이다.
    [Range(0f, 1f)]
    public float startAlpha = 1f;

    // 수명이 끝나기 직전 알파값이다.
    [Range(0f, 1f)]
    public float endAlpha = 0f;

    [Header("Runtime")]
    // 현재 실제 오브젝트로 남아 있는 점 개수이다.
    public int renderedCount = 0;

    // 현재 살아 있는 점 오브젝트들을 id 기준으로 저장한다.
    private readonly Dictionary<int, RuntimeDot> activeDots = new Dictionary<int, RuntimeDot>();

    // 시작 시 카메라를 자동으로 연결한다.
    private void Awake()
    {
        // targetCamera가 비어 있으면 Main Camera를 사용한다.
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    // 매 프레임 점 데이터를 실제 오브젝트와 동기화한다.
    private void Update()
    {
        // 필수 참조가 없으면 실행하지 않는다.
        if (revealTrail == null || dotPrefab == null)
        {
            return;
        }

        // 카메라가 비어 있으면 다시 찾는다.
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // 현재 살아 있는 점 id들을 기록한다.
        HashSet<int> aliveIds = new HashSet<int>();

        // 점 데이터 목록을 순회한다.
        for (int i = 0; i < revealTrail.myDots.Count; i++)
        {
            // 현재 점 데이터를 가져온다.
            RevealDot dotData = revealTrail.myDots[i];

            // 살아 있는 id 목록에 기록한다.
            aliveIds.Add(dotData.id);

            // 아직 오브젝트가 없으면 새로 생성한다.
            if (!activeDots.ContainsKey(dotData.id))
            {
                CreateRuntimeDot(dotData);
            }

            // 이미 있는 오브젝트는 현재 데이터 기준으로 갱신한다.
            UpdateRuntimeDot(dotData);
        }

        // 더 이상 데이터에 없는 점 오브젝트를 정리한다.
        RemoveMissingDots(aliveIds);

        // 현재 렌더링 개수를 갱신한다.
        renderedCount = activeDots.Count;
    }

    // 새 점 데이터를 실제 점 오브젝트로 생성하는 함수이다.
    private void CreateRuntimeDot(RevealDot dotData)
    {
        // 점의 부모를 정한다.
        Transform parent = dotParent != null ? dotParent : transform;

        // 기본 회전값을 만든다.
        Quaternion rotation = Quaternion.identity;

        // 카메라를 보게 할 설정이면 생성 순간 회전도 맞춘다.
        if (faceCamera && targetCamera != null)
        {
            Vector3 toCamera = targetCamera.transform.position - dotData.worldPos;

            if (toCamera.sqrMagnitude > 0.0001f)
            {
                rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
        }

        // 점 프리팹을 실제 위치에 생성한다.
        GameObject dotObject = Instantiate(dotPrefab, dotData.worldPos, rotation, parent);

        // 점 크기를 데이터에 맞춘다.
        dotObject.transform.localScale = Vector3.one * dotData.size;

        // 점에는 충돌과 물리가 필요 없으므로 비활성화한다.
        DisablePhysics(dotObject);

        // Ignore Raycast 레이어가 있으면 자식까지 포함해서 적용한다.
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer != -1)
        {
            SetLayerRecursively(dotObject, ignoreRaycastLayer);
        }

        // 렌더러 배열을 미리 저장한다.
        Renderer[] renderers = dotObject.GetComponentsInChildren<Renderer>(true);

        // 런타임 점 정보를 저장한다.
        activeDots[dotData.id] = new RuntimeDot(dotObject, renderers);
    }

    // 이미 생성된 점 오브젝트를 갱신하는 함수이다.
    private void UpdateRuntimeDot(RevealDot dotData)
    {
        // 대응되는 런타임 점 정보가 없으면 종료한다.
        if (!activeDots.TryGetValue(dotData.id, out RuntimeDot runtimeDot))
        {
            return;
        }

        // 오브젝트가 파괴되어 있으면 딕셔너리에서 제거하고 종료한다.
        if (runtimeDot.instance == null)
        {
            activeDots.Remove(dotData.id);
            return;
        }

        // 위치를 데이터 기준으로 맞춘다.
        runtimeDot.instance.transform.position = dotData.worldPos;

        // 크기를 데이터 기준으로 맞춘다.
        runtimeDot.instance.transform.localScale = Vector3.one * dotData.size;

        // 카메라를 향하게 할 설정이면 회전을 갱신한다.
        if (faceCamera && targetCamera != null)
        {
            Vector3 toCamera = targetCamera.transform.position - runtimeDot.instance.transform.position;

            if (toCamera.sqrMagnitude > 0.0001f)
            {
                runtimeDot.instance.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }
        }

        // 기본 알파값을 시작 알파로 둔다.
        float alpha = startAlpha;

        // 수명 기반 페이드를 쓰고, lifetime이 실제로 양수일 때만 보간한다.
        if (fadeWithLifetime && dotData.lifetime > 0.0001f)
        {
            float lifeT = Mathf.Clamp01((Time.time - dotData.spawnTime) / dotData.lifetime);
            alpha = Mathf.Lerp(startAlpha, endAlpha, lifeT);
        }

        // 최종 색과 알파를 적용한다.
        ApplyDotColor(runtimeDot.renderers, alpha);
    }

    // 현재 데이터에 더 이상 없는 점 오브젝트를 삭제하는 함수이다.
    private void RemoveMissingDots(HashSet<int> aliveIds)
    {
        // 제거할 id 목록을 만든다.
        List<int> idsToRemove = new List<int>();

        // 현재 살아 있는 런타임 점을 하나씩 검사한다.
        foreach (KeyValuePair<int, RuntimeDot> pair in activeDots)
        {
            // 데이터에 없는 id면 제거 대상으로 넣는다.
            if (!aliveIds.Contains(pair.Key))
            {
                idsToRemove.Add(pair.Key);
            }
        }

        // 실제로 제거한다.
        for (int i = 0; i < idsToRemove.Count; i++)
        {
            int id = idsToRemove[i];

            if (activeDots.TryGetValue(id, out RuntimeDot runtimeDot))
            {
                if (runtimeDot.instance != null)
                {
                    Destroy(runtimeDot.instance);
                }
            }

            activeDots.Remove(id);
        }
    }

    // 점 오브젝트의 충돌과 물리를 제거하는 함수이다.
    private void DisablePhysics(GameObject obj)
    {
        // 자식까지 포함한 모든 Collider를 찾는다.
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        // Collider를 모두 끈다.
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // 자식까지 포함한 모든 Rigidbody를 찾는다.
        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>(true);

        // Rigidbody를 모두 제거한다.
        foreach (Rigidbody rb in rigidbodies)
        {
            Destroy(rb);
        }
    }

    // 렌더러 배열에 점 색과 알파를 적용하는 함수이다.
    private void ApplyDotColor(Renderer[] renderers, float alpha)
    {
        // 렌더러가 없으면 종료한다.
        if (renderers == null)
        {
            return;
        }

        // 최종 색상을 만든다.
        Color finalColor = forceDotColor ? dotColor : Color.white;
        finalColor.a = alpha;

        // 렌더러를 하나씩 순회하며 적용한다.
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (renderer == null || renderer.sharedMaterial == null)
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            if (renderer.sharedMaterial.HasProperty("_Color"))
            {
                block.SetColor("_Color", finalColor);
            }

            if (renderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                block.SetColor("_BaseColor", finalColor);
            }

            renderer.SetPropertyBlock(block);
        }
    }

    // 자식까지 포함해서 레이어를 바꾸는 함수이다.
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        // 현재 오브젝트 레이어를 바꾼다.
        obj.layer = layer;

        // 자식도 같은 레이어로 바꾼다.
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
        }
    }

    // 현재 생성된 점 오브젝트를 전부 삭제하는 함수이다.
    public void ClearAllDots()
    {
        // 모든 런타임 점을 순회한다.
        foreach (KeyValuePair<int, RuntimeDot> pair in activeDots)
        {
            if (pair.Value.instance != null)
            {
                Destroy(pair.Value.instance);
            }
        }

        // 딕셔너리를 비운다.
        activeDots.Clear();

        // 개수도 0으로 맞춘다.
        renderedCount = 0;
    }

    // 런타임 점 정보를 저장하는 구조체이다.
    private struct RuntimeDot
    {
        // 실제 생성된 오브젝트이다.
        public GameObject instance;

        // 색상 적용에 사용할 렌더러 배열이다.
        public Renderer[] renderers;

        // 생성자이다.
        public RuntimeDot(GameObject instance, Renderer[] renderers)
        {
            this.instance = instance;
            this.renderers = renderers;
        }
    }
}