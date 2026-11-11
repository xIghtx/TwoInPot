using UnityEngine;

namespace TwoInPot
{
    public enum PlayerRole : byte { Physical, Logic }
    public enum MovementMode : byte { Platform, Launch }

    [CreateAssetMenu(menuName = "Two In Pot/Stage Definition")]
    public sealed class StageDefinition : ScriptableObject
    {
        public string displayName = "01 / FIRST CONTACT";
        public MovementMode movementMode;
        public float moveSpeed = 6f;
        public float jumpSpeed = 10f;
        public float gravityScale = 2.5f;
        [Header("Launch mode")]
        public float minimumLaunchAngle = 5f;
        public float maximumLaunchAngle = 175f;
        public float startingLaunchAngle = 45f;
        public float minimumLaunchPower = 7f;
        public float maximumLaunchPower = 18f;
        public float aimSpeed = 42f;
        public float chargeSpeed = 9f;
        public Vector2 physicalSpawn = new Vector2(-7, 0);
        public Vector2 logicSpawn = new Vector2(-10, 0);
        public Vector2 Spawn(PlayerRole role) => role == PlayerRole.Physical ? physicalSpawn : logicSpawn;
    }

    public static class MotionMath
    {
        public static Vector2 LaunchVelocity(float angleDegrees, float speed)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Mathf.Max(0, speed);
        }
        public static Vector2 PositionAt(Vector2 start, Vector2 velocity, Vector2 gravity, float seconds)
            => start + velocity * seconds + 0.5f * gravity * seconds * seconds;
    }
}
