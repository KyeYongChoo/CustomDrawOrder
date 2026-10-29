using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace CustomDrawOrder;

/// <summary>
/// Cards the game itself put at a fixed spot in the draw pile, rather than where a shuffle happened to leave them.
/// A perfect shuffle can't move these, so they keep their place: top cards are drawn first and bottom cards last.
/// Every other card in the draw pile is the player's pick.
///
/// Top: Innate cards on turn 1, cards put on top by effects (Headbutt, Thinking Ahead, ...), Perfect Fit after a
/// reshuffle. Bottom: enchantments that start at the bottom of the draw pile on turn 1.
/// A card loses its pin as soon as it leaves the draw pile (drawn, discarded, reshuffled, ...).
/// </summary>
internal static class Pins
{
    public static readonly HashSet<CardModel> Top = new();
    public static readonly HashSet<CardModel> Bottom = new();

    /// <summary>Cards on their way to the top of a draw pile; pinned once they land there.</summary>
    private static readonly HashSet<CardModel> PendingTop = new();

    // Combat start: the draw pile is built and shuffled from scratch.
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.RandomizeOrderInternal))]
    private static class CombatStart
    {
        private static void Prefix()
        {
            Top.Clear();
            Bottom.Clear();
            PendingTop.Clear();
        }
    }

    // Every way of putting a card on top of the draw pile goes through this overload.
    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Add), typeof(IEnumerable<CardModel>), typeof(CardPile),
        typeof(CardPilePosition), typeof(AbstractModel), typeof(bool), typeof(bool))]
    private static class AddToTop
    {
        private static void Prefix(IEnumerable<CardModel> cards, CardPile newPile, CardPilePosition position)
        {
            if (newPile.Type == PileType.Draw && position == CardPilePosition.Top)
            {
                PendingTop.UnionWith(cards);
            }
        }
    }

    // A reshuffle re-adds every card, which unpins them; drop requests from adds that never landed.
    [HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Shuffle))]
    private static class ReshuffleStart
    {
        private static void Prefix() => PendingTop.Clear();
    }

    // Perfect Fit: "whenever you shuffle, put this card on top of your draw pile".
    [HarmonyPatch(typeof(PerfectFit), nameof(PerfectFit.ModifyShuffleOrder))]
    private static class PerfectFitOnTop
    {
        private static void Postfix(PerfectFit __instance, List<CardModel> cards, bool isInitialShuffle)
        {
            if (!isInitialShuffle && cards.Contains(__instance.Card))
            {
                PendingTop.Add(__instance.Card);
            }
        }
    }

    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    private static class Landed
    {
        private static void Postfix(CardPile __instance, CardModel card)
        {
            if (__instance.Type == PileType.Draw && PendingTop.Remove(card))
            {
                Top.Add(card);
            }
        }
    }

    [HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
    private static class Left
    {
        private static void Postfix(CardPile __instance, CardModel card)
        {
            if (__instance.Type == PileType.Draw)
            {
                Top.Remove(card);
                Bottom.Remove(card);
            }
        }
    }

    // Turn 1: CombatManager moves Innate cards to the top (and raises the hand draw to fit them).
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.MoveToTopInternal))]
    private static class MovedToTop
    {
        private static void Postfix(CardPile __instance, CardModel card)
        {
            if (__instance.Type == PileType.Draw)
            {
                Bottom.Remove(card);
                Top.Add(card);
            }
        }
    }

    // Turn 1: CombatManager moves cards whose enchantment says "start at the bottom" to the bottom.
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.MoveToBottomInternal))]
    private static class MovedToBottom
    {
        private static void Postfix(CardPile __instance, CardModel card)
        {
            if (__instance.Type == PileType.Draw)
            {
                Top.Remove(card);
                Bottom.Add(card);
            }
        }
    }
}
