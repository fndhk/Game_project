using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SkillChoiceUIController : MonoBehaviour
{
    [System.Serializable]
    public struct SkillButtonInfo
    {
        public Button button;
        public string skillName;
        public Sprite skillIcon; // ★ 새로 추가: 인스펙터에서 이미지를 넣을 칸
        public Outline outline;
    }

    [Header("Civilian Skills (Left to Right)")]
    [SerializeField] private List<SkillButtonInfo> civilianButtons = new List<SkillButtonInfo>();

    [Header("Imposter Skills (Left to Right)")]
    [SerializeField] private List<SkillButtonInfo> imposterButtons = new List<SkillButtonInfo>();

    private string currentSelectedCivilian = "";
    private string currentSelectedImposter = "";

    void Start()
    {
        InitGroup(civilianButtons, true);
        InitGroup(imposterButtons, false);

        if (civilianButtons.Count > 0) SelectCivilianSkill(civilianButtons[0]);
        if (imposterButtons.Count > 0) SelectImposterSkill(imposterButtons[0]);
    }

    private void InitGroup(List<SkillButtonInfo> buttonList, bool isCivilian)
    {
        foreach (var btnInfo in buttonList)
        {
            SkillButtonInfo currentInfo = btnInfo;

            // ★ [핵심 추가]: 인스펙터에 넣은 이미지를 실제 버튼의 Image 컴포넌트에 입혀줍니다.
            if (currentInfo.button != null && currentInfo.skillIcon != null)
            {
                currentInfo.button.image.sprite = currentInfo.skillIcon;
            }

            if (currentInfo.outline != null) currentInfo.outline.enabled = false;

            currentInfo.button.onClick.AddListener(() =>
            {
                if (isCivilian) SelectCivilianSkill(currentInfo);
                else SelectImposterSkill(currentInfo);
            });
        }
    }

    private void SelectCivilianSkill(SkillButtonInfo targetInfo)
    {
        currentSelectedCivilian = targetInfo.skillName;
        UpdateVisuals(civilianButtons, targetInfo);
        Debug.Log($"시민 스킬 선택 완료: {currentSelectedCivilian}");
    }

    private void SelectImposterSkill(SkillButtonInfo targetInfo)
    {
        currentSelectedImposter = targetInfo.skillName;
        UpdateVisuals(imposterButtons, targetInfo);
        Debug.Log($"임포스터 스킬 선택 완료: {currentSelectedImposter}");
    }

    private void UpdateVisuals(List<SkillButtonInfo> group, SkillButtonInfo selectedInfo)
    {
        foreach (var btnInfo in group)
        {
            if (btnInfo.outline != null)
            {
                if (btnInfo.button == selectedInfo.button)
                {
                    btnInfo.outline.enabled = true;
                    btnInfo.outline.effectColor = Color.yellow;
                    btnInfo.outline.effectDistance = new Vector2(5, 5);
                }
                else
                {
                    btnInfo.outline.enabled = false;
                }
            }
        }
    }
}