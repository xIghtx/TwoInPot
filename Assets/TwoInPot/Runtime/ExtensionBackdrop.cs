using UnityEngine;
namespace TwoInPot
{
    public sealed class ExtensionBackdrop : MonoBehaviour
    {
        public SpriteRenderer source;
        SpriteRenderer target;
        void Awake() { target = GetComponent<SpriteRenderer>(); }
        void LateUpdate() { if (source != null) target.color = source.color; }
    }
}
