using UnityEngine;

namespace TwoInPot
{
    public static class CircuitRules
    {
        public static int Family(int room) => (room - 1) % 3;
        public static int Length(int room) => room < 4 ? 2 : room < 9 ? 3 : 4;
        public static int Source(int room, int station, int index) => (room + station * 3 + index * (station + 1) + index * index) % 4;
        public static int Answer(int room, int station, int index) => Family(room) == 1 ? Source(room, station, Length(room) - 1 - index) : (Source(room, station, index) + 1 + station % 2) % 4;
        public static int Toggle(int button) => (1 << button) | (button > 0 ? 1 << (button - 1) : 0) | (button < 3 ? 1 << (button + 1) : 0);
        public static int Target(int room, int station)
        {
            int presses = (room * 5 + station * 3) % 15 + 1, result = 0;
            for (int i = 0; i < 4; i++) if ((presses & (1 << i)) != 0) result ^= Toggle(i);
            return result;
        }
        public static string Lamps(int bits) => ((bits & 1) != 0 ? "●" : "○") + " " + ((bits & 2) != 0 ? "●" : "○") + " " + ((bits & 4) != 0 ? "●" : "○") + " " + ((bits & 8) != 0 ? "●" : "○");
        public static string Hint(int room, int station)
        {
            if (Family(room) == 2) return Lamps(Target(room, station));
            string result = ""; for (int i = 0; i < Length(room); i++) result += (i > 0 ? "  " : "") + (Source(room, station, i) + 1);
            return result;
        }
    }

    public sealed class StageCircuit : MonoBehaviour
    {
        public Transform[] gates;
        public SpriteRenderer[] markers;
        StageState stage;
        CoopPlayer physical;
        float nextInput;
        public static Vector2 Point(int room, int station) => new Vector2(16 + station * 8, -1.4f + ((room + station) % 3) * .65f);
        void Awake() { stage = GetComponent<StageState>(); }
        public Vector2 RespawnPoint => stage.Station.Value > 0 ? Point(stage.StageIndex.Value, Mathf.Min(2, stage.Station.Value - 1)) : new Vector2(12, -2.3f);
        public bool TickServer()
        {
            if (physical != null && (!physical.IsSpawned || physical.Role.Value != PlayerRole.Physical)) physical = null;
            if (physical == null && stage.NetworkManager.SpawnManager != null)
                foreach (var item in stage.NetworkManager.SpawnManager.SpawnedObjectsList)
                    if (item.TryGetComponent<CoopPlayer>(out var player) && player.Role.Value == PlayerRole.Physical) { physical = player; break; }
            if (physical == null) return false;
            int station = stage.Station.Value;
            if (station < 3)
            {
                if (stage.CircuitSolved.Value && Vector2.Distance(physical.transform.position, Point(stage.StageIndex.Value, station)) < 1.1f)
                {
                    stage.Station.Value++; stage.CircuitStep.Value = stage.CircuitMask.Value = 0; stage.CircuitSolved.Value = false;
                }
                return false;
            }
            return physical.transform.position.x > 35;
        }
        public void Submit(CoopPlayer sender, int button)
        {
            if (!stage.IsServer || sender.Role.Value != PlayerRole.Logic || !stage.GameStarted.Value || stage.PracticeActive.Value || stage.Complete.Value || !stage.RouteFinished.Value || stage.Station.Value >= 3 || stage.CircuitSolved.Value || button < 0 || button > 3 || Time.unscaledTime < nextInput) return;
            nextInput = Time.unscaledTime + .08f;
            int room = stage.StageIndex.Value, station = stage.Station.Value;
            if (CircuitRules.Family(room) == 2)
            {
                stage.CircuitMask.Value ^= CircuitRules.Toggle(button);
                stage.CircuitSolved.Value = stage.CircuitMask.Value == CircuitRules.Target(room, station);
            }
            else if (button == CircuitRules.Answer(room, station, stage.CircuitStep.Value))
            {
                stage.CircuitStep.Value++;
                stage.CircuitSolved.Value = stage.CircuitStep.Value == CircuitRules.Length(room);
            }
            else { stage.CircuitStep.Value = 0; stage.CircuitErrors.Value++; }
        }
        void Update()
        {
            if (gates == null || markers == null) return;
            int offset = (stage.StageIndex.Value - 1) * 3;
            for (int i = 0; i < 3; i++)
            {
                if (offset + i >= gates.Length) break;
                bool open = stage.Station.Value > i || stage.Station.Value == i && stage.CircuitSolved.Value;
                gates[offset + i].gameObject.SetActive(!open);
                markers[offset + i].color = stage.Station.Value > i ? new Color(.35f, 1, .65f) : new Color(1, .65f, .18f);
            }
        }
        public string Instruction(bool orange, bool korean)
        {
            if (!stage.RouteFinished.Value) return korean ? "먼저 원래 장치를 이용해 오른쪽 깃발까지 가세요. 그 뒤 긴 통로가 이어져요." : "Reach the first flag using the room's device. A longer route follows.";
            int station = stage.Station.Value, room = stage.StageIndex.Value;
            if (station >= 3) return korean ? "세 장치 작동! 주황색은 오른쪽 마지막 깃발로 가세요." : "All three devices are active. Orange: reach the final flag.";
            string header = (korean ? "통로 " : "STATION ") + (station + 1) + "/3 · ";
            if (orange) return header + (stage.CircuitSolved.Value ? (korean ? "문이 열렸어요. 주황 불빛에 닿아 다음 길을 여세요." : "Gate open. Touch the amber marker.") : (korean ? "현장 표식: " : "FIELD MARK: ") + CircuitRules.Hint(room, station) + (korean ? " · 파란색에게 알려주세요." : " · Tell Blue."));
            string rule = CircuitRules.Family(room) == 0 ? (korean ? "들은 숫자에 " + (1 + station % 2) + " 더하기. 4 다음은 1." : "Add " + (1 + station % 2) + " to each number. Wrap 4 to 1.") : CircuitRules.Family(room) == 1 ? (korean ? "들은 숫자를 뒤에서부터 입력하세요." : "Enter the numbers in reverse order.") : (korean ? "버튼은 자기 불과 양옆 불을 함께 바꿔요. 들은 모양을 만드세요." : "Each button flips its lamp and its neighbors. Match Orange's pattern.");
            return header + (stage.CircuitSolved.Value ? (korean ? "정답! 주황색이 표식에 닿을 때까지 기다려요." : "Solved! Wait for Orange to touch the marker.") : rule + "\n" + (CircuitRules.Family(room) == 2 ? CircuitRules.Lamps(stage.CircuitMask.Value) : (korean ? "입력 " : "ENTERED ") + stage.CircuitStep.Value + "/" + CircuitRules.Length(room)));
        }
    }
}
