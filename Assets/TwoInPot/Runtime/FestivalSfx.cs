using UnityEngine;

namespace TwoInPot
{
    public sealed class FestivalSfx : MonoBehaviour
    {
        public enum Cue { Select, Contact, Push, Land, Jump, Gate, Record, Clear, Ending }
        public AudioClip[] clips;
        static FestivalSfx instance;
        public static bool Muted { get; set; }
        readonly AudioSource[] voices = new AudioSource[4];
        readonly float[] nextAllowed = new float[9];
        int voiceIndex, previousStage, previousMemory, previousEnding;
        bool previousGate, previousPractice, initialized;
        bool wasMuted;
        StageState stage;
        public int PlayedCount { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; Muted = false; }
        void Awake()
        {
            instance = this;
            stage = FindAnyObjectByType<StageState>();
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false; voices[i].spatialBlend = 0;
                voices[i].volume = .24f; voices[i].dopplerLevel = 0;
            }
        }
        public static void Play(Cue cue)
        {
            if (instance == null || Muted) return;
            instance.PlayLocal(cue);
        }
        void PlayLocal(Cue cue)
        {
            int index = (int)cue;
            if (clips == null || index >= clips.Length || clips[index] == null || Time.unscaledTime < nextAllowed[index]) return;
            nextAllowed[index] = Time.unscaledTime + (cue == Cue.Contact ? .12f : .045f);
            var source = voices[voiceIndex]; voiceIndex = (voiceIndex + 1) % voices.Length;
            source.clip = clips[index]; source.Play(); PlayedCount++;
        }
        void Update()
        {
            if (wasMuted != Muted)
            { foreach (var source in voices) source.mute = Muted; wasMuted = Muted; }
            if (stage == null || !stage.IsSpawned || !stage.GameStarted.Value) { initialized = false; return; }
            int number = stage.StageIndex.Value, memory = stage.MemoryBits.Value, ending = stage.EndingCode.Value;
            bool gate = stage.GateOpen.Value, practice = stage.PracticeActive.Value;
            if (initialized)
            {
                if (number > previousStage) Play(Cue.Clear);
                else if (gate && !previousGate) Play(Cue.Gate);
                else if (previousPractice && !practice) Play(Cue.Select);
                if ((memory & ~previousMemory) != 0) Play(Cue.Record);
                if (ending != 0 && ending != previousEnding) Play(Cue.Ending);
            }
            previousStage = number; previousMemory = memory; previousEnding = ending;
            previousGate = gate; previousPractice = practice; initialized = true;
        }
        void OnDestroy() { if (instance == this) instance = null; }
    }
}
