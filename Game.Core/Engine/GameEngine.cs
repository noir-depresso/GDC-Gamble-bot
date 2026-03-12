using System;
using System.Collections.Generic;
using Game.Core.Cards;
using Game.Core.Effects;
using Game.Core.Models;
using Game.Core.Random;

namespace Game.Core.Engine
{
    public class GameEngine
    {
        public GameState State { get; private set; } = new();

        private readonly EffectRunner _runner = new();
        private readonly IRandom _random;

        public GameEngine(IRandom? random = null)
        {
            _random = random ?? new DefaultRandom();
        }

        public void StartNewGame()
        {
            State = CreateInitialState(CharacterClass.Thief);
        }

        public void LoadState(GameState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public GameState CreateInitialState(CharacterClass characterClass)
        {
            var state = new GameState
            {
                GameStateVersion = GameState.CurrentVersion,
                CharacterClass = characterClass,
                Player = new Combatant("Player", 0, 100, false),
                Enemy = new Combatant("Enemy", 20, 120, true),
                BasicIncome = 100,
                Money = characterClass == CharacterClass.Thief ? 400 : 600,
                Bits = characterClass == CharacterClass.Thief ? 80 : 120,
                BitsPerTurn = 25,
                MaxHandSize = 6,
                TurnNumber = 1,
                IsOver = false,
                Phase = GamePhase.Betting,
                Statuses = new Dictionary<string, StatusInstance>(),
                DiscardPileCardIds = new List<string>(),
                HandCardIds = new List<string>(),
                GeneratedItems = new List<string>(),
                LockedDeckCardIds = GetLockedCards(characterClass),
                FullDeckCardIds = BuildDeck(characterClass),
                JobFatigue = 0,
                JobsCompleted = 0,
                LastJobType = null
            };

            state.DrawPileCardIds = new List<string>(state.FullDeckCardIds);
            Shuffle(state.DrawPileCardIds);

            for (int i = 0; i < 6; i++) DrawCard(state);

            state.LastCheckpointMoney = state.Money;
            state.LastCheckpointPlayerHp = state.Player.CurrentHealth;
            return state;
        }

        public GameUpdate Apply(GameAction action) => Apply(State, action);

        public GameUpdate Apply(GameState state, GameAction action)
        {
            var update = action switch
            {
                SelectCharacterAction select => ApplySelectCharacter(state, select.CharacterClass),
                PlaceBetAction bet => ApplyPlaceBet(state, bet.Amount),
                StartNextCombatAction => ApplyStartNextCombat(state),
                WorkJobAction job => ApplyWorkJob(state, job.JobType),
                PlayCardAction play => ApplyPlayCard(state, play.HandIndex),
                ChooseOptionAction choice => ApplyChooseOption(state, choice),
                UseGeneratedItemAction useItem => ApplyUseItem(state, useItem.ItemIndex),
                EndTurnAction => ApplyEndTurn(state),
                _ => new GameUpdate { Errors = { $"Unsupported action type: {action.GetType().Name}" } }
            };

            if (ReferenceEquals(state, State)) State = state;
            return update;
        }

        public int FinalCost(CardDef card) => FinalCost(State, card);

        public int FinalCost(GameState state, CardDef card)
        {
            int stacks = Math.Min(2, state.GetStacks(EffectIds.BUY_LOW));
            float mult = 1f - (0.20f * stacks);
            if (mult < 0f) mult = 0f;

            int cost = (int)MathF.Round(card.BaseCostBits * mult);
            if (card.BaseCostBits > 0 && cost < 1) cost = 1;
            return cost;
        }

        private GameUpdate ApplySelectCharacter(GameState state, CharacterClass characterClass)
        {
            if (state.TurnNumber != 1 || state.Phase != GamePhase.Betting || state.BetAmount > 0 || state.BitsSpentThisCombat > 0)
                return new GameUpdate { Errors = { "Character can only be selected at the very start of combat (before betting/playing)." } };

            var next = CreateInitialState(characterClass);
            CopyState(next, state);
            return new GameUpdate { Messages = { $"Character selected: {characterClass}." }, StateChanged = true };
        }

        private GameUpdate ApplyPlaceBet(GameState state, int amount)
        {
            var update = new GameUpdate();

            if (state.IsOver || state.Phase == GamePhase.CombatEnded)
            {
                update.Errors.Add("Combat has ended. Start the next combat before betting.");
                return update;
            }

            if (state.InDebt)
            {
                update.Errors.Add("You are in debt. Use jobs to clear debt before starting the next combat.");
                return update;
            }

            if (state.TurnNumber != 1 || state.Phase != GamePhase.Betting)
            {
                update.Errors.Add("Bets can only be placed before the first turn.");
                return update;
            }

            if (amount < 1 || amount > 1000)
            {
                update.Errors.Add("Bet amount must be between 1 and 1000.");
                return update;
            }

            if (state.Money < amount)
            {
                update.Errors.Add($"Not enough money to bet {amount}. You have {state.Money}.");
                return update;
            }

            state.Money -= amount;
            state.BetAmount += amount;
            state.Bits += amount;
            update.Messages.Add($"Bet placed: {amount}. Starting bits increased by {amount}.");
            update.StateChanged = true;
            return update;
        }

        private GameUpdate ApplyStartNextCombat(GameState state)
        {
            var update = new GameUpdate();

            if (!state.IsOver || state.Phase != GamePhase.CombatEnded)
            {
                update.Errors.Add("Current combat is still active.");
                return update;
            }

            if (state.InDebt)
            {
                state.Money = Math.Max(0, state.LastCheckpointMoney);
                state.Player.CurrentHealth = Math.Max(1, Math.Min(state.Player.MaxHealth, state.LastCheckpointPlayerHp));
                state.JobFatigue = 0;
                state.LastJobType = null;
                update.Messages.Add("Debt unresolved before combat: you were reset to the last checkpoint.");
            }

            if (state.Player.IsDead)
            {
                state.Player.CurrentHealth = Math.Max(1, state.LastCheckpointPlayerHp);
                update.Messages.Add("You were restored to your last checkpoint health.");
            }

            ResetCombatState(state);
            update.Messages.Add("New combat started.");
            update.StateChanged = true;
            return update;
        }

        private GameUpdate ApplyWorkJob(GameState state, string jobType)
        {
            var update = new GameUpdate();

            if (!state.IsOver || state.Phase != GamePhase.CombatEnded)
            {
                update.Errors.Add("Jobs are only available between combats.");
                return update;
            }

            string normalized = jobType?.Trim().ToLowerInvariant() ?? string.Empty;
            int basePayout = normalized switch
            {
                "cleaning" => 60,
                "fetch" => 80,
                "delivery" => 100,
                "snake" => 65,
                "coinflip" => 50,
                _ => -1
            };

            if (basePayout < 0)
            {
                update.Errors.Add("Unknown job. Valid jobs: cleaning, fetch, delivery, snake, coinflip.");
                return update;
            }

            if (string.Equals(state.LastJobType, normalized, StringComparison.OrdinalIgnoreCase))
            {
                update.Errors.Add("Job cooldown: you cannot run the same job twice in a row.");
                return update;
            }

            float fatiguePenalty = Math.Min(0.50f, state.JobFatigue * 0.05f);
            int payout = (int)MathF.Round(basePayout * (1f - fatiguePenalty));
            if (payout < 1) payout = 1;
            int bitsReward = Math.Max(1, (int)MathF.Round(payout * 0.20f));

            if (normalized == "snake")
            {
                int apples = _random.Next(1, 8);
                int length = 3 + apples;
                int snakeMoney = 40 + (apples * 35);
                payout = Math.Max(1, (int)MathF.Round(snakeMoney * (1f - fatiguePenalty)));
                bitsReward = 6 + (apples * 3);
                update.Messages.Add($"Mini-game Snake: length `{length}` (apples eaten `{apples}`).");
            }
            else if (normalized == "coinflip")
            {
                int streak = 0;
                while (streak < 5 && _random.Next(0, 100) < 55) streak++;
                int coinMoney = 35 + (streak * 45);
                payout = Math.Max(1, (int)MathF.Round(coinMoney * (1f - fatiguePenalty)));
                bitsReward = 4 + (streak * 6);
                update.Messages.Add($"Mini-game Coinflip: streak `{streak}`.");
            }

            state.Money += payout;
            state.Bits += bitsReward;
            state.JobFatigue += 1;
            state.JobsCompleted += 1;
            state.LastJobType = normalized;

            update.Messages.Add($"Job complete: {normalized}. Earned +{payout} money and +{bitsReward} bits (fatigue penalty {(int)MathF.Round(fatiguePenalty * 100)}%).");

            if (state.JobFatigue % 3 == 0)
            {
                state.Player.TakeDamage(5);
                if (state.Player.CurrentHealth <= 0)
                {
                    state.Player.CurrentHealth = 1;
                }
                update.Messages.Add("Overwork consequence: took 5 strain damage.");
            }

            if (!state.InDebt)
            {
                update.Messages.Add("Debt cleared or not present. You can start the next combat.");
            }

            update.StateChanged = true;
            return update;
        }

        private GameUpdate ApplyPlayCard(GameState state, int index)
        {
            var update = new GameUpdate();

            if (state.IsOver || state.Phase == GamePhase.CombatEnded)
            {
                update.Errors.Add("Combat has ended. Use next combat or jobs.");
                return update;
            }

            if (state.PendingChoice != null)
            {
                update.Errors.Add("Resolve the pending choice first (`choose <option>`).");
                return update;
            }

            if (!(state.Phase == GamePhase.Betting || state.Phase == GamePhase.PlayerMain))
            {
                update.Errors.Add("You cannot play a card right now.");
                return update;
            }

            if (index < 0 || index >= state.HandCardIds.Count)
            {
                update.Errors.Add("Invalid card index.");
                return update;
            }

            string cardId = state.HandCardIds[index];
            CardDef card = CardLibrary.GetById(cardId);

            if (state.BlockedCardType != null && string.Equals(state.BlockedCardType, card.Type.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                update.Errors.Add($"Card type {card.Type} is temporarily blocked.");
                return update;
            }

            int cost = FinalCost(state, card);
            if (state.Bits < cost)
            {
                update.Errors.Add($"Not enough bits for **{card.Name}**. Need {cost}, you have {state.Bits}.");
                return update;
            }

            state.Phase = GamePhase.PlayerMain;
            state.Bits -= cost;
            state.BitsSpentThisCombat += cost;

            state.HandCardIds.RemoveAt(index);
            state.DiscardPileCardIds.Add(card.Id);

            update.Messages.Add($"You played **{card.Name}** (paid {cost} bits).\nType: {card.Type}");

            var ctx = new EffectContext(state.Player, state.Enemy, state, _random);
            string log = _runner.RunOnPlay(card, ctx);
            if (!string.IsNullOrWhiteSpace(log)) update.Messages.Add(log);

            HandlePostPlayRules(state, card, update);

            if (state.Enemy.IsDead)
            {
                if (card.Id == "WIRE") state.PendingExtraDraws += 1;
                ResolveWin(state, update);
            }

            update.StateChanged = true;
            return update;
        }

        private GameUpdate ApplyChooseOption(GameState state, ChooseOptionAction action)
        {
            var update = new GameUpdate();
            var pending = state.PendingChoice;

            if (pending == null)
            {
                update.Errors.Add("No pending choice.");
                return update;
            }

            if (!string.Equals(pending.ChoiceId, action.ChoiceId, StringComparison.Ordinal))
            {
                update.Errors.Add("Choice ID mismatch.");
                return update;
            }

            if (!pending.Options.Contains(action.OptionId))
            {
                update.Errors.Add("Invalid option.");
                return update;
            }

            switch (pending.ChoiceType)
            {
                case "PERSUASION":
                    if (action.OptionId == "enemy_debuff")
                    {
                        int newAttack = (int)MathF.Round(state.Enemy.Attack * 0.8f);
                        int delta = state.Enemy.Attack - newAttack;
                        state.Enemy.Attack = newAttack;
                        update.Messages.Add($"Persuasion: enemy attack reduced by {delta}.");
                    }
                    else
                    {
                        state.AddStacks(EffectIds.PERSUASION_BUFF, 1, 2);
                        update.Messages.Add("Persuasion: your card damage is boosted by 25% for 1 turn.");
                    }
                    break;
                case "DDOS":
                    if (action.OptionId == "see_cards")
                    {
                        update.Messages.Add($"D-dos peek: enemy attack={state.Enemy.Attack}, HP={state.Enemy.CurrentHealth}/{state.Enemy.MaxHealth}.");
                    }
                    else
                    {
                        update.Messages.Add("D-dos probability: next draw is uniformly distributed from current draw pile.");
                    }
                    break;
                case "SCAPEGOAT":
                    state.BlockedCardType = action.OptionId;
                    state.BlockedCardTypeTurns = 2;
                    update.Messages.Add($"ScapeGoat: blocked card type `{action.OptionId}` for 2 turns.");
                    break;
                case "EXPOSE":
                    int handIndex = int.Parse(action.OptionId);
                    if (handIndex >= 0 && handIndex < state.HandCardIds.Count)
                    {
                        string sacrificed = state.HandCardIds[handIndex];
                        state.HandCardIds.RemoveAt(handIndex);
                        state.DiscardPileCardIds.Add(sacrificed);
                        state.Enemy.TakeDamage(75);
                        update.Messages.Add("Expose: sacrificed one card and dealt 75 damage.");
                        if (state.Enemy.IsDead)
                            ResolveWin(state, update);
                    }
                    break;
                default:
                    update.Messages.Add($"Choice resolved for {pending.ChoiceType} with option {action.OptionId}.");
                    break;
            }

            state.PendingChoice = null;
            update.StateChanged = true;
            return update;
        }

        private GameUpdate ApplyUseItem(GameState state, int itemIndex)
        {
            var update = new GameUpdate();
            if (itemIndex < 0 || itemIndex >= state.GeneratedItems.Count)
            {
                update.Errors.Add("Invalid generated item index.");
                return update;
            }

            string item = state.GeneratedItems[itemIndex];
            state.GeneratedItems.RemoveAt(itemIndex);

            switch (item)
            {
                case "BloodyBullet":
                    state.Enemy.TakeDamage(30);
                    update.Messages.Add("Used BloodyBullet: dealt 30 damage.");
                    break;
                case "PatchKit":
                    state.Player.Heal(15);
                    update.Messages.Add("Used PatchKit: healed 15 HP.");
                    break;
                case "BitCache":
                    state.Bits += 20;
                    update.Messages.Add("Used BitCache: gained 20 bits.");
                    break;
            }

            if (state.Enemy.IsDead)
                ResolveWin(state, update);

            update.StateChanged = true;
            return update;
        }

        private void HandlePostPlayRules(GameState state, CardDef card, GameUpdate update)
        {
            if (card.Id == "HIREDGUN")
            {
                if (state.HandCardIds.Count < state.MaxHandSize)
                {
                    state.HandCardIds.Add(card.Id);
                    if (state.DiscardPileCardIds.Count > 0)
                        state.DiscardPileCardIds.RemoveAt(state.DiscardPileCardIds.Count - 1);
                    update.Messages.Add("Hired Gun returned to your hand.");
                }
                else
                {
                    update.Messages.Add("Hired Gun could not return (hand full).");
                }
            }

            if (state.GetStacks("CHAOS_RESHUFFLE_HAND") > 0)
            {
                state.RemoveStatus("CHAOS_RESHUFFLE_HAND");
                int toDraw = state.HandCardIds.Count;
                state.DiscardPileCardIds.AddRange(state.HandCardIds);
                state.HandCardIds.Clear();
                for (int i = 0; i < toDraw; i++) DrawCard(state);
                update.Messages.Add("Chaos reshuffled your hand.");
            }
        }

        private GameUpdate ApplyEndTurn(GameState state)
        {
            var update = new GameUpdate();

            if (state.IsOver || state.Phase == GamePhase.CombatEnded)
            {
                update.Errors.Add("Combat has ended. Use next combat or jobs.");
                return update;
            }

            if (state.PendingChoice != null)
            {
                update.Errors.Add("Resolve pending choice before ending turn.");
                return update;
            }

            if (!(state.Phase == GamePhase.Betting || state.Phase == GamePhase.PlayerMain))
            {
                update.Errors.Add("You cannot end turn right now.");
                return update;
            }

            state.Phase = GamePhase.EnemyTurn;
            update.Messages.Add("**You ended your turn.**");

            int incoming = state.Enemy.Attack;
            var beforeCtx = new EffectContext(state.Player, state.Enemy, state, _random) { PendingDamage = incoming };
            string beforeLog = _runner.FireTrigger(EffectTrigger.OnBeforeTakeDamage, beforeCtx);
            if (!string.IsNullOrWhiteSpace(beforeLog)) update.Messages.Add(beforeLog);

            int finalIncoming = beforeCtx.PendingDamage;
            state.Player.TakeDamage(finalIncoming);
            state.WasAttackedThisTurn = finalIncoming > 0;
            update.Messages.Add($"Enemy attacks for {finalIncoming}. Player HP: {state.Player.CurrentHealth}/{state.Player.MaxHealth}.");

            var afterCtx = new EffectContext(state.Player, state.Enemy, state, _random) { PendingDamage = finalIncoming };
            string afterLog = _runner.FireTrigger(EffectTrigger.OnDamageTaken, afterCtx);
            if (!string.IsNullOrWhiteSpace(afterLog)) update.Messages.Add(afterLog);

            if (state.Player.IsDead)
            {
                update.Messages.Add("You died. Combat lost.");
                state.IsOver = true;
                state.Phase = GamePhase.CombatEnded;
                update.StateChanged = true;
                return update;
            }

            state.Phase = GamePhase.RoundEnd;
            update.Messages.Add(string.Empty);
            update.Messages.Add("**End of Round**");

            var roundCtx = new EffectContext(state.Player, state.Enemy, state, _random);
            string roundLog = _runner.FireTrigger(EffectTrigger.OnRoundEnd, roundCtx);
            if (!string.IsNullOrWhiteSpace(roundLog)) update.Messages.Add(roundLog);

            int bitsGain = state.BitsPerTurn;
            if (state.GetStacks(EffectIds.HEDGING_INCOME_PENALTY) > 0)
                bitsGain = (int)MathF.Round(bitsGain * 0.5f);

            state.Bits += bitsGain;
            state.BitsGainedThisCombat += bitsGain;
            update.Messages.Add($"Bits gained this round: +{bitsGain}. Current bits: {state.Bits}.");

            string turnEndLog = _runner.FireTrigger(EffectTrigger.OnTurnEnd, roundCtx);
            if (!string.IsNullOrWhiteSpace(turnEndLog)) update.Messages.Add(turnEndLog);

            var spider = state.GetStatus("SPIDER_BUILD");
            if (spider != null && spider.DurationTurns == 1)
            {
                string[] pool = { "BloodyBullet", "PatchKit", "BitCache" };
                string item = pool[_random.Next(0, pool.Length)];
                state.GeneratedItems.Add(item);
                update.Messages.Add($"Spider Androids built item: {item}.");
            }

            state.TickDurations();

            state.TurnNumber++;
            DrawCard(state);
            while (state.PendingExtraDraws > 0)
            {
                DrawCard(state);
                state.PendingExtraDraws -= 1;
            }

            state.WasAttackedThisTurn = false;
            state.Phase = GamePhase.PlayerMain;
            update.Messages.Add(string.Empty);
            update.Messages.Add("**New turn started. You drew cards.**");

            update.StateChanged = true;
            return update;
        }

        private void ResolveWin(GameState state, GameUpdate update)
        {
            state.IsOver = true;
            state.Phase = GamePhase.CombatEnded;

            int bitConversion = (int)MathF.Round(state.BitsGainedThisCombat * 0.50f);
            int betWinnings = state.BetAmount;
            int total = bitConversion + betWinnings;
            int bitsFromMoneyWin = Math.Max(0, (int)MathF.Round(total * 0.10f));
            state.Money += total;
            state.Bits += bitsFromMoneyWin;

            state.LastCheckpointMoney = state.Money;
            state.LastCheckpointPlayerHp = state.Player.CurrentHealth;

            update.Messages.Add("Enemy defeated. You win.");
            update.Messages.Add($"Post-combat winnings: +{bitConversion} (bits) +{betWinnings} (bet) = +{total} money.");
            update.Messages.Add($"Economic sync bonus: +{bitsFromMoneyWin} bits from winnings.");
        }

        private void ResetCombatState(GameState state)
        {
            state.IsOver = false;
            state.Phase = GamePhase.Betting;
            state.TurnNumber = 1;
            state.BetAmount = 0;
            state.BitsSpentThisCombat = 0;
            state.BitsGainedThisCombat = 0;
            state.LastRoundMoneyGain = 0;
            state.PendingExtraDraws = 0;
            state.WasAttackedThisTurn = false;
            state.BlockedCardType = null;
            state.BlockedCardTypeTurns = 0;
            state.PendingChoice = null;
            state.GeneratedItems.Clear();
            state.Statuses.Clear();

            state.Enemy = new Combatant("Enemy", 20, 120, true);
            state.Bits = state.CharacterClass == CharacterClass.Thief ? 80 : 120;

            state.DrawPileCardIds = new List<string>(state.FullDeckCardIds);
            state.DiscardPileCardIds = new List<string>();
            state.HandCardIds = new List<string>();
            Shuffle(state.DrawPileCardIds);
            for (int i = 0; i < 6; i++) DrawCard(state);
        }

        private void DrawCard(GameState state)
        {
            if (state.HandCardIds.Count >= state.MaxHandSize) return;

            if (state.DrawPileCardIds.Count == 0)
            {
                if (state.DiscardPileCardIds.Count == 0) return;
                state.DrawPileCardIds = new List<string>(state.DiscardPileCardIds);
                state.DiscardPileCardIds.Clear();
                Shuffle(state.DrawPileCardIds);
            }

            string next = state.DrawPileCardIds[0];
            state.DrawPileCardIds.RemoveAt(0);
            state.HandCardIds.Add(next);
        }

        private void Shuffle(List<string> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        private static List<string> GetLockedCards(CharacterClass cls)
        {
            return cls == CharacterClass.Thief
                ? new List<string> { "TROJAN", "WIRE", "HIREDGUN" }
                : new List<string> { "SOCIAL", "PERSUASION", "GUARDDOWN" };
        }

        private static List<string> BuildDeck(CharacterClass cls)
        {
            var deck = CardLibrary.StarterDeckCardIds();
            var lockedCards = GetLockedCards(cls);

            for (int i = 0; i < lockedCards.Count && i < deck.Count; i++)
                deck[i] = lockedCards[i];

            if (deck.Count > 32)
                deck = deck.GetRange(0, 32);

            while (deck.Count < 32)
                deck.Add("TROJAN");

            return deck;
        }

        private static void CopyState(GameState src, GameState dst)
        {
            dst.GameStateVersion = src.GameStateVersion;
            dst.TurnNumber = src.TurnNumber;
            dst.IsOver = src.IsOver;
            dst.Phase = src.Phase;
            dst.CharacterClass = src.CharacterClass;
            dst.LockedDeckCardIds = new List<string>(src.LockedDeckCardIds);
            dst.FullDeckCardIds = new List<string>(src.FullDeckCardIds);
            dst.BasicIncome = src.BasicIncome;
            dst.Money = src.Money;
            dst.Bits = src.Bits;
            dst.BitsPerTurn = src.BitsPerTurn;
            dst.MaxHandSize = src.MaxHandSize;
            dst.BetAmount = src.BetAmount;
            dst.BitsSpentThisCombat = src.BitsSpentThisCombat;
            dst.BitsGainedThisCombat = src.BitsGainedThisCombat;
            dst.LastRoundMoneyGain = src.LastRoundMoneyGain;
            dst.PendingExtraDraws = src.PendingExtraDraws;
            dst.WasAttackedThisTurn = src.WasAttackedThisTurn;
            dst.BlockedCardType = src.BlockedCardType;
            dst.BlockedCardTypeTurns = src.BlockedCardTypeTurns;
            dst.PendingChoice = src.PendingChoice;
            dst.GeneratedItems = new List<string>(src.GeneratedItems);
            dst.LastCheckpointMoney = src.LastCheckpointMoney;
            dst.LastCheckpointPlayerHp = src.LastCheckpointPlayerHp;
            dst.JobFatigue = src.JobFatigue;
            dst.JobsCompleted = src.JobsCompleted;
            dst.LastJobType = src.LastJobType;
            dst.Player = src.Player;
            dst.Enemy = src.Enemy;
            dst.DrawPileCardIds = new List<string>(src.DrawPileCardIds);
            dst.DiscardPileCardIds = new List<string>(src.DiscardPileCardIds);
            dst.HandCardIds = new List<string>(src.HandCardIds);
            dst.Statuses = src.Statuses;
        }
    }
}





