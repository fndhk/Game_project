using UnityEngine;

[CreateAssetMenu(fileName = "New Skill", menuName = "Skill/SkillData")]
public class SkillData : ScriptableObject
{
    public string skillName; // 스킬 이름
    public int skillID; // 스킬 고유 ID
    public Sprite skillIcon; // 스킬 아이콘
    public float cooldown; // 쿨타임 (초)
    public int maxCharges; // 최대 사용 횟수 (0: 무제한)
    [TextArea] public string description; // 스킬 설명
}