using UnityEngine;

// 현재 선택된 인벤토리 아이템을 사용하는 스크립트이다.
// 기본 사용 키는 F이다.
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
    // 아이템 사용 키이다.
    public KeyCode useItemKey = KeyCode.F;

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

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Update()
    {
        if (Input.GetKeyDown(useItemKey))
        {
            UseSelectedItem();
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
            dotRenderer = FindObjectOfType<InstancedScanDotRenderer>();
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

    // 현재 선택된 아이템을 사용한다.
    public void UseSelectedItem()
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

        PlayAudio(cameraUseAudio);

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

        PlayAudio(knifeUseAudio);

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

        PlayAudio(medkitUseAudio);

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
    private void PlayAudio(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        if (source.clip != null)
        {
            source.PlayOneShot(source.clip);
            return;
        }

        source.Play();
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