using UnityEngine;

namespace TwoInPot
{
    // Evaluated only by the server. The player object replicates results to its owner only.
    internal sealed class PersonalAssignment
    {
        readonly CoopPlayer player;
        int number = -1;
        float held, travel, previousValue;
        public PersonalAssignment(CoopPlayer player) { this.player = player; }
        public void ResetAttempt() { number = -1; held = travel = 0; player.AssignmentProgress.Value = 0; }
        public void Launch(StageState stage, float power)
        {
            if (stage != null && stage.GameStarted.Value && !stage.PracticeActive.Value && !stage.Complete.Value && stage.StageIndex.Value == 2)
                Report(stage, power / (stage.CurrentDefinition.maximumLaunchPower * .7f));
        }
        void Report(StageState stage, float progress)
        {
            int bit = 1 << (stage.StageIndex.Value - 1);
            if ((player.AssignmentBits.Value & bit) != 0) { player.AssignmentProgress.Value = 100; return; }
            player.AssignmentProgress.Value = Mathf.Max(player.AssignmentProgress.Value, Mathf.Clamp(Mathf.FloorToInt(progress * 20) * 5, 0, 100));
            if (progress >= 1) player.AssignmentBits.Value |= bit;
        }
        public void Tick(StageState stage, float delta)
        {
            if (stage == null || !stage.GameStarted.Value || stage.PracticeActive.Value || stage.Complete.Value) return;
            int room = stage.StageIndex.Value;
            if (number != room) { number = room; held = travel = 0; previousValue = room == 2 ? stage.LaunchAngle.Value : stage.MechanicValue.Value; player.AssignmentProgress.Value = 0; }
            if ((player.AssignmentBits.Value & (1 << (room - 1))) != 0) { player.AssignmentProgress.Value = 100; return; }
            float progress = 0;
            if (player.Role.Value == PlayerRole.Physical)
            {
                switch (room)
                {
                    case 2: return; // A successful launch, rather than charge without releasing.
                    case 3: case 7: case 11:
                        held = player.ToolGripped ? held + delta : 0; progress = held / .6f; break;
                    case 4:
                        held += !player.IsGrounded && player.HorizontalSpeed > 2 && stage.MechanicValue.Value > .2f ? delta : 0; progress = held / .6f; break;
                    case 6: progress = player.HorizontalSpeed / 6; break;
                    case 9: progress = (player.transform.position.y - stage.CurrentDefinition.physicalSpawn.y) / 1.2f; break;
                    default: progress = player.PracticeJumpCount / 2f; break;
                }
            }
            else
            {
                float value = room == 2 ? stage.LaunchAngle.Value : stage.MechanicValue.Value;
                travel += Mathf.Abs(value - previousValue); previousValue = value;
                switch (room)
                {
                    case 2: progress = travel / 12; break;
                    case 3: case 5: progress = travel / .6f; break;
                    case 7: case 11:
                        held = Mathf.Abs(value) < .18f ? held + delta : 0; progress = held / .6f; break;
                    case 4:
                        held = value >= .75f ? held + delta : 0; progress = held / .8f; break;
                    case 9: progress = value / .75f; break;
                    case 12:
                        held = stage.RelayStep.Value == 2 && stage.GateOpen.Value ? held + delta : 0; progress = held; break;
                    default:
                        held = stage.GateOpen.Value ? held + delta : 0; progress = held / 1.5f; break;
                }
            }
            Report(stage, progress);
        }
    }

