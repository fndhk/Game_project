using UnityEngine;

// 현재 선택된 인벤토리 아이템을 사용하는 스크립트이다.
// 기본 사용 입력은 마우스 좌클릭이다.
// Camera는 강한 점 스캔, Knife는 근접 공격, Medkit은 자가 회복을 한다.
public class PlayerItemUser : MonoBehaviour
{
    [Header("References")]
    // 아이템을 읽을 인벤토리이다.
    public PlayerInventory inventory;

    // 플레이어 카메라이다.
    public Camera playerCamera;

    // 점을 실제로 그리는 GPU 인스턴싱 렌더러이다.
    public InstancedScanDotRenderer dotRenderer;

    // 자기 자신의 체력 스탯이다.
    public PlayerStats playerStats;

    // 자기 자신의 역할/사망 상태이다.
    public PlayerCombatTarget selfTarget;

    [Header("Input")]
    // 아이템 사용 마우스 버튼이다. 0이면 좌클릭이다.
    public int useItemMouseButton = 0;

    // 선택 아이템을 버리는 키이다.
    public KeyCode dropItemKey = KeyCode.G;

    // 아이템을 버릴 때 카메라 앞쪽으로 떨어뜨릴 거리이다.
    public float dropForwardDistance = 1.15f;

    // 아이템을 버릴 때 바닥 쪽으로 내릴 높이이다.
    public float dropDownOffset = 0.65f;

    [Header("Drop Prefabs")]
    // 버릴 때 생성할 카메라 아이템 프리팹이다.
    public GameObject cameraDropPrefab;

    // 버릴 때 생성할 칼 아이템 프리팹이다.
    public GameObject knifeDropPrefab;

    // 버릴 때 생성할 구급상자 아이템 프리팹이다.
    public GameObject medkitDropPrefab;

    [Header("Camera Item")]
    // 카메라 아이템 1회 사용 시 생성할 점 개수이다.
    public int cameraPointCount = 700;

    // 카메라 아이템의 최대 거리이다.
    public float cameraMaxDistance = 18f;

    // 카메라 아이템의 화면 가로 범위이다.
    public float cameraScreenHalfWidth = 0.48f;

    // 카메라 아이템의 화면 세로 범위이다.
    public float cameraScreenHalfHeight = 0.36f;

    // 점 하나를 만들기 위해 재시도할 횟수이다.
    public int cameraAttemptsPerPoint = 4;

    // 점을 표면에서 살짝 띄우는 값이다.
    public float cameraSurfaceOffset = 0.012f;

    // 스캔 대상 레이어이다.
    public LayerMask scanMask = ~0;

    [Header("Knife Item")]
    // 칼 피해량이다. 100이면 기본 체력 기준 즉사에 가깝다.
    public float knifeDamage = 100f;

    // 칼 공격 거리이다.
    public float knifeAttackDistance = 1.55f;

    // 칼 공격 판정 반지름이다.
    public float knifeAttackRadius = 0.55f;

    // 칼 공격 대상 레이어이다.
    public LayerMask knifeTargetMask = ~0;

    // 벽 가림 판정 레이어이다.
    public LayerMask knifeObstacleMask = ~0;

    // 켜면 같은 시민도 찌를 수 있다.
    // 역할을 모르는 심리전 게임이면 true 추천.
    public bool allowFriendlyFire = true;

    // 칼이 빗나가도 소모할지 정한다.
    // 처음 테스트할 때는 false 추천.
    public bool consumeKnifeWhenMissed = false;

    [Header("Medkit Item")]
    // 구급상자 회복량이다.
    public float medkitHealAmount = 50f;

    // 켜면 체력 회복량만큼 스태미나도 같이 회복한다.
    // 기본은 false가 더 밸런스가 좋다.
    public bool restoreStaminaWithMedkit = false;

    [Header("Audio Optional")]
    // 카메라 사용 사운드이다.
    public AudioSource cameraUseAudio;

    // 칼 사용 사운드이다.
    public AudioSource knifeUseAudio;

    // 구급상자 사용 사운드이다.
    public AudioSource medkitUseAudio;

    [Header("Default Item Audio Clips")]
    // 비워두면 Resources/Audio/Items/Item_CameraUse를 자동으로 불러온다.
    public AudioClip cameraUseClip;

