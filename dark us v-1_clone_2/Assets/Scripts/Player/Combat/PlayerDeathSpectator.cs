using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class PlayerDeathSpectator : MonoBehaviour
{
    [Header("Camera")]
    public Camera spectatorCamera;
    public Vector3 followOffset = new Vector3(0f, 2.35f, -4.1f);
    public float lookHeight = 1.25f;
    public float followSharpness = 12f;
    public float rotationSharpness = 14f;

    [Header("Targeting")]
    public KeyCode nextTargetKey = KeyCode.Tab;
    public float targetRefreshInterval = 0.35f;

    private readonly List<PlayerCombatTarget> availableTargets = new List<PlayerCombatTarget>();
    private PlayerCombatTarget ownerTarget;
    private PlayerCombatTarget currentTarget;
    private PlayerVisibleAvatar visibleAvatarOverride;
    private bool previousHideRenderers;
    private bool previousHideWhenLocalScannerOwner;
    private float nextTargetRefreshTime;
    private bool spectating;

    public void EnterSpectatorMode(PlayerCombatTarget deadOwner)
    {
        ownerTarget = deadOwner != null ? deadOwner : GetComponent<PlayerCombatTarget>();

        if (ownerTarget == null || !ownerTarget.isDead || !IsLocalOwner())
        {
            return;
        }

        if (spectatorCamera == null)
        {
            spectatorCamera = GetComponentInChildren<Camera>(true);
        }

        if (spectatorCamera == null)
        {
            spectatorCamera = Camera.main;
        }

        if (spectatorCamera == null)
        {
            return;
        }

        spectating = true;
        spectatorCamera.gameObject.SetActive(true);
        RefreshTargets(true);
        SelectTarget(0);
    }

    private void OnDisable()
    {
        ClearVisibleAvatarOverride();
    }

    private void LateUpdate()
    {
        if (!spectating)
        {
            EnterSpectatorMode(ownerTarget);
        }

        if (!spectating || spectatorCamera == null)
        {
            return;
        }

        if (Input.GetKeyDown(nextTargetKey) || Input.GetMouseButtonDown(0))
        {
            CycleTarget();
        }

        if (Time.unscaledTime >= nextTargetRefreshTime || !IsValidTarget(currentTarget))
        {
            RefreshTargets(false);

            if (!IsValidTarget(currentTarget))
            {
                SelectTarget(0);
            }
        }

        FollowCurrentTarget();
    }

    private bool IsLocalOwner()
    {
        if (ownerTarget == null || ownerTarget.isRemoteProxy)
        {
            return false;
        }

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
        {
            return true;
        }

        return ownerTarget.GetActorNumber() == PhotonNetwork.LocalPlayer.ActorNumber;
    }

    private void RefreshTargets(bool force)
    {
        if (!force && Time.unscaledTime < nextTargetRefreshTime)
        {
            return;
        }

        nextTargetRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, targetRefreshInterval);
        availableTargets.Clear();

        PlayerCombatTarget[] targets = Object.FindObjectsByType<PlayerCombatTarget>(FindObjectsInactive.Exclude);
        for (int i = 0; i < targets.Length; i++)
        {
            PlayerCombatTarget target = targets[i];
            if (IsValidTarget(target))
            {
                availableTargets.Add(target);
            }
        }

        availableTargets.Sort(CompareTargets);
    }

    private int CompareTargets(PlayerCombatTarget left, PlayerCombatTarget right)
    {
        int leftActor = left != null ? left.GetActorNumber() : int.MaxValue;
        int rightActor = right != null ? right.GetActorNumber() : int.MaxValue;
        return leftActor.CompareTo(rightActor);
    }

    private bool IsValidTarget(PlayerCombatTarget target)
    {
        if (target == null || target == ownerTarget || target.isDead)
        {
            return false;
        }

        int targetActor = target.GetActorNumber();
        if (targetActor <= 0)
        {
            return false;
        }

        if (ownerTarget != null && targetActor == ownerTarget.GetActorNumber())
        {
            return false;
        }

        return true;
    }

    private void CycleTarget()
    {
        RefreshTargets(true);

        if (availableTargets.Count == 0)
        {
            SelectTarget(-1);
            return;
        }

        int currentIndex = availableTargets.IndexOf(currentTarget);
        int nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % availableTargets.Count;
        SelectTarget(nextIndex);
    }

    private void SelectTarget(int index)
    {
        ClearVisibleAvatarOverride();

        currentTarget = index >= 0 && index < availableTargets.Count ? availableTargets[index] : null;

        if (currentTarget != null)
        {
            ApplyVisibleAvatarOverride(currentTarget.GetComponent<PlayerVisibleAvatar>());
        }
    }

    private void FollowCurrentTarget()
    {
        Transform targetTransform = IsValidTarget(currentTarget) ? currentTarget.transform : GetFallbackTransform();
        if (targetTransform == null)
        {
            return;
        }

        Vector3 forward = targetTransform.forward;
        if (forward.sqrMagnitude <= 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 desiredPosition =
            targetTransform.position +
            targetTransform.right * followOffset.x +
            Vector3.up * followOffset.y +
            forward.normalized * followOffset.z;

        Transform cameraTransform = spectatorCamera.transform;
        float followT = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, followT);

        Vector3 lookPoint = targetTransform.position + Vector3.up * lookHeight;
        Vector3 lookDirection = lookPoint - cameraTransform.position;
        if (lookDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);
        cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, desiredRotation, rotationT);
    }

    private Transform GetFallbackTransform()
    {
        return ownerTarget != null ? ownerTarget.transform : transform;
    }

    private void ApplyVisibleAvatarOverride(PlayerVisibleAvatar avatar)
    {
        if (avatar == null)
        {
            return;
        }

        visibleAvatarOverride = avatar;
        previousHideRenderers = avatar.hideRenderers;
        previousHideWhenLocalScannerOwner = avatar.hideWhenLocalScannerOwner;
        avatar.hideRenderers = false;
        avatar.hideWhenLocalScannerOwner = false;
    }

    private void ClearVisibleAvatarOverride()
    {
        if (visibleAvatarOverride == null)
        {
            return;
        }

        visibleAvatarOverride.hideRenderers = previousHideRenderers;
        visibleAvatarOverride.hideWhenLocalScannerOwner = previousHideWhenLocalScannerOwner;
        visibleAvatarOverride = null;
    }
}
