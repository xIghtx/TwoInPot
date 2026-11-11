#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using UnityEngine;

namespace TwoInPot
{
    internal static class SessionResetRegression
    {
        internal static IEnumerator Run(SessionController session)
        {
            session.HostDirect();
            yield return new WaitForSecondsRealtime(1);
            var stage = UnityEngine.Object.FindAnyObjectByType<StageState>();
            stage.StageIndex.Value = 6; stage.MemoryBits.Value = 7; stage.EndingCode.Value = 1;
            stage.Complete.Value = true; stage.GameStarted.Value = true;
            var leaving = session.LeaveAsync(); while (!leaving.IsCompleted) yield return null;
            session.HostDirect(); yield return new WaitForSecondsRealtime(1);
            if (stage.StageIndex.Value != 1 || stage.MemoryBits.Value != 0 || stage.EndingCode.Value != 0 || stage.Complete.Value || stage.GameStarted.Value || !stage.PracticeActive.Value)
                throw new InvalidOperationException("[POT_RESET] FAIL: a new room retained previous run state");
            Debug.Log("[POT_RESET] PASS: shutdown and rehost start a clean run");
            leaving = session.LeaveAsync(); while (!leaving.IsCompleted) yield return null;
            Application.Quit(0);
        }
    }
}
#endif