    // 비워두면 Resources/Audio/Items/Item_KnifeUse를 자동으로 불러온다.
    public AudioClip knifeUseClip;

    // 비워두면 Resources/Audio/Items/Item_MedkitUse를 자동으로 불러온다.
    public AudioClip medkitUseClip;

    // 전용 AudioSource가 비어 있을 때 사용할 공용 아이템 사운드 소스이다.
    private AudioSource itemAudioSource;

    private void Awake()
    {
        AutoFindReferences();
        AutoFindAudio();
    }

    private void Update()
    {
        if (Input.GetKeyDown(GameInputBindings.UseItem))
        {
            UseSelectedItem();
        }

        if (Input.GetKeyDown(GameInputBindings.DropItem))
        {
            DropSelectedItem();
        }
    }

    // 필요한 참조를 자동으로 찾는다.
    private void AutoFindReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (dotRenderer == null && playerCamera != null)
        {
            dotRenderer = playerCamera.GetComponent<InstancedScanDotRenderer>();
        }

        if (dotRenderer == null)
        {
            dotRenderer = GetComponentInChildren<InstancedScanDotRenderer>(true);
        }

        if (dotRenderer == null)
        {
            dotRenderer = Object.FindFirstObjectByType<InstancedScanDotRenderer>();
        }

        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        if (selfTarget == null)
        {
            selfTarget = GetComponent<PlayerCombatTarget>();
        }
    }

    // 현재 선택된 아이템을 1개 바닥에 버린다.
    public void DropSelectedItem()
    {
        AutoFindReferences();

        if (inventory == null)
        {
            Debug.LogWarning("PlayerItemUser: PlayerInventory가 없음.");
            return;
        }

        if (selfTarget != null && selfTarget.isDead)
        {
            return;
        }

        if (!inventory.TryRemoveSelectedItem(1, out ItemType droppedItemType, out int droppedAmount))
        {
            Debug.Log("No item selected.");
            return;
        }

        SpawnDroppedItem(droppedItemType, droppedAmount);
        GameAudioManager.PlayItemDrop();
    }

    // 버린 아이템을 다시 주울 수 있는 월드 아이템으로 만든다.
    private void SpawnDroppedItem(ItemType itemType, int amount)
    {
        if (itemType == ItemType.None || amount <= 0)
        {
            return;
        }

        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position + Vector3.up;
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        Vector3 position = origin + forward.normalized * Mathf.Max(0.1f, dropForwardDistance) + Vector3.down * Mathf.Max(0f, dropDownOffset);

        if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit hit, 2.5f, scanMask, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.12f;
        }

        GameObject dropPrefab = GetDropPrefab(itemType);
        GameObject itemObject;

        if (dropPrefab != null)
        {
            itemObject = Instantiate(dropPrefab, position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            itemObject.name = GetDroppedItemObjectName(itemType);
        }
        else
        {
            itemObject = CreateFallbackDroppedItem(itemType, position);
        }

        itemObject.transform.position = position;
        itemObject.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        ConfigureDroppedPickup(itemObject, itemType, amount);
        HideDroppedItemRenderers(itemObject);
    }

    // 드롭할 실제 아이템 프리팹을 반환한다.
    private GameObject GetDropPrefab(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Camera:
                return cameraDropPrefab;

            case ItemType.Knife:
                return knifeDropPrefab;

            case ItemType.Medkit:
                return medkitDropPrefab;

            default:
                return null;
        }
    }

    // 프리팹 연결이 없을 때도 기능은 유지되게 최소 Collider만 만든다.
    private GameObject CreateFallbackDroppedItem(ItemType itemType, Vector3 position)
    {
        GameObject itemObject = new GameObject(GetDroppedItemObjectName(itemType));
        itemObject.transform.position = position;
        itemObject.layer = gameObject.layer;

        BoxCollider collider = itemObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.12f, 0f);
        collider.size = GetDroppedItemColliderSize(itemType);

        return itemObject;
    }

    // 드롭된 오브젝트가 다시 주울 수 있는 아이템 상태가 되도록 보정한다.
    private void ConfigureDroppedPickup(GameObject itemObject, ItemType itemType, int amount)
    {
        if (itemObject == null)
        {
            return;
        }

        WorldItemPickup pickup = itemObject.GetComponent<WorldItemPickup>();

        if (pickup == null)
        {
            pickup = itemObject.AddComponent<WorldItemPickup>();
        }

        pickup.itemType = itemType;
        pickup.amount = Mathf.Max(1, amount);
        pickup.hideAfterPickup = true;
        pickup.destroyAfterPickup = false;
        pickup.onlyRemoveItemColorDots = true;

        ScanSurfaceInfo surfaceInfo = itemObject.GetComponent<ScanSurfaceInfo>();

        if (surfaceInfo == null)
        {
            surfaceInfo = itemObject.AddComponent<ScanSurfaceInfo>();
        }

        surfaceInfo.surfaceType = ScanSurfaceType.Item;
    }

    // 화면에는 보이지 않게 Renderer만 끈다. Collider는 유지되어 스캔 점은 실제 형태로 찍힌다.
    private void HideDroppedItemRenderers(GameObject itemObject)
    {
        if (itemObject == null)
        {
            return;
        }

        Renderer[] renderers = itemObject.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }
    }

    private string GetDroppedItemObjectName(ItemType itemType)
    {
        return itemType + "Drop";
    }

    private Vector3 GetDroppedItemColliderSize(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Camera:
                return new Vector3(0.48f, 0.24f, 0.34f);

            case ItemType.Knife:
                return new Vector3(0.18f, 0.18f, 0.7f);

            case ItemType.Medkit:
                return new Vector3(0.52f, 0.28f, 0.38f);

            default:
                return new Vector3(0.35f, 0.25f, 0.35f);
        }
    }

    // 아이템 사용 효과음을 자동으로 준비한다.
    private void AutoFindAudio()
    {
        if (itemAudioSource == null)
        {
            itemAudioSource = GetComponent<AudioSource>();
        }

        if (itemAudioSource == null)
        {
            itemAudioSource = gameObject.AddComponent<AudioSource>();
            itemAudioSource.playOnAwake = false;
            itemAudioSource.spatialBlend = 0f;
        }

        if (cameraUseClip == null)
        {
            cameraUseClip = Resources.Load<AudioClip>("Audio/Items/Item_CameraUse");
        }

        if (knifeUseClip == null)
        {
            knifeUseClip = Resources.Load<AudioClip>("Audio/Items/Item_KnifeUse");
        }

        if (medkitUseClip == null)
        {
            medkitUseClip = Resources.Load<AudioClip>("Audio/Items/Item_MedkitUse");
        }
    }

    // 현재 선택된 아이템을 사용한다.
    public void UseSelectedItem()
    {
        AutoFindReferences();
        AutoFindAudio();

        if (inventory == null)
        {
            Debug.LogWarning("PlayerItemUser: PlayerInventory가 없음.");
            return;
        }

        if (selfTarget != null && selfTarget.isDead)
        {
            return;
        }

        ItemType selectedItem = inventory.GetSelectedItemType();

        if (selectedItem == ItemType.None)
        {
            Debug.Log("No item selected.");
            return;
        }

        bool shouldConsume = false;

        switch (selectedItem)
        {
            case ItemType.Camera:
                shouldConsume = TryUseCameraItem();
                break;

            case ItemType.Knife:
                shouldConsume = TryUseKnifeItem();
                break;

            case ItemType.Medkit:
                shouldConsume = TryUseMedkitItem();
                break;
        }

        if (shouldConsume)
        {
            inventory.ConsumeSelectedItem(1);
        }
    }

    // 카메라 아이템을 사용해서 정면 넓은 범위에 점을 찍는다.
    private bool TryUseCameraItem()
    {
        if (playerCamera == null || dotRenderer == null)
        {
            Debug.LogWarning("Camera item failed: Camera 또는 DotRenderer가 없음.");
            return false;
        }

        int createdCount = 0;
        int safePointCount = Mathf.Max(1, cameraPointCount);
        int safeAttempts = Mathf.Max(1, cameraAttemptsPerPoint);

        for (int i = 0; i < safePointCount; i++)
        {
            bool created = TryCreateOneCameraScanDot(safeAttempts);

            if (created)
            {
                createdCount++;
            }
        }

        if (!PlayAudio(cameraUseAudio, cameraUseClip))
        {
            GameAudioManager.PlayItemUse(ItemType.Camera);
        }

        Debug.Log("Camera scan dots: " + createdCount);

        // 점이 하나도 안 찍혔더라도 카메라를 사용한 것으로 처리한다.
        return true;
    }

    // 카메라 아이템으로 점 하나 생성을 시도한다.
    private bool TryCreateOneCameraScanDot(int attempts)
    {
        for (int i = 0; i < attempts; i++)
        {
            Vector2 viewportPoint = GetRandomCameraViewportPoint();

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, cameraMaxDistance, scanMask, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            Vector3 dotPosition = hit.point + hit.normal * cameraSurfaceOffset;
            ScanDotColorGroup colorGroup = ResolveDotColorGroup(hit);

            dotRenderer.AddDot(dotPosition, hit.normal, colorGroup);
            return true;
        }

        return false;
    }

    // 카메라 아이템용 랜덤 화면 좌표를 만든다.
    private Vector2 GetRandomCameraViewportPoint()
    {
        float x = 0.5f + Random.Range(-cameraScreenHalfWidth, cameraScreenHalfWidth);
        float y = 0.5f + Random.Range(-cameraScreenHalfHeight, cameraScreenHalfHeight);

        x = Mathf.Clamp(x, 0.02f, 0.98f);
        y = Mathf.Clamp(y, 0.02f, 0.98f);

        return new Vector2(x, y);
    }

    // 칼 아이템을 사용한다.
    private bool TryUseKnifeItem()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("Knife item failed: Camera가 없음.");
            return false;
        }

        PlayerCombatTarget target = FindBestKnifeTarget();

        if (!PlayAudio(knifeUseAudio, knifeUseClip))
        {
            GameAudioManager.PlayItemUse(ItemType.Knife);
        }

        if (target == null)
        {
            Debug.Log("Knife missed.");
            return consumeKnifeWhenMissed;
        }

        PlayerStats targetStats = target.GetComponent<PlayerStats>();

        if (targetStats != null)
        {
            targetStats.TakeDamage(knifeDamage);
        }
        else
        {
            target.Die();
        }

        Debug.Log("Knife hit: " + target.name);

        return true;
    }

    // 칼 범위 안에서 가장 적합한 대상을 찾는다.
    private PlayerCombatTarget FindBestKnifeTarget()
    {
        Vector3 origin = playerCamera.transform.position + playerCamera.transform.forward * 0.05f;
        Vector3 attackCenter = origin + playerCamera.transform.forward * knifeAttackDistance;

        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            knifeAttackRadius,
            knifeTargetMask,
            QueryTriggerInteraction.Ignore
        );

        PlayerCombatTarget bestTarget = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            PlayerCombatTarget candidate = hits[i].GetComponentInParent<PlayerCombatTarget>();

            if (candidate == null)
            {
                continue;
            }

            if (candidate == selfTarget)
            {
                continue;
            }

            if (candidate.isDead)
            {
                continue;
            }

            if (!allowFriendlyFire && selfTarget != null && candidate.role == selfTarget.role)
            {
                continue;
            }

            if (!HasLineOfSight(hits[i], candidate, origin))
            {
                continue;
            }

            Vector3 targetCenter = hits[i].bounds.center;
            Vector3 toTarget = (targetCenter - origin).normalized;

            float forwardScore = Vector3.Dot(playerCamera.transform.forward, toTarget);
            float distance = Vector3.Distance(origin, targetCenter);
            float score = forwardScore * 10f - distance;

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    // 칼 공격 대상이 벽에 가려졌는지 확인한다.
    private bool HasLineOfSight(Collider targetCollider, PlayerCombatTarget target, Vector3 origin)
    {
        if (targetCollider == null || target == null)
        {
            return false;
        }

        Vector3 targetPoint = targetCollider.bounds.center;
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        direction /= distance;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, knifeObstacleMask, QueryTriggerInteraction.Ignore))
        {
            PlayerCombatTarget hitTarget = hit.collider.GetComponentInParent<PlayerCombatTarget>();

            if (hitTarget != null && hitTarget == target)
            {
                return true;
            }

            return false;
        }

        return true;
    }

    // 구급상자를 사용한다.
    private bool TryUseMedkitItem()
    {
        if (playerStats == null)
        {
            Debug.LogWarning("Medkit failed: PlayerStats가 없음.");
            return false;
        }

        if (playerStats.currentHealth <= 0f)
        {
            return false;
        }

        if (playerStats.currentHealth >= playerStats.maxHealth)
        {
            Debug.Log("Health is already full.");
            return false;
        }

        float beforeHealth = playerStats.currentHealth;

        playerStats.currentHealth += medkitHealAmount;
        playerStats.currentHealth = Mathf.Clamp(playerStats.currentHealth, 0f, playerStats.maxHealth);

        if (restoreStaminaWithMedkit)
        {
            playerStats.currentStamina += playerStats.currentHealth - beforeHealth;
        }

        playerStats.currentStamina = Mathf.Clamp(playerStats.currentStamina, 0f, playerStats.currentHealth);

        if (!PlayAudio(medkitUseAudio, medkitUseClip))
        {
            GameAudioManager.PlayItemUse(ItemType.Medkit);
        }

        Debug.Log("Medkit used. Health: " + beforeHealth + " -> " + playerStats.currentHealth);

        return true;
    }

    // 스캔 표면 정보에 따라 점 색상 그룹을 정한다.
    private ScanDotColorGroup ResolveDotColorGroup(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }

        ScanSurfaceInfo surfaceInfo = hit.collider.GetComponent<ScanSurfaceInfo>();

        if (surfaceInfo == null)
        {
            surfaceInfo = hit.collider.GetComponentInParent<ScanSurfaceInfo>();
        }

        if (surfaceInfo == null)
        {
            return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }

        switch (surfaceInfo.surfaceType)
        {
            case ScanSurfaceType.Floor:
                return ScanDotColorGroup.Floor;

            case ScanSurfaceType.Wall:
                return ScanDotColorGroup.Wall;

            case ScanSurfaceType.Metal:
                return ScanDotColorGroup.Metal;

            case ScanSurfaceType.Glass:
                return ScanDotColorGroup.Glass;

            case ScanSurfaceType.AccessCore:
                return ScanDotColorGroup.AccessCore;

            case ScanSurfaceType.SecurityTerminal:
                return ScanDotColorGroup.SecurityTerminal;

            case ScanSurfaceType.EmergencyExit:
                return ScanDotColorGroup.EmergencyExit;

            case ScanSurfaceType.PlayerBody:
                return ScanDotColorGroup.PlayerBody;

            case ScanSurfaceType.Creature:
                return ScanDotColorGroup.Creature;

            case ScanSurfaceType.WrongComputer:
                return ScanDotColorGroup.WrongComputer;

            case ScanSurfaceType.RestoredEscapeComputer:
                return ScanDotColorGroup.RestoredEscapeComputer;

            case ScanSurfaceType.Item:
                return ScanDotColorGroup.Item;

            default:
                return ResolveFallbackDotColorGroupByNormal(hit.normal);
        }
    }

    // ScanSurfaceInfo가 없는 표면은 노멀 기준으로 최소 분류한다.
    private ScanDotColorGroup ResolveFallbackDotColorGroupByNormal(Vector3 normal)
    {
        if (normal.y >= 0.55f)
        {
            return ScanDotColorGroup.Floor;
        }

        return ScanDotColorGroup.Wall;
    }

    // 오디오가 있으면 한 번 재생한다.
    private bool PlayAudio(AudioSource source, AudioClip fallbackClip)
    {
        AudioSource targetSource = source != null ? source : itemAudioSource;

        if (targetSource == null)
        {
            return false;
        }

        if (source != null && source.clip != null)
        {
            targetSource.PlayOneShot(source.clip);
            return true;
        }

        if (fallbackClip != null)
        {
            targetSource.PlayOneShot(fallbackClip);
            return true;
        }

        if (targetSource.clip != null)
        {
            targetSource.Play();
            return true;
        }

        return false;
    }

    // Scene View에서 칼 공격 범위를 확인하기 위한 기즈모이다.
    private void OnDrawGizmosSelected()
    {
        Camera viewCamera = playerCamera;

        if (viewCamera == null)
        {
            viewCamera = GetComponentInChildren<Camera>();
        }

        if (viewCamera == null)
        {
            return;
        }

        Vector3 origin = viewCamera.transform.position + viewCamera.transform.forward * 0.05f;
        Vector3 attackCenter = origin + viewCamera.transform.forward * knifeAttackDistance;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackCenter, knifeAttackRadius);
    }
}
