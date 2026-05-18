using UnityEngine;

public class SkillManager : MonoBehaviour
{
    public static SkillManager Instance;

    // 현재 유저가 선택한 스킬의 ID 또는 이름
    public string SelectedSkill { get; private set; } = "None";

    void Awake()
    {
        // 씬이 바뀌어도 이 오브젝트는 파괴되지 않고 유지됩니다.
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

    // 스킬을 저장하는 함수
    public void SetSkill(string skillName)
    {
        SelectedSkill = skillName;
        Debug.Log($"[SkillManager] 현재 적용된 스킬: {SelectedSkill}");
    }
}