using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerRoleAndSkillManager : MonoBehaviour
{
    [Header("Player Role (0: Civilian, 1: Imposter)")]
    public int currentRole = -1;

    [Header("All Skill Database")]
    // 중요: 기획한 시민/임포스터 스킬 데이터들(ScriptableObject)을 에디터에서 여기에 다 넣어두세요!
    [SerializeField] private List<SkillData> allSkills = new List<SkillData>();

    // 현재 유저의 스킬 정보 (게임 내에서 사용)
    public SkillData currentSkill;
    private float currentCooldown;
    private int currentCharges;

    [Header("Current Applied Skill Name")]
    public string currentSkillName = "None";

    void Start()
    {
        // 1. 서버로부터 직업 할당받음 (이 예시에서는 Start에서 임의 할당)
        currentRole = 0; // 예: 시민 할당

        // 2. 직업에 맞는 선택된 스킬 데이터 가져오기
        ApplySelectedSkill();
    }

    void Update()
    {
        // 3. 스킬 사용 및 쿨타임 관리
        HandleSkillInput();
        UpdateCooldown();
    }

    // 직업 할당 후 호출
    public void ApplySelectedSkill()
    {
        if (SkillManager.Instance == null)
        {
            Debug.LogError("SkillManager 인스턴스를 찾을 수 없습니다! 스킬을 적용할 수 없습니다.");
            return;
        }

        // 1. 갱신된 SkillManager의 구조(string 변수)에 맞추어 스킬 이름을 먼저 가져옵니다.
        if (currentRole == 0) // 시민팀
        {
            currentSkillName = SkillManager.Instance.savedCivilianSkill;
        }
        else if (currentRole == 1) // 임포스터팀
        {
            currentSkillName = SkillManager.Instance.savedImposterSkill;
        }

        // 2. [핵심 추가] 가져온 스킬 이름과 일치하는 ScriptableObject 데이터를 리스트에서 찾아서 주입합니다.
        currentSkill = allSkills.Find(skill => skill != null && skill.skillName == currentSkillName);

        if (currentSkill != null)
        {
            // 데이터가 성공적으로 찾아졌으므로, 쿨타임과 사용 횟수를 초기화합니다.
            currentCharges = currentSkill.maxCharges == 0 ? 9999 : currentSkill.maxCharges;
            currentCooldown = 0f;

            Debug.Log($"[플레이어] 직업({currentRole})에 맞는 스킬 '{currentSkillName}'의 수치 데이터가 정상 장착되었습니다!");
        }
        else
        {
            Debug.LogWarning($"[플레이어] 선택한 스킬명 '{currentSkillName}'에 해당하는 SkillData를 데이터베이스(All Skills)에서 찾을 수 없습니다.");
        }
    }

    // 스킬 입력 처리 (예: F키 사용)
    void HandleSkillInput()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            // 이제 currentSkill이 정상 주입되므로 정상 작동합니다!
            if (currentSkill != null && currentCooldown <= 0 && currentCharges > 0)
            {
                UseSkill();
            }
        }
    }

    // 실제 스킬 사용 로직 (각 스킬 ID에 따라 구현)
    void UseSkill()
    {
        // 각 스킬 효과 구현 (예: 탐지, 고철꾼 등)
        switch (currentSkill.skillID)
        {
            case 0: // 탐지
                Debug.Log("탐지 스킬 사용! 주변 3초간 스캔 로직 발동.");
                break;
            case 1: // 고철꾼
                Debug.Log("고철꾼 스킬 사용! 카메라 아이템 획득 로직 발동.");
                break;
            case 2: // 아드레날린
                Debug.Log("아드레날린 스킬 사용! 이동속도 증가(*1.5).");
                break;
            case 3: // 비상탈출
                Debug.Log("비상탈출 스킬 사용! 랜덤 좌표 텔레포트.");
                break;

                // ... 임포스터 스킬 ID들(정전, 혼란 등)도 이어서 기재하시면 됩니다.
        }

        // 쿨타임 및 사용 횟수 차감
        currentCooldown = currentSkill.cooldown;
        if (currentSkill.maxCharges > 0)
        {
            currentCharges--;
        }
    }

    // 쿨타임 업데이트
    void UpdateCooldown()
    {
        if (currentCooldown > 0)
        {
            currentCooldown -= Time.deltaTime;
        }
    }
}