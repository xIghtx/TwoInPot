using Unity.Netcode;
using UnityEngine;

namespace TwoInPot
{
    // Server-owned state is replicated to late joiners as well as existing peers.
    public sealed class StageState : NetworkBehaviour
    {
        public readonly NetworkVariable<int> StageIndex = new NetworkVariable<int>(1);
        public readonly NetworkVariable<bool> GateOpen = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> Complete = new NetworkVariable<bool>();
        public readonly NetworkVariable<float> LaunchAngle = new NetworkVariable<float>(45f);
        public readonly NetworkVariable<float> LaunchPower = new NetworkVariable<float>();
        public readonly NetworkVariable<float> MechanicValue = new NetworkVariable<float>();
        public readonly NetworkVariable<bool> GameStarted = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> PracticeActive = new NetworkVariable<bool>(true);
        public readonly NetworkVariable<int> RelayStep = new NetworkVariable<int>();
        public readonly NetworkVariable<int> HostRoleChoice = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<int> GuestRoleChoice = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<bool> HostAccepted = new NetworkVariable<bool>();
        public readonly NetworkVariable<bool> GuestAccepted = new NetworkVariable<bool>();
        public StageDefinition stageOne;
        public StageDefinition stageTwo;
        public GameObject stageOneRoot;
        public GameObject stageTwoRoot;
        public StageDefinition[] additionalStages;
        public GameObject[] additionalStageRoots;
        public Transform[] logicTargets;
        public Transform[] exits;
        public GameObject[] puzzleGates;
        public Transform[] puzzleMechanisms;
        public Transform logicSwitch;
        public GameObject gate;
        public SpriteRenderer switchVisual;
        SpriteRenderer aimShaft, aimHead;
        CoopPlayer aimPlayer;
        public int TotalStages => 2 + (additionalStages == null ? 0 : additionalStages.Length);
        float gateSeconds;
        float StartingMechanicValue => StageIndex.Value == 7 || StageIndex.Value == 11 ? -.85f : 0;
        public StageDefinition CurrentDefinition
        {
            get
            {
                if (StageIndex.Value == 1) return stageOne;
                if (StageIndex.Value == 2) return stageTwo;
                int index = StageIndex.Value - 3;
                return additionalStages != null && index >= 0 && index < additionalStages.Length ? additionalStages[index] : stageOne;
            }
        }
        public override void OnNetworkSpawn()
        {
            GateOpen.OnValueChanged += Changed;
            StageIndex.OnValueChanged += StageChanged;
            Apply();
        }
        public override void OnNetworkDespawn() { GateOpen.OnValueChanged -= Changed; StageIndex.OnValueChanged -= StageChanged; gate.SetActive(true); }
        void Changed(bool before, bool after) => Apply();
        void StageChanged(int before, int after) => Apply();
        void Apply()
        {
            bool firstStage = StageIndex.Value == 1;
            if (stageOneRoot != null) stageOneRoot.SetActive(firstStage);
            if (stageTwoRoot != null) stageTwoRoot.SetActive(StageIndex.Value == 2);
            if (additionalStageRoots != null)
                for (int i = 0; i < additionalStageRoots.Length; i++) if (additionalStageRoots[i] != null) additionalStageRoots[i].SetActive(StageIndex.Value == i + 3);
            if (gate != null) gate.SetActive(firstStage && !GateOpen.Value);
            if (switchVisual != null) switchVisual.color = GateOpen.Value ? new Color(.4f, 1f, .7f) : new Color(.25f, .55f, .57f);
            SetAimVisible(StageIndex.Value == 2);
        }
        void Update()
        {
            if (StageIndex.Value >= 3) { ApplyPuzzleVisuals(); return; }
            if (StageIndex.Value != 2) return;
            EnsureAim();
            if (aimShaft == null) return;
            if (aimPlayer == null && NetworkManager != null && NetworkManager.SpawnManager != null)
                foreach (var item in NetworkManager.SpawnManager.SpawnedObjectsList)
                    if (item.TryGetComponent<CoopPlayer>(out var player) && player.Role.Value == PlayerRole.Physical) { aimPlayer = player; break; }
            Vector2 origin = aimPlayer != null ? aimPlayer.transform.position : stageTwo.physicalSpawn;
            Vector2 direction = MotionMath.LaunchVelocity(LaunchAngle.Value, 1).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float previewPower = Mathf.Max(stageTwo.minimumLaunchPower, LaunchPower.Value);
            float arrowLength = Mathf.Lerp(1.6f, 4.8f, Mathf.InverseLerp(stageTwo.minimumLaunchPower, stageTwo.maximumLaunchPower, previewPower));
            SetAimPart(aimShaft, origin + direction * (arrowLength * .5f), angle, new Vector2(arrowLength, .10f));
            Vector2 tip = origin + direction * arrowLength;
            SetAimPart(aimHead, tip - direction * .06f, angle, new Vector2(.30f, .30f));
        }
        void EnsureAim()
        {
            if (aimShaft != null || stageTwoRoot == null) return;
            var template = stageTwoRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (template == null) return;
            aimShaft = CreateAimPart("LaunchAimArrow", template);
            aimHead = CreateAimPart("LaunchAimArrowHead", template);
            aimHead.sprite = CreateArrowHeadSprite();
        }
        Sprite CreateArrowHeadSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float halfHeight = x * .48f;
                texture.SetPixel(x, y, Mathf.Abs(y - 15.5f) <= halfHeight ? Color.white : Color.clear);
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
        }
        SpriteRenderer CreateAimPart(string name, SpriteRenderer template)
        {
            var go = new GameObject(name); go.transform.SetParent(stageTwoRoot.transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = template.sprite; sprite.sharedMaterial = template.sharedMaterial;
            sprite.color = new Color(.32f, .83f, .85f); sprite.sortingOrder = 10;
            return sprite;
        }
        void SetAimPart(SpriteRenderer part, Vector2 position, float rotation, Vector2 scale)
        {
            part.transform.position = position; part.transform.rotation = Quaternion.Euler(0, 0, rotation); part.transform.localScale = scale;
        }
        void SetAimVisible(bool visible)
        {
            if (aimShaft == null) return;
            aimShaft.enabled = visible; aimHead.enabled = visible;
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (NetworkManager.ConnectedClients.Count < 2)
            {
                GameStarted.Value = false; HostRoleChoice.Value = -1; GuestRoleChoice.Value = -1;
                HostAccepted.Value = false; GuestAccepted.Value = false;
                return;
            }
            if (!GameStarted.Value) return;
            if (Complete.Value) return;
            if (StageIndex.Value == 2) { UpdateLaunchStage(); return; }
            if (StageIndex.Value >= 3) { UpdatePuzzleStage(); return; }
            bool onSwitch = false, physicalAtExit = false;
            CoopPlayer physicalPlayer = null, logicPlayer = null;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var player = client.PlayerObject.GetComponent<CoopPlayer>();
                if (player.Role.Value == PlayerRole.Logic)
                { logicPlayer = player; onSwitch = Vector2.Distance(player.transform.position, logicSwitch.position) < .7f; }
                else { physicalPlayer = player; physicalAtExit = player.transform.position.x > 9f; }
            }
            GateOpen.Value = onSwitch && NetworkManager.ConnectedClients.Count == 2;
            if (PracticeActive.Value)
            {
                if (onSwitch && physicalPlayer != null && logicPlayer != null && physicalPlayer.PracticeJumpCount > 0)
                    BeginStageObjective();
                return;
            }
            if (GateOpen.Value && physicalAtExit)
            {
                Complete.Value = true;
                AdvanceStage();
            }
        }
        public void ChooseRole(ulong clientId, PlayerRole role)
        {
            if (!IsServer || NetworkManager.ConnectedClients.Count != 2) return;
            if (clientId == NetworkManager.ServerClientId) HostRoleChoice.Value = (int)role;
            else GuestRoleChoice.Value = (int)role;
            HostAccepted.Value = false; GuestAccepted.Value = false;
        }
        public void AcceptRole(ulong clientId)
        {
            if (!IsServer || HostRoleChoice.Value < 0 || GuestRoleChoice.Value < 0 || HostRoleChoice.Value == GuestRoleChoice.Value) return;
            if (clientId == NetworkManager.ServerClientId) HostAccepted.Value = true; else GuestAccepted.Value = true;
            if (!HostAccepted.Value || !GuestAccepted.Value) return;
            var host = NetworkManager.ConnectedClients[NetworkManager.ServerClientId].PlayerObject.GetComponent<CoopPlayer>();
            foreach (var client in NetworkManager.ConnectedClientsList)
                if (client.ClientId != NetworkManager.ServerClientId) { var guest = client.PlayerObject.GetComponent<CoopPlayer>(); host.AssignRole((PlayerRole)HostRoleChoice.Value); guest.AssignRole((PlayerRole)GuestRoleChoice.Value); break; }
            GameStarted.Value = true;
        }
        void UpdateLaunchStage()
        {
            CoopPlayer physical = null, logic = null;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var player = client.PlayerObject.GetComponent<CoopPlayer>();
                if (player.Role.Value == PlayerRole.Physical) physical = player; else logic = player;
            }
            if (physical == null || logic == null) return;
            var definition = stageTwo;
            // In screen space, a lower angle points farther right.  D/right
            // therefore rotates the arrow right, while A/left rotates it up.
            LaunchAngle.Value = Mathf.Clamp(LaunchAngle.Value - logic.LaunchAimInput * definition.aimSpeed * Time.fixedDeltaTime,
                definition.minimumLaunchAngle, definition.maximumLaunchAngle);
            if (physical.IsGrounded && physical.LaunchHeld)
                LaunchPower.Value = Mathf.Min(definition.maximumLaunchPower, LaunchPower.Value + definition.chargeSpeed * Time.fixedDeltaTime);
            if (physical.ConsumeLaunchRelease())
            {
                if (physical.IsGrounded)
                    physical.Launch(LaunchAngle.Value, Mathf.Max(definition.minimumLaunchPower, LaunchPower.Value));
                LaunchPower.Value = 0;
            }
            if (PracticeActive.Value)
            {
                if (logic.PracticeMoveCount > 0 && physical.PracticeLaunchCount > 0) BeginStageObjective();
                return;
            }
            if (physical.transform.position.x > 10f && physical.transform.position.y > -3.2f)
            {
                Complete.Value = true;
                AdvanceStage();
            }
        }
        void UpdatePuzzleStage()
        {
            int index = StageIndex.Value - 3;
            if (index < 0 || logicTargets == null || index >= logicTargets.Length) return;
            CoopPlayer physical = null, logic = null;
            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var player = client.PlayerObject.GetComponent<CoopPlayer>();
                if (player.Role.Value == PlayerRole.Physical) physical = player; else logic = player;
            }
            if (physical == null || logic == null) return;
            bool logicAtTarget = logicTargets[index] != null && Vector2.Distance(logic.transform.position, logicTargets[index].position) < .8f;
            float input = logic.LaunchAimInput;
            switch (StageIndex.Value)
            {
                case 3: // moving platform
                case 5: // magnetic crate
                case 7: // rotating bridge
                case 11: // counterweight bridge
                    MechanicValue.Value = Mathf.Clamp(MechanicValue.Value + input * Time.fixedDeltaTime, -1f, 1f);
                    break;
                case 9: // lift is controlled independently of the operator's walking.
                    MechanicValue.Value = Mathf.Clamp01(MechanicValue.Value + input * .65f * Time.fixedDeltaTime);
                    break;
                case 10: // each press grants a fresh crossing window.
                    if (logic.ConsumeMechanicPress()) gateSeconds = 3.5f;
                    gateSeconds = Mathf.Max(0, gateSeconds - Time.fixedDeltaTime);
                    MechanicValue.Value = gateSeconds / 3.5f; GateOpen.Value = gateSeconds > 0;
                    break;
                case 12: // first console latches; the second must be held.
                    if (logicAtTarget && RelayStep.Value == 0) RelayStep.Value = 1;
                    bool secondConsole = Vector2.Distance(logic.transform.position, new Vector2(3, -2.7f)) < .8f;
                    if (RelayStep.Value >= 1 && secondConsole) RelayStep.Value = 2;
                    GateOpen.Value = RelayStep.Value >= 1 && secondConsole;
                    break;
                case 4: // wind turns on only while the logic player holds its console.
                    MechanicValue.Value = logicAtTarget ? Mathf.Clamp01(MechanicValue.Value + .8f * Time.fixedDeltaTime) : Mathf.Max(0, MechanicValue.Value - .8f * Time.fixedDeltaTime);
                    if (MechanicValue.Value > .05f) physical.AddHorizontalImpulse(MechanicValue.Value * 14f * Time.fixedDeltaTime);
                    break;
                case 6: // speed gate is opened by the logic player.
                    GateOpen.Value = logicAtTarget;
                    break;
                case 8: // final switch must be held while the physical player reaches the exit.
                    GateOpen.Value = logicAtTarget;
                    break;
            }
            ApplyPuzzleVisuals();
            if (PracticeActive.Value)
            {
                bool mechanicTried = StageIndex.Value switch
                {
                    3 or 5 or 7 or 9 or 11 => logic.PracticeMoveCount > 0 && Mathf.Abs(MechanicValue.Value - StartingMechanicValue) > .05f,
                    4 => logicAtTarget && MechanicValue.Value > .05f,
                    6 or 8 => GateOpen.Value,
                    10 => GateOpen.Value,
                    12 => RelayStep.Value >= 1,
                    _ => false
                };
                if (mechanicTried && physical.PracticeJumpCount > 0) BeginStageObjective();
                return;
            }
            bool reachedExit = exits != null && index < exits.Length && exits[index] != null && physical.transform.position.x > exits[index].position.x && physical.transform.position.y >= exits[index].position.y - .8f;
            bool passed = StageIndex.Value == 6 ? reachedExit && GateOpen.Value && physical.HorizontalSpeed > 5f :
                          StageIndex.Value == 8 || StageIndex.Value == 10 || StageIndex.Value == 12 ? reachedExit && GateOpen.Value : reachedExit;
            if (!passed) return;
            Complete.Value = true;
            if (StageIndex.Value < TotalStages) AdvanceStage();
        }
        void ApplyPuzzleVisuals()
        {
            int index = StageIndex.Value - 3;
            if (index < 0 || puzzleMechanisms == null || index >= puzzleMechanisms.Length) return;
            var mechanism = puzzleMechanisms[index];
            if (mechanism != null) switch (StageIndex.Value)
            {
                case 3: mechanism.localPosition = new Vector3(MechanicValue.Value * 5f, 0, 0); break;
                case 4: mechanism.localScale = new Vector3(6 * Mathf.Max(.15f, MechanicValue.Value), 2.4f, 1); break;
                case 5: mechanism.localPosition = new Vector3(MechanicValue.Value * 5f, -2.35f, 0); break;
                case 7: mechanism.localRotation = Quaternion.Euler(0, 0, MechanicValue.Value * 32f); break;
                case 9: mechanism.localPosition = new Vector3(0, -3.1f + MechanicValue.Value * 4.1f, 0); break;
                case 11: mechanism.localRotation = Quaternion.Euler(0, 0, MechanicValue.Value * 24f); break;
                case 12: mechanism.gameObject.SetActive(!GateOpen.Value); break;
            }
            if (puzzleGates != null && index < puzzleGates.Length && puzzleGates[index] != null)
                puzzleGates[index].SetActive(StageIndex.Value == 12 ? RelayStep.Value == 0 : !GateOpen.Value);
        }
        void AdvanceStage()
        {
            if (!IsServer || StageIndex.Value >= TotalStages) return;
            StageIndex.Value++;
            PracticeActive.Value = true;
            RelayStep.Value = 0; gateSeconds = 0;
            GateOpen.Value = false; Complete.Value = false; MechanicValue.Value = StartingMechanicValue;
            LaunchPower.Value = 0;
            if (StageIndex.Value == 2) LaunchAngle.Value = stageTwo.startingLaunchAngle;
            foreach (var client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject != null) { var player = client.PlayerObject.GetComponent<CoopPlayer>(); player.ResetPracticeActions(); player.Respawn(); }
            Apply(); ApplyPuzzleVisuals();
        }
        void BeginStageObjective()
        {
            PracticeActive.Value = false;
            RelayStep.Value = 0; gateSeconds = 0;
            GateOpen.Value = false; Complete.Value = false; MechanicValue.Value = StartingMechanicValue; LaunchPower.Value = 0;
            if (StageIndex.Value == 2) LaunchAngle.Value = stageTwo.startingLaunchAngle;
            foreach (var client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject != null) { var player = client.PlayerObject.GetComponent<CoopPlayer>(); player.ResetPracticeActions(); player.Respawn(); }
            Apply(); ApplyPuzzleVisuals();
        }
        public void BeginNextStage()
        {
            if (!IsServer || !Complete.Value) return;
            AdvanceStage();
        }
        public void ResetStage()
        {
            if (!IsServer) return;
            Complete.Value = false;
            PracticeActive.Value = true;
            RelayStep.Value = 0; gateSeconds = 0;
            LaunchPower.Value = 0; MechanicValue.Value = StartingMechanicValue; GateOpen.Value = false;
            if (StageIndex.Value == 2) LaunchAngle.Value = stageTwo.startingLaunchAngle;
            foreach (var client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject != null) { var player = client.PlayerObject.GetComponent<CoopPlayer>(); player.ResetPracticeActions(); player.Respawn(); }
            Apply(); ApplyPuzzleVisuals();
        }
        public void RestartFromBeginning()
        {
            if (!IsServer) return;
            StageIndex.Value = 1; GateOpen.Value = false; Complete.Value = false;
            PracticeActive.Value = true;
            RelayStep.Value = 0; gateSeconds = 0;
            LaunchAngle.Value = stageTwo.startingLaunchAngle; LaunchPower.Value = 0; MechanicValue.Value = 0;
            foreach (var client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject != null) { var player = client.PlayerObject.GetComponent<CoopPlayer>(); player.ResetPracticeActions(); player.Respawn(); }
        }
    }
}
