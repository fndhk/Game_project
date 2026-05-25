using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    // 현재 선택된 시민팀/임포스터 스킬 데이터 (유저가 선택한 것)
    public SkillData selectedCivilianSkill;
    public SkillData selectedImposterSkill;

    void Awake()
    {
        // 씬 전환 시 파괴되지 않도록 설정 (DontDestroyOnLoad)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 각 스킬 버튼에 연결할 함수 (예: 버튼 클릭 시 호출)
    public void SelectSkill(SkillData skillData, bool isCivilian)
    {
        if (isCivilian)
        {
            selectedCivilianSkill = skillData;
        }
        else
        {
            selectedImposterSkill = skillData;
        }
    }
}