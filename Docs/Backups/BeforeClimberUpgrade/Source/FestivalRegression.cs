#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TwoInPot
{
    // Explicit development-build command only. Never active in a normal game.
    internal static class FestivalRegression
    {
        static int checks;
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[POT_TEST] FAIL: " + message);
            checks++;
        }
        static IEnumerator Until(Func<bool> predicate, float seconds, string description)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("[POT_TEST] " + description);
                yield return null;
            }
            checks++;
        }
        internal static IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null; bool more = false; Exception failure = null;
                try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { Debug.LogException(failure); Application.Quit(2); yield break; }
                if (!more) { stack.Pop(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
        }
        internal static IEnumerator Run(SessionController session, bool host)
        {
            CoopPlayer.ProbeControlled = true;
            var view = UnityEngine.Object.FindAnyObjectByType<ConnectionView>(); view.ShowProbeHud();
            yield return new WaitForSecondsRealtime(host ? 1 : 3);
            if (host) session.HostDirect(); else session.JoinDirect("127.0.0.1");
            yield return Until(() => session.Ready && UnityEngine.Object.FindObjectsByType<CoopPlayer>().Length == 2, 30, "two peers connect");
            var stage = UnityEngine.Object.FindAnyObjectByType<StageState>();
            var players = UnityEngine.Object.FindObjectsByType<CoopPlayer>();
            CoopPlayer mine = null;
            foreach (var player in players) if (player.IsOwner) mine = player;
            Check(mine != null, "local player ownership");
            mine.ChooseRole(host ? PlayerRole.Physical : PlayerRole.Logic);
            yield return Until(() => stage.HostRoleChoice.Value >= 0 && stage.GuestRoleChoice.Value >= 0, 10, "role selection RPCs");
            mine.AcceptRole();
            yield return Until(() => stage.GameStarted.Value, 10, "both role acceptances start game");
            if (!host)
            {
                mine.ProbeSubmitInput(.75f);
                int practiceMask = 0, objectiveMask = 0; float deadline = Time.realtimeSinceStartup + 150;
                bool sawComplete = false;
                var ui = view.GetComponent<UIDocument>().rootVisualElement;
                while (session.Ready && Time.realtimeSinceStartup < deadline)
                {
                    int bit = 1 << (stage.StageIndex.Value - 1);
                    if (stage.PracticeActive.Value) practiceMask |= bit; else objectiveMask |= bit;
                    if (stage.Complete.Value && stage.StageIndex.Value == 12) sawComplete = true;
                    if (stage.GameStarted.Value)
                        Check(ui.Q<VisualElement>("practiceCard").style.display == (stage.PracticeActive.Value ? DisplayStyle.Flex : DisplayStyle.None), "guest practice UI matches network phase");
                    yield return new WaitForSecondsRealtime(.1f);
                }
                Check(practiceMask == 4095, "guest saw practice for all 12 stages");
                Check(objectiveMask == 4095, "guest saw objective phase for all 12 stages");
                Check(sawComplete, "guest received final completion");
                Debug.Log("[POT_TEST] GUEST PASS checks=" + checks + " practiceMask=" + practiceMask + " objectiveMask=" + objectiveMask);
                Application.Quit(0); yield break;
            }
            CoopPlayer physical = null, logic = null;
            foreach (var player in players) if (player.Role.Value == PlayerRole.Physical) physical = player; else logic = player;
            yield return Until(() => logic.LaunchAimInput > .7f, 10, "guest owner input RPC reaches server");
            logic.ProbeInput();
            for (int number = 1; number <= 12; number++)
            {
                Check(stage.StageIndex.Value == number && stage.PracticeActive.Value && !stage.Complete.Value, "stage " + number + " starts in practice");
                physical.ProbeInput(); logic.ProbeInput();
                Vector2 goal = number <= 2 ? new Vector2(11.6f, -1.2f) : (Vector2)stage.exits[number - 3].position + Vector2.right;
                physical.ProbePlace(goal);
                yield return new WaitForSecondsRealtime(.25f);
                Check(stage.StageIndex.Value == number && stage.PracticeActive.Value, "practice exit cannot skip stage " + number);
                stage.ResetStage();
                yield return new WaitForSecondsRealtime(.4f);
                Check(Vector2.Distance(physical.transform.position, stage.CurrentDefinition.physicalSpawn) < 1, "reset respawns physical stage " + number);
                if (number == 2)
                {
                    physical.ProbePlace(new Vector2(-8, 1)); logic.ProbeInput(1); physical.ProbeInput(release: true);
                    yield return new WaitForSecondsRealtime(.15f);
                    Check(stage.PracticeActive.Value, "airborne release does not count as a practice launch");
                    stage.ResetStage(); yield return new WaitForSecondsRealtime(.4f);
                }
                if (number == 9)
                {
                    logic.ProbeInput(-1); physical.ProbeInput(jump: true);
                    yield return new WaitForSecondsRealtime(.2f);
                    Check(stage.PracticeActive.Value, "pressing against the lift lower limit does not finish practice");
                    stage.ResetStage(); yield return new WaitForSecondsRealtime(.4f);
                }
                yield return Practice(stage, physical, logic, number);
                Check(!stage.PracticeActive.Value && !stage.Complete.Value, "practice becomes objective stage " + number);
                Check(physical.PracticeJumpCount == 0 && physical.PracticeLaunchCount == 0 && logic.PracticeMoveCount == 0, "practice action history clears stage " + number);
                yield return new WaitForSecondsRealtime(.35f);
                if (number == 1)
                {
                    float y = physical.transform.position.y;
                    physical.ProbeInput(jump: true); yield return new WaitForSecondsRealtime(.15f);
                    Check(physical.transform.position.y > y + .3f, "real jump physics moves upward");
                    Check(Mathf.Abs(physical.GetComponent<PotPresentation>().model.localScale.y - 1) > .015f, "jump animates the pot mesh");
                    yield return new WaitForSecondsRealtime(.9f);
                    Check(physical.PresentationGrounded, "jump lands on ground");
                    physical.ProbePlace(new Vector2(-7, -1.9f), new Vector2(0, -3));
                    yield return new WaitForSecondsRealtime(.14f);
                    physical.ProbeInput(jump: true); yield return new WaitForSecondsRealtime(.22f);
                    Check(physical.PresentationVelocity.y > 0, "jump input just before landing is buffered");
                }
                if (number == 4)
                {
                    logic.ProbePlace(stage.logicTargets[1].position);
                    float x = physical.transform.position.x;
                    yield return new WaitForSecondsRealtime(1.25f);
                    Check(physical.transform.position.x > x + .2f, "wind affects movement instead of being overwritten");
                }
                if (number == 9)
                {
                    physical.ProbePlace(new Vector2(0, -2.3f)); yield return new WaitForSecondsRealtime(.3f);
                    float y = physical.transform.position.y;
                    float end = Time.realtimeSinceStartup + .65f;
                    while (Time.realtimeSinceStartup < end) { logic.ProbeInput(1); yield return null; }
                    logic.ProbeInput();
                    Check(physical.transform.position.y > y + .7f, "lift carries the physical player upward");
                }
                if (number == 10)
                {
                    logic.ProbeInput(jump: true); yield return new WaitForSecondsRealtime(.15f);
                    Check(stage.GateOpen.Value, "timed gate opens from key press");
                    yield return new WaitForSecondsRealtime(3.7f);
                    Check(!stage.GateOpen.Value && stage.MechanicValue.Value == 0, "timed gate closes after window");
                }
                if (number == 12)
                {
                    physical.ProbePlace(goal); yield return new WaitForSecondsRealtime(.2f);
                    Check(!stage.Complete.Value, "final exit requires both relay gates");
                    logic.ProbePlace(stage.logicTargets[9].position); yield return new WaitForSecondsRealtime(.2f);
                    Check(stage.RelayStep.Value == 1 && !stage.puzzleGates[9].activeSelf, "first relay gate latches open");
                    logic.ProbePlace(new Vector2(3, -2.7f));
                }
                else if (number == 1) logic.ProbePlace(stage.logicSwitch.position);
                else if (number == 6 || number == 8) logic.ProbePlace(stage.logicTargets[number - 3].position);
                else if (number == 10) logic.ProbeInput(jump: true);
                if (number == 6) { physical.ProbeInput(1); physical.ProbePlace(goal, new Vector2(6, 0)); }
                else physical.ProbePlace(goal);
                yield return Until(() => number == 12 ? stage.Complete.Value : stage.StageIndex.Value == number + 1, 3, "stage " + number + " clears");
                Debug.Log("[POT_TEST] STAGE " + number.ToString("00") + " PASS: practice guard, reset, demo, objective and clear");
                yield return new WaitForSecondsRealtime(.4f);
            }
            yield return new WaitForSecondsRealtime(.8f);
            Check(stage.StageIndex.Value == 12 && stage.Complete.Value, "completion remains at last stage");
            stage.RestartFromBeginning(); yield return new WaitForSecondsRealtime(.35f);
            Check(stage.StageIndex.Value == 1 && stage.PracticeActive.Value && !stage.Complete.Value && stage.RelayStep.Value == 0, "restart resets all progression");
            Debug.Log("[POT_TEST] HOST PASS checks=" + checks);
            var leave = session.LeaveAsync(); while (!leave.IsCompleted) yield return null;
            yield return new WaitForSecondsRealtime(1); Application.Quit(0);
        }
        static IEnumerator Practice(StageState stage, CoopPlayer physical, CoopPlayer logic, int number)
        {
            if (number == 1) logic.ProbePlace(stage.logicSwitch.position);
            else if (number == 4 || number == 6 || number == 8 || number == 12) logic.ProbePlace(stage.logicTargets[number - 3].position);
            if (number == 2)
            {
                logic.ProbeInput(1); physical.ProbeInput(hold: true);
                yield return new WaitForSecondsRealtime(.15f); physical.ProbeInput(release: true);
            }
            else
            {
                if (number == 3 || number == 5 || number == 7 || number == 9 || number == 11) logic.ProbeInput(1);
                if (number == 10) logic.ProbeInput(jump: true);
                physical.ProbeInput(jump: true);
            }
            yield return Until(() => !stage.PracticeActive.Value, 3, "practice completes stage " + number);
            physical.ProbeInput(); logic.ProbeInput();
        }
    }
}
#endif
