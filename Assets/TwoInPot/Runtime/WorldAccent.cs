using UnityEngine;

namespace TwoInPot
{
    public sealed class WorldAccent : MonoBehaviour
    {
        public enum Motion { Console, Flag, Wind, Counterweight }
        public Motion motion;
        public Transform[] parts;
        StageState stage;
        Vector3 home, scale;
        Quaternion rotation;
        float press;

        void Awake()
        {
            stage = FindAnyObjectByType<StageState>();
            home = transform.localPosition; scale = transform.localScale; rotation = transform.localRotation;
        }
        void Update()
        {
            if (stage == null) return;
            switch (motion)
            {
                case Motion.Console:
                    bool active = stage.GateOpen.Value || stage.StageIndex.Value == 4 && stage.MechanicValue.Value > .05f || stage.StageIndex.Value == 12 && stage.RelayStep.Value > 0;
                    press = Mathf.MoveTowards(press, active ? 1 : 0, Time.deltaTime * 6);
                    transform.localPosition = home - Vector3.up * press * .12f;
                    transform.localScale = new Vector3(scale.x, scale.y * (1 - press * .3f), scale.z);
                    break;
                case Motion.Flag:
                    transform.localRotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 3.6f) * 5);
                    break;
                case Motion.Wind:
                    for (int i = 0; i < parts.Length; i++)
                    {
                        float t = Mathf.Repeat(Time.time * (.2f + stage.MechanicValue.Value * .7f) + i * .19f, 1);
                        parts[i].localPosition = new Vector3(-.45f + t * .9f, -.32f + i * .13f, -.25f);
                    }
                    break;
                case Motion.Counterweight:
                    transform.localPosition = home + Vector3.up * stage.MechanicValue.Value * 1.3f;
                    break;
            }
        }
    }
}
