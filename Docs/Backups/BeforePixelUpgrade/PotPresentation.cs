using UnityEngine;

namespace TwoInPot
{
    // Animate only the model. The server's collider and network transform stay stable.
    [DefaultExecutionOrder(100)]
    public sealed class PotPresentation : MonoBehaviour
    {
        public Transform model;
        public Transform[] eyes;
        public Transform[] feet;
        public Renderer[] ceramic;
        public Transform[] dust;
        public GameObject climberAppearance, engineerAppearance, hammerRig;
        public Transform toolPivot, shaft, hammerHead;
        public Transform[] arms, hands;
        CoopPlayer player;
        StageState stage;
        MaterialPropertyBlock properties;
        Vector3 restPosition;
        Quaternion restRotation;
        Vector3[] footRest;
        bool wasGrounded, initialized;
        float impulse, previousVerticalSpeed, dustAge = 1, facing = 1;
        int paintedRole = -1, previousStage = -1;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            player = GetComponent<CoopPlayer>();
            stage = FindAnyObjectByType<StageState>();
            properties = new MaterialPropertyBlock();
            restPosition = model.localPosition;
            restRotation = model.localRotation;
            footRest = new Vector3[feet.Length];
            for (int i = 0; i < feet.Length; i++) footRest[i] = feet[i].localPosition;
            foreach (var puff in dust) puff.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!player.IsSpawned) return;
            if (paintedRole != (int)player.Role.Value)
            {
                paintedRole = (int)player.Role.Value;
                climberAppearance.SetActive(paintedRole == 0); engineerAppearance.SetActive(paintedRole == 1); hammerRig.SetActive(paintedRole == 0);
                properties.SetColor(ColorId, paintedRole == 0 ? new Color(1f, .58f, .27f) : new Color(.25f, .82f, .82f));
                foreach (var part in ceramic) part.SetPropertyBlock(properties);
            }
            Vector2 velocity = player.PresentationVelocity;
            bool grounded = player.PresentationGrounded;
            bool teleported = stage != null && previousStage != stage.StageIndex.Value;
            if (!initialized || teleported) { wasGrounded = grounded; previousVerticalSpeed = 0; initialized = true; impulse = 0; }
            if (stage != null) previousStage = stage.StageIndex.Value;
            if (grounded && !wasGrounded && previousVerticalSpeed < -1.5f)
            {
                impulse = -Mathf.Clamp(Mathf.Abs(previousVerticalSpeed) * .012f, .06f, .12f);
                dustAge = 0;
                for (int i = 0; i < dust.Length; i++)
                {
                    dust[i].gameObject.SetActive(true);
                    dust[i].position = transform.position + new Vector3(0, -.52f, -.6f);
                }
            }
            else if (!grounded && wasGrounded && velocity.y > 2) impulse = .10f;
            impulse = Mathf.MoveTowards(impulse, 0, Time.deltaTime * 1.4f);
            float speed = Mathf.Abs(velocity.x);
            if (speed > .2f) facing = Mathf.Sign(velocity.x);
            float stride = Time.time * 15f;
            float breath = Mathf.Sin(Time.time * 2.8f + player.OwnerClientId) * .016f;
            float charge = stage != null && stage.StageIndex.Value == 2 && player.Role.Value == PlayerRole.Physical ? stage.LaunchPower.Value / 18f : 0;
            float stretch = grounded ? impulse + breath - charge * .10f : Mathf.Clamp(velocity.y * .007f, -.05f, .08f) + impulse;
            Vector3 targetScale = new Vector3(1 - stretch * .5f, 1 + stretch, 1);
            model.localScale = Vector3.Lerp(model.localScale, targetScale, 1 - Mathf.Exp(-22 * Time.deltaTime));
            float lean = -velocity.x * 1.2f + (grounded ? Mathf.Sin(stride) * Mathf.Min(speed, 5) * .4f : -facing * 3);
            model.localRotation = Quaternion.Slerp(model.localRotation, restRotation * Quaternion.Euler(0, 0, lean), 1 - Mathf.Exp(-14 * Time.deltaTime));
            model.localPosition = restPosition + Vector3.up * (grounded ? Mathf.Abs(Mathf.Sin(stride)) * Mathf.Min(speed, 5) * .006f + stretch * .25f : 0);
            float blinkPhase = Mathf.Repeat(Time.time + player.OwnerClientId * .73f, 4.1f);
            float blink = blinkPhase < .12f ? .12f : 1;
            foreach (var eye in eyes) eye.localScale = new Vector3(1, blink, 1);
            for (int i = 0; i < feet.Length; i++) feet[i].localPosition = footRest[i] + Vector3.up * (grounded ? Mathf.Max(0, Mathf.Sin(stride + i * Mathf.PI)) * Mathf.Min(speed, 5) * .018f : .04f);
            if (dustAge < .35f)
            {
                dustAge += Time.deltaTime;
                for (int i = 0; i < dust.Length; i++)
                {
                    float direction = i % 2 == 0 ? -1 : 1;
                    dust[i].position += new Vector3(direction * (1.2f + i * .25f), .55f, 0) * Time.deltaTime;
                    dust[i].localScale = Vector3.one * Mathf.Max(0, (1 - dustAge / .35f) * .16f);
                    if (dustAge >= .35f) dust[i].gameObject.SetActive(false);
                }
            }
            previousVerticalSpeed = velocity.y;
            wasGrounded = grounded;
            if (paintedRole == 0) PoseHammer();
        }
        void PoseHammer()
        {
            toolPivot.localRotation = Quaternion.Euler(0, 0, player.PresentationHammerAngle);
            float reach = player.ToolInContact || player.ToolGripped ? Mathf.Clamp(Vector2.Distance(toolPivot.position, player.ToolAnchor), .25f, CoopPlayer.HammerReach) : CoopPlayer.HammerReach;
            if (player.ToolGripped)
            {
                Vector2 direction = player.ToolAnchor - (Vector2)toolPivot.position;
                toolPivot.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
            shaft.localPosition = new Vector3(reach * .5f, 0, 0); shaft.localScale = new Vector3(reach, .085f, .09f);
            hammerHead.localPosition = new Vector3(reach, 0, 0);
            for (int i = 0; i < 2; i++)
            {
                Vector3 shoulder = model.TransformPoint(new Vector3(i == 0 ? -.17f : .17f, .45f, -.22f));
                Vector3 wrist = toolPivot.TransformPoint(new Vector3(Mathf.Min(reach * .7f, .38f + i * .25f), .025f, -.06f));
                Vector3 elbow = Vector3.Lerp(shoulder, wrist, .5f) + new Vector3(i == 0 ? -.1f : .1f, .12f, -.04f);
                PoseLimb(arms[i * 2], shoulder, elbow); PoseLimb(arms[i * 2 + 1], elbow, wrist);
                hands[i].position = wrist;
            }
        }
        static void PoseLimb(Transform limb, Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start; limb.position = (start + end) * .5f;
            limb.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            limb.localScale = new Vector3(delta.magnitude, .12f, .14f);
        }
    }
}
