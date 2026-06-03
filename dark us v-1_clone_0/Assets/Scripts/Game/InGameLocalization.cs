using UnityEngine;

public static class InGameLocalization
{
    public static int LanguageIndex => PlayerPrefs.GetInt("setting_language", 0);

    public static string Text(string key)
    {
        switch (LanguageIndex)
        {
            case 1:
                return English(key);
            case 2:
                return Japanese(key);
            default:
                return Korean(key);
        }
    }

    public static string RoleName(PlayerRole role)
    {
        return role == PlayerRole.Killer ? Text("Imposter") : Text("Citizen");
    }

    public static string ItemName(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Camera:
                return Text("Camera");
            case ItemType.Knife:
                return Text("Knife");
            case ItemType.Medkit:
                return Text("Medkit");
            default:
                return Text("Item");
        }
    }

    private static string Korean(string key)
    {
        switch (key)
        {
            case "Role": return "역할";
            case "Citizen": return "시민";
            case "Imposter": return "도플갱어";
            case "Objective Find Computers": return "시간 안에 목표 컴퓨터 4개를 찾아 탈출하시오";
            case "Objective Kill Crew": return "대원을 모두 죽이고 탈출을 저지하십시오";
            case "VITAL": return "체력";
            case "STAM": return "스태미나";
            case "DOT MEMORY": return "스캔 메모리";
            case "SCAN": return "스캔";
            case "SCAN RDY": return "스캔 준비";
            case "Find Target Computers": return "목표 컴퓨터 찾기";
            case "Restore Computers": return "목표 컴퓨터 찾기";
            case "Progress": return "진행";
            case "Carrying": return "보유";
            case "Exit Open": return "탈출구 개방";
            case "Exit Unlocked": return "탈출구 개방";
            case "Find Access Cores": return "액세스 코어 찾기";
            case "Install Access Cores": return "액세스 코어 삽입";
            case "Reach The Exit": return "탈출구로 이동";
            case "Use Terminal": return "터미널 사용";
            case "Exit Power Restored": return "탈출 전원 복구됨";
            case "Need Access Core": return "액세스 코어 필요";
            case "Insert Access Core": return "액세스 코어 삽입";
            case "Take Access Core": return "액세스 코어 획득";
            case "Take": return "획득";
            case "Camera": return "카메라";
            case "Knife": return "칼";
            case "Medkit": return "구급상자";
            case "Item": return "아이템";
            case "Inventory is full.": return "인벤토리가 가득 찼습니다.";
            case "Cannot carry more": return "더 이상 들 수 없음";
            case "No item selected.": return "선택한 아이템 없음";
            case "Used": return "사용";
            case "Dropped": return "버림";
            case "Cannot use item": return "아이템 사용 불가";
            case "Health is already full.": return "체력이 이미 가득 찼습니다.";
            case "Target Computer Found": return "목표 컴퓨터 확인됨";
            case "Escape Computer Restored": return "목표 컴퓨터 확인됨";
            case "Wrong Computer": return "오답 컴퓨터";
            case "Wrong Computer Restored": return "오답 컴퓨터";
            case "Checking Computer": return "컴퓨터 확인 중";
            case "Restoring Computer": return "컴퓨터 확인 중";
            case "Check Computer": return "컴퓨터 확인";
            case "Restore Computer": return "컴퓨터 확인";
            case "Sabotage Computer": return "컴퓨터 망가뜨리기";
            case "Sabotaging Computer": return "컴퓨터 망가뜨리는 중";
            case "Computer Sabotaged": return "컴퓨터 고장남";
            case "Repair Computer": return "컴퓨터 재수리";
            case "Repairing Computer": return "컴퓨터 재수리 중";
            case "Exit Locked": return "탈출구 잠김";
            case "Escape Route Open": return "탈출 경로 열림";
            case "Open Exit": return "탈출구 열기";
            case "Escape": return "탈출";
            case "Citizens Win": return "시민 승리";
            case "Killer Wins": return "도플갱어 승리";
            case "Citizens Escaped": return "시민이 탈출했습니다";
            case "All Citizens Down": return "모든 시민이 쓰러졌습니다";
            case "Killer Disconnected": return "도플갱어가 이탈했습니다";
            case "Time Expired": return "시간 초과";
            case "Round Complete": return "라운드 종료";
            case "Kill Time": return "킬타임";
            case "One Shot Available": return "즉사 1회 가능";
            case "Press": return "누르기";
            case "to Kill": return "처치";
            case "Press Q to Kill": return "[Q] 즉사";
            case "Hide From Imposter": return "도플갱어를 피하십시오";
            case "Alive Citizens": return "생존 시민";
            case "MIC OPEN": return "마이크 켜짐";
            case "MIC MUTED": return "마이크 음소거";
            case "Returning to Lobby": return "대기방으로 이동 중";
            case "PAUSED": return "일시정지";
            case "MENU": return "메뉴";
            case "SETTINGS": return "설정";
            case "CONTROLS": return "조작";
            case "PLAYERS": return "플레이어";
            case "SESSION": return "세션";
            case "STATUS": return "상태";
            case "ROOM CODE": return "방 코드";
            case "CONNECTED": return "연결됨";
            case "ESC closes this menu.": return "ESC로 메뉴를 닫습니다.";
            case "IN-GAME SETTINGS": return "인게임 설정";
            case "WASD        MOVE": return "WASD        이동";
            case "MOUSE       LOOK": return "마우스      시점";
            case "SHIFT       SPRINT": return "SHIFT       달리기";
            case "CTRL        CROUCH": return "CTRL        앉기";
            case "E           INTERACT": return "E           상호작용";
            case "F           PICK UP": return "F           줍기";
            case "1 / 2       SELECT ITEM": return "1 / 2       아이템 선택";
            case "LMB         USE ITEM": return "좌클릭      아이템 사용";
            case "G           DROP ITEM": return "G           아이템 버리기";
            case "V           VOICE": return "V           음성";
            case "ESC         PAUSE": return "ESC         일시정지";
            case "Not connected to a Photon room.": return "Photon 방에 연결되어 있지 않습니다.";
            case "CREW": return "대원";
            case "HOST": return "호스트";
            case "PLAYER": return "플레이어";
            case "YOU": return "나";
            case "REMOTE": return "원격";
            case "RETURN TO LOBBY": return "방 로비로 돌아가기";
            case "Return everyone to the room lobby?": return "모두 방 로비로 돌아갈까요?";
            case "QUIT TO MAIN MENU": return "메인 메뉴로 나가기";
            case "Leave the current room and return to main menu?": return "현재 방을 나가고 메인 메뉴로 돌아갈까요?";
            case "QUIT GAME": return "게임 종료";
            case "Close the game?": return "게임을 종료할까요?";
            case "dark Us": return "dark Us";
            case "Resume": return "계속";
            case "Settings": return "설정";
            case "Controls": return "조작";
            case "Players": return "플레이어";
            case "Return to Lobby": return "방 로비로";
            case "Quit to Main Menu": return "메인 메뉴로";
            case "Quit Game": return "게임 종료";
            case "Master Volume": return "전체 볼륨";
            case "BGM Volume": return "배경음 볼륨";
            case "SFX Volume": return "효과음 볼륨";
            case "Voice Volume": return "마이크 볼륨";
            case "Display": return "화면";
            case "Audio": return "오디오";
            case "Controls & Keybindings": return "조작 및 키 설정";
            case "Gameplay": return "게임플레이";
            case "Screen Mode": return "화면 모드";
            case "FPS Limit": return "FPS 제한";
            case "Language": return "언어";
            case "Mouse Sensitivity X": return "마우스 감도 X";
            case "Mouse Sensitivity Y": return "마우스 감도 Y";
            case "Mouse Sens X": return "마우스 감도 X";
            case "Mouse Sens Y": return "마우스 감도 Y";
            case "HUD Opacity": return "HUD 투명도";
            case "Move Forward": return "앞으로 이동";
            case "Move Back": return "뒤로 이동";
            case "Move Left": return "왼쪽 이동";
            case "Move Right": return "오른쪽 이동";
            case "Sprint": return "달리기";
            case "Crouch": return "앉기";
            case "Interact": return "상호작용";
            case "Pick Up": return "줍기";
            case "Scan": return "스캔";
            case "Use Item": return "아이템 사용";
            case "Drop Item": return "아이템 버리기";
            case "Slot 1": return "슬롯 1";
            case "Slot 2": return "슬롯 2";
            case "Mic Mute": return "마이크 음소거";
            case "Kill": return "킬";
            case "Pause": return "일시정지";
            case "Change": return "변경";
            case "Bind": return "지정";
            case "Press a key": return "키를 누르세요";
            case "BORDERLESS": return "테두리 없음";
            case "WINDOWED": return "창 모드";
            case "UNLIMITED": return "제한 없음";
            case "KOREAN": return "한국어";
            case "ENGLISH": return "영어";
            case "JAPANESE": return "일본어";
            case "Field of View": return "시야각";
            case "Apply": return "적용";
            case "Reset": return "초기화";
            case "Back": return "뒤로";
            case "ON": return "켜짐";
            case "OFF": return "꺼짐";
            case "CONFIRM": return "확인";
            case "Confirm": return "확인";
            case "Cancel": return "취소";
            default: return key;
        }
    }

    private static string English(string key)
    {
        switch (key)
        {
            case "Imposter": return "Doppelganger";
            case "Killer Wins": return "Doppelganger Wins";
            case "Killer Disconnected": return "Doppelganger disconnected";
            case "Hide From Imposter": return "Hide From Doppelganger";
            default: return key;
        }
    }

    private static string Japanese(string key)
    {
        switch (key)
        {
            case "Role": return "役職";
            case "Citizen": return "市民";
            case "Imposter": return "ドッペルゲンガー";
            case "Objective Find Computers": return "目標コンピューター4台を探して出口を開け";
            case "Objective Kill Crew": return "クルーを妨害し、脱出を阻止せよ";
            case "VITAL": return "体力";
            case "STAM": return "スタミナ";
            case "DOT MEMORY": return "スキャンメモリ";
            case "SCAN": return "スキャン";
            case "SCAN RDY": return "スキャン準備";
            case "Find Target Computers": return "目標コンピューター探索";
            case "Restore Computers": return "目標コンピューター探索";
            case "Progress": return "進行";
            case "Carrying": return "所持";
            case "Exit Open": return "出口開放";
            case "Exit Unlocked": return "出口開放";
            case "Find Access Cores": return "アクセスコア探索";
            case "Install Access Cores": return "アクセスコア挿入";
            case "Reach The Exit": return "出口へ向かう";
            case "Use Terminal": return "端末を使う";
            case "Exit Power Restored": return "出口電源復旧";
            case "Need Access Core": return "アクセスコアが必要";
            case "Insert Access Core": return "アクセスコア挿入";
            case "Take Access Core": return "アクセスコア取得";
            case "Take": return "取得";
            case "Camera": return "カメラ";
            case "Knife": return "ナイフ";
            case "Medkit": return "救急キット";
            case "Item": return "アイテム";
            case "Inventory is full.": return "インベントリが満杯です。";
            case "Cannot carry more": return "これ以上持てません";
            case "No item selected.": return "アイテム未選択";
            case "Used": return "使用";
            case "Dropped": return "捨てた";
            case "Cannot use item": return "アイテム使用不可";
            case "Health is already full.": return "体力は満タンです。";
            case "Target Computer Found": return "目標コンピューター確認済み";
            case "Escape Computer Restored": return "目標コンピューター確認済み";
            case "Wrong Computer": return "違うコンピューター";
            case "Wrong Computer Restored": return "違うコンピューター";
            case "Checking Computer": return "コンピューター確認中";
            case "Restoring Computer": return "コンピューター確認中";
            case "Check Computer": return "コンピューター確認";
            case "Restore Computer": return "コンピューター確認";
            case "Sabotage Computer": return "コンピューター破壊";
            case "Sabotaging Computer": return "コンピューター破壊中";
            case "Computer Sabotaged": return "コンピューター故障中";
            case "Repair Computer": return "コンピューター再修理";
            case "Repairing Computer": return "コンピューター再修理中";
            case "Exit Locked": return "出口ロック中";
            case "Escape Route Open": return "脱出経路開放";
            case "Open Exit": return "出口を開く";
            case "Escape": return "脱出";
            case "Citizens Win": return "市民の勝利";
            case "Killer Wins": return "ドッペルゲンガーの勝利";
            case "Citizens Escaped": return "市民が脱出しました";
            case "All Citizens Down": return "市民が全員倒れました";
            case "Killer Disconnected": return "ドッペルゲンガーが離脱しました";
            case "Time Expired": return "時間切れ";
            case "Round Complete": return "ラウンド終了";
            case "Kill Time": return "キルタイム";
            case "One Shot Available": return "即死1回可能";
            case "Press": return "押す";
            case "to Kill": return "キル";
            case "Press Q to Kill": return "[Q] 即死";
            case "Hide From Imposter": return "ドッペルゲンガーから逃げろ";
            case "Alive Citizens": return "生存市民";
            case "MIC OPEN": return "マイクオン";
            case "MIC MUTED": return "マイクミュート";
            case "Returning to Lobby": return "待機ルームへ移動中";
            case "PAUSED": return "一時停止";
            case "MENU": return "メニュー";
            case "SETTINGS": return "設定";
            case "CONTROLS": return "操作";
            case "PLAYERS": return "プレイヤー";
            case "SESSION": return "セッション";
            case "STATUS": return "状態";
            case "ROOM CODE": return "ルームコード";
            case "CONNECTED": return "接続済み";
            case "ESC closes this menu.": return "ESCでメニューを閉じます。";
            case "IN-GAME SETTINGS": return "ゲーム内設定";
            case "WASD        MOVE": return "WASD        移動";
            case "MOUSE       LOOK": return "マウス      視点";
            case "SHIFT       SPRINT": return "SHIFT       ダッシュ";
            case "CTRL        CROUCH": return "CTRL        しゃがむ";
            case "E           INTERACT": return "E           インタラクト";
            case "F           PICK UP": return "F           拾う";
            case "1 / 2       SELECT ITEM": return "1 / 2       アイテム選択";
            case "LMB         USE ITEM": return "左クリック  アイテム使用";
            case "G           DROP ITEM": return "G           アイテムを落とす";
            case "V           VOICE": return "V           ボイス";
            case "ESC         PAUSE": return "ESC         一時停止";
            case "Not connected to a Photon room.": return "Photonルームに接続されていません。";
            case "CREW": return "クルー";
            case "HOST": return "ホスト";
            case "PLAYER": return "プレイヤー";
            case "YOU": return "自分";
            case "REMOTE": return "リモート";
            case "RETURN TO LOBBY": return "ルームロビーへ戻る";
            case "Return everyone to the room lobby?": return "全員をルームロビーへ戻しますか？";
            case "QUIT TO MAIN MENU": return "メインメニューへ戻る";
            case "Leave the current room and return to main menu?": return "現在のルームを退出してメインメニューへ戻りますか？";
            case "QUIT GAME": return "ゲーム終了";
            case "Close the game?": return "ゲームを終了しますか？";
            case "Resume": return "再開";
            case "Settings": return "設定";
            case "Controls": return "操作";
            case "Players": return "プレイヤー";
            case "Return to Lobby": return "ルームロビーへ";
            case "Quit to Main Menu": return "メインメニューへ";
            case "Quit Game": return "ゲーム終了";
            case "Master Volume": return "全体音量";
            case "BGM Volume": return "BGM音量";
            case "SFX Volume": return "効果音音量";
            case "Voice Volume": return "マイク音量";
            case "Display": return "画面";
            case "Audio": return "オーディオ";
            case "Controls & Keybindings": return "操作・キー設定";
            case "Gameplay": return "ゲームプレイ";
            case "Screen Mode": return "画面モード";
            case "FPS Limit": return "FPS制限";
            case "Language": return "言語";
            case "Mouse Sensitivity X": return "マウス感度 X";
            case "Mouse Sensitivity Y": return "マウス感度 Y";
            case "Mouse Sens X": return "マウス感度 X";
            case "Mouse Sens Y": return "マウス感度 Y";
            case "HUD Opacity": return "HUD透明度";
            case "Move Forward": return "前進";
            case "Move Back": return "後退";
            case "Move Left": return "左移動";
            case "Move Right": return "右移動";
            case "Sprint": return "ダッシュ";
            case "Crouch": return "しゃがむ";
            case "Interact": return "インタラクト";
            case "Pick Up": return "拾う";
            case "Scan": return "スキャン";
            case "Use Item": return "アイテム使用";
            case "Drop Item": return "アイテムを捨てる";
            case "Slot 1": return "スロット 1";
            case "Slot 2": return "スロット 2";
            case "Mic Mute": return "マイクミュート";
            case "Kill": return "キル";
            case "Pause": return "一時停止";
            case "Change": return "変更";
            case "Bind": return "割当";
            case "Press a key": return "キーを押してください";
            case "BORDERLESS": return "ボーダーレス";
            case "WINDOWED": return "ウィンドウ";
            case "UNLIMITED": return "無制限";
            case "KOREAN": return "韓国語";
            case "ENGLISH": return "英語";
            case "JAPANESE": return "日本語";
            case "Field of View": return "視野角";
            case "Apply": return "適用";
            case "Reset": return "リセット";
            case "Back": return "戻る";
            case "ON": return "オン";
            case "OFF": return "オフ";
            case "CONFIRM": return "確認";
            case "Confirm": return "確認";
            case "Cancel": return "キャンセル";
            default: return key;
        }
    }
}
