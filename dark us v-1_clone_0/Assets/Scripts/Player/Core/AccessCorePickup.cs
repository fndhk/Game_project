using UnityEngine;

// 플레이어가 주워서 Terminal에 넣는 연구소 탈출 아이템이다.
public class AccessCorePickup : MonoBehaviour, IPlayerInteractable
{
    [Header("Pickup")]
    // 한 번 주웠을 때 증가할 Access Core 개수이다.
    public int coreAmount = 1;

    // 주운 뒤 오브젝트를 비활성화할지 정한다.
    public bool hideAfterPickup = true;

    [Header("Scan Dot Cleanup")]
    // 주웠을 때 이미 찍혀 있던 Access Core 점도 같이 지울지 정한다.
    public bool removeScanDotsAfterPickup = true;

    // 점을 지울 최소 반경이다.
    public float scanDotRemoveRadius = 0.9f;

    // Collider/Renderer 크기를 기준으로 실제 제거 반경을 자동 보정한다.
    public bool useObjectBoundsForDotCleanup = true;

    // Bounds 기준 반경에 추가로 더할 여유값이다.
    public float scanDotRemovePadding = 0.18f;

    // 켜면 AccessCore 색상 그룹 점만 지운다. 꺼두면 작은 반경 안의 점을 전부 지운다.
    public bool onlyRemoveAccessCoreColorDots = false;

    // 점 렌더러 참조이다. 비워두면 씬에서 자동으로 찾는다.
    public InstancedScanDotRenderer[] scanDotRenderers;

    // 이미 획득되었는지 저장한다.
    private bool collected = false;

    // 상호작용 문구를 반환한다.
    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        return "[E] Take Access Core";
    }

    // 아직 먹지 않은 상태면 상호작용 가능하다.
    public bool CanInteract(PlayerObjectiveInteractor interactor)
    {
        return !collected;
    }

    // Access Core를 획득한다.
    public void Interact(PlayerObjectiveInteractor interactor)
    {
        if (collected)
        {
            return;
        }

        collected = true;

        if (LabObjectiveManager.Instance != null)
        {
            LabObjectiveManager.Instance.CollectCore(coreAmount);
        }

        // 오브젝트를 숨기기 전에 이미 생성된 스캔 점을 먼저 제거한다.
        RemoveExistingScanDots();

        if (hideAfterPickup)
        {
            gameObject.SetActive(false);
        }
    }

    // Access Core 주변에 이미 찍힌 스캔 점을 제거한다.
    private void RemoveExistingScanDots()
    {
        if (!removeScanDotsAfterPickup)
        {
            return;
        }

        if (scanDotRenderers == null || scanDotRenderers.Length <= 0)
        {
            scanDotRenderers = FindObjectsOfType<InstancedScanDotRenderer>();
        }

        if (scanDotRenderers == null || scanDotRenderers.Length <= 0)
        {
            return;
        }

        Vector3 center = transform.position;
        float radius = Mathf.Max(0.01f, scanDotRemoveRadius);

        if (useObjectBoundsForDotCleanup && TryGetObjectBounds(out Bounds bounds))
        {
            center = bounds.center;
            radius = Mathf.Max(radius, bounds.extents.magnitude + Mathf.Max(0f, scanDotRemovePadding));
        }

        for (int i = 0; i < scanDotRenderers.Length; i++)
        {
            if (scanDotRenderers[i] == null)
            {
                continue;
            }

            if (onlyRemoveAccessCoreColorDots)
            {
                scanDotRenderers[i].RemoveDotsInSphere(center, radius, ScanDotColorGroup.AccessCore);
            }
            else
            {
                scanDotRenderers[i].RemoveDotsInSphere(center, radius);
            }
        }
    }

    // Collider 또는 Renderer를 기준으로 오브젝트 전체 Bounds를 계산한다.
    private bool TryGetObjectBounds(out Bounds resultBounds)
    {
        bool hasBounds = false;
        resultBounds = new Bounds(transform.position, Vector3.zero);

        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                resultBounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                resultBounds.Encapsulate(colliders[i].bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                resultBounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                resultBounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }
}
