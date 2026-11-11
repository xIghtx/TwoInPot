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
            var sound = UnityEngine.Object.FindAnyObjectByType<FestivalSfx>();
            Check(sound != null && sound.clips.Length == 9, "nine original effects are connected");
            foreach (var clip in sound.clips)
            {
                Check(clip != null && clip.channels == 1 && clip.length > .04f && clip.length < .6f, "effects are short mono clips");
                var samples = new float[clip.samples]; Check(clip.GetData(samples, 0), "effect PCM is readable");
                float peak = 0, energy = 0, tail = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(samples[i])); energy += samples[i] * samples[i];
                    if (i > samples.Length * .9f) tail += samples[i] * samples[i];
                }
                Check(peak > .02f && peak < .5f && energy > .01f && tail / Mathf.Max(1, samples.Length / 10) < .0002f, "effect has signal, headroom and a quiet release");
            }
            Check(view.GetComponent<UIDocument>().rootVisualElement.Q<Button>("toggleSfx") != null, "sound toggle is available");
            int played = sound.PlayedCount;
            FestivalSfx.Muted = true; FestivalSfx.Play(FestivalSfx.Cue.Select); yield return null;
            Check(sound.PlayedCount == played, "mute prevents new sound playback");
            foreach (var voice in sound.GetComponents<AudioSource>()) Check(voice.mute && voice.spatialBlend == 0, "pooled voices respect mute and use 2D audio");
            FestivalSfx.Muted = false; FestivalSfx.Play(FestivalSfx.Cue.Select);
            Check(sound.PlayedCount == played + 1, "unmute restores effect playback");
            yield return new WaitForSecondsRealtime(host ? 1 : 3);
            string target = SmokeProbe.DirectAddress(Environment.GetCommandLineArgs());
            Debug.Log(host ? "[POT_NET] Host listening on 0.0.0.0:7777; LAN address=" + session.LocalAddress : "[POT_NET] Client target=" + target + ":7777");
            if (host) session.HostDirect(); else session.JoinDirect(target);
            yield return Until(() => session.Ready && UnityEngine.Object.FindObjectsByType<CoopPlayer>().Length == 2, 30, "two peers connect");
            var stage = UnityEngine.Object.FindAnyObjectByType<StageState>();
            var players = UnityEngine.Object.FindObjectsByType<CoopPlayer>();
            CoopPlayer mine = null;
            foreach (var player in players) if (player.IsOwner) mine = player;
            Check(mine != null, "local player ownership");
            mine.ChooseRole(host ? PlayerRole.Physical : PlayerRole.Logic);
            Check(session.transport.MaxPacketQueueSize >= 1024 && session.manager.NetworkConfig.TickRate == 30, "transport capacity and network frequency are configured");
            yield return Until(() => stage.HostRoleChoice.Value >= 0 && stage.GuestRoleChoice.Value >= 0, 10, "role selection RPCs");
            mine.AcceptRole();
            yield return Until(() => stage.GameStarted.Value, 10, "both role acceptances start game");
            foreach (var player in players)
            {
                var art = player.GetComponent<PotPresentation>();
                Check(art.pixelArt && art.climberFrames.Length == 8 && art.engineerFrames.Length == 8, "both roles have complete pixel animation sets");
                foreach (var mesh in player.GetComponentsInChildren<MeshRenderer>(true)) Check(!mesh.enabled, "legacy player mesh is disabled");
                foreach (var frame in art.climberFrames) Check(frame != null && frame.texture.filterMode == FilterMode.Point, "physical frames remain crisp");
                foreach (var frame in art.engineerFrames) Check(frame != null && frame.texture.filterMode == FilterMode.Point, "logic frames remain crisp");
                Check(art.pixelHammerHead != null && Mathf.Approximately(art.pixelHammerHead.sprite.pixelsPerUnit, 24), "hammer head uses the character pixel density");
                Check(art.pixelHammerHead.transform.localScale == Vector3.one, "hammer head keeps its original shape");
            }
            Check(Camera.main.targetTexture != null && Camera.main.targetTexture.width == 640 && Camera.main.targetTexture.height == 360, "game uses the native pixel viewport");
            for (int room = 1; room <= 12; room++)
                Check(AssignmentText.Request(room, true, true) != AssignmentText.Request(room, false, true), "roles receive different notes in room " + room);
            Check(AssignmentText.Ending(7, true, true) != AssignmentText.Ending(15, true, true), "four successes changes the normal epilogue");
            Check(AssignmentText.Ending(127, true, true) != AssignmentText.Ending(255, true, true), "eight successes changes the normal epilogue");
            if (!host)
            {
                mine.ProbeSubmitInput(.75f);
                int practiceMask = 0, objectiveMask = 0, extensionMask = 0; float deadline = Time.realtimeSinceStartup + 240;
                bool sawComplete = false, sawTrueEnding = false, sawPrivateSuccess = false, sawNoteLayout = false, sentCircuitInput = false;
                var ui = view.GetComponent<UIDocument>().rootVisualElement;
                while (session.Ready && Time.realtimeSinceStartup < deadline)
                {
                    int bit = 1 << (stage.StageIndex.Value - 1);
                    if (stage.PracticeActive.Value) practiceMask |= bit; else objectiveMask |= bit;
                    if (stage.RouteFinished.Value) extensionMask |= bit;
                    if (!sentCircuitInput && stage.StageIndex.Value == 1 && stage.RouteFinished.Value)
                    {
                        mine.ProbeCircuitSubmit((CircuitRules.Answer(1, 0, 0) + 1) % 4); sentCircuitInput = true;
                    }
                    if (stage.Complete.Value && stage.StageIndex.Value == 12) sawComplete = true;
                    if (stage.EndingCode.Value == 2 && stage.MemoryBits.Value == 15) sawTrueEnding = true;
                    sawPrivateSuccess |= mine.AssignmentBits.Value != 0;
                    foreach (var remote in players) if (!remote.IsOwner) Check(remote.AssignmentBits.Value == 0 && remote.AssignmentProgress.Value == 0, "partner assignment results are not sent to guest");
                    if (!stage.PracticeActive.Value && !stage.Complete.Value && stage.GameStarted.Value)
                    {
                        var note = ui.Q<Label>("receivedNote");
                        Check(note != null && note.text.Contains(AssignmentText.Request(stage.StageIndex.Value, false, true)), "guest sees only its own instruction");
                        sawNoteLayout |= note.resolvedStyle.width > 0 && note.resolvedStyle.height > 0;
                    }
                    if (stage.GameStarted.Value)
                        Check(ui.Q<VisualElement>("practiceCard").style.display == (stage.PracticeActive.Value ? DisplayStyle.Flex : DisplayStyle.None), "guest practice UI matches network phase");
                    yield return new WaitForSecondsRealtime(.1f);
                }
                Check(practiceMask == 4095, "guest saw practice for all 12 stages");
                Check(objectiveMask == 4095, "guest saw objective phase for all 12 stages");
                Check(extensionMask == 4095, "guest received all twelve extended routes");
                Check(sawComplete, "guest received final completion");
                Check(sawTrueEnding, "guest received records and true ending");
                Check(sawPrivateSuccess, "guest received its own completed assignment");
                Check(sawNoteLayout, "private note has a visible layout after the UI layout pass");
                Check(sentCircuitInput, "guest submits its own circuit input RPC");
                Debug.Log("[POT_TEST] GUEST PASS checks=" + checks + " practiceMask=" + practiceMask + " objectiveMask=" + objectiveMask);
                Application.Quit(0); yield break;
            }
            CoopPlayer physical = null, logic = null;
            foreach (var player in players) if (player.Role.Value == PlayerRole.Physical) physical = player; else logic = player;
            yield return Until(() => logic.LaunchAimInput > .7f, 10, "guest owner input RPC reaches server");
            logic.ProbeInput();
            Check(Physics2D.GetIgnoreCollision(physical.GetComponent<BoxCollider2D>(), logic.GetComponent<BoxCollider2D>()), "players ignore each other's body colliders");
            logic.ProbePlace(new Vector2(-9, -2.3f)); physical.ProbePlace(new Vector2(-9, -.8f));
            yield return new WaitForSecondsRealtime(.9f);
            Check(Mathf.Abs(physical.transform.position.y - logic.transform.position.y) < .12f, "physical falls through partner and lands on the same floor");
            Check(physical.PresentationGrounded && logic.PresentationGrounded, "overlapping players still stand on scenery");
            stage.ResetStage(); yield return new WaitForSecondsRealtime(.3f);
            var story = stage.GetComponent<FacilityStory>();
            Check(story != null && stage.MemoryBits.Value == 0 && stage.EndingCode.Value == 0, "new session has no hidden records or ending");
            Check(!story.HasNearbySignal(mine), "ordinary rooms do not advertise a hidden ending path");
            Check(physical.GetComponent<PotPresentation>().climberAppearance.activeSelf && !physical.GetComponent<PotPresentation>().engineerAppearance.activeSelf, "physical has a human-in-pot climber appearance");
            Check(logic.GetComponent<PotPresentation>().engineerAppearance.activeSelf && !logic.GetComponent<PotPresentation>().climberAppearance.activeSelf, "logic has a distinct engineer appearance");
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
                if (number == 12)
                {
                    physical.ProbePlace(story.PhysicalPad); logic.ProbePlace(story.LogicPad);
                    yield return new WaitForSecondsRealtime(1.35f);
                    Check(stage.PracticeActive.Value && stage.MemoryBits.Value == 7, "record markers cannot unlock during practice");
                    stage.ResetStage(); yield return new WaitForSecondsRealtime(.4f);
                }
                yield return Practice(stage, physical, logic, number);
                Check(!stage.PracticeActive.Value && !stage.Complete.Value, "practice becomes objective stage " + number);
                if (number == 1) Check(physical.AssignmentBits.Value == 0 && logic.AssignmentBits.Value == 0, "practice actions cannot complete assignments");
                Check(physical.PracticeJumpCount == 0 && physical.PracticeLaunchCount == 0 && logic.PracticeMoveCount == 0, "practice action history clears stage " + number);
                if (number == 3 || number == 6 || number == 9)
                {
                    int previous = stage.MemoryBits.Value;
                    Check(!FacilityStory.Fragment(number / 3 - 1, true).Contains("기록 0"), "discoveries do not display a numbered collection counter");
                    physical.ProbePlace(story.PhysicalPad); logic.ProbePlace(story.LogicPad + Vector2.right * 3);
                    yield return new WaitForSecondsRealtime(1.35f);
                    Check(stage.MemoryBits.Value == previous, "one player cannot restore record " + number);
                    physical.ProbePlace(story.PhysicalPad); logic.ProbePlace(story.LogicPad);
                    yield return Until(() => stage.MemoryBits.Value != previous, 2, "two players restore record " + number);
                    Check(story.HasReadableFragment(physical) && !story.HasNearbySignal(physical), "restored notes can be reread without another collection prompt");
                    int restored = stage.MemoryBits.Value;
                    stage.ResetStage(); yield return new WaitForSecondsRealtime(.3f);
                    Check(stage.MemoryBits.Value == restored, "retry preserves a discovered record");
                    yield return new WaitForSecondsRealtime(1.3f);
                    Check(stage.MemoryBits.Value == restored, "practice cannot grant another record");
                    yield return Practice(stage, physical, logic, number);
                }
                yield return new WaitForSecondsRealtime(.35f);
                foreach (var mesh in UnityEngine.Object.FindObjectsByType<MeshRenderer>()) Check(!mesh.enabled, "stage " + number + " uses sprite scenery");
                if (number == 1 || number == 6 || number == 12)
                {
                    System.IO.Directory.CreateDirectory("Docs/Previews");
                    Camera.main.GetComponent<PixelViewport>().CaptureWorld("Docs/Previews/PixelRuntimeStage" + number.ToString("00") + ".png");
                }
                if (number == 1)
                {
                    float startX = physical.transform.position.x;
                    physical.ProbeInput(.75f); yield return new WaitForSecondsRealtime(.15f);
                    Check(Mathf.Abs(physical.transform.position.x - startX) < .12f && Mathf.Abs(physical.PresentationHammerAngle + 135) > 5, "A/D rotates hammer without walking the climber");
                    physical.ProbeInput(); physical.ProbeHammerAngle(-135);
                    float y = physical.transform.position.y;
                    physical.ProbeInput(jump: true); yield return new WaitForSecondsRealtime(.15f);
                    Check(physical.transform.position.y > y + .3f, "hammer contact pushes climber upward");
                    var hammerArt = physical.GetComponent<PotPresentation>();
                    Check(Vector3.Distance(hammerArt.pixelHammerHead.transform.position, hammerArt.hammerHead.position) < .01f, "drawn hammer head follows the actual contact head");
                    Check(Mathf.Abs(physical.GetComponent<PotPresentation>().model.localScale.y - 1) > .015f, "hammer push animates the climber");
                    Check(physical.GetComponent<PotPresentation>().CurrentPixelFrame == 5, "hammer ascent selects the launch pose");
                    yield return Until(() => physical.PresentationGrounded, 2, "hammer push lands on a platform or ground");
                    Check(physical.PresentationGrounded, "jump lands on ground");
                    physical.ProbePlace(stage.CurrentDefinition.physicalSpawn); physical.ProbeHammerAngle(-135);
                    yield return new WaitForSecondsRealtime(.3f);
                    physical.ProbeInput(jump: true);
                    yield return new WaitForSecondsRealtime(.1f);
                    float shortPushSpeed = physical.PresentationVelocity.y;
                    physical.ProbePlace(stage.CurrentDefinition.physicalSpawn);
                    yield return new WaitForSecondsRealtime(.3f);
                    int beforeCharge = physical.PracticeJumpCount;
                    float chargeEnd = Time.realtimeSinceStartup + .85f;
                    while (Time.realtimeSinceStartup < chargeEnd) { physical.ProbeInput(hold: true); yield return null; }
                    Check(physical.PracticeJumpCount == beforeCharge && physical.PushCharge.Value >= .95f, "holding charges without automatically launching");
                    physical.ProbeInput(release: true);
                    yield return new WaitForSecondsRealtime(.1f);
                    Check(physical.PresentationVelocity.y > shortPushSpeed + 2, "long SPACE hold produces a stronger push than a tap");
                    Check(physical.PushCharge.Value == 0, "release clears charge display");
                    yield return Until(() => physical.PresentationGrounded, 2, "charged push lands safely");
                    int pushes = physical.PracticeJumpCount;
                    physical.ProbePlace(new Vector2(-7, 2)); physical.ProbeInput(jump: true);
                    yield return new WaitForSecondsRealtime(.15f);
                    Check(physical.PracticeJumpCount == pushes && physical.PresentationVelocity.y < 0, "Space in empty air cannot create a free jump");
                    physical.ProbePlace(new Vector2(-7, -1.9f), new Vector2(0, -3));
                    yield return new WaitForSecondsRealtime(.14f);
                    physical.ProbeInput(jump: true); yield return new WaitForSecondsRealtime(.22f);
                    Check(physical.PresentationVelocity.y > 0, "hammer push input shortly before contact is buffered");
                    physical.ProbePlace(new Vector2(3.1f, -1)); physical.ProbeHammerAngle(0); physical.ProbeInput(hold: true);
                    yield return new WaitForSecondsRealtime(.12f);
                    Check(physical.ToolGripped, "Space holds hammer against a wall");
                    float climbingStart = physical.transform.position.y;
                    float climbEnd = Time.realtimeSinceStartup + .4f;
                    while (Time.realtimeSinceStartup < climbEnd) { physical.ProbeInput(1, hold: true); yield return null; }
                    Check(physical.transform.position.y > climbingStart + .4f, "rotating the braced hammer pulls the climber upward");
                    physical.ProbeInput(); yield return new WaitForSecondsRealtime(.06f);
                    Check(!physical.ToolGripped, "releasing Space releases the hammer anchor");
                    physical.ProbePlace(new Vector2(3.1f, -1)); physical.ProbeHammerAngle(0); physical.ProbeInput(hold: true);
                    yield return new WaitForSecondsRealtime(.12f);
                    Check(physical.ToolGripped, "climber can brace against the cooperative gate");
                    logic.ProbePlace(stage.logicSwitch.position);
                    yield return new WaitForSecondsRealtime(.12f);
                    Check(stage.GateOpen.Value && !physical.ToolGripped, "opening the partner's gate releases its hammer anchor");
                    yield return new WaitForSecondsRealtime(1.6f);
                    Check((physical.AssignmentBits.Value & 1) != 0, "two actual hammer pushes answer the physical note");
                    Check((logic.AssignmentBits.Value & 1) != 0, "holding the actual gate answers the logic note");
                    int physicalNotes = physical.AssignmentBits.Value, logicNotes = logic.AssignmentBits.Value;
                    stage.ResetStage(); yield return new WaitForSecondsRealtime(.4f);
                    Check(physical.AssignmentBits.Value == physicalNotes && logic.AssignmentBits.Value == logicNotes, "retry preserves replies without duplicating rewards");
                    yield return Practice(stage, physical, logic, 1);
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
                yield return Extension(stage, physical, logic, number);
                yield return Until(() => number == 12 ? stage.Complete.Value : stage.StageIndex.Value == number + 1, 3, "stage " + number + " clears");
                Debug.Log("[POT_TEST] STAGE " + number.ToString("00") + " PASS: practice guard, reset, demo, objective and clear");
                yield return new WaitForSecondsRealtime(.4f);
            }
            yield return new WaitForSecondsRealtime(.8f);
            Check(stage.StageIndex.Value == 12 && stage.Complete.Value, "completion remains at last stage");
            Check(stage.MemoryBits.Value == 7 && stage.EndingCode.Value == 1, "missing a record leads to the normal ending");
            stage.ResetStage(); yield return new WaitForSecondsRealtime(.4f);
            yield return Practice(stage, physical, logic, 12);
            physical.ProbePlace(story.PhysicalPad); logic.ProbePlace(story.LogicPad);
            yield return Until(() => stage.MemoryBits.Value == 15, 2, "final optional record restores all four");
            logic.ProbePlace(stage.logicTargets[9].position); yield return new WaitForSecondsRealtime(.2f);
            logic.ProbePlace(new Vector2(3, -2.7f)); physical.ProbePlace((Vector2)stage.exits[9].position + Vector2.right);
            yield return Extension(stage, physical, logic, 12);
            yield return Until(() => stage.Complete.Value, 2, "true ending final cooperative gate");
            Check(stage.EndingCode.Value == 2, "all optional records unlock the true ending");
            yield return new WaitForSecondsRealtime(.8f);
            Camera.main.GetComponent<PixelPerformance>().Report();
            stage.RestartFromBeginning(); yield return new WaitForSecondsRealtime(.35f);
            Check(stage.StageIndex.Value == 1 && stage.PracticeActive.Value && !stage.Complete.Value && stage.RelayStep.Value == 0, "restart resets all progression");
            Check(stage.MemoryBits.Value == 0 && stage.EndingCode.Value == 0, "full restart clears records and ending");
            Check(physical.AssignmentBits.Value == 0 && logic.AssignmentBits.Value == 0, "full restart clears personal replies");
            Check(!stage.RouteFinished.Value && stage.Station.Value == 0 && !stage.CircuitSolved.Value, "full restart clears all extension progress");
            stage.StageIndex.Value = 6; stage.MemoryBits.Value = 7; stage.RouteFinished.Value = true; stage.Station.Value = 2; stage.Complete.Value = true;
            physical.AssignmentBits.Value = 15;
            session.manager.DisconnectClient(logic.OwnerClientId);
            yield return Until(() => stage.StageIndex.Value == 1 && !stage.GameStarted.Value, 3, "peer departure resets the run for the next participant");
            Check(stage.MemoryBits.Value == 0 && stage.Station.Value == 0 && !stage.RouteFinished.Value && physical.AssignmentBits.Value == 0 && stage.EndingCode.Value == 0, "departure clears records, stations, notes and ending");
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
        static IEnumerator Extension(StageState stage, CoopPlayer physical, CoopPlayer logic, int number)
        {
            yield return Until(() => stage.RouteFinished.Value, 3, "original route opens the extension " + number);
            if (number == 1) yield return Until(() => stage.CircuitErrors.Value > 0, 3, "guest circuit RPC reaches the server and applies the rule");
            Check(stage.StageIndex.Value == number && !stage.Complete.Value, "first flag cannot skip the extension " + number);
            for (int station = 0; station < 3; station++)
            {
                int before = stage.CircuitStep.Value;
                stage.circuit.Submit(physical, 0);
                stage.circuit.Submit(logic, -1);
                Check(stage.CircuitStep.Value == before && stage.CircuitMask.Value == 0, "invalid or physical circuit input is rejected");
                physical.ProbePlace(StageCircuit.Point(number, 2)); yield return new WaitForSecondsRealtime(.1f);
                Check(stage.Station.Value == station, "checkpoint cannot be skipped before solving");
                if (CircuitRules.Family(number) != 2)
                {
                    stage.circuit.Submit(logic, (CircuitRules.Answer(number, station, 0) + 1) % 4);
                    yield return new WaitForSecondsRealtime(.1f);
                    Check(!stage.CircuitSolved.Value && stage.CircuitStep.Value == 0, "wrong sequence stays locked");
                    for (int i = 0; i < CircuitRules.Length(number); i++)
                    { stage.circuit.Submit(logic, CircuitRules.Answer(number, station, i)); yield return new WaitForSecondsRealtime(.1f); }
                }
                else
                {
                    int solution = -1;
                    for (int combination = 0; combination < 16; combination++)
                    {
                        int mask = 0; for (int button = 0; button < 4; button++) if ((combination & (1 << button)) != 0) mask ^= CircuitRules.Toggle(button);
                        if (mask == CircuitRules.Target(number, station)) { solution = combination; break; }
                    }
                    Check(solution >= 0, "lamp pattern is solvable");
                    // Move away so solving the third station cannot immediately touch its marker.
                    physical.ProbePlace(new Vector2(12, -2.3f));
                    for (int button = 0; button < 4; button++) if ((solution & (1 << button)) != 0)
                    { stage.circuit.Submit(logic, button); yield return new WaitForSecondsRealtime(.1f); }
                }
                // Correct answers alone cannot finish a route; Orange must reach each marker.
                if (stage.Station.Value == station) Check(stage.CircuitSolved.Value, "correct reasoning unlocks crossing");
                physical.ProbePlace(StageCircuit.Point(number, station));
                yield return Until(() => stage.Station.Value == station + 1, 2, "orange reaches station " + station);
                physical.Respawn();
                Check(Vector2.Distance(physical.transform.position, StageCircuit.Point(number, station)) < .15f, "respawn uses the last reached checkpoint");
                yield return new WaitForSecondsRealtime(.15f);
                if (station == 1 && (number == 1 || number == 6 || number == 12))
                {
                    Camera.main.GetComponent<PixelViewport>().CaptureWorld("Docs/Previews/ExtendedStage" + number.ToString("00") + ".png");
                }
            }
            if (number == 1)
            {
                physical.AssignRole(PlayerRole.Logic); logic.AssignRole(PlayerRole.Physical);
                physical.ProbePlace(new Vector2(36, -2.3f)); logic.ProbePlace(new Vector2(32, -2.3f));
                Check(!stage.circuit.TickServer(), "role swap cannot complete a route using the former orange player");
                logic.ProbePlace(new Vector2(36, -2.3f));
                Check(stage.circuit.TickServer(), "role swap tracks the current orange player");
                physical.AssignRole(PlayerRole.Physical); logic.AssignRole(PlayerRole.Logic);
            }
            physical.ProbePlace(new Vector2(36, -2.3f));
        }
    }
}
#endif

