using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwoInPot
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody2D))]
    public sealed class CoopPlayer : NetworkBehaviour
    {
        static readonly System.Collections.Generic.HashSet<CoopPlayer> activePlayers = new System.Collections.Generic.HashSet<CoopPlayer>();
        public static int ActivePlayerCount => activePlayers.Count;
        public static CoopPlayer OwnedPlayer { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { activePlayers.Clear(); OwnedPlayer = null; }
        public StageDefinition stage;
        public SpriteRenderer bodyVisual;
        public readonly NetworkVariable<PlayerRole> Role = new NetworkVariable<PlayerRole>();
        public readonly NetworkVariable<float> PushCharge = new NetworkVariable<float>();
        float chargeStarted, releasedStrength = .65f;
        bool charging;
        public readonly NetworkVariable<int> AssignmentBits = new NetworkVariable<int>(0, NetworkVariableReadPermission.Owner);
        public readonly NetworkVariable<int> AssignmentProgress = new NetworkVariable<int>(0, NetworkVariableReadPermission.Owner);
        PersonalAssignment assignment;
        readonly NetworkVariable<Vector2> ServerPosition = new NetworkVariable<Vector2>();
        readonly NetworkVariable<Vector2> VisualMotion = new NetworkVariable<Vector2>();
        readonly NetworkVariable<bool> OnGround = new NetworkVariable<bool>();
        readonly NetworkVariable<float> HammerAngle = new NetworkVariable<float>(-135);
        readonly NetworkVariable<bool> HammerContact = new NetworkVariable<bool>();
        readonly NetworkVariable<bool> HammerGripped = new NetworkVariable<bool>();
        readonly NetworkVariable<Vector2> HammerAnchor = new NetworkVariable<Vector2>();
        Rigidbody2D body;
        BoxCollider2D shape;
        float axis, receivedAt, nextSend, lastJump;
        float predictedX;
        float predictedMove;
        bool predictionInitialized;
        Vector2 authoritativePosition;
        bool launchHeld, launchReleased;
        float launchAimInput;
        float jumpBufferedUntil = -1, lastGroundedAt = -1;
        bool mechanicPressed;
        float windDrift;
        Transform support;
        Vector3 supportLocalPoint, supportWorldPoint;
        int practiceMoveCount, practiceJumpCount, practiceLaunchCount;
        Transform hammerSurface;
        Vector2 hammerLocalAnchor;
        float lastHammerPush = -1;
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[8];
        public static bool InputBlocked;
        StageState stageState;
        public float LaunchAimInput => launchAimInput;
        public bool LaunchHeld => launchHeld;
        public Vector2 PresentationVelocity => IsServer ? body.linearVelocity : VisualMotion.Value;
        public bool PresentationGrounded => OnGround.Value;
        public float PresentationHammerAngle => HammerAngle.Value;
        public bool ToolInContact => HammerContact.Value;
        public bool ToolGripped => HammerGripped.Value;
        public Vector2 ToolAnchor => HammerAnchor.Value;
        public const float HammerReach = 1.8f;
        public static readonly Vector2 HammerShoulder = new Vector2(0, .45f);
        public static Vector2 PushVelocity(Vector2 away, bool floor, float strength) => new Vector2(away.x * 11 * strength, Mathf.Max(floor ? 13.5f : 5, away.y * 15) * strength);
        public bool ConsumeMechanicPress() { bool pressed = mechanicPressed; mechanicPressed = false; return pressed; }
        public int PracticeMoveCount => practiceMoveCount;
        public int PracticeJumpCount => practiceJumpCount;
        public int PracticeLaunchCount => practiceLaunchCount;
        public bool ConsumeLaunchRelease() { bool result = launchReleased; launchReleased = false; return result; }
        public bool IsGrounded => Grounded();
        public float HorizontalSpeed => body == null ? 0 : Mathf.Abs(body.linearVelocity.x);
        bool OperatesMechanism => Role.Value == PlayerRole.Logic && stageState != null &&
            (stageState.StageIndex.Value == 3 || stageState.StageIndex.Value == 5 || stageState.StageIndex.Value == 7 || stageState.StageIndex.Value == 9 || stageState.StageIndex.Value == 11);
        StageDefinition CurrentStage => stageState != null ? stageState.CurrentDefinition : stage;
        bool LaunchMode => CurrentStage != null && CurrentStage.movementMode == MovementMode.Launch && (stageState == null || !stageState.RouteFinished.Value);
        int circuitSelection;
        public int CircuitSelection => circuitSelection;
#if UNITY_EDITOR || DEBUG
        public static bool TestInput;
        public static bool ProbeControlled;
        internal void ProbeInput(float move = 0, bool jump = false, bool hold = false, bool release = false)
        {
            ApplyInput(move, jump, hold, release);
            // A probe jump without hold represents a complete short key tap.
            if (jump && !hold && Role.Value == PlayerRole.Physical && CurrentStage.movementMode != MovementMode.Launch) ApplyInput(move, false, false, true);
        }
        internal void ProbeSubmitInput(float move) => SubmitInputRpc(move, false, false, false);
        internal void ProbeCircuitSubmit(int button) => SubmitCircuitRpc(button);
        internal void ProbeHammerAngle(float degrees) { if (IsServer) HammerAngle.Value = degrees; }
        internal void ProbePlace(Vector2 position, Vector2 velocity = default)
        {
            if (!IsServer) return;
            support = null; windDrift = 0; body.position = position; body.linearVelocity = velocity;
            hammerSurface = null; HammerGripped.Value = false;
            transform.position = position; ServerPosition.Value = position;
            GetComponent<NetworkTransform>().Teleport(position, Quaternion.identity, Vector3.one);
        }
#endif
        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<BoxCollider2D>();
            assignment = new PersonalAssignment(this);
        }
        public override void OnNetworkSpawn()
        {
            activePlayers.Add(this); if (IsOwner) OwnedPlayer = this;
            if (IsServer)
            {
                Role.Value = OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? PlayerRole.Physical : PlayerRole.Logic;
                body.position = stage.Spawn(Role.Value);
                transform.position = body.position;
                ServerPosition.Value = body.position;
            }
            body.simulated = IsServer;
            foreach (var other in activePlayers)
                if (other != this && other.shape != null) Physics2D.IgnoreCollision(shape, other.shape, true);
            // Remote players remain interpolated.  The guest's own player is
            // predicted locally, so an interpolation buffer would make its
            // input appear delayed even on the same computer.
            if (IsOwner && !IsServer) GetComponent<NetworkTransform>().Interpolate = false;
            stageState = FindAnyObjectByType<StageState>();
            authoritativePosition = IsServer ? body.position : transform.position;
            predictedX = authoritativePosition.x;
            predictionInitialized = true;
            Role.OnValueChanged += RoleChanged;
            ServerPosition.OnValueChanged += ServerPositionChanged;
            Paint();
        }
        public override void OnNetworkDespawn()
        {
            activePlayers.Remove(this); if (OwnedPlayer == this) OwnedPlayer = null;
            Role.OnValueChanged -= RoleChanged;
            ServerPosition.OnValueChanged -= ServerPositionChanged;
            ResetPredictedVisual();
        }
        void ServerPositionChanged(Vector2 previous, Vector2 current) => authoritativePosition = current;
        void RoleChanged(PlayerRole previous, PlayerRole current) => Paint();
        void Paint()
        {
            bodyVisual.color = Role.Value == PlayerRole.Physical ? new Color(1f, .62f, .30f) : new Color(.32f, .83f, .85f);
            name = Role.Value + (IsOwner ? " (YOU)" : " (PARTNER)");
        }
        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
#if UNITY_EDITOR || DEBUG
            if (ProbeControlled) return;
#endif
            float move = 0;
            bool jump = false, hold = false, release = false;
            var keys = Keyboard.current;
            if (!InputBlocked && Application.isFocused && keys != null)
            {
                if (Role.Value == PlayerRole.Logic && stageState != null && stageState.RouteFinished.Value)
                {
                    if (keys.qKey.wasPressedThisFrame) circuitSelection = (circuitSelection + 3) % 4;
                    if (keys.eKey.wasPressedThisFrame) circuitSelection = (circuitSelection + 1) % 4;
                    if (keys.enterKey.wasPressedThisFrame) SubmitCircuitRpc(circuitSelection);
                }
                move = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) -
                       (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                jump = keys.spaceKey.wasPressedThisFrame;
                hold = keys.spaceKey.isPressed;
                release = keys.spaceKey.wasReleasedThisFrame;
            }
            if (LaunchMode && Role.Value == PlayerRole.Physical)
            { move = 0; jump = false; }
#if UNITY_EDITOR || DEBUG
            if (TestInput) { move = Mathf.Sin(Time.unscaledTime * 1.4f) > 0 ? 1 : -1; jump = Time.frameCount % 90 == 0; }
#endif
            predictedMove = move;
            if (Time.unscaledTime >= nextSend || jump || release)
            {
                nextSend = Time.unscaledTime + 1f / 20f;
                // The host owns the physical character.  Applying its release
                // locally avoids waiting for a network tick before launch.
                if (IsServer) ApplyInput(move, jump, hold, release);
                else if (jump || release) SubmitActionRpc(move, jump, hold, release);
                else SubmitInputRpc(move, false, hold, false);
            }
        }

        void LateUpdate()
        {
            if (IsSpawned && IsOwner) UpdatePredictedVisual(predictedMove);
        }

        void UpdatePredictedVisual(float move)
        {
            bool canPredict = !IsServer && IsOwner && stageState != null && stageState.GameStarted.Value &&
                              CurrentStage != null && CurrentStage.movementMode == MovementMode.Platform && Role.Value == PlayerRole.Logic && !OperatesMechanism;
            if (!canPredict)
            {
                ResetPredictedVisual();
                return;
            }

            float authoritativeX = authoritativePosition.x;
            if (!predictionInitialized || Mathf.Abs(predictedX - authoritativeX) > 2.2f)
            {
                predictedX = authoritativeX;
                predictionInitialized = true;
            }

            predictedX += move * CurrentStage.moveSpeed * Time.deltaTime;
            // Keep the locally owned visual ahead of the delayed server snapshot.
            // The server still owns collision and the final position, but a slower
            // correction gives the guest an immediate, responsive first movement.
            float reconciliation = 1f - Mathf.Exp(-4f * Time.deltaTime);
            predictedX = Mathf.Lerp(predictedX, authoritativeX, reconciliation);
            float movement = predictedX - transform.position.x;
            movement = ConstrainVisualOffset(movement);
            predictedX = transform.position.x + movement;
            transform.position = new Vector3(predictedX, transform.position.y, transform.position.z);
        }

        float ConstrainVisualOffset(float desiredOffset)
        {
            if (shape == null || Mathf.Abs(desiredOffset) < .001f) return desiredOffset;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(1 << 6);
            filter.useTriggers = false;
            Vector2 direction = desiredOffset > 0 ? Vector2.right : Vector2.left;
            int count = shape.Cast(direction, filter, wallHits, Mathf.Abs(desiredOffset));
            if (count == 0) return desiredOffset;
            float allowed = Mathf.Max(0, wallHits[0].distance - .015f);
            return Mathf.Sign(desiredOffset) * Mathf.Min(Mathf.Abs(desiredOffset), allowed);
        }

        void ResetPredictedVisual()
        {
            predictionInitialized = false;
            if (!IsServer && IsOwner)
                transform.position = new Vector3(authoritativePosition.x, transform.position.y, transform.position.z);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        void SubmitInputRpc(float move, bool jump, bool hold, bool release)
        {
            ApplyInput(move, jump, hold, release);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitActionRpc(float move, bool jump, bool hold, bool release) => ApplyInput(move, jump, hold, release);
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitCircuitRpc(int button) { if (stageState != null && stageState.circuit != null) stageState.circuit.Submit(this, button); }
        void ApplyInput(float move, bool jump, bool hold, bool release)
        {
            if (float.IsNaN(move) || float.IsInfinity(move)) return;
            if (Mathf.Abs(move) > .5f) practiceMoveCount++;
            bool hammer = Role.Value == PlayerRole.Physical && !LaunchMode;
            if (hammer)
            {
                if ((jump || hold) && !charging) { charging = true; chargeStarted = Time.time; }
                if (release && charging)
                {
                    releasedStrength = Mathf.Lerp(.65f, 1f, Mathf.Clamp01((Time.time - chargeStarted) / .75f));
                    if (!HammerGripped.Value) jumpBufferedUntil = Time.time + .16f;
                    charging = false; PushCharge.Value = 0;
                }
            }
            else if (jump) { jumpBufferedUntil = Time.time + .12f; mechanicPressed = true; }
            axis = Mathf.Clamp(move, -1, 1);
            launchAimInput = Mathf.Clamp(move, -1, 1);
            launchHeld = hold;
            launchReleased |= release;
            receivedAt = Time.unscaledTime;
        }
        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (support != null && body.linearVelocity.y <= .5f)
                body.position += (Vector2)(support.TransformPoint(supportLocalPoint) - supportWorldPoint);
            if (Time.unscaledTime - receivedAt > .3f) { axis = 0; launchAimInput = 0; launchHeld = false; charging = false; PushCharge.Value = 0; }
            if (stageState != null && !stageState.GameStarted.Value) { body.linearVelocity = Vector2.zero; PublishServerPosition(); return; }
            assignment.Tick(stageState, Time.fixedDeltaTime);
            var activeStage = CurrentStage;
            if (activeStage == null) { PublishServerPosition(); return; }
            if (LaunchMode)
            {
                // The physical player has no walking input here, but a launched
                // player must keep both velocity components for its trajectory.
                if (Grounded() && Mathf.Abs(body.linearVelocity.y) < .05f) body.linearVelocity = Vector2.zero;
                if (body.position.y < -9f) Respawn();
                PublishServerPosition();
                return;
            }
            if (Role.Value == PlayerRole.Physical)
            {
                UpdateHammer();
                if (body.position.y < -9f) Respawn();
                PublishServerPosition();
                return;
            }
            bool grounded = Grounded();
            if (grounded) lastGroundedAt = Time.time;
            float acceleration = grounded ? (Mathf.Abs(axis) > .01f ? 65f : 85f) : 48f;
            windDrift = Mathf.MoveTowards(windDrift, 0, 4.5f * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, OperatesMechanism ? 0 : axis * activeStage.moveSpeed + windDrift, acceleration * Time.fixedDeltaTime), body.linearVelocity.y);
            if (Time.time <= jumpBufferedUntil && Time.time - lastJump > .18f && Time.time - lastGroundedAt <= .1f)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, activeStage.jumpSpeed);
                practiceJumpCount++;
                lastJump = Time.time; jumpBufferedUntil = -1; lastGroundedAt = -1;
            }
            if (body.position.y < -9f) Respawn();
            PublishServerPosition();
        }
        void UpdateHammer()
        {
            if (charging) PushCharge.Value = Mathf.Floor(Mathf.Clamp01((Time.time - chargeStarted) / .75f) * 20) / 20;
            HammerAngle.Value = Mathf.Repeat(HammerAngle.Value - axis * 115 * Time.fixedDeltaTime + 180, 360) - 180;
            float radians = HammerAngle.Value * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 shoulder = body.position + HammerShoulder;
            var hit = Physics2D.CircleCast(shoulder, .16f, direction, HammerReach, 1 << 6);
            HammerContact.Value = hit.collider != null;
            if (hit.collider != null && !HammerGripped.Value) HammerAnchor.Value = hit.point;
            if (!launchHeld || hammerSurface == null || !hammerSurface.gameObject.activeInHierarchy) { HammerGripped.Value = false; hammerSurface = null; }
            if (launchHeld && hit.collider != null && hit.normal.y < .55f)
            {
                if (hammerSurface == null)
                {
                    hammerSurface = hit.transform; hammerLocalAnchor = hammerSurface.InverseTransformPoint(hit.point);
                    HammerGripped.Value = true;
                }
            }
            if (HammerGripped.Value && hammerSurface != null)
            {
                Vector2 anchor = hammerSurface.TransformPoint(hammerLocalAnchor);
                if (Vector2.Distance(anchor, shoulder) > HammerReach + .7f)
                { HammerGripped.Value = false; hammerSurface = null; }
                else
                {
                    HammerAnchor.Value = anchor;
                    Vector2 target = anchor - direction * HammerReach - HammerShoulder;
                    body.linearVelocity = Vector2.ClampMagnitude((target - body.position) * 16, 12);
                }
            }
            bool pushRequested = Time.time <= jumpBufferedUntil;
            if (pushRequested && hit.collider != null && Time.time - lastHammerPush > .25f && !HammerGripped.Value)
            {
                Vector2 away = (shoulder - hit.point).normalized;
                body.linearVelocity = PushVelocity(away, hit.normal.y > .55f, releasedStrength) + Vector2.right * windDrift;
                lastHammerPush = Time.time; jumpBufferedUntil = -1; support = null; practiceJumpCount++;
            }
            if (!HammerGripped.Value)
            {
                if (Grounded() && Time.time - lastHammerPush > .15f)
                    body.linearVelocity = new Vector2(Mathf.MoveTowards(body.linearVelocity.x, windDrift, 20 * Time.fixedDeltaTime), body.linearVelocity.y);
                else body.linearVelocity += Vector2.right * windDrift * .8f * Time.fixedDeltaTime;
            }
            windDrift = Mathf.MoveTowards(windDrift, 0, 4.5f * Time.fixedDeltaTime);
        }
        void PublishServerPosition()
        {
            if (!IsServer) return;
            ServerPosition.Value = body.position; VisualMotion.Value = body.linearVelocity; OnGround.Value = Grounded();
            support = OnGround.Value && body.linearVelocity.y <= .5f ? groundHits[0].transform : null;
            if (support != null) { supportWorldPoint = body.position; supportLocalPoint = support.InverseTransformPoint(supportWorldPoint); }
        }
        bool Grounded()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(1 << 6);
            filter.useTriggers = false;
            return shape.Cast(Vector2.down, filter, groundHits, .08f) > 0;
        }
        public void Launch(float angle, float power)
        {
            if (!IsServer || !IsGrounded) return;
            body.linearVelocity = MotionMath.LaunchVelocity(angle, power);
            practiceLaunchCount++;
            assignment.Launch(stageState, power);
        }
        public void AddHorizontalImpulse(float amount)
        {
            if (!IsServer || body == null) return;
            windDrift = Mathf.Clamp(windDrift + amount, -5, 5);
        }
        public void ChooseRole(PlayerRole role) => ChooseRoleRpc(role);
        public void AcceptRole() => AcceptRoleRpc();
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ChooseRoleRpc(PlayerRole role) { (stageState ??= FindAnyObjectByType<StageState>()).ChooseRole(OwnerClientId, role); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void AcceptRoleRpc() { (stageState ??= FindAnyObjectByType<StageState>()).AcceptRole(OwnerClientId); }
        public void AssignRole(PlayerRole role)
        {
            if (!IsServer) return;
            Role.Value = role; Paint(); Respawn();
        }
        public void ResetPracticeActions()
        {
            if (IsServer) assignment.ResetAttempt();
            practiceMoveCount = 0;
            practiceJumpCount = 0;
            practiceLaunchCount = 0;
            mechanicPressed = false;
        }
        public void Respawn()
        {
            if (!IsServer) return;
            axis = 0; launchAimInput = 0; launchHeld = false; launchReleased = false;
            jumpBufferedUntil = -1; lastGroundedAt = -1;
            windDrift = 0; support = null;
            hammerSurface = null; HammerGripped.Value = false; HammerContact.Value = false; HammerAngle.Value = -135;
            lastHammerPush = -1;
            charging = false; PushCharge.Value = 0; releasedStrength = .65f;
            body.linearVelocity = Vector2.zero;
            body.position = CurrentStage.Spawn(Role.Value);
            if (Role.Value == PlayerRole.Physical && stageState != null && stageState.RouteFinished.Value && stageState.circuit != null) body.position = stageState.circuit.RespawnPoint;
            ServerPosition.Value = body.position;
            GetComponent<NetworkTransform>().Teleport(body.position, Quaternion.identity, Vector3.one);
        }
        public void ClearAssignments()
        {
            if (!IsServer) return;
            AssignmentBits.Value = 0; assignment.ResetAttempt();
        }
    }
}
