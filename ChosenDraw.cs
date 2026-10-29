using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using ModToggle;

namespace CustomDrawOrder;

/// <summary>
/// Replaces the game's draw (every draw goes through <c>CardPileCmd.DrawInternal</c>: the hand draw at the start of
/// the turn and any "draw X cards" effect). Instead of taking the top card of the draw pile, the player chooses which
/// cards to draw.
///
/// The rest is the game's own draw, unchanged: draw prevention, the 10-card hand limit, reshuffling the discard pile
/// when the draw pile runs out, combat history, and every "when drawn" / "after a card is drawn" hook. Nothing is
/// played, generated or exhausted, so no other card, relic or power can tell the difference from a normal draw.
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), "DrawInternal")]
internal static class ChosenDraw
{
    private const string PromptKey = "CUSTOM_DRAW_ORDER.TO_DRAW";

    private static readonly Func<Player, bool> CheckIfDrawIsPossible =
        AccessTools.MethodDelegate<Func<Player, bool>>(
            AccessTools.Method(typeof(CardPileCmd), "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot"));

    private static bool Prefix(PlayerChoiceContext choiceContext, decimal count, Player player, bool fromHandDraw,
        ref Task<IEnumerable<CardModel>> __result)
    {
        // Turned off in the mod's settings: the game's own draw runs. (Pins keep tracking either way.)
        if (!ToggleSettings.Enabled)
        {
            return true;
        }
        __result = Draw(choiceContext, count, player, fromHandDraw);
        return false;
    }

    /// <summary>The game's DrawInternal (v0.111.0) with the top card replaced by <see cref="NextCard"/>.</summary>
    private static async Task<IEnumerable<CardModel>> Draw(PlayerChoiceContext choiceContext, decimal count,
        Player player, bool fromHandDraw)
    {
        if (CombatManager.Instance.IsOverOrEnding)
        {
            return Array.Empty<CardModel>();
        }
        ICombatState combatState = player.Creature.CombatState!;
        if (!Hook.ShouldDraw(combatState, player, fromHandDraw, out AbstractModel? modifier))
        {
            await Hook.AfterPreventingDraw(combatState, modifier!);
            return Array.Empty<CardModel>();
        }
        List<CardModel> result = new List<CardModel>();
        CardPile hand = PileType.Hand.GetPile(player);
        CardPile drawPile = PileType.Draw.GetPile(player);
        int drawsRequested = count > 0m ? (int)Math.Ceiling(count) : 0;
        if (drawsRequested == 0)
        {
            return result;
        }
        int num = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
        if (num == 0)
        {
            CheckIfDrawIsPossible(player);
            return result;
        }
        List<CardModel> picked = new List<CardModel>();
        for (int i = 0; i < drawsRequested; i++)
        {
            if (num <= 0)
            {
                break;
            }
            if (CombatManager.Instance.IsOverOrEnding)
            {
                break;
            }
            if (!CheckIfDrawIsPossible(player))
            {
                break;
            }
            await CardPileCmd.ShuffleIfNecessary(choiceContext, player);
            if (!CheckIfDrawIsPossible(player))
            {
                break;
            }
            CardModel? card = await NextCard(choiceContext, player, drawPile, picked, Math.Min(drawsRequested - i, num));
            if (card == null || hand.Cards.Count >= CardPile.MaxCardsInHand)
            {
                break;
            }
            result.Add(card);
            await CardPileCmd.Add(card, hand);
            CombatManager.Instance.History.CardDrawn(combatState, card, fromHandDraw);
            await Hook.AfterCardDrawn(combatState, choiceContext, card, fromHandDraw);
            card.InvokeDrawn();
            NDebugAudioManager.Instance?.Play("card_deal.mp3", 0.25f, PitchVariance.Small);
            num = Math.Max(0, CardPile.MaxCardsInHand - hand.Cards.Count);
        }
        return result;
    }

    /// <summary>
    /// The card to draw next. Pinned top cards come first, as they would in game. Otherwise the player picks up to
    /// <paramref name="wanted"/> cards in one screen (the rest of this draw, capped by hand space and by what is left
    /// before a reshuffle), which are then drawn one by one. A reshuffle part-way through a draw empties the pick, so
    /// the remaining draws get a new screen showing the reshuffled pile.
    /// </summary>
    private static async Task<CardModel?> NextCard(PlayerChoiceContext choiceContext, Player player, CardPile drawPile,
        List<CardModel> picked, int wanted)
    {
        CardModel? pinned = drawPile.Cards.FirstOrDefault(Pins.Top.Contains);
        if (pinned != null)
        {
            return pinned;
        }
        // A hook triggered by an earlier draw may have moved a picked card (or a nested draw taken it).
        picked.RemoveAll(c => c.Pile != drawPile);
        if (picked.Count == 0)
        {
            List<CardModel> free = drawPile.Cards.Where(c => !Pins.Bottom.Contains(c)).ToList();
            if (free.Count == 0)
            {
                return drawPile.Cards.FirstOrDefault();
            }
            int k = Math.Min(wanted, free.Count);
            if (k == free.Count || choiceContext is ThrowingPlayerChoiceContext)
            {
                // No choice to make: the draw takes everything that is left before the reshuffle.
                picked.AddRange(free.Take(k));
            }
            else
            {
                CardSelectorPrefs prefs = new CardSelectorPrefs(Prompt(), k) { RequireManualConfirmation = true };
                HashSet<CardModel> choosable = free.ToHashSet();
                picked.AddRange(await CardSelectCmd.FromCombatPile(choiceContext, drawPile, player, prefs, choosable.Contains));
                picked.RemoveAll(c => c.Pile != drawPile);
                if (picked.Count == 0)
                {
                    return drawPile.Cards.FirstOrDefault(c => !Pins.Bottom.Contains(c)) ?? drawPile.Cards.FirstOrDefault();
                }
            }
        }
        CardModel card = picked[0];
        picked.RemoveAt(0);
        return card;
    }

    /// <summary>"Choose N cards to Draw.", added to the card_selection table in whatever language is loaded.</summary>
    private static LocString Prompt()
    {
        LocTable table = LocManager.Instance.GetTable("card_selection");
        if (!table.IsLocalKey(PromptKey))
        {
            table.MergeWith(new Dictionary<string, string>
            {
                [PromptKey] = "Choose {Amount:plural:a card|[blue]{}[/blue] cards} to [gold]Draw[/gold].",
            });
        }
        return new LocString("card_selection", PromptKey);
    }
}
