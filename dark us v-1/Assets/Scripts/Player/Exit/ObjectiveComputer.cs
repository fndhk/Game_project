using UnityEngine;
using UnityEngine.Serialization;

// 맵 안의 모든 컴퓨터에 붙이는 복구 오브젝트이다.
// 모든 컴퓨터는 복구 가능하지만, 랜덤으로 선택된 컴퓨터만 탈출 진행도에 반영된다.
public class ObjectiveComputer : MonoBehaviour, IPlayerHoldInteractable
{
    [Header("Objective State")]
    // 이번 판에 실제 탈출 시스템 컴퓨터로 선택되었는지 저장한다.
    [SerializeField] private bool isSelectedObjective = false;

    // 복구 완료 상태이다.
    [SerializeField] private bool isRestored = false;

    [Header("Restore")]
    // 복구에 필요한 시간이다.
    public float restoreDuration = 6f;

    // 복구가 중간에 끊기면 진행도를 0으로 되돌릴지 정한다.
    public bool resetProgressWhenCancelled = true;

    // 복구 중 플레이어가 이 거리 이상 움직이면 취소한다. 0 이하면 검사하지 않는다.
    public float maxInteractorMoveDistance = 0.45f;

    // 시민만 복구할 수 있게 제한할지 정한다.
    // 싱글 테스트가 편하도록 기본값은 꺼둔다.
    public bool requireCitizenRole = false;

    [Header("Visual Roots")]
    // 복구 전 일반 상태에서 켤 오브젝트이다.
    public GameObject inactiveVisualRoot;

    // 목표 컴퓨터임을 미리 드러내고 싶을 때만 사용하는 오브젝트이다.
    public GameObject activeVisualRoot;

    // 복구 완료 후 켤 오브젝트이다.
    public GameObject restoredVisualRoot;

    // 켜면 진짜 목표 컴퓨터가 복구 전부터 activeVisualRoot로 표시된다.
    // 기본값은 꺼서 진짜/가짜 컴퓨터가 복구 전에는 구분되지 않게 한다.
    public bool revealSelectedVisualBeforeRestore = false;

    [Header("Scan Surface")]
    // 스캔 점 색상 구분용 컴포넌트들이다. 비워두면 자식에서 자동으로 찾는다.
    public ScanSurfaceInfo[] scanSurfaceInfos;

    // 복구 전 모든 컴퓨터의 스캔 타입이다.
    [FormerlySerializedAs("inactiveScanType")]
    public ScanSurfaceType beforeRestoreScanType = ScanSurfaceType.SecurityTerminal;

    // 가짜 컴퓨터를 복구 완료했을 때의 스캔 타입이다.
    public ScanSurfaceType fakeRestoredScanType = ScanSurfaceType.WrongComputer;

    // 진짜 탈출 시스템 컴퓨터를 복구 완료했을 때의 스캔 타입이다.
    public ScanSurfaceType escapeRestoredScanType = ScanSurfaceType.RestoredEscapeComputer;

    [Header("Existing Scan Dot Recolor")]
    // 복구 완료 순간 이미 찍혀 있는 컴퓨터 점 색도 즉시 바꿀지 정한다.
    public bool recolorExistingDotsOnRestore = true;

    // 이 반경 안에 있는 기존 컴퓨터 점만 색을 바꾼다.
    public float existingDotRecolorRadius = 1.25f;

    // 색을 바꿀 중심점이다. 비워두면 이 오브젝트 위치를 사용한다.
    public Transform existingDotRecolorCenter;

    // 점 렌더러 참조이다. 비워두면 씬에서 자동으로 찾는다.
    public InstancedScanDotRenderer[] scanDotRenderers;

    [Header("Audio")]
    // 복구 시작 시 재생할 소리이다.
    public AudioSource startAudioSource;

    // 복구 진행 중 재생할 소리이다.
    // 이름은 기존 Inspector 호환을 위해 loopAudioSource로 유지하지만, 아래 옵션으로 루프 여부를 정한다.
    public AudioSource loopAudioSource;

