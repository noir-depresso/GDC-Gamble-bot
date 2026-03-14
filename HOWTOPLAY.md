# HOW TO PLAY

This is the full player guide for the current version of the bot. It is written from the game as it exists right now, not a design draft.

## What Kind of Game This Is

You are playing a turn-based card combat game with an economy loop.

- Combat is powered by **bits** (you spend bits to play cards).
- Long-term progression and risk are tied to **money**.
- Between fights, you make economic decisions (packets, jobs, deck setup, difficulty).
- Over time, you earn **meta credits** and unlock tier perks.

Think of it as: "fight -> choose risk/reward -> prep economy -> fight again," with a lot of room for greedy lines vs safe lines.

---

## Quick Start (First 5 Minutes)

1. Start a run with `!newgame`.
2. Check your current state with `!status`.
3. Set your difficulty if needed: `!difficulty 2` (easy learning) or stay at default `3`.
4. Optional: place a pre-fight bet with `!bet 50` for extra opening bits.
5. View your hand with `!hand`.
6. If you are unsure what a card does, use `!inspect <index>`.
7. Play cards with `!play <index>`.
8. End your turn with `!end`.
9. After combat, resolve packet choice with `!packet <convert|credit|bank>`.
10. Start next combat with `!nextcombat`.

If you lose money too hard, run jobs with `!job <type>` between combats.

---

## Full Command Guide

## Core Commands

- `!help`
- `!newgame`
- `!status`
- `!hand`
- `!inspect <index>`
- `!meta`

## Combat Commands

- `!difficulty <1-5>`
- `!kit <thief|politician>`
- `!bet <amount>`
- `!play <index>`
- `!end`
- `!choose <choiceId> <option>`
- `!packet <convert|credit|bank>`
- `!useitem <index>`
- `!nextcombat`

## Deck and Economy Commands

- `!deck <bruiser> <medicate> <investment>`
- `!job <cleaning|fetch|delivery|snake|coinflip>`

## Duel Commands

- `!duel <@user|userId>`
- `!accept`
- `!decline`
- `!cancelduel`
- `!rematch`
- `!forfeit`

## How To Start Multiplayer

Right now, multiplayer is handled as a **duel** in a channel.

A duel is not started automatically just because two people are present. One player has to issue the challenge, and the other player has to accept it.

Here is the normal flow:

1. Player A types `!duel @PlayerB` in the channel.
2. The bot creates a pending duel challenge for that channel.
3. Player B types `!accept`.
4. The duel becomes active immediately.
5. The bot starts the duel game and gives the **first turn to the challenger**.

If the challenged player does not want to play:

- they can type `!decline`
- or the challenger can type `!cancelduel` before it is accepted

The challenge times out if it sits too long without being accepted.

### Example

- Ryan: `!duel @Alex`
- Alex: `!accept`
- Bot: duel starts, Ryan takes the first turn

### Important Things To Know

- Duels are **channel-based**, so only one duel flow should be running in a channel at a time.
- Only the two duel participants can control the duel once it starts.
- If it is not your turn, the bot will reject turn-locked actions like playing cards or ending turn.
- Each time a turn ends, control passes to the other player.
- Duel score is tracked across rounds.
- A duel is currently **first to 3 round wins**.
- When a round ends, the winner gets the next round's first turn.
- If someone gives up, they can use `!forfeit` and the other player is declared the winner.
- After a duel finishes, either participant can type `!rematch` to start a fresh duel set.

### Recommended Multiplayer Setup

If you want the smoothest experience, do this in order:

1. Both players decide who is challenging.
2. Challenger types `!duel @otherplayer`.
3. Challenged player types `!accept`.
4. Each player sets their preferred class with `!kit <thief|politician>`.
5. Optional: each player sets deck preference with `!deck <bruiser> <medicate> <investment>` before future new runs.
6. Use `!status` often so both players can see whose turn it is and what the duel score is.

### If You Just Want The Short Version

To start multiplayer:

- one player types `!duel @otherplayer`
- the other player types `!accept`
- the challenger goes first

## Useful Aliases

- `pkt` -> `packet`
- `next` -> `nextcombat`
- `cancel` -> `cancelduel`
- `resign` -> `forfeit`
- `create` -> `newgame`

## Slash Commands

`/game` supports these subcommands:

- `create`
- `kit`
- `deck`
- `difficulty`
- `bet`
- `choose`
- `useitem`
- `inspect`
- `job`
- `nextcombat`
- `status`
- `hand`
- `play`
- `end`
- `help`

