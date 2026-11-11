using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TwoInPot
{
    // Opt-in integration probe. Only enabled by explicit command-line args in development builds.
    public sealed class SmokeProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEBUG
        internal static string DirectAddress(string[] args)
        {
            int index = Array.IndexOf(args, "-potAddress");
            if (index < 0) return "127.0.0.1";
            if (index + 1 >= args.Length) throw new ArgumentException("-potAddress requires an IPv4 address.");
            return args[index + 1];
        }
        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool host = Array.IndexOf(args, "-potHost") >= 0;
            bool client = Array.IndexOf(args, "-potClient") >= 0;
            if (!host && !client) yield break;
            var session = GetComponent<SessionController>();
            if (Array.IndexOf(args, "-potSessionResetProbe") >= 0)
            {
                yield return FestivalRegression.Guard(SessionResetRegression.Run(session));
                yield break;
            }
            if (Array.IndexOf(args, "-potFestivalTests") >= 0)
            {
                yield return FestivalRegression.Guard(FestivalRegression.Run(session, host));
                yield break;
            }
            yield return new WaitForSecondsRealtime(2);
            bool online = Array.IndexOf(args, "-potOnline") >= 0;
            if (online)
            {
                int codeIndex = Array.IndexOf(args, "-potCode");
                var start = host ? session.HostOnlineAsync() : session.JoinOnlineAsync(codeIndex >= 0 ? args[codeIndex + 1] : "");
                while (!start.IsCompleted) yield return null;
                Debug.Log("[POT_ROOM_CODE] " + session.JoinCode);
            }
            else if (host) session.HostDirect(); else session.JoinDirect(DirectAddress(args));
            CoopPlayer.TestInput = Array.IndexOf(args, "-potMove") >= 0;
            float duration = 25;
            int durationIndex = Array.IndexOf(args, "-potDuration");
            if (durationIndex >= 0 && durationIndex + 1 < args.Length) float.TryParse(args[durationIndex + 1], out duration);
            float end = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < end)
            {
                yield return new WaitForSecondsRealtime(1);
                var players = FindObjectsByType<CoopPlayer>();
                string report = "[POT_PROBE] " + (host ? "HOST" : "CLIENT") + " count=" + players.Length;
                foreach (var player in players)
                    report += " | " + player.OwnerClientId + ":" + player.Role.Value + ":" + player.transform.position.ToString("F2");
                var stage = FindAnyObjectByType<StageState>();
                report += " stage=" + (stage != null && stage.IsSpawned) + " gate=" + (stage != null && stage.GateOpen.Value);
                Debug.Log(report);
            }
            awaitLeave = session.LeaveAsync();
            while (!awaitLeave.IsCompleted) yield return null;
            Application.Quit();
        }
        System.Threading.Tasks.Task awaitLeave;
#endif
    }
}
