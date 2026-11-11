using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TwoInPot
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ConnectionView : MonoBehaviour
    {
        enum GameLanguage { Korean, English }
        const string LanguageKey = "TwoInPot.Language";
        public SessionController session;
        VisualElement root, menu, lobby, hud, languageMenu, settingsPanel, languagePanel, connectionLostPanel;
        VisualElement practiceCard;
        Label status, hudStatus, code, objective, stageLabel, controls, lobbyStatus, lobbyCode, playerOneChoice, playerTwoChoice;
        Label practiceTitle, practiceInstructions;
        Label stageAnnouncement;
        int announcedStage = -1;
        bool wasPracticing;
        float announcementUntil;
        TextField address, roomCode;
        GameLanguage language;
        bool languageChosen;
#if UNITY_EDITOR || DEBUG
        internal void ShowProbeHud() { language = GameLanguage.Korean; languageChosen = true; ApplyLanguage(); }
#endif
        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            menu = root.Q("menu"); lobby = root.Q("lobby"); hud = root.Q("hud");
            languageMenu = root.Q("languageMenu"); settingsPanel = root.Q("settingsPanel"); languagePanel = root.Q("languagePanel"); connectionLostPanel = root.Q("connectionLostPanel");
            practiceCard = root.Q("practiceCard"); practiceTitle = root.Q<Label>("practiceTitle"); practiceInstructions = root.Q<Label>("practiceInstructions");
            stageAnnouncement = root.Q<Label>("stageAnnouncement");
            status = root.Q<Label>("status"); hudStatus = root.Q<Label>("hudStatus");
            objective = root.Q<Label>("objective"); code = root.Q<Label>("code"); stageLabel = root.Q<Label>("stageLabel");
            controls = root.Q<Label>("controls");
            lobbyStatus = root.Q<Label>("lobbyStatus");
            lobbyCode = root.Q<Label>("lobbyCode");
            playerOneChoice = root.Q<Label>("playerOneChoice"); playerTwoChoice = root.Q<Label>("playerTwoChoice");
            address = root.Q<TextField>("address"); roomCode = root.Q<TextField>("roomCode");
            address.value = "127.0.0.1";
            root.Q<Button>("hostLan").clicked += session.HostDirect;
            root.Q<Button>("joinLan").clicked += () => session.JoinDirect(address.value);
            root.Q<Button>("hostOnline").clicked += async () => await session.HostOnlineAsync();
            root.Q<Button>("joinOnline").clicked += async () => await session.JoinOnlineAsync(roomCode.value);
            root.Q<Button>("copyCode").clicked += () => GUIUtility.systemCopyBuffer = session.JoinCode;
            root.Q<Button>("copyLobbyCode").clicked += () => GUIUtility.systemCopyBuffer = session.JoinCode;
            root.Q<Button>("quit").clicked += () => Application.Quit();
            root.Q<Button>("selectKorean").clicked += () => SelectLanguage(GameLanguage.Korean);
            root.Q<Button>("selectEnglish").clicked += () => SelectLanguage(GameLanguage.English);
            root.Q<Button>("settingsKorean").clicked += () => SelectLanguage(GameLanguage.Korean);
            root.Q<Button>("settingsEnglish").clicked += () => SelectLanguage(GameLanguage.English);
            root.Q<Button>("settingsMenu").clicked += OpenSettings;
            root.Q<Button>("settingsHud").clicked += OpenSettings;
            root.Q<Button>("settingsLobby").clicked += OpenSettings;
            root.Q<Button>("openLanguage").clicked += () => languagePanel.style.display = DisplayStyle.Flex;
            root.Q<Button>("closeLanguage").clicked += () => languagePanel.style.display = DisplayStyle.None;
            root.Q<Button>("settingsRestartGame").clicked += () => FindAnyObjectByType<StageState>()?.RestartFromBeginning();
            root.Q<Button>("dismissConnectionLost").clicked += () => { session.DismissHostConnectionLost(); connectionLostPanel.style.display = DisplayStyle.None; };
            root.Q<Button>("choosePhysical").clicked += () => LocalPlayer()?.ChooseRole(PlayerRole.Physical);
            root.Q<Button>("chooseLogic").clicked += () => LocalPlayer()?.ChooseRole(PlayerRole.Logic);
            root.Q<Button>("acceptRole").clicked += () => LocalPlayer()?.AcceptRole();
            root.Q<Button>("closeSettings").clicked += CloseSettings;
            root.Q<Button>("settingsReset").clicked += () => { FindAnyObjectByType<StageState>()?.ResetStage(); CloseSettings(); };
            root.Q<Button>("settingsLeave").clicked += async () => { await session.LeaveAsync(); CloseSettings(); };
            root.Q<Button>("settingsQuit").clicked += () => Application.Quit();
            int saved = PlayerPrefs.GetInt(LanguageKey, -1);
            languageChosen = saved >= 0;
            language = saved == (int)GameLanguage.English ? GameLanguage.English : GameLanguage.Korean;
            settingsPanel.style.display = DisplayStyle.None; languagePanel.style.display = DisplayStyle.None; connectionLostPanel.style.display = DisplayStyle.None;
            if (languageChosen) ApplyLanguage();
        }
        string L(string korean, string english) => language == GameLanguage.Korean ? korean : english;
        void SetLabel(string name, string korean, string english) => root.Q<Label>(name).text = L(korean, english);
        void SetButton(string name, string korean, string english) => root.Q<Button>(name).text = L(korean, english);
        CoopPlayer LocalPlayer() { foreach (var player in FindObjectsByType<CoopPlayer>()) if (player.IsOwner) return player; return null; }
        void SelectLanguage(GameLanguage selected)
        {
            language = selected; languageChosen = true;
            PlayerPrefs.SetInt(LanguageKey, (int)selected); PlayerPrefs.Save();
            ApplyLanguage(); languageMenu.style.display = DisplayStyle.None; languagePanel.style.display = DisplayStyle.None; CloseSettings();
        }
        void OpenSettings() => settingsPanel.style.display = DisplayStyle.Flex;
        void CloseSettings() => settingsPanel.style.display = DisplayStyle.None;
        void ApplyLanguage()
        {
            SetLabel("languageTitle", "언어를 선택하세요", "Choose a language");
            SetLabel("languageDescription", "언제든 설정에서 화면 언어를 바꿀 수 있습니다.", "You can change the display language in Settings at any time.");
            SetLabel("menuTitle", "TWO IN POT", "TWO IN POT");
            SetLabel("menuSubtitle", "둘의 생각, 하나의 길.", "Two minds. One way forward.");
            SetLabel("onlineEyebrow", "01 / 인터넷", "01 / INTERNET");
            SetLabel("onlineHeading", "어디서나 함께 플레이", "Play from anywhere");
            SetLabel("onlineDescription", "방 코드를 상대방에게 공유하세요.", "Share a room code with your partner.");
            SetLabel("localEyebrow", "02 / 로컬 네트워크", "02 / LOCAL NETWORK");
            SetLabel("localHeading", "PC 두 대 연결", "Connect two PCs");
            SetLabel("localDescription", "같은 네트워크: 호스트 PC의 IP를 입력하세요.\n같은 PC: 127.0.0.1", "Same network: enter the host PC's IP.\nSame PC: 127.0.0.1");
            SetLabel("settingsTitle", "설정", "Settings"); SetLabel("settingsDescription", "게임 설정", "Game settings");
            SetLabel("languageSettingsTitle", "언어", "Language"); SetLabel("languageSettingsDescription", "현재 지원하는 언어를 선택하세요.", "Choose an available language.");
            SetLabel("lobbyTitle", "역할 선택", "Choose your role"); SetLabel("lobbyDescription", "두 사람이 다른 역할을 고르고 수락하면 시작합니다.", "Choose different roles, then both accept to start.");
            address.label = L("호스트 IP", "Host IP"); roomCode.label = L("방 코드", "Room code");
            SetButton("hostOnline", "방 만들기", "CREATE ROOM"); SetButton("joinOnline", "코드로 참가", "JOIN WITH CODE");
            SetButton("hostLan", "이 PC에서 호스트", "HOST ON THIS PC"); SetButton("joinLan", "IP로 참가", "JOIN BY IP");
            SetButton("quit", "종료", "QUIT"); SetButton("settingsMenu", "설정", "SETTINGS"); SetButton("settingsHud", "설정", "SETTINGS");
            SetButton("copyCode", "코드 복사", "COPY CODE"); SetButton("closeSettings", "닫기", "CLOSE");
            SetButton("settingsReset", "다시 시작", "RESTART"); SetButton("settingsLeave", "나가기", "LEAVE ROOM"); SetButton("settingsQuit", "게임 종료", "QUIT GAME");
            SetButton("openLanguage", "언어", "LANGUAGE"); SetButton("settingsRestartGame", "처음부터 다시하기", "RESTART FROM BEGINNING"); SetButton("closeLanguage", "뒤로", "BACK"); SetButton("dismissConnectionLost", "확인", "OK");
            SetButton("choosePhysical", "피지컬 선택", "CHOOSE PHYSICAL"); SetButton("chooseLogic", "로직 선택", "CHOOSE LOGIC"); SetButton("acceptRole", "선택 수락", "ACCEPT ROLE"); SetButton("settingsLobby", "설정", "SETTINGS");
            SetButton("copyLobbyCode", "코드 복사", "COPY CODE");
        }
        void Update()
        {
            if (!languageChosen) { languageMenu.style.display = DisplayStyle.Flex; menu.style.display = DisplayStyle.None; lobby.style.display = DisplayStyle.None; hud.style.display = DisplayStyle.None; return; }
            languageMenu.style.display = DisplayStyle.None;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (session.HostConnectionLost) { session.DismissHostConnectionLost(); connectionLostPanel.style.display = DisplayStyle.None; }
                else if (settingsPanel.style.display == DisplayStyle.Flex) CloseSettings(); else OpenSettings();
            }
            connectionLostPanel.style.display = session.HostConnectionLost ? DisplayStyle.Flex : DisplayStyle.None;
            bool ready = session.Ready;
            var stage = FindAnyObjectByType<StageState>();
            bool started = ready && stage != null && stage.GameStarted.Value;
            CoopPlayer.InputBlocked = !started;
            menu.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            lobby.style.display = ready && !started ? DisplayStyle.Flex : DisplayStyle.None;
            hud.style.display = started ? DisplayStyle.Flex : DisplayStyle.None;
            bool practicing = started && stage != null && stage.PracticeActive.Value;
            practiceCard.style.display = practicing ? DisplayStyle.Flex : DisplayStyle.None;
            if (!started) { announcedStage = -1; wasPracticing = false; stageAnnouncement.style.display = DisplayStyle.None; }
            status.text = LocalizedStatus(session.Status);
            foreach (var button in menu.Query<Button>().ToList()) button.SetEnabled(!session.Busy);
            address.SetEnabled(!session.Busy); roomCode.SetEnabled(!session.Busy);
            root.Q<Button>("settingsReset").style.display = ready && session.manager.IsHost ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Button>("settingsLeave").style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            code.text = string.IsNullOrEmpty(session.JoinCode) ? (session.manager.IsHost ? L("호스트 IP / ", "HOST IP / ") + session.LocalAddress : L("직접 연결됨", "DIRECT CONNECTED")) : L("방 / ", "ROOM / ") + session.JoinCode;
            lobbyCode.text = !session.UsesOnlineRoom ? code.text : session.manager.IsHost && string.IsNullOrEmpty(session.JoinCode)
                ? L("방 코드 생성 중…", "CREATING ROOM CODE…")
                : string.IsNullOrEmpty(session.JoinCode) ? code.text : L("방 코드 / ", "ROOM CODE / ") + session.JoinCode;
            root.Q<Button>("copyCode").style.display = string.IsNullOrEmpty(session.JoinCode) ? DisplayStyle.None : DisplayStyle.Flex;
            root.Q<Button>("copyLobbyCode").style.display = string.IsNullOrEmpty(session.JoinCode) ? DisplayStyle.None : DisplayStyle.Flex;
            if (!ready) return;
            var players = FindObjectsByType<CoopPlayer>(); bool physical = session.manager.IsHost;
            if (!started)
            {
                bool twoPlayers = players.Length == 2;
                int mine = physical ? stage.HostRoleChoice.Value : stage.GuestRoleChoice.Value;
                int partner = physical ? stage.GuestRoleChoice.Value : stage.HostRoleChoice.Value;
                UpdateChoice(playerOneChoice, 1, L("호스트", "HOST"), stage.HostRoleChoice.Value, stage.HostAccepted.Value);
                UpdateChoice(playerTwoChoice, 2, L("참가자", "GUEST"), stage.GuestRoleChoice.Value, stage.GuestAccepted.Value);
                bool valid = mine >= 0 && partner >= 0 && mine != partner;
                root.Q<Button>("choosePhysical").SetEnabled(twoPlayers);
                root.Q<Button>("chooseLogic").SetEnabled(twoPlayers);
                root.Q<Button>("acceptRole").SetEnabled(valid);
                string roomLine = !session.manager.IsHost ? "" : !session.UsesOnlineRoom
                    ? L("호스트 IP: ", "HOST IP: ") + session.LocalAddress
                    : string.IsNullOrEmpty(session.JoinCode) ? L("방 코드 생성 중…", "CREATING ROOM CODE…") : L("방 코드: ", "ROOM CODE: ") + session.JoinCode;
                string roleLine = !twoPlayers ? L("참가자를 기다리는 중입니다.", "Waiting for the second player.") : !valid ? L("서로 다른 역할을 선택하세요.", "Choose different roles.") : L("내 선택: ", "Your choice: ") + (mine == (int)PlayerRole.Physical ? L("피지컬", "PHYSICAL") : L("로직", "LOGIC")) + L(" / 두 사람 모두 수락하면 시작합니다.", " / Both players must accept.");
                lobbyStatus.text = string.IsNullOrEmpty(roomLine) ? roleLine : roomLine + "\n" + roleLine;
                return;
            }
            var localPlayer = LocalPlayer();
            bool localPhysical = localPlayer != null && localPlayer.Role.Value == PlayerRole.Physical;
            hudStatus.text = (localPhysical ? L("피지컬 / ", "PHYSICAL / ") : L("로직 / ", "LOGIC / ")) + players.Length + L(" / 2명 연결", " OF 2 CONNECTED");
            stageLabel.text = "TWO IN POT / " + stage.StageIndex.Value.ToString("00") + " / " + stage.TotalStages.ToString("00");
            if (announcedStage != stage.StageIndex.Value)
            {
                announcedStage = stage.StageIndex.Value;
                stageAnnouncement.text = stage.CurrentDefinition.displayName;
                announcementUntil = Time.unscaledTime + 2.2f;
            }
            else if (wasPracticing && !practicing)
            {
                stageAnnouncement.text = L("연습 끝! 이제 함께 도전해요", "PRACTICE COMPLETE! LET'S GO TOGETHER");
                announcementUntil = Time.unscaledTime + 1.7f;
            }
            wasPracticing = practicing;
            stageAnnouncement.style.display = Time.unscaledTime < announcementUntil ? DisplayStyle.Flex : DisplayStyle.None;
            stageAnnouncement.style.opacity = Mathf.Clamp01((announcementUntil - Time.unscaledTime) * 3);
            if (practicing)
            {
                practiceTitle.text = L("기믹 연습", "MECHANIC PRACTICE");
                practiceInstructions.text = PracticeInstruction(stage.StageIndex.Value, localPhysical);
            }
            bool launchStage = stage != null && stage.StageIndex.Value == 2;
            controls.text = launchStage ? (localPhysical ? L("피지컬: SPACE를 누른 뒤 떼면 발사합니다. 길게 누르면 더 멀리 갑니다.", "PHYSICAL: Release SPACE to launch. Hold longer for more power.") : L("로직: A/D 또는 ←/→로 발사 각도를 조절합니다. 청록색 화살표를 보세요.", "LOGIC: A/D or arrows adjust the angle. Watch the cyan arrow.")) : StageControls(stage.StageIndex.Value, localPhysical);
            objective.text = practicing ? L("각자 화면의 안내대로 기믹을 한 번씩 사용해 보세요. 둘 다 연습하면 본 스테이지가 시작됩니다.", "Try the mechanic once using your role's prompt. The stage starts when both players finish.") :
                players.Length < 2 ? L("상대방을 기다리는 중입니다. 연결되면 함께 시작합니다.", "Waiting for your partner. You can practice moving.") :
                launchStage && stage.Complete.Value ? L("성공! 발사 스테이지를 완료했습니다. 호스트가 다시 시작할 수 있습니다.", "TOGETHER! Launch stage complete. Host can reset to try again.") :
                launchStage ? (localPhysical ? L("피지컬: 청록색 화살표 방향으로 발사됩니다. SPACE를 떼세요. 힘 ", "PHYSICAL: You launch toward the cyan arrow. Release SPACE. Power ") + stage.LaunchPower.Value.ToString("0.0") : L("로직: A/D로 청록색 화살표를 움직여 각도를 정하세요.", "LOGIC: Use A/D to move the cyan arrow.")) :
                stage.Complete.Value ? L("12개 스테이지 모두 성공! 함께 만든 멋진 모험이었어요. 설정에서 처음부터 다시 할 수 있어요.", "ALL 12 STAGES COMPLETE! Great teamwork. Restart from Settings to play again.") :
                StageObjective(stage.StageIndex.Value, localPhysical);
            if (!practicing && stage.StageIndex.Value == 10) objective.text += L("  / 남은 시간 ", "  / TIME ") + (stage.MechanicValue.Value * 3.5f).ToString("0.0");
            if (!practicing && stage.StageIndex.Value == 12 && !stage.Complete.Value) objective.text += stage.RelayStep.Value == 0 ? L("  / 첫 콘솔로!", "  / FIRST CONSOLE!") : L("  / 오른쪽 두 번째 콘솔로!", "  / SECOND CONSOLE ON THE RIGHT!");
        }
        string PracticeInstruction(int number, bool physical)
        {
            if (number == 9) return physical ? L("피지컬: SPACE로 점프해 보세요. 곧 승강기를 탈 거예요.", "PHYSICAL: Try SPACE to jump. Next, ride the lift.") : L("로직: D로 승강기를 올리고 A로 내려 보세요.", "LOGIC: D raises the lift; A lowers it.");
            if (number == 10) return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: SPACE를 눌러 3.5초 동안 문을 열어 보세요.", "LOGIC: Press SPACE to open the gate for 3.5 seconds.");
            if (number == 11) return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: A/D로 균형 다리를 기울여 보세요.", "LOGIC: Use A/D to tilt the balance bridge.");
            if (number == 12) return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: 왼쪽 첫 콘솔을 밟아 첫 문을 열어 보세요.", "LOGIC: Stand on the first console to unlock the first gate.");
            if (number == 1) return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: 청록색 스위치 위에 올라가 보세요.", "LOGIC: Stand on the cyan switch.");
            if (number == 2) return physical ? L("피지컬: SPACE를 눌렀다 떼어 발사해 보세요.", "PHYSICAL: Hold and release SPACE to launch.") : L("로직: A/D로 발사 각도를 바꿔 보세요.", "LOGIC: Use A/D to adjust the launch angle.");
            if (number == 3 || number == 5 || number == 7) return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: A/D로 기믹을 움직여 보세요.", "LOGIC: Use A/D to move the mechanic.");
            if (number == 4) return physical ? L("피지컬: SPACE로 점프해 바람을 느껴 보세요.", "PHYSICAL: Press SPACE while your partner starts the wind.") : L("로직: 콘솔 위에 서서 바람을 켜 보세요.", "LOGIC: Stand on the console to turn on the wind.");
            if (number == 6) return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: 콘솔에 올라 게이트를 열어 보세요.", "LOGIC: Stand on the console to open the gate.");
            if (number == 8) return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: 마지막 스위치에 올라 문을 열어 보세요.", "LOGIC: Stand on the final switch to open the gate.");
            return physical ? L("피지컬: SPACE로 점프해 보세요.", "PHYSICAL: Press SPACE to jump.") : L("로직: A/D로 움직이고 스위치를 찾아보세요.", "LOGIC: Move with A/D and find the switch.");
        }
        void UpdateChoice(Label label, int number, string playerType, int choice, bool accepted)
        {
            bool selected = choice >= 0;
            string role = !selected ? L("선택 대기", "WAITING") : choice == (int)PlayerRole.Physical ? L("피지컬", "PHYSICAL") : L("로직", "LOGIC");
            string acceptedText = accepted ? L("수락 완료", "ACCEPTED") : selected ? L("수락 대기", "WAITING FOR ACCEPT") : "";
            label.text = "PLAYER " + number + " / " + playerType + "\n" + role + (string.IsNullOrEmpty(acceptedText) ? "" : "\n" + acceptedText);
            label.EnableInClassList("player-choice-waiting", !selected);
            label.EnableInClassList("player-choice-accepted", accepted);
        }
        string StageControls(int number, bool physical)
        {
            if (number == 9) return physical ? L("피지컬: 이동 A/D · 점프 SPACE · 승강기를 타세요.", "PHYSICAL: A/D to move, SPACE to jump. Ride the lift.") : L("로직: D 승강기 올리기 · A 내리기", "LOGIC: D raises the lift; A lowers it.");
            if (number == 10) return physical ? L("피지컬: A/D 이동 · SPACE 점프 · 열린 문으로 달리세요.", "PHYSICAL: A/D to move, SPACE to jump. Run through the open gate.") : L("로직: SPACE를 누를 때마다 문이 3.5초 열려요.", "LOGIC: Each SPACE press opens the gate for 3.5 seconds.");
            if (number == 11) return physical ? L("피지컬: 다리를 건너며 SPACE로 점프하세요.", "PHYSICAL: Move across the bridge and jump with SPACE.") : L("로직: A/D로 다리 균형을 맞추세요.", "LOGIC: A/D balances the bridge.");
            if (number == 12) return L("A/D 또는 ←/→ 이동 · SPACE 점프 · 서로 신호를 주세요!", "A/D or arrows to move, SPACE to jump. Talk to your partner!");
            if (number == 3 || number == 5 || number == 7) return physical ? L("피지컬: 이동과 점프로 건너가세요.", "PHYSICAL: Move and jump across.") : L("로직: A/D로 장치를 움직이세요.", "LOGIC: Use A/D to move the device.");
            if (number == 4) return physical ? L("피지컬: 바람을 이용해 건너가세요.", "PHYSICAL: Use the wind to cross.") : L("로직: 콘솔 위에서 바람을 켜세요.", "LOGIC: Stand on the console to power wind.");
            if (number == 6) return physical ? L("피지컬: 충분히 빠르게 게이트를 통과하세요.", "PHYSICAL: Build speed and pass the gate.") : L("로직: 콘솔로 속도 게이트를 여세요.", "LOGIC: Open the speed gate from the console.");
            if (number == 8) return physical ? L("피지컬: 문을 지나 깃발로 가세요.", "PHYSICAL: Pass the gate and reach the flag.") : L("로직: 스위치를 계속 밟으세요.", "LOGIC: Keep the switch pressed.");
            return L("A/D 또는 ←/→ 이동     SPACE 점프", "A/D or ARROWS   Move     SPACE   Jump");
        }
        string StageObjective(int number, bool physical)
        {
            if (number == 9) return physical ? L("승강기를 타고 위쪽 테라스의 깃발로 가세요.", "Ride the lift to the flag on the upper terrace.") : L("파트너가 올라타면 D를 눌러 승강기를 올려주세요.", "Wait for your partner to board, then hold D to raise the lift.");
            if (number == 10) return physical ? L("파트너의 신호에 맞춰 시간제 문을 통과하세요.", "Cross the timed gate when your partner gives the signal.") : L("파트너가 준비되면 SPACE! 언제든 다시 열 수 있어요.", "Press SPACE when your partner is ready. You can reopen the gate anytime.");
            if (number == 11) return physical ? L("다리 기울기를 보며 반대쪽으로 건너세요.", "Watch the bridge angle and cross together.") : L("A/D로 파트너가 건너기 편한 각도를 만들어 주세요.", "Use A/D to make a safe bridge angle for your partner.");
            if (number == 12) return physical ? L("두 문을 지나 마지막 깃발로! 파트너가 콘솔을 바꿔야 해요.", "Pass both gates to the final flag. Your partner must change consoles.") : L("첫 콘솔을 밟고, 오른쪽 두 번째 콘솔로 가서 계속 밟아 주세요.", "Activate the first console, then move right and hold the second console.");
            if (number == 3) return physical ? L("움직이는 발판을 타고 건너가세요.", "Ride the moving platform.") : L("발판을 피지컬 플레이어 쪽으로 움직이세요.", "Move the platform for your partner.");
            if (number == 4) return physical ? L("바람이 켜졌을 때 틈을 건너세요.", "Cross when the wind is active.") : L("청록색 콘솔 위에서 바람을 유지하세요.", "Hold the cyan console to sustain wind.");
            if (number == 5) return physical ? L("자석 상자를 발판으로 사용하세요.", "Use the magnetic crate as a platform.") : L("A/D로 자석 상자를 옮기세요.", "Move the magnetic crate with A/D.");
            if (number == 6) return physical ? L("달려서 열린 속도 게이트를 통과하세요.", "Run through the open speed gate.") : L("콘솔을 밟아 게이트를 여세요.", "Stand on the console to open the gate.");
            if (number == 7) return physical ? L("회전 다리가 수평일 때 건너세요.", "Cross when the bridge is level.") : L("A/D로 회전 다리 각도를 맞추세요.", "Use A/D to set the bridge angle.");
            if (number == 8) return physical ? L("로직이 문을 여는 동안 깃발로 가세요.", "Reach the flag while the gate is open.") : L("스위치를 밟은 채로 파트너를 기다리세요.", "Hold the switch while your partner crosses.");
            return physical ? L("피지컬: 깃발까지 가세요. 로직 플레이어가 문을 열어야 합니다.", "PHYSICAL: Reach the flag. Your partner opens the gate.") : L("로직: 청록색 스위치 위에 서서 파트너의 문을 여세요.", "LOGIC: Stand on the cyan switch to open your partner's gate.");
        }
        string LocalizedStatus(string value)
        {
            if (language == GameLanguage.English || string.IsNullOrEmpty(value)) return value;
            if (value == "Choose how to connect.") return "접속 방식을 선택하세요.";
            if (value == "Starting a local room...") return "이 PC에서 방을 여는 중입니다...";
            if (value.StartsWith("Room open.")) return "방이 열렸습니다. 상대방의 참가를 기다리는 중입니다.";
            if (value.StartsWith("Connected.")) return "연결되었습니다. 역할을 선택하세요.";
            if (value.StartsWith("Connecting to ")) return "접속 중: " + value.Substring("Connecting to ".Length);
            if (value.StartsWith("Connection timed out")) return "접속 시간이 초과되었습니다. 호스트 IP와 방화벽을 확인하세요.";
            if (value.StartsWith("Network unavailable")) return "네트워크를 사용할 수 없습니다. IP와 방화벽을 확인하세요.";
            if (value == "This room already has two players.") return "이 방에는 이미 두 명이 참가했습니다.";
            if (value == "Game versions differ. Use the same build.") return "게임 버전이 다릅니다. 같은 빌드를 사용하세요.";
            return value;
        }
    }
}
