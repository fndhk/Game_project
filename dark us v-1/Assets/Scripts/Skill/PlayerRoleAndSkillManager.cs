using UnityEngine;
using System.Collections;

public class PlayerRoleAndSkillManager : MonoBehaviour
{
    // 직업 정의 (예: 시민: 0, 임포스터: 1)
    public int currentRole = -1;

    // 현재 유저의 스킬 정보 (게임 내에서 사용)
    public SkillData currentSkill;
    private float currentCooldown;
    private int currentCharges;

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
        if (currentRole == 0) // 시민팀
        {
            currentSkill = SkillManager.Instance.selectedCivilianSkill;
        }
        else if (currentRole == 1) // 임포스터팀
        {
            currentSkill = SkillManager.Instance.selectedImposterSkill;
        }

        // 스킬 초기화
        if (currentSkill != null)
        {
            currentCharges = currentSkill.maxCharges == 0 ? 9999 : currentSkill.maxCharges;
            currentCooldown = 0;
            // UI에 스킬 아이콘, 설명 적용 등 (여기서는 생략)
        }
    }

    // 스킬 입력 처리 (예: F키 사용)
    void HandleSkillInput()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
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
                Debug.Log("탐지 스킬 사용!");
                // 주변 스캔 로직 구현
                break;
            case 1: // 고철꾼
                Debug.Log("고철꾼 스킬 사용!");
                // 아이템 카메라 획득 로직 구현
                break;
                // ... 다른 스킬 ID에 대한 구현 ...
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
            // UI에 쿨타임 표시 업데이트 (여기서는 생략)
        }
    }
}