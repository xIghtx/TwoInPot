using UnityEngine;

namespace TwoInPot
{
    // No managed allocations per frame. Report actual rendered frame intervals after warm-up.
    public sealed class PixelPerformance : MonoBehaviour
    {
        readonly float[] samples = new float[8192];
        float elapsed, total, slowest, stageTime;
        int frames, slowFrames, sampleCount, stageFrames, stageSlow, previousStage;
        bool reported;
        StageState stage;
        void Awake()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.realtimeReflectionProbes = false;
        }
        void Start() { stage = FindAnyObjectByType<StageState>(); }
        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < 5 || stage == null || !stage.GameStarted.Value) return;
            if (previousStage != stage.StageIndex.Value)
            {
                if (stageFrames > 0) Debug.Log($"[POT_PERF] stage={previousStage} average={stageFrames / stageTime:F1}fps over33ms={stageSlow}/{stageFrames}");
                previousStage = stage.StageIndex.Value; stageFrames = stageSlow = 0; stageTime = 0;
            }
            float dt = Time.unscaledDeltaTime;
            total += dt; frames++; slowest = Mathf.Max(slowest, dt);
            stageTime += dt; stageFrames++;
            if (sampleCount < samples.Length) samples[sampleCount++] = dt;
            if (dt > 1f / 30) { slowFrames++; stageSlow++; }
        }
        public void Report()
        {
            if (reported || sampleCount == 0) return;
            reported = true;
            System.Array.Sort(samples, 0, sampleCount);
            int tail = Mathf.Max(1, Mathf.CeilToInt(sampleCount * .01f)); float tailTime = 0;
            for (int i = sampleCount - tail; i < sampleCount; i++) tailTime += samples[i];
            Debug.Log($"[POT_PERF] SUMMARY average={frames / total:F1}fps onePercentLow={tail / tailTime:F1}fps p95Ms={samples[Mathf.Min(sampleCount - 1, (int)(sampleCount * .95f))] * 1000:F1} over33ms={slowFrames}/{frames} worstMs={slowest * 1000:F1} resolution={Screen.width}x{Screen.height} internal=640x360 samples={sampleCount}. Measured on this device; not a guarantee for other laptops.");
        }
        void OnApplicationQuit() { Report(); }
    }
}
