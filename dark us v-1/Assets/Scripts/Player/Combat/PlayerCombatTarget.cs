using UnityEngine;

// 이 스크립트는 플레이어의 역할과 사망 상태를 관리한다.
// 이 스크립트는 GameObject에 붙여서 사용한다.
public class PlayerCombatTarget : MonoBehaviour
{
    [Header("현재 역할")]
    // 현재 이 플레이어의 역할이다.
    // 기본값은 시민으로 시작한다.
    public PlayerRole role = PlayerRole.Citizen;

    [Header("멀티플레이")]
    public int photonActorNumber = -1;
    public bool isRemoteProxy = false;

    [Header("상태")]
    // 현재 이 플레이어가 죽었는지 저장한다.
    public bool isDead = false;

    [Header("죽었을 때 끌 스크립트")]
    // 죽었을 때 꺼줄 스크립트들을 Inspector에서 넣는다.
    public MonoBehaviour[] scriptsToDisableOnDeath;

    [Header("죽었을 때 끌 Collider")]
    // 죽었을 때 꺼줄 Collider들을 Inspector에서 넣는다.
    public Collider[] collidersToDisable;

    [Header("죽었을 때 숨길 오브젝트")]
    // 죽었을 때 숨길 비주얼 오브젝트가 있으면 넣는다.
    public GameObject bodyVisualRoot;

    // CharacterController를 저장하는 변수이다.
    private CharacterController controller;

    // 시작 전에 CharacterController를 가져온다.
    private void Awake()
    {
        // 같은 오브젝트에 CharacterController가 붙어 있으면 가져온다.
        controller = GetComponent<CharacterController>();
    }

    // 이 함수를 호출하면 현재 플레이어 역할을 바꾼다.
    public void SetRole(PlayerRole newRole)
    {
        // 새 역할을 저장한다.
        role = newRole;

        // 테스트하기 쉽게 콘솔에 현재 역할을 출력한다.
        Debug.Log(name + " role = " + role);
    }

    // 이 함수를 호출하면 플레이어를 죽은 상태로 만든다.
    public void Die()
    {
        Die(true);
    }

    public void ApplyDeathFromNetwork()
    {
        Die(false);
    }

    private void Die(bool broadcast)
    {
        // 이미 죽어 있으면 다시 처리하지 않는다.
        if (isDead)
        {
            return;
        }

        // 죽은 상태로 바꾼다.
        isDead = true;

        // 죽었을 때 꺼야 하는 스크립트들을 전부 끈다.
        for (int i = 0; i < scriptsToDisableOnDeath.Length; i++)
        {
            // 비어 있지 않은 스크립트만 끈다.
            if (scriptsToDisableOnDeath[i] != null)
            {
                scriptsToDisableOnDeath[i].enabled = false;
            }
        }

        // CharacterController가 있으면 꺼서 움직이지 못하게 만든다.
        if (controller != null)
        {
            controller.enabled = false;
        }

        // 추가 Collider들이 있으면 꺼서 충돌 판정도 비활성화한다.
        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            // 비어 있지 않은 Collider만 끈다.
            if (collidersToDisable[i] != null)
            {
                collidersToDisable[i].enabled = false;
            }
        }

        // 숨길 비주얼 오브젝트가 있으면 숨긴다.
        if (bodyVisualRoot != null)
        {
            bodyVisualRoot.SetActive(false);
        }

        // 콘솔에 사망 로그를 남긴다.
        Debug.Log(name + " died.");

        if (broadcast)
        {
            GameLoopManager.EnsureExists().ReportPlayerDeath(GetActorNumber());
        }
    }

    public int GetActorNumber()
    {
        if (photonActorNumber > 0)
        {
            return photonActorNumber;
        }

        Photon.Pun.PhotonView photonView = GetComponent<Photon.Pun.PhotonView>();
        if (photonView != null && photonView.Owner != null)
        {
            return photonView.Owner.ActorNumber;
        }

        if (!isRemoteProxy && Photon.Pun.PhotonNetwork.LocalPlayer != null)
        {
            return Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber;
        }

        return -1;
    }
}
