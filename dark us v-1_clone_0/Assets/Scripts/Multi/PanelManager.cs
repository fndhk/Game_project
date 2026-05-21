using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PanelManager : MonoBehaviour
{
    // 연결할 패널 (FindRoomPanel)
    [Header("UI Panels")]
    [SerializeField] private GameObject findRoomPanel;

    [Header("Input Sync")]
    [SerializeField] private TMP_InputField roomInputField;

    void Start()
    {
        // 게임 시작 시 패널은 기본적으로 꺼져 있도록 설정
        if (findRoomPanel != null)
        {
            findRoomPanel.SetActive(false);
        }
    }

    // 패널 열기 함수 (Find Room 버튼에 연결)
    public void OpenFindRoomPanel()
    {
        if (findRoomPanel != null)
        {
            findRoomPanel.SetActive(true);
            // 여기서 열릴 때 사운드나 파티클 효과를 추가하면 더 좋습니다!
            Debug.Log("Find Room 패널이 열렸습니다.");
        }
    }

    // 패널 닫기 함수 (X 버튼에 연결)
    public void CloseFindRoomPanel()
    {
        if (findRoomPanel != null)
        {
            findRoomPanel.SetActive(false);
            Debug.Log("Find Room 패널이 닫혔습니다.");
        }
    }

    public void ClickInsertButton()
    {
        // InputField에 엔터(Submit)가 입력된 것과 동일한 이벤트를 강제로 발생시킵니다.
        roomInputField.onSubmit.Invoke(roomInputField.text);
    }
}