Notes:
- `packet`, `meta`, and duel flow are still strongest via text commands.
- Multiplayer start flow is text-command only right now.

---

## Core Mechanics Explained

## Resources

- **Money**: long-term currency used in bets, packets, and economy flow.
- **Bits**: combat-only fuel for card plays.
- **Basic Income**: base round-end money gain before multipliers/effects.
- **Debt**: if money drops below zero.

## Betting

Before first turn of combat:

- `!bet <amount>` removes that amount from money and adds the same amount to starting bits.
- You can only bet at combat start.

## Turn Structure

Each turn goes like this:

1. You play cards in `Betting` or `PlayerMain` phase.
2. You end with `!end`.
3. Enemy attacks.
4. Round-end effects trigger (economy/status processing).
5. You draw (and extra draws if queued).
6. Next turn starts.

## Hand, Deck, Draw Rules

- Deck size is 32 cards.
- Max hand is 6.
- Start combat with 6 drawn cards.
- At new turns, card draw respects hand cap.
- Empty draw pile reshuffles discard pile.

## Winning a Combat

On win:

- Money reward = `50% of bits gained this combat + bet winnings`.
- Extra bits reward = `10% of money reward`.
- Unlock-tier money bonus may be added.
- You get a packet choice before next combat.

## Losing a Combat

On loss:

- Combat ends.
- Run streak resets.
- You still get some meta progress.
- If a growth-risk penalty is active, money penalty applies.

---

## Difficulty System (1 to 5)

Default is `3`.

## Difficulty Intent

- `1`: learning mode, forgiving numbers.
- `2`: easy mode, still generous.
- `3`: standard baseline.
- `4`: hard, mistakes hurt.
- `5`: expert, demands clean play.

## Multipliers by Difficulty

- Outgoing damage:
  - 1: x1.30
  - 2: x1.15
  - 3: x1.00
  - 4: x0.90
  - 5: x0.80

- Incoming damage:
  - 1: x0.75
  - 2: x0.90
  - 3: x1.00
  - 4: x1.15
  - 5: x1.30

- Round money gain:
  - 1: x1.30
  - 2: x1.15
  - 3: x1.00
  - 4: x0.90
  - 5: x0.80

Your `!status` screen shows both base multipliers and final multipliers (after encounter modifier).

---

## Encounter Modifiers

Every combat rolls one modifier.

- `None`: no global modifier.
- `Market Crash`: round money gains -25%.
- `Power Surge`: outgoing +15%, incoming +10%, round money -5%.
- `Audit`: outgoing -10%, round money +10%.

Higher difficulties bias toward harsher mixes more often.

---

## Intermission Packets

After each combat, choose one packet:

- `convert`
  - Spend money now.
  - Get next-combat bits bonus.
  - Good for tempo spikes.

- `credit`
  - Take debt for stronger next fight.
  - Adds combat drawback and defeat risk penalty.
  - High-risk/high-reward line.

- `bank`
  - Gain safe money now.
  - Next combat starts slower (bits penalty).
  - Stability option.

If you use `!nextcombat` or `!job` with a pending packet, the game auto-picks `bank` for pace.

---

## Jobs and Debt Recovery

Jobs are available between combats:

- `cleaning`
- `fetch`
- `delivery`
- `snake`
- `coinflip`

Job details:

- Jobs give money and some bits.
- There is fatigue scaling.
- Repeating the same job applies payout penalty.
- Some job outcomes are random (snake/coinflip mini-game style roll).
- Overwork periodically deals small strain damage.

Use jobs when:

- debt is blocking progress,
- your next packet/combat line needs money,
- you need to stabilize before a risky push.

---

## Meta Progression and Unlock Tiers

Meta progression currently tracks in runtime profile by user.

## How You Earn Meta Credits

- Combat win: +3
- Defeat: +1
- Every 3 combat wins: +5 milestone
- Run completion milestone: +10

## Unlock Tier Thresholds (Lifetime)

- Tier 1: 0+
- Tier 2: 20+
- Tier 3: 50+
- Tier 4: 90+

## Tier Perks

- Bonus starting bits each combat
- Bonus money on combat win

Use `!meta` to view:

- current/lifetime credits,
- unlock tier,
- runs completed/failed,
- distance to next tier.

---

## Deck Composition Command

`!deck <bruiser> <medicate> <investment>` lets you set your preferred card type mix for new runs.

- This preference is saved per user in memory for the bot runtime.
- It applies when you start a new game.
- Remaining slots are filled as special/available pool fallback.

Example:

- `!deck 14 10 8`

---

## Card Reference (Starter-Relevant and Playable)

Below are practical descriptions of cards currently in the starter pool.

## Bruiser Cards

- `TROJAN` (Trojan Malware, cost 100): single-hit heavy damage (35 base).
- `SOCIAL` (Social Pressure, cost 50): grows with its own stacks; stronger each time you keep using it.
- `WIRE` (The Wire, cost 50): cheap damage, with special post-kill synergy.
- `HIREDGUN` (Hired Gun, cost 150): deals damage and returns to hand if room.
- `TRAUMA` (Trauma Team, cost 125): if attacked soon, retaliates hard; otherwise heals.
- `EMP` (EMP, cost 75): applies short suppression utility.

## Medicate / Utility Cards

- `THERAPY` (50): heal 10.
- `NEURAL` (100): heal 30.
- `SUTURE` (100): heal + one-turn immunity.
- `HEDGING` (75): huge incoming reduction, but income penalty next turn.
- `ENCHANT` (100): damage buff setup line.
- `GUARDDOWN` (75): enemy attack reduction.
- `WANEWAX` (100): delayed heal based on dealt damage.
- `FIREWALL` (100): reflect part of incoming damage.
- `DISCREDIT` (50): conditional defense/reflect; punishes bad timing.
- `RAAN` (50): delayed extra draw.
- `CHAOS` (50): reshuffles your hand.
- `COIN` (75): high-variance damage (double/half).
- `RELEASE` (100): huge payoff only when low-HP condition is met.
- `PROSTH` (50): heal now, reduce max HP permanently.

## Investment Cards

- `BANK` (25): scales round money via stacks.
- `SELLHIGH` (75): increases round money gain (max stacks matter).
- `BUYLOW` (150): reduces card costs by 20% per stack (max 2).
- `STOCKS` (50): random economy swing each round.
- `LOAN` (25): money now, delayed repayment, catastrophe risk.
- `CRYPTO` (100): next-round gain boost from last round.
- `REALESTATE` (50): boosts basic income by random amount.

## Special / Variance

- `ROULETTE` (50): random self-damage, self-heal, or enemy-damage.

## Cards Present but Functionally Deferred

These exist but are intentionally placeholder/deferred right now:

- `RICHER`, `HACKERV`, `CROWBAR`, `TARGET`, `POSTER`, `EYE`, `SLICKTALK`

They may show in data/library but are not full strategic picks yet.

---

## Beginner Strategy (Simple, Reliable)

If you just want consistent wins:

1. Play on difficulty 2 or 3.
2. Open with at least one economy anchor (`BANK`/`SELLHIGH`) unless under pressure.
3. Keep enough bits for one defensive option before ending turn.
4. Use `HEDGING`/`SUTURE` before high-risk turns.
5. Take `bank` packets when your money is unstable.
6. Use jobs to avoid debt spirals instead of forcing bad combats.
7. Build toward tier 2 quickly for smoother starts.

---

## Advanced Tips and Tricks

- `BUYLOW` early is often worth the expensive setup if combat lasts multiple turns.
- `SELLHIGH` and `BANK` stack into strong long fights; avoid over-investing if you are close to lethal danger.
- `Power Surge` encounter makes burst lines stronger but also makes greed much riskier.
- `Audit` encounter rewards longer economy turns.
- `Market Crash` punishes passive economy play; lean into combat finish speed.
- `credit` packet can win hard combats but creates nasty lose-state penalties. Use it when you can actually leverage tempo.
- Repeating jobs is allowed but not free; rotate jobs when possible.
- Always read `!status` before committing to a line if you have pending packet effects or encounter pressure.

---

## Common Errors and What They Mean

- "Combat has ended" -> use `!nextcombat` or run jobs.
- "Resolve pending choice first" -> use `!choose` or `!packet`.
- "Bets can only be placed before first turn" -> you are past opening phase.
- "Not enough bits" -> lower-cost play, setup economy, or save bits.
- "You are in debt" -> do jobs to stabilize before forcing combat.
- "No pending choice" -> choice was already resolved or not created yet.

---

## Suggested Learning Path

- Session 1: play difficulty 2, focus on understanding turn order and bits.
- Session 2: learn packet choices and when to use jobs.
- Session 3: deliberately test one greedy economy run and one safe control run.
- Session 4+: start optimizing for meta tiers and encounter adaptation.

---

## Final Note

The game already has enough systems to reward careful planning, but it is still evolving. Some cards and subsystems are placeholders by design. If something feels odd, check whether the card is marked deferred above before building your whole strategy around it.

