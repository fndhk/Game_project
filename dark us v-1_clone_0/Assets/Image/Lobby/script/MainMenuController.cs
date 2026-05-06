using UnityEngine;
using UnityEngine.SceneManagement;

// 메인 메뉴 버튼 동작을 관리하는 스크립트이다.
// 방 만들기, 방 찾기, 설정 버튼을 눌렀을 때의 기본 흐름을 담당한다.
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Names")]
    // 방 만들기를 눌렀을 때 이동할 씬 이름이다.
    public string createRoomSceneName = "LobbyScene";

    // 방 찾기를 눌렀을 때 이동할 씬 이름이다.
    public string findRoomSceneName = "RoomListScene";

    [Header("Panels")]
    // 설정 버튼을 눌렀을 때 켜고 끌 설정 패널이다.
    public GameObject settingsPanel;

    [Header("Audio Optional")]
    // 버튼 클릭 사운드이다.
    public AudioSource clickAudioSource;

    // 시작 시 설정 패널은 꺼둔다.
    private void Start()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    // 방 만들기 버튼에서 호출한다.
    public void OnClickCreateRoom()
    {
        PlayClickSound();

        // 아직 멀티 로비 씬이 없으면 콘솔에만 표시하고 멈춘다.
        if (string.IsNullOrWhiteSpace(createRoomSceneName))
        {
            Debug.LogWarning("Create room scene name is empty.");
            return;
        }

        SceneManager.LoadScene(createRoomSceneName);
    }

    // 방 찾기 버튼에서 호출한다.
    public void OnClickFindRoom()
    {
        PlayClickSound();

        // 아직 방 목록 씬이 없으면 콘솔에만 표시하고 멈춘다.
        if (string.IsNullOrWhiteSpace(findRoomSceneName))
        {
            Debug.LogWarning("Find room scene name is empty.");
            return;
        }

        SceneManager.LoadScene(findRoomSceneName);
    }

    // 설정 버튼에서 호출한다.
    public void OnClickSettings()
    {
        PlayClickSound();

        if (settingsPanel == null)
        {
            Debug.LogWarning("Settings panel is not assigned.");
            return;
        }

        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    // 게임 종료 버튼을 나중에 만들 경우 호출한다.
    public void OnClickQuit()
    {
        PlayClickSound();

        // 에디터에서는 종료가 눈에 보이지 않지만 빌드에서는 게임이 종료된다.
        Application.Quit();
    }

    // 버튼 클릭 사운드를 재생한다.
    private void PlayClickSound()
    {
        if (clickAudioSource == null)
        {
            return;
        }

        if (clickAudioSource.clip != null)
        {
            clickAudioSource.PlayOneShot(clickAudioSource.clip);
            return;
        }

        clickAudioSource.Play();
    }
}