    // 진행 중 소리를 실제로 반복 재생할지 정한다.
    // 6초짜리 상승음처럼 한 번만 재생할 사운드는 꺼둔다.
    public bool loopProgressAudio = false;

    // 복구를 다시 시작할 때 진행 중 소리를 처음부터 다시 재생할지 정한다.
    public bool restartProgressAudioOnBegin = true;

    // 진짜 탈출 시스템 컴퓨터 복구 완료 시 재생할 소리이다.
    public AudioSource completeAudioSource;

    // 가짜 컴퓨터 복구 완료 시 재생할 소리이다.
    // 비워두면 completeAudioSource를 대신 재생한다.
    public AudioSource fakeCompleteAudioSource;

    // 현재 복구 진행도이다. 0~1 값이다.
    [SerializeField] private float restoreProgress = 0f;

    // 현재 복구 중인 플레이어이다.
    private PlayerObjectiveInteractor currentInteractor;

    // 복구 시작 위치이다.
    private Vector3 holdStartPosition;

    // 외부에서 복구 완료 상태를 읽기 위한 프로퍼티이다.
    public bool IsRestored => isRestored;

    // 외부에서 목표 선택 상태를 읽기 위한 프로퍼티이다.
    public bool IsSelectedObjective => isSelectedObjective;

    // 이 컴퓨터가 복구 완료된 가짜 컴퓨터인지 확인하는 프로퍼티이다.
    public bool IsFakeRestoredComputer => isRestored && !isSelectedObjective;

    // 이 컴퓨터가 복구 완료된 진짜 탈출 컴퓨터인지 확인하는 프로퍼티이다.
    public bool IsEscapeRestoredComputer => isRestored && isSelectedObjective;

    // 시작 전에 참조를 자동으로 채운다.
    private void Awake()
    {
        AutoFindReferences();
        RefreshVisualState();
    }

    // 비활성화될 때 진행 중인 복구를 정리한다.
    private void OnDisable()
    {
        StopLoopAudio();
        currentInteractor = null;
    }

    // 컴퓨터 목표 선택 상태를 설정한다.
    public void SetSelectedObjective(bool selected, bool resetState)
    {
        isSelectedObjective = selected;

        if (resetState)
        {
            isRestored = false;
            restoreProgress = 0f;
            currentInteractor = null;
        }

        RefreshVisualState();
    }

    // 현재 진행도를 0~1로 반환한다.
    public float GetRestoreNormalized()
    {
        return Mathf.Clamp01(restoreProgress);
    }

    // 상호작용 문구를 반환한다.
    public string GetPrompt(PlayerObjectiveInteractor interactor)
    {
        if (isRestored)
        {
            return isSelectedObjective ? T("Escape Computer Restored") : T("Wrong Computer Restored");
        }

        int percent = Mathf.RoundToInt(GetRestoreNormalized() * 100f);

        if (currentInteractor == interactor)
        {
            return T("Restoring Computer") + " " + percent + "%";
        }

        if (restoreProgress > 0f)
        {
            return "[Hold E] " + T("Restore Computer") + " " + percent + "%";
        }

        return "[Hold E] " + T("Restore Computer");
    }

    // 지금 상호작용 가능한지 반환한다.
    public bool CanInteract(PlayerObjectiveInteractor interactor)
    {
        // 이제 목표 컴퓨터가 아니어도 복구 시도는 가능하다.
        // 단, 이미 복구한 컴퓨터는 다시 복구하지 못한다.
        if (isRestored)
        {
            return false;
        }

        if (currentInteractor != null && currentInteractor != interactor)
        {
            return false;
        }

        if (requireCitizenRole && interactor != null)
        {
            PlayerCombatTarget target = interactor.GetComponentInParent<PlayerCombatTarget>();

            if (target != null && target.role != PlayerRole.Citizen)
            {
                return false;
            }
        }

        return true;
    }

    // 일반 클릭 상호작용은 사용하지 않는다.
    public void Interact(PlayerObjectiveInteractor interactor)
    {
        // 이 오브젝트는 길게 누르기 상호작용만 사용한다.
    }

