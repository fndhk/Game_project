using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 포톤 상속을 버리고 일반 MonoBehaviour로 돌아와 꼬임을 원천 차단합니다.
public class PlayerRoleAndSkillManager : MonoBehaviour
{
    [Header("Player Role (0: Civilian, 1: Imposter)")]
    public int currentRole = -1;

    [Header("All Skill Database")]
    [SerializeField] private List<SkillData> allSkills = new List<SkillData>();

    public SkillData currentSkill;
    private float currentCooldown;
    private int currentCharges;

    [Header("Current Applied Skill Name")]
    public string currentSkillName = "None";

    // 내 캐릭터인지 판단할 변수
    private bool isMyLocalPlayer = false;

    void Start()
    {
        Debug.Log($"--- [스킬 추적 1] {gameObject.name} 스폰 완료! Start 진입 ---");

        // ★ [핵심] 캐릭터에 붙어있는 기존 컴포넌트(예: PlayerCombatTarget)를 통해 진짜 내가 조종하는 로컬 유저인지 검사합니다.
        // 만약 기존 스크립트에 본인이 맞는지 판별하는 변수가 있다면 그것을 활용해도 좋습니다.
        // 우선은 RoleAssignmentManager의 정석적인 로직과 타이밍을 맞추기 위해 코루틴을 돌립니다.

        StartCoroutine(CheckAndApplyRoleRoutine());
    }

    void Update()
    {
        // 내 캐릭터가 아니라면 키보드 입력을 철저히 무시합니다.
        if (!isMyLocalPlayer) return;

        HandleSkillInput();
        UpdateCooldown();
    }

    IEnumerator CheckAndApplyRoleRoutine()
    {
        Debug.Log("--- [스킬 추적 2] 직업 할당 대기 중... ---");

        // RoleAssignmentManager가 방에서 직업 세팅을 완료할 때까지 대기합니다.
        int maxWaitCount = 20;
        int currentWait = 0;

        while (RoleAssignmentManager.IsWaitingForPhotonRole())
        {
            currentWait++;
            if (currentWait >= maxWaitCount)
            {
                Debug.LogWarning("--- [스킬 추적 3] 🚨 대기 시간 초과! 강제로 내 직업 조회를 시도합니다. ---");
                break;
            }
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("--- [스킬 추적 4] 대기 종료! 내 직업 및 로컬 여부 판별 시작 ---");

        // 올려주신 RoleAssignmentManager의 정석 함수를 호출하여 현재 내 화면의 직업을 가져옵니다.
        // 이 함수는 알아서 PhotonNetwork.LocalPlayer를 체크하므로 안전합니다.
        PlayerRole myLocalRole = RoleAssignmentManager.GetLocalPhotonRole();

        // 이 스크립트가 붙은 오브젝트가 진짜 '내가 조종하는 플레이어'인지 검증합니다.
        // 기존 캐릭터 시스템(PlayerCombatTarget 등)의 ActorNumber와 내 포톤 ActorNumber가 일치하는지 비교하는 것이 가장 정확합니다.
        var combatTarget = GetComponentInChildren<PlayerCombatTarget>();
        if (combatTarget == null) combatTarget = GetComponentInParent<PlayerCombatTarget>();

        if (combatTarget != null && PhotonPhotonNetworkInRoomCheck())
        {
            // 내 포톤 고유 번호와 이 캐릭터의 번호가 같다면 진짜 내 캐릭터입니다!
            int myActorNumber = Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber;
            if (combatTarget.GetActorNumber() == myActorNumber)
            {
                isMyLocalPlayer = true;
            }
        }
        else
        {
            // 만약 오프라인 테스트 중이라 컴포넌트 조회가 안 된다면, 일단 내 캐릭터로 인정하고 테스트를 허용합니다.
            isMyLocalPlayer = true;
        }

        // 내 캐릭터가 아닌 복제본(남의 캐릭터)이라면 여기서 장착 로직을 중단합니다.
        if (!isMyLocalPlayer)
        {
            Debug.Log($"--- [스킬 추적 탈출] {gameObject.name}은 다른 플레이어의 캐릭터이므로 스킬을 적용하지 않습니다. ---");
            yield break;
        }

        // 직업 변환 (Killer면 1, 아니면 0)
        currentRole = (myLocalRole == PlayerRole.Killer) ? 1 : 0;
        Debug.Log($"--- [스킬 추적 5] 🎯 내 캐릭터 판정 완료! 할당된 직업: {(currentRole == 0 ? "시민" : "임포스터")} ---");

        ApplySelectedSkill();
    }

    private bool PhotonPhotonNetworkInRoomCheck()
    {
        return Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.LocalPlayer != null;
    }

    public void ApplySelectedSkill()
    {
        if (SkillManager.Instance == null)
        {
            Debug.LogWarning("SkillManager가 없습니다! 기본 스킬 강제 주입!");
            currentSkillName = currentRole == 0 ? "탐지" : "정전";
        }
        else
        {
            currentSkillName = currentRole == 0 ? SkillManager.Instance.savedCivilianSkill : SkillManager.Instance.savedImposterSkill;
        }

        currentSkill = allSkills.Find(skill => skill != null && skill.skillName == currentSkillName);

        if (currentSkill != null)
        {
            currentCharges = currentSkill.maxCharges == 0 ? 9999 : currentSkill.maxCharges;
            currentCooldown = 0f;
            Debug.Log($"--- [스킬 추적 6] 🎯 스킬 '{currentSkillName}' 장착 성공! Z키 활성화 ---");
        }
        else
        {
            Debug.LogError($"--- [스킬 추적 6-실패] '{currentSkillName}' 스킬을 데이터베이스에서 찾을 수 없습니다. ---");
        }
    }

    void HandleSkillInput()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (currentSkill == null) return;

            if (currentCooldown <= 0 && currentCharges > 0)
            {
                UseSkill();
            }
            else if (currentCooldown > 0)
            {
                Debug.Log($"스킬 쿨타임 중입니다. 남은 시간: {currentCooldown:F1}초");
            }
        }
    }

    void UseSkill()
    {
        switch (currentSkill.skillID)
        {
            case 0: Debug.Log("탐지 스킬 사용! 주변 3초간 스캔 로직 발동."); break;
            case 1: Debug.Log("고철꾼 스킬 사용! 카메라 아이템 획득 로직 발동."); break;
            case 2: Debug.Log("아드레날린 스킬 사용! 이동속도 증가(*1.5)."); break;
            case 3: Debug.Log("비상탈출 스킬 사용! 랜덤 좌표 텔레포트."); break;
            case 10: Debug.Log("정전 스킬 사용! 맵 전체 조명 차단 로직 발동."); break;
            case 11: Debug.Log("혼란 스킬 사용! 시민들의 방향키 반전 로직 발동."); break;
            case 12: Debug.Log("혼란2 스킬 사용! (추가 기획에 맞게 로직 발동)."); break;
            case 13: Debug.Log("해킹 스킬 사용! 특정 문 잠금 또는 CCTV 무력화 로직 발동."); break;
        }
        currentCooldown = currentSkill.cooldown;
        if (currentSkill.maxCharges > 0) currentCharges--;
    }

    void UpdateCooldown()
    {
        if (currentCooldown > 0) currentCooldown -= Time.deltaTime;
    }
}