using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwoInPot
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody2D))]
    public sealed class CoopPlayer : NetworkBehaviour
    {
        public StageDefinition stage;
        public SpriteRenderer bodyVisual;
        public readonly NetworkVariable<PlayerRole> Role = new NetworkVariable<PlayerRole>();
        readonly NetworkVariable<Vector2> ServerPosition = new NetworkVariable<Vector2>();
        readonly NetworkVariable<Vector2> VisualMotion = new NetworkVariable<Vector2>();
        readonly NetworkVariable<bool> OnGround = new NetworkVariable<bool>();
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
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[8];
        public static bool InputBlocked;
        StageState stageState;
        public float LaunchAimInput => launchAimInput;
        public bool LaunchHeld => launchHeld;
        public Vector2 PresentationVelocity => IsServer ? body.linearVelocity : VisualMotion.Value;
        public bool PresentationGrounded => OnGround.Value;
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
#if UNITY_EDITOR || DEBUG
        public static bool TestInput;
        public static bool ProbeControlled;
        internal void ProbeInput(float move = 0, bool jump = false, bool hold = false, bool release = false) => ApplyInput(move, jump, hold, release);
        internal void ProbeSubmitInput(float move) => SubmitInputRpc(move, false, false, false);
        internal void ProbePlace(Vector2 position, Vector2 velocity = default)
        {
            if (!IsServer) return;
            support = null; windDrift = 0; body.position = position; body.linearVelocity = velocity;
            transform.position = position; ServerPosition.Value = position;
            GetComponent<NetworkTransform>().Teleport(position, Quaternion.identity, Vector3.one);
        }
#endif
        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            shape = GetComponent<BoxCollider2D>();
        }
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                Role.Value = OwnerClientId == Unity.Netcode.NetworkManager.ServerClientId ? PlayerRole.Physical : PlayerRole.Logic;
                body.position = stage.Spawn(Role.Value);
                transform.position = body.position;
                ServerPosition.Value = body.position;
            }
            body.simulated = IsServer;
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
                move = (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) -
                       (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0);
                jump = keys.spaceKey.wasPressedThisFrame;
                hold = keys.spaceKey.isPressed;
                release = keys.spaceKey.wasReleasedThisFrame;
            }
            if (CurrentStage != null && CurrentStage.movementMode == MovementMode.Launch && Role.Value == PlayerRole.Physical)
            { move = 0; jump = false; }
#if UNITY_EDITOR || DEBUG
            if (TestInput) { move = Mathf.Sin(Time.unscaledTime * 1.4f) > 0 ? 1 : -1; jump = Time.frameCount % 90 == 0; }
#endif
            predictedMove = move;
            if (Time.unscaledTime >= nextSend || jump || release)
            {
                nextSend = Time.unscaledTime + 1f / 60f;
                // The host owns the physical character.  Applying its release
                // locally avoids waiting for a network tick before launch.
                if (IsServer) ApplyInput(move, jump, hold, release);
                else SubmitInputRpc(move, jump, hold, release);
            }
        }

        void LateUpdate()
        {
            if (IsSpawned && IsOwner) UpdatePredictedVisual(predictedMove);
        }

        void UpdatePredictedVisual(float move)
        {
            bool canPredict = !IsServer && IsOwner && stageState != null && stageState.GameStarted.Value &&
                              CurrentStage != null && CurrentStage.movementMode == MovementMode.Platform && !OperatesMechanism;
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
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SubmitInputRpc(float move, bool jump, bool hold, bool release)
        {
            ApplyInput(move, jump, hold, release);
        }
        void ApplyInput(float move, bool jump, bool hold, bool release)
        {
            if (float.IsNaN(move) || float.IsInfinity(move)) return;
            if (Mathf.Abs(move) > .5f) practiceMoveCount++;
            if (jump) { jumpBufferedUntil = Time.time + .12f; mechanicPressed = true; }
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
            if (Time.unscaledTime - receivedAt > .3f) { axis = 0; launchAimInput = 0; launchHeld = false; }
            if (stageState != null && !stageState.GameStarted.Value) { body.linearVelocity = Vector2.zero; PublishServerPosition(); return; }
            var activeStage = CurrentStage;
            if (activeStage == null) { PublishServerPosition(); return; }
            if (activeStage.movementMode == MovementMode.Launch)
            {
                // The physical player has no walking input here, but a launched
                // player must keep both velocity components for its trajectory.
                if (Grounded() && Mathf.Abs(body.linearVelocity.y) < .05f) body.linearVelocity = Vector2.zero;
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
            body.linearVelocity = Vector2.zero;
            body.position = CurrentStage.Spawn(Role.Value);
            ServerPosition.Value = body.position;
            GetComponent<NetworkTransform>().Teleport(body.position, Quaternion.identity, Vector3.one);
        }
    }
}
