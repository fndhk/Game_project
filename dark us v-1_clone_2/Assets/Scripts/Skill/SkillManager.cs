using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    // 최종적으로 저장될 스킬 이름을 담을 변수들 (외부에서 읽을 수 있도록 public 설정)
    public string savedCivilianSkill { get; private set; } = "탐지";
    public string savedImposterSkill { get; private set; } = "정전";

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

    // [시민 스킬 저장] UI 스크립트에서 완료 버튼을 누를 때 이 함수를 호출해 값을 넘겨받습니다.
    public void SetCivilianSkill(string skillName)
    {
        savedCivilianSkill = skillName;
        Debug.Log($"[SkillManager] 시민 스킬 최종 저장 완료: {savedCivilianSkill}");
    }

    // [임포스터 스킬 저장] UI 스크립트에서 완료 버튼을 누를 때 이 함수를 호출해 값을 넘겨받습니다.
    public void SetImposterSkill(string skillName)
    {
        savedImposterSkill = skillName;
        Debug.Log($"[SkillManager] 임포스터 스킬 최종 저장 완료: {savedImposterSkill}");
    }
}