# GDC Gambling Bot - Game Design Implementation Plan

Date: 2026-03-14
Scope: Game design and player experience (assume AI/multiplayer quality is already strong)

## 1. Goals

- Runs feel less samey after a few combats.
- Economy and combat choices impact each other quickly.
- Players have medium-term and long-term goals.
- Between-combat flow avoids chore turns.
- Difficulty feels fair, readable, and intentional.

## 2. Current Execution Status

- Phase A: Complete.
- Phase D: Started (difficulty profile + messaging implemented).
- Phase E: Started (meta currency + milestones + encounter modifiers foundation implemented).

## 3. Phase A - Core Loop Cohesion (Complete)

A1. Intermission packets (done)

- Post-combat packet choice exists.
- Current packet options: `convert`, `credit`, `bank`.
- Backward aliases supported: `safe`, `growth`, `spike`.

A2. Economy-to-combat conversion (done)

- `convert`: money to next-combat bits.
- `credit`: debt for next-combat power with penalties.
- `bank`: immediate money with next-combat tempo penalty.

A3. Dead-turn reduction (done)

- `!nextcombat` and `!job` auto-resolve intermission packet as `bank` for pacing.
- Job repeat changed from hard lockout to repeat penalty.
- Packet shortcut command available: `!packet <convert|credit|bank>`.

Definition of done check

- Every intermission has meaningful tradeoff: met.
- Economy choice impacts next combat in visible way: met.

## 4. Phase D - Difficulty and Fairness (In Progress)

D1. Difficulty intent per level

- Level 1-2: experiment-friendly and forgiving.
- Level 3: baseline tuned challenge.
- Level 4-5: punishing if planning is weak.

D2. Difficulty profile table (implemented)

| Level | Outgoing Damage | Incoming Damage | Round Money Gain | AI Tier Intent |
|---|---:|---:|---:|---|
| 1 | x1.30 | x0.75 | x1.30 | Tier 1 (simple, forgiving) |
| 2 | x1.15 | x0.90 | x1.15 | Tier 2 (light pressure) |
| 3 | x1.00 | x1.00 | x1.00 | Tier 3 (baseline) |
| 4 | x0.90 | x1.15 | x0.90 | Tier 4 (strong punish windows) |
| 5 | x0.80 | x1.30 | x0.80 | Tier 5 (high optimization expected) |

D3. Messaging improvements (implemented)

- Status text now shows:
  - difficulty level
  - base multipliers
  - final multipliers after encounter modifier
- Round logs call out scaling adjustments when values change.
- Incoming damage logs call out scaling adjustments when values change.

Remaining D work

- Tune exact multiplier numbers based on playtest data.
- Connect `AiIntelligenceTier` to actual AI behavior logic when AI phase starts.

## 5. Phase E - Replayability and Meta Progress (Started)

E1. Meta currency + unlock track (foundation implemented)

- Added `MetaCredits` and `LifetimeMetaCredits`.
- Added `UnlockTier` derived from lifetime meta credits.
- Combat outcomes now grant meta progress:
  - win: +3
  - defeat: +1
- Unlock tier thresholds:
  - Tier 1: 0+
  - Tier 2: 20+
  - Tier 3: 50+
  - Tier 4: 90+

E2. Run milestones (foundation implemented)

- Every 3 combat wins in a run grants milestone meta reward (+5).

E3. Encounter modifiers (foundation implemented)

- Per-combat lightweight modifier rolls at combat start:
  - `none`
  - `market_crash`: money gain down
  - `power_surge`: higher outgoing + higher incoming + slight money penalty
  - `audit`: lower outgoing + higher money gain
- Modifiers are surfaced in status and start-of-combat messaging.

Remaining E work

- Persist meta progression per player outside runtime memory.
- Add unlock content gates (card pools/relic-like options) tied to unlock tier.
- Expand milestone reward types beyond meta credits.

## 6. Next Prioritized Tasks

1. Phase B1: add post-combat deck reward choice (`add`/`upgrade`/`remove`/`reroll`).
2. Tie unlock tiers to tangible gameplay unlocks (first unlock set).
3. Add metrics export command for balancing checks.
4. Implement AI-tier behavior differences using current `AiIntelligenceTier`.
5. Run a tuning pass on packet values, milestone values, and encounter modifier frequencies.

## 7. Delivery Notes

- Keep phases deployable in small increments.
- Prefer additive changes over rewrites.
- Keep tuning data-driven and reversible.
- Prioritize features that improve decision quality or motivation.

## 8. Multiplayer Flow (Current Implementation)

The current multiplayer mode is a channel-based duel system.

It works like this:

1. One player challenges another with `!duel <@user|userId>`.
2. The challenged player accepts with `!accept`.
3. The duel becomes active in that channel.
4. The challenger takes the first turn.
5. Players alternate turns as they use normal game commands like `!play`, `!end`, `!status`, and so on.
6. When a round ends, the winner is recorded and the duel score is updated.
7. The winner of the round starts the next round.
8. The duel ends when one player reaches the round-win target.

Important implementation details:

- Duel state is stored in runtime memory at the service layer.
- Duel challenges are temporary and can expire if ignored.
- Only duel participants can control the duel once active.
- Turn-locked commands are restricted to the current turn owner.
- `!newgame` is reused internally as part of duel round setup/reset behavior.
- Preferred duel class is tracked per participant so class choice can be applied when turns/rounds rotate.
- `!rematch` restarts the duel set after a finished duel.
- `!forfeit` immediately ends the active duel and awards the win to the opponent.

What this means in practice for players:

- Multiplayer is already usable, but it is not a separate lobby system.
- You start it manually by challenging another user in the same channel.
- The bot treats it as an organized turn-based versus session rather than an always-on shared game.
- Text commands are the intended interface for this mode right now.

Suggested documentation standard going forward:

When multiplayer is mentioned in design or player-facing docs, describe the actual startup flow explicitly:

- who sends the challenge
- who accepts
- who gets first turn
- how turns pass
- how a round ends
- how the full duel ends
- how rematch/forfeit work

That is much easier for players to follow than just listing command names.
