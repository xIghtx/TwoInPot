# Festival revision design and implementation plan

> **For agentic workers:** Use superpowers:executing-plans. Continue inline under the user's standing instruction to implement clear requirements without repeated approval.

**Goal:** Fresh runs after leaving; longer, distinct stages; readable orange movement; meaningful blue reasoning.
**Architecture:** Preserve existing twelve mechanics and optional true-ending records. Add three extension stations after each original route. StageState owns replicated circuit progress; a focused StageCircuit handles rules and checkpoint geometry. Extend the world to x38 and follow the orange player locally; blue retains the original console area and receives a keyboard circuit panel. Original mechanics remain required before the extension is activated.
**Tech Stack:** Existing Unity 6000.6 / NGO / URP / UI Toolkit / keyboard. No new dependency.
**Spec:** This document records the user's requested revision and implementation decisions.

## Approaches and rulings

- Repeating the same map three times would lengthen play without adding identity; rejected.
- Replacing all twelve mechanics at once would discard working content; rejected.
- Extend each existing route with three station puzzles and distinctive obstacle profiles; chosen by agent under prior autonomy authorization. No new art-generation dependency. Three rule families: transform symbols, reverse a pattern, solve a four-lamp toggle circuit; clues originate on orange's screen and controls on blue's.
- Reset the whole run whenever a player leaves and whenever a host starts; not merely on app restart. Retain language/audio settings.
- Keep both secret assignments private, and keep four-record true-ending condition unchanged. Do not add time pressure or failure penalties to reasoning puzzles.
- Intermediate orange respawns reduce backtracking. Do not move the existing record pads.

## Tasks

- [x] Write/run failing real-process restart probe with dirty stage, records and ending, then same-process host shutdown/rehost.
- [x] Reset progression and player notes on peer departure/new network spawn. Re-run probe.
- [x] Add/test circuit rules and progression: wrong answer cannot open gate, ordered checkpoints cannot be skipped, correct answers unlock next crossing, practice/reset clears attempt, full restart clears all.
- [x] Build three extension stations in all twelve worlds; retain original obstacles and goals as entrance requirement. Distinct heights, spacing, rails, columns and lamp arrangement by stage. Physical camera follows extension; blue camera remains original.
- [x] Add blue Q/E selection + Enter confirmation panel, orange hint / contact / launch-direction dots, charge instructions. Preserve A/D/SPACE original mechanisms.
- [x] Run hidden two-process regression, include both role ownership paths, twelve extension routes, checkpoint respawn and session-reset probe. Inspect native captures.
- [x] Update readme; build and package a new ZIP without debug backups/old archives. Verify packed assembly hash.

## Review focus

- Reconnect cannot keep an old ending, mission bits or station latch.
- Controls are role-authorized and server-validated; keyboard repeat cannot skip stations.
- Physical orange cannot bypass the circuit by jumping past it; blue cannot complete before original route.
- Stage2 becomes hammer mode only in extension; stage2 original charging unchanged.
- Late client sees circuit and current checkpoint state. Existing secret record visibility and true ending remain.

## Verification limits

Automated placement/input is not a human difficulty assessment or a two-laptop wireless test. Report them separately. No focus-stealing UI tests.

Execution record: reset failed before fix (RevisionResetRed.log) and passed after (RevisionResetFinal.log). Final host/guest 1305/2098 checks, exit 0. Focused review found and fixed stale role caches. Ruling: implement inline without new worktree because Unity project is untracked and current runnable scene/assets must stay available in the user's checkout. No commits or remote changes. Human difficulty and two-device field testing remain explicitly unverified; checked tasks refer to implementation and automated verification only.

