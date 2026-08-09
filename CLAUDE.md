# Weapons of Order

This repository is being rebuilt from the approved title/landing screen while the game systems are defined deliberately before implementation.

## Current product direction

Weapons of Order is a forging-centered fantasy autobattler.

Players forge weapons and mundane armor, Runeforge weapons with Runes, equip Units and Hero Units before battle, and then watch an automatically resolved battle play back from its battle log/state transitions.

Pre-battle preparation matters. Under the current direction, players do not swap equipment or manually cast combat abilities during a battle.

The current title/landing screen is an approved visual baseline. Do not redesign it unless the task explicitly asks for a redesign.

## Source-of-truth order

When sources conflict, use this order:

1. The creator's explicit decision in the current task/conversation.
2. Canon references inside the relevant `.claude/skills/` skill.
3. `docs/design/GAME_VISION.md`.
4. `docs/design/SYSTEM_INDEX.md` and `docs/design/OPEN_QUESTIONS.md`.
5. Existing implementation/code.
6. Older drafts or historical implementation ideas.

A newer explicit creator decision overrides an older draft or implementation.

Never infer that existing code is canon merely because it exists.

## Context loading

Before substantial game-design or gameplay implementation work:

1. Read `docs/design/GAME_VISION.md`.
2. Read `docs/design/SYSTEM_INDEX.md`.
3. Load/read only the skill(s) relevant to the task.
4. Check `docs/design/OPEN_QUESTIONS.md` before filling a gap with an assumption.

Do not load every skill for every task. Skills exist so detailed subsystem context is loaded only when relevant.

## Creator authority and naming

The creator defines canonical names and rosters.

Do not invent as canon:
- Units
- Hero Units
- combat specialization/class names
- kingdom roster entries
- Runes or Rune derivations
- synergy names/effects
- settlements, factions, characters, or other lore names

Examples may be used only when clearly labeled as examples and never written into canonical game data without approval.

## Current design phase

System definition comes before production implementation.

When a system is still unresolved:
- keep the uncertainty explicit;
- add it to `docs/design/OPEN_QUESTIONS.md` when it is project-relevant;
- do not implement a convenient assumption as if it were final.

When the creator locks a subsystem decision:
- update the relevant skill's canonical reference if one exists;
- update `docs/design/SYSTEM_INDEX.md` status if needed;
- remove or revise the corresponding open question.

Create a new project skill only when a subsystem has enough stable rules that Claude will repeatedly need them. Broad project-wide facts belong in `docs/design/`, not in a skill.

## Architecture status

The current repository contains the React/Vite title-screen foundation.

Do not resurrect the old gameplay contracts, old grid assumptions, old fixed rune effects, old unit schemas, or old roadmap merely because they existed in previous commits.

The gameplay architecture beyond the currently locked design principles should be chosen after the relevant systems are defined.

The current combat direction is deterministic/pre-resolved presentation: the outcome/rules are resolved before or independently of the visual playback, and the client presents the resulting combat events rather than letting the player alter the outcome mid-battle.

Exact simulation implementation, persistence, networking, API contracts, grid dimensions, squad size, targeting, movement, and combat formulas are not globally locked unless a current design document says otherwise.

## Engineering discipline

- Prefer simple implementations that match the currently approved system.
- Do not over-engineer future systems.
- Keep tunable balance values data-driven when practical.
- Keep game rules out of purely visual UI components.
- Add focused tests for rules once those rules are actually approved.
- Do not build placeholder gameplay content with invented canonical names.
- Do not create a roadmap from assumptions. The roadmap should be rebuilt after enough core systems are defined.