    // 길게 누르기 상호작용을 시작한다.
    public bool BeginHold(PlayerObjectiveInteractor interactor)
    {
        if (!CanInteract(interactor))
        {
            return false;
        }

        currentInteractor = interactor;

        if (interactor != null)
        {
            holdStartPosition = interactor.transform.position;
        }

        PlayStartAudio();
        PlayLoopAudio();
        return true;
    }

    // 길게 누르는 동안 복구 진행도를 갱신한다.
    public bool UpdateHold(PlayerObjectiveInteractor interactor, float deltaTime)
    {
        if (currentInteractor != interactor)
        {
            return false;
        }

        if (!CanInteract(interactor))
        {
            CancelHold(interactor);
            return false;
        }

        if (ShouldCancelByMovement(interactor))
        {
            CancelHold(interactor);
            return false;
        }

        float safeDuration = Mathf.Max(0.01f, restoreDuration);
        restoreProgress += deltaTime / safeDuration;
        restoreProgress = Mathf.Clamp01(restoreProgress);

        if (restoreProgress >= 1f)
        {
            CompleteRestore();
            return false;
        }

        return true;
    }

    // 키를 떼거나 시야에서 벗어나면 복구를 취소한다.
    public void CancelHold(PlayerObjectiveInteractor interactor)
    {
        if (currentInteractor != interactor)
        {
            return;
        }

        currentInteractor = null;
        StopLoopAudio();

        if (!isRestored && resetProgressWhenCancelled)
        {
            restoreProgress = 0f;
        }
    }

    // 플레이어가 복구 중 움직였는지 확인한다.
    private bool ShouldCancelByMovement(PlayerObjectiveInteractor interactor)
    {
        if (maxInteractorMoveDistance <= 0f || interactor == null)
        {
            return false;
        }

        float distance = Vector3.Distance(holdStartPosition, interactor.transform.position);
        return distance > maxInteractorMoveDistance;
    }

    // 복구를 완료한다.
    private void CompleteRestore()
    {
        if (isRestored)
        {
            return;
        }

        isRestored = true;
        restoreProgress = 1f;
        currentInteractor = null;

        StopLoopAudio();
        PlayResultAudio();
        RefreshVisualState();
        RecolorExistingScanDots();

        // 진짜 탈출 시스템 컴퓨터만 목표 진행도에 반영한다.
        if (LabObjectiveManager.Instance != null)
        {
            if (isSelectedObjective)
            {
                LabObjectiveManager.Instance.CompleteComputer(this);
            }
            else
            {
                LabObjectiveManager.Instance.RefreshHud();
            }
        }
    }

    // 복구 완료 순간 이미 찍혀 있는 컴퓨터 점 색을 바로 바꾼다.
    private void RecolorExistingScanDots()
    {
        // 옵션이 꺼져 있으면 처리하지 않는다.
        if (!recolorExistingDotsOnRestore)
        {
            return;
        }

        // 반경이 0 이하이면 처리하지 않는다.
        if (existingDotRecolorRadius <= 0f)
        {
            return;
        }

        // 점 렌더러가 비어 있으면 씬에서 자동으로 찾는다.
        if (scanDotRenderers == null || scanDotRenderers.Length == 0)
        {
            scanDotRenderers = FindObjectsOfType<InstancedScanDotRenderer>();
        }

        // 그래도 없으면 처리하지 않는다.
        if (scanDotRenderers == null || scanDotRenderers.Length == 0)
        {
            return;
        }

        // 색을 바꿀 중심점을 정한다.
        Vector3 center = existingDotRecolorCenter != null ? existingDotRecolorCenter.position : transform.position;

        // 진짜/가짜 여부에 따라 바꿀 최종 색상 그룹을 정한다.
        ScanSurfaceType resultSurfaceType = isSelectedObjective ? escapeRestoredScanType : fakeRestoredScanType;
        ScanDotColorGroup resultColorGroup = SurfaceTypeToDotColorGroup(resultSurfaceType);

        // 복구 전 컴퓨터 색상 그룹만 바꾼다.
        // 이렇게 해야 컴퓨터 주변 바닥/벽 점까지 같이 빨간색/파란색으로 바뀌는 일을 줄일 수 있다.
        ScanDotColorGroup beforeColorGroup = SurfaceTypeToDotColorGroup(beforeRestoreScanType);

        for (int i = 0; i < scanDotRenderers.Length; i++)
        {
            if (scanDotRenderers[i] == null)
            {
                continue;
            }

            scanDotRenderers[i].RecolorDotsInSphere(
                center,
                existingDotRecolorRadius,
                resultColorGroup,
                beforeColorGroup
            );
        }
    }

