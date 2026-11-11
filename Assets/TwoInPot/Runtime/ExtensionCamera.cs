using UnityEngine;

namespace TwoInPot
{
    [DefaultExecutionOrder(150)]
    public sealed class ExtensionCamera : MonoBehaviour
    {
        Vector3 home;
        StageState stage;
        void Awake() { home = transform.position; stage = FindAnyObjectByType<StageState>(); }
        void LateUpdate()
        {
            var player = CoopPlayer.OwnedPlayer;
            float x = player != null && player.Role.Value == PlayerRole.Physical && stage != null && stage.GameStarted.Value ? Mathf.Clamp(player.transform.position.x - 2, home.x, 27) : home.x;
            transform.position = new Vector3(Mathf.Round(x * 24) / 24, home.y, home.z);
        }
    }
}