    public static class AssignmentText
    {
        public static int Count(int bits) { int count = 0; for (int i = 0; i < 12; i++) if ((bits & (1 << i)) != 0) count++; return count; }
        public static string Request(int room, bool physical, bool korean)
        {
            if (physical) switch (room)
            {
                case 2: return korean ? "발사 힘을 70% 이상 모아 날아가 줘." : "Launch with at least 70% power.";
                case 3: case 7: case 11: return korean ? "망치로 벽을 0.6초 붙잡아 줘." : "Brace the hammer against a wall for 0.6 seconds.";
                case 4: return korean ? "바람을 타고 공중에서 0.6초 이동해 줘." : "Ride the wind in the air for 0.6 seconds.";
                case 6: return korean ? "SPACE를 길게 눌렀다 떼서 빠르게 밀어 줘." : "Hold SPACE, then release for a fast push.";
                case 9: return korean ? "승강기를 타고 위로 올라가 줘." : "Ride the lift upward.";
                default: return korean ? "망치로 바닥이나 벽을 두 번 밀어 줘." : "Push off a surface twice with the hammer.";
            }
            switch (room)
            {
                case 2: return korean ? "발사 각도를 합계 12도 이상 조정할 것." : "Adjust the launch angle by 12 degrees in total.";
                case 3: case 5: return korean ? "A/D를 눌러 장치를 옮겨 줘." : "Hold A/D to move the device.";
                case 7: case 11: return korean ? "다리를 평평하게 맞추고 잠깐 기다려 줘." : "Make the bridge level and wait a moment.";
                case 4: return korean ? "바람을 세게 켜고 잠깐 기다려 줘." : "Keep the fan blowing strongly for a moment.";
                case 9: return korean ? "승강기를 위쪽까지 올려 줘." : "Raise the lift near the top.";
                case 12: return korean ? "스위치 두 개를 밟고 마지막 문을 잠깐 열어 줘." : "Step on both switches and hold the last gate open briefly.";
                default: return korean ? "문을 열고 잠깐 기다려 줘." : "Keep the gate open for a moment.";
            }
        }
        public static string Sender(int room, bool physical, bool korean)
        {
            if (physical) return korean ? room <= 3 ? "접힌 메모 · 남겨도 괜찮아." : room <= 6 ? "접힌 메모 · 여기 왔었다는 표시는 남겨 줘." : room <= 9 ? "접힌 메모 · 지워져도 손은 기억할 거야." : "접힌 메모 · 이번에는 빈칸으로 두지 말아 줘." : "FOLDED NOTE · Leave a sign that you were here.";
            return korean ? room <= 3 ? "업무 수신 · 점검 후 확인." : room <= 6 ? "업무 수신 · 이전 확인자는 파란 도장." : room <= 9 ? "업무 수신 · 삭제 승인 이력과 서명이 일치함." : "업무 수신 · 마지막 확인란은 아직 비어 있음." : "WORK ORDER · The previous approval bears a blue stamp.";
        }
        public static string Ending(int bits, bool physical, bool korean)
        {
            int count = Count(bits);
            if (count < 4) return korean ? physical ? "빈 봉투\n문은 열렸다. 주머니의 메모에는 아직 접힌 자국만 남아 있었다." : "미확인 인계\n문은 열렸다. 파란 서명 아래, 확인하지 않은 칸들이 남았다." : "UNFINISHED HANDOVER\nThe door opened. Empty boxes remained beneath the signature.";
            if (count < 8) return korean ? physical ? "남은 자국\n문은 열렸다. 사라진 이름 대신, 몇 개의 망치 자국이 복도에 남았다." : "접힌 인계서\n문은 열렸다. 파란 손은 확인한 칸이 보이도록 인계서를 접어 두었다." : "MARKS LEFT BEHIND\nThe door opened. A few marks survived in the corridor.";
            return korean ? physical ? "돌아올 자리\n문은 열렸다. 빈 이름표 뒤에 메모를 끼웠다. 다음에 올 사람도 찾을 수 있도록." : "남겨 둔 인계서\n문은 열렸다. 파란 손은 점검한 인계서를 반환대에 두었다. 그날의 기록은 지워진 채였다." : "A PLACE TO RETURN\nThe door opened. The completed note was left where someone else could find it.";
        }
    }
}