    // ScanSurfaceType 값을 ScanDotColorGroup 값으로 바꾼다.
    // 두 enum은 같은 숫자 체계를 쓰도록 맞춰두었기 때문에 캐스팅으로 처리한다.
    private ScanDotColorGroup SurfaceTypeToDotColorGroup(ScanSurfaceType surfaceType)
    {
        return (ScanDotColorGroup)((int)surfaceType);
    }

    // 비어 있는 참조를 자동으로 찾는다.
    private void AutoFindReferences()
    {
        if (scanSurfaceInfos == null || scanSurfaceInfos.Length == 0)
        {
            scanSurfaceInfos = GetComponentsInChildren<ScanSurfaceInfo>(true);
        }
    }

    // 현재 상태에 맞춰 시각 상태를 갱신한다.
    private void RefreshVisualState()
    {
        bool showSelectedVisual = revealSelectedVisualBeforeRestore && isSelectedObjective && !isRestored;

        SetVisualRootActive(inactiveVisualRoot, !isRestored && !showSelectedVisual);
        SetVisualRootActive(activeVisualRoot, showSelectedVisual);
        SetVisualRootActive(restoredVisualRoot, isRestored);
        ApplyScanSurfaceType();
    }

    // 특정 시각 루트를 켜거나 끈다.
    private void SetVisualRootActive(GameObject targetRoot, bool active)
    {
        if (targetRoot != null && targetRoot.activeSelf != active)
        {
            targetRoot.SetActive(active);
        }
    }

    // 스캔 점 색상 구분 타입을 현재 상태에 맞게 바꾼다.
    private void ApplyScanSurfaceType()
    {
        if (scanSurfaceInfos == null)
        {
            return;
        }

        ScanSurfaceType targetType = beforeRestoreScanType;

        if (isRestored)
        {
            targetType = isSelectedObjective ? escapeRestoredScanType : fakeRestoredScanType;
        }

        for (int i = 0; i < scanSurfaceInfos.Length; i++)
        {
            if (scanSurfaceInfos[i] != null)
            {
                scanSurfaceInfos[i].surfaceType = targetType;
            }
        }
    }

    // 시작 소리를 재생한다.
    private void PlayStartAudio()
    {
        if (startAudioSource != null)
        {
            startAudioSource.Play();
        }
    }

    // 진행 중 소리를 재생한다.
    private void PlayLoopAudio()
    {
        if (loopAudioSource == null)
        {
            return;
        }

        // 기존 이름은 Loop Audio Source지만, 실제 루프 여부는 옵션으로 정한다.
        loopAudioSource.loop = loopProgressAudio;

        if (restartProgressAudioOnBegin)
        {
            loopAudioSource.Stop();
            loopAudioSource.Play();
            return;
        }

        if (!loopAudioSource.isPlaying)
        {
            loopAudioSource.Play();
        }
    }

    // 진행 중 소리를 멈춘다.
    private void StopLoopAudio()
    {
        if (loopAudioSource != null && loopAudioSource.isPlaying)
        {
            loopAudioSource.Stop();
        }
    }

    // 복구 결과에 맞는 완료 소리를 재생한다.
    private void PlayResultAudio()
    {
        AudioSource targetAudioSource = isSelectedObjective ? completeAudioSource : fakeCompleteAudioSource;

        // 가짜 완료음이 비어 있으면 기존 완료음을 대신 사용한다.
        if (targetAudioSource == null)
        {
            targetAudioSource = completeAudioSource;
        }

        if (targetAudioSource != null)
        {
            targetAudioSource.Play();
        }
    }

    private string T(string key)
    {
        return InGameLocalization.Text(key);
    }
}
