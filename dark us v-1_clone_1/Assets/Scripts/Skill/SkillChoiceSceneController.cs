using UnityEngine;
using UnityEngine.SceneManagement;

public class SkillChoiceSceneController : MonoBehaviour
{
    // 임시로 선택된 스킬을 담아둘 변수
    private string temporarySelectedSkill = "None";

    // 이전 씬의 이름을 기억해두거나, 고정된 메인메뉴 씬 이름을 적어줍니다.
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // 각 스킬 버튼을 누를 때 호출될 함수 (예: 스킬 버튼에 연결)
    public void SelectSkill(string skillName)
    {
        temporarySelectedSkill = skillName;
        Debug.Log($"스킬 임시 선택됨: {temporarySelectedSkill}");
        // 여기에 선택된 버튼만 하이라이트(불빛) 주는 UI 연출을 넣으면 좋습니다.
    }

    // "선택 완료" 버튼에 연결할 함수
    public void OnClickConfirm()
    {
        if (temporarySelectedSkill == "None")
        {
            Debug.LogWarning("스킬을 선택하지 않았습니다!");
            return;
        }

        // 1. 싱글톤 매니저에 최종 스킬 적용
        if (SkillManager.Instance != null)
        {
            SkillManager.Instance.SetSkill(temporarySelectedSkill);
        }

        // 2. 실제 플레이어 오브젝트가 현재 씬에 있다면 즉시 적용하는 로직 호출 가능
        ApplySkillToCurrentPlayer();

        // 3. 원래 씬(메인 메뉴)으로 돌아가기
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // "돌아가기" 버튼에 연결할 함수 (선택 취소)
    public void OnClickBack()
    {
        // 아무것도 저장하지 않고 메인 메뉴로 복귀
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // 현재 플레이 중인 캐릭터에게 스킬을 실시간 주입하는 함수
    private void ApplySkillToCurrentPlayer()
    {
        // 예시: Player 스크립트를 찾아 스킬을 새로고침
        // PlayerController player = FindObjectOfType<PlayerController>();
        // if(player != null) player.RefreshSkill();
    }
}