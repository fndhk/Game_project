using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 맵 루트 아래에 있는 시각 요소를
// 게임 시작 시 한 번에 숨기기 위한 용도이다.
public class MapVisualHider : MonoBehaviour
{
    [Header("Hide Roots")]
    // 이 배열에 넣은 루트들의 자식 오브젝트를 전부 검사해서
    // MeshRenderer와 SkinnedMeshRenderer를 찾아 숨긴다.
    [SerializeField] private Transform[] hideRoots;

    [Header("Terrains")]
    // Terrain은 일반 MeshRenderer가 아니기 때문에
    // 따로 배열로 받아서 Terrain 컴포넌트를 꺼서 숨긴다.
    [SerializeField] private Terrain[] terrains;

    [Header("Options")]
    // 이 값이 켜져 있으면 게임이 시작될 때 자동으로 맵 시각 요소를 숨긴다.
    [SerializeField] private bool hideOnAwake = true;

    // 찾은 MeshRenderer들을 저장해두는 리스트이다.
    // 나중에 다시 보이게 할 때도 같은 목록을 재사용할 수 있다.
    private readonly List<MeshRenderer> cachedMeshRenderers = new List<MeshRenderer>();

    // 찾은 SkinnedMeshRenderer들을 저장해두는 리스트이다.
    // 캐릭터형 메쉬나 스킨드 메쉬가 섞여 있어도 함께 처리할 수 있다.
    private readonly List<SkinnedMeshRenderer> cachedSkinnedRenderers = new List<SkinnedMeshRenderer>();

    // 시작 전에 한 번 대상들을 캐싱하고,
    // 옵션에 따라 즉시 숨김을 적용한다.
    private void Awake()
    {
        CacheTargets();

        if (hideOnAwake)
        {
            HideMapVisuals();
        }
    }

    // 인스펙터 우클릭이나 컨텍스트 메뉴에서 수동으로 캐싱할 수 있게 만든다.
    [ContextMenu("Cache Targets")]
    public void CacheTargets()
    {
        // 이전에 저장된 목록을 먼저 비운다.
        cachedMeshRenderers.Clear();
        cachedSkinnedRenderers.Clear();

        // hideRoots가 비어 있으면 더 진행하지 않는다.
        if (hideRoots == null)
        {
            return;
        }

        // 각 루트를 돌면서 자식 렌더러를 전부 수집한다.
        for (int i = 0; i < hideRoots.Length; i++)
        {
            // 루트가 비어 있으면 건너뛴다.
            if (hideRoots[i] == null)
            {
                continue;
            }

            // 해당 루트 아래의 모든 MeshRenderer를 가져온다.
            MeshRenderer[] meshRenderers = hideRoots[i].GetComponentsInChildren<MeshRenderer>(true);

            // 찾은 MeshRenderer들을 캐시에 추가한다.
            for (int j = 0; j < meshRenderers.Length; j++)
            {
                // 중복 추가를 막기 위해 이미 들어간 렌더러는 제외한다.
                if (!cachedMeshRenderers.Contains(meshRenderers[j]))
                {
                    cachedMeshRenderers.Add(meshRenderers[j]);
                }
            }

            // 해당 루트 아래의 모든 SkinnedMeshRenderer를 가져온다.
            SkinnedMeshRenderer[] skinnedRenderers = hideRoots[i].GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // 찾은 SkinnedMeshRenderer들을 캐시에 추가한다.
            for (int j = 0; j < skinnedRenderers.Length; j++)
            {
                // 중복 추가를 막기 위해 이미 들어간 렌더러는 제외한다.
                if (!cachedSkinnedRenderers.Contains(skinnedRenderers[j]))
                {
                    cachedSkinnedRenderers.Add(skinnedRenderers[j]);
                }
            }
        }
    }

    // 현재 캐싱된 맵 시각 요소를 전부 숨긴다.
    [ContextMenu("Hide Map Visuals")]
    public void HideMapVisuals()
    {
        // 저장된 MeshRenderer를 하나씩 꺼서 보이지 않게 만든다.
        for (int i = 0; i < cachedMeshRenderers.Count; i++)
        {
            if (cachedMeshRenderers[i] != null)
            {
                cachedMeshRenderers[i].enabled = false;
            }
        }

        // 저장된 SkinnedMeshRenderer도 같은 방식으로 보이지 않게 만든다.
        for (int i = 0; i < cachedSkinnedRenderers.Count; i++)
        {
            if (cachedSkinnedRenderers[i] != null)
            {
                cachedSkinnedRenderers[i].enabled = false;
            }
        }

        // Terrain은 일반 렌더러가 아니므로 Terrain 컴포넌트를 꺼서 숨긴다.
        // TerrainCollider는 별도 컴포넌트라서 그대로 남아 플레이가 가능하다.
        if (terrains != null)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null)
                {
                    terrains[i].enabled = false;
                }
            }
        }
    }

    // 숨긴 맵 시각 요소를 다시 보이게 한다.
    // 디버그나 맵 편집 중에 다시 켜보고 싶을 때 사용할 수 있다.
    [ContextMenu("Show Map Visuals")]
    public void ShowMapVisuals()
    {
        // 저장된 MeshRenderer를 다시 켠다.
        for (int i = 0; i < cachedMeshRenderers.Count; i++)
        {
            if (cachedMeshRenderers[i] != null)
            {
                cachedMeshRenderers[i].enabled = true;
            }
        }

        // 저장된 SkinnedMeshRenderer도 다시 켠다.
        for (int i = 0; i < cachedSkinnedRenderers.Count; i++)
        {
            if (cachedSkinnedRenderers[i] != null)
            {
                cachedSkinnedRenderers[i].enabled = true;
            }
        }

        // Terrain도 다시 켜서 보이게 만든다.
        if (terrains != null)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null)
                {
                    terrains[i].enabled = true;
                }
            }
        }
    }
}