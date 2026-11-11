using UnityEngine;

namespace TwoInPot
{
    // Optional discoveries are server-authoritative, cosmetic room changes are local.
    public sealed class FacilityStory : MonoBehaviour
    {
        public SpriteRenderer backdrop;
        public SpriteRenderer[] memoryPads;
        public SpriteRenderer[] traces;
        StageState stage;
        CoopPlayer physical, logic;
        float together;
        int shownStage = -1;
        int shownEnding = -1;
        bool wasPhysicalNear, wasLogicNear;
        public int RecordIndex => stage != null && stage.StageIndex.Value % 3 == 0 ? stage.StageIndex.Value / 3 - 1 : -1;
        public Vector2 PhysicalPad => stage.CurrentDefinition.physicalSpawn + Vector2.right * 2;
        public Vector2 LogicPad => stage.CurrentDefinition.logicSpawn;
        public bool HasNearbySignal(CoopPlayer player)
        {
            if (player == null || RecordIndex < 0 || (stage.MemoryBits.Value & (1 << RecordIndex)) != 0) return false;
            return Vector2.Distance(player.transform.position, player.Role.Value == PlayerRole.Physical ? PhysicalPad : LogicPad) < 2.4f;
        }
        public bool HasReadableFragment(CoopPlayer player)
        {
            return player != null && RecordIndex >= 0 && (stage.MemoryBits.Value & (1 << RecordIndex)) != 0 &&
                Vector2.Distance(player.transform.position, player.Role.Value == PlayerRole.Physical ? PhysicalPad : LogicPad) < 2.4f;
        }
        void CachePlayers()
        {
            if (physical != null && logic != null || stage.NetworkManager == null || stage.NetworkManager.SpawnManager == null) return;
            foreach (var item in stage.NetworkManager.SpawnManager.SpawnedObjectsList)
                if (item.TryGetComponent<CoopPlayer>(out var player))
                { if (player.Role.Value == PlayerRole.Physical) physical = player; else logic = player; }
        }
        void Awake() { stage = GetComponent<StageState>(); }
        void Update()
        {
            if (stage == null) return;
            int number = stage.StageIndex.Value;
            if (shownEnding != stage.EndingCode.Value)
            {
                shownEnding = stage.EndingCode.Value;
                if (shownEnding == 2 && backdrop != null) backdrop.color = new Color(.8f, .9f, 1);
            }
            if (shownStage != number)
            {
                shownStage = number; together = 0; wasPhysicalNear = wasLogicNear = false;
                Color[] shades = { Color.white, new Color(.76f, .82f, 1), new Color(.8f, .64f, .57f), new Color(.46f, .52f, .68f) };
                if (backdrop != null) backdrop.color = shades[(number - 1) / 3];
                if (memoryPads != null && memoryPads.Length == 2)
                {
                    memoryPads[0].transform.position = (Vector3)PhysicalPad + new Vector3(0, -.55f, -.1f);
                    memoryPads[1].transform.position = (Vector3)LogicPad + new Vector3(0, -.55f, -.1f);
                }
                if (traces != null)
                    for (int i = 0; i < traces.Length; i++) traces[i].enabled = number >= 4 + i * 2;
            }
            int index = RecordIndex;
            if (stage.GameStarted.Value) CachePlayers();
            bool discovered = index >= 0 && (stage.MemoryBits.Value & (1 << index)) != 0;
            bool visible = index >= 0 && stage.GameStarted.Value && !stage.PracticeActive.Value && !stage.Complete.Value;
            if (memoryPads != null)
                for (int i = 0; i < memoryPads.Length; i++)
                {
                    memoryPads[i].enabled = visible;
                    var occupant = i == 0 ? physical : logic;
                    Vector2 position = i == 0 ? PhysicalPad : LogicPad;
                    bool nearby = occupant != null && Vector2.Distance(occupant.transform.position, position) < .95f;
                    bool wasNear = i == 0 ? wasPhysicalNear : wasLogicNear;
                    if (visible && !discovered && nearby && !wasNear) FestivalSfx.Play(FestivalSfx.Cue.Select);
                    if (i == 0) wasPhysicalNear = nearby; else wasLogicNear = nearby;
                    Color color = discovered ? new Color(.64f, .76f, .74f) : i == 0 ? new Color(1, .55f, .15f) : new Color(.3f, .8f, 1);
                    color.a = discovered ? .5f : nearby ? .9f : .42f;
                    memoryPads[i].color = color;
                }
        }
        void FixedUpdate()
        {
            if (stage == null || !stage.IsServer || !stage.GameStarted.Value || stage.PracticeActive.Value || stage.Complete.Value || RecordIndex < 0) { together = 0; return; }
            int bit = 1 << RecordIndex;
            if ((stage.MemoryBits.Value & bit) != 0) return;
            CachePlayers();
            bool both = physical != null && logic != null && Vector2.Distance(physical.transform.position, PhysicalPad) < .95f && Vector2.Distance(logic.transform.position, LogicPad) < .95f;
            together = both ? together + Time.fixedDeltaTime : 0;
            if (together >= 1.2f) { stage.MemoryBits.Value |= bit; together = 0; }
        }
        public static string Fragment(int index, bool korean)
        {
            switch (index)
            {
                case 0: return korean ? "낡은 안내문 · '훈련이 끝나면 이름표를 돌려주세요.' 파란 손자국 위에 새 종이가 붙어 있다." : "OLD NOTICE · 'Return your name tag after training.' Fresh paper covers a blue handprint.";
                case 1: return korean ? "접힌 사진 · 사진 속 두 사람은 우리와 닮았다. '관리자'라는 글자만 파란 쪽에 남아 있다." : "FOLDED PHOTO · The two people look like us. Only the blue one's label still says 'OPERATOR'.";
                case 2: return korean ? "작업 지시 · '사람은 내보내고, 기록은 지운다.' 허락한 자리에는 파란 손도장이 찍혀 있다." : "WORK NOTE · 'Let the people out. Erase the records.' A blue handprint marks the approval.";
                default: return korean ? "마지막 쪽지 · '고장이 아니다. 그대로 둘 것.' 지우기 버튼 아래 같은 파란 손자국이 겹쳐 있다." : "LAST NOTE · 'Not a fault. Leave it this way.' The same blue handprint repeats beneath ERASE.";
            }
        }
        public static string Ending(int code, bool korean)
        {
            if (code == 2) return korean ? "남겨 둔 이름\n파란 손이 지우기 버튼에서 떨어졌다. 비어 있던 이름들이 화면에 돌아왔다. 두 사람은 잠시 더 그 앞에 서 있었다." : "NAMES LEFT BEHIND\nThe blue hand left the erase button. Names returned to the screen. The two people stayed a little longer.";
            return korean ? "퇴실 승인\n문이 열렸다. 파란 손은 잠깐 멈췄지만, 뒤쪽 화면은 다시 비워졌다." : "DEPARTURE APPROVED\nThe door opened. The blue hand paused, but the screen behind them went blank again.";
        }
    }
}
