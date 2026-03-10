using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public sealed class BattleDeckEditSession
    {
        private readonly int _requiredDeckSize;
        private readonly int _maxDeckSwaps;
        private readonly int _maxNeutralCards;
        private readonly int _maxSupportCards;
        private readonly int _minPrimaryCards;
        private readonly int _maxCopiesPerCard;
        private readonly List<string> _editingDeckCardIds = new();
        private readonly List<string> _baseDeckCardIds = new();
        private ElementType _primaryElement = ElementType.None;

        public BattleDeckEditSession(int requiredDeckSize, int maxDeckSwaps, int maxNeutralCards, int maxSupportCards, int minPrimaryCards, int maxCopiesPerCard)
        {
            _requiredDeckSize = requiredDeckSize;
            _maxDeckSwaps = maxDeckSwaps;
            _maxNeutralCards = maxNeutralCards;
            _maxSupportCards = maxSupportCards;
            _minPrimaryCards = minPrimaryCards;
            _maxCopiesPerCard = maxCopiesPerCard;
        }

        public int SelectedSlotIndex { get; private set; } = -1;

        public IReadOnlyList<string> EditingDeckCardIds => _editingDeckCardIds;

        public void ApplyPreset(BattleDeckPresetData preset)
        {
            _baseDeckCardIds.Clear();
            _editingDeckCardIds.Clear();
            _primaryElement = preset?.primaryElement ?? ElementType.None;

            if (preset?.cardIds != null)
            {
                for (var i = 0; i < preset.cardIds.Count; i++)
                {
                    _baseDeckCardIds.Add(preset.cardIds[i]);
                    _editingDeckCardIds.Add(preset.cardIds[i]);
                }
            }

            SelectedSlotIndex = _editingDeckCardIds.Count > 0 ? 0 : -1;
        }

        public void SelectSlot(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < _editingDeckCardIds.Count)
            {
                SelectedSlotIndex = slotIndex;
            }
        }

        public List<BattleCardData> GetEditingDeckCards(BattleDeckRuntime deckRuntime)
        {
            var cards = new List<BattleCardData>(_editingDeckCardIds.Count);
            for (var i = 0; i < _editingDeckCardIds.Count; i++)
            {
                var card = deckRuntime.FindCardById(_editingDeckCardIds[i]);
                if (card != null)
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        public List<int> GetCatalogInteractableIndices(BattleDeckRuntime deckRuntime)
        {
            var indices = new List<int>();
            for (var i = 0; i < deckRuntime.AllCards.Count; i++)
            {
                var card = deckRuntime.AllCards[i];
                if (card == null)
                {
                    continue;
                }

                if (SelectedSlotIndex < 0 || CanReplaceCard(SelectedSlotIndex, card.id, deckRuntime))
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        public bool TryReplaceCard(string cardId, BattleDeckRuntime deckRuntime, out string message)
        {
            return TryReplaceCard(SelectedSlotIndex, cardId, deckRuntime, out message);
        }

        public bool TryReplaceCard(int slotIndex, string cardId, BattleDeckRuntime deckRuntime, out string message)
        {
            message = string.Empty;
            if (!CanReplaceCard(slotIndex, cardId, deckRuntime, out message))
            {
                return false;
            }

            _editingDeckCardIds[slotIndex] = cardId;
            deckRuntime.ReplaceSelectedDeckByIds(_editingDeckCardIds);
            return true;
        }

        public bool CanReplaceCard(int slotIndex, string cardId, BattleDeckRuntime deckRuntime)
        {
            return CanReplaceCard(slotIndex, cardId, deckRuntime, out _);
        }

        public bool CanReplaceCard(int slotIndex, string cardId, BattleDeckRuntime deckRuntime, out string message)
        {
            message = string.Empty;
            if (slotIndex < 0 || slotIndex >= _editingDeckCardIds.Count)
            {
                message = "差し替える枠を先に選ぶぬめ";
                return false;
            }

            var candidateDeck = new List<string>(_editingDeckCardIds);
            candidateDeck[slotIndex] = cardId;

            if (CountCopies(candidateDeck, cardId) > _maxCopiesPerCard)
            {
                message = "同じカードは2枚までぬめ";
                return false;
            }

            var neutralCount = 0;
            var primaryCount = 0;
            var supportCount = 0;
            for (var i = 0; i < candidateDeck.Count; i++)
            {
                var card = deckRuntime.FindCardById(candidateDeck[i]);
                if (card == null)
                {
                    continue;
                }

                if (card.elementType == ElementType.None)
                {
                    neutralCount++;
                }
                else if (card.elementType == _primaryElement)
                {
                    primaryCount++;
                }
                else
                {
                    supportCount++;
                }
            }

            if (neutralCount > _maxNeutralCards)
            {
                message = "無属性は4枚までぬめ";
                return false;
            }

            if (supportCount > _maxSupportCards)
            {
                message = "補助属性は3枚までぬめ";
                return false;
            }

            if (_primaryElement != ElementType.None && primaryCount < _minPrimaryCards)
            {
                message = "主属性カードは6枚以上ほしいぬめ";
                return false;
            }

            if (GetSwapCount(candidateDeck) > _maxDeckSwaps)
            {
                message = "差し替えは5枚までぬめ";
                return false;
            }

            return true;
        }

        public int GetRemainingSwapCount()
        {
            return Mathf.Max(0, _maxDeckSwaps - GetSwapCount(_editingDeckCardIds));
        }

        public bool CanStartBattle()
        {
            return _editingDeckCardIds.Count == _requiredDeckSize && GetRemainingSwapCount() >= 0;
        }

        public string BuildSummary(BattleDeckRuntime deckRuntime)
        {
            var primaryCount = 0;
            var neutralCount = 0;
            var supportCount = 0;
            var totalSp = 0;
            var defenseCount = 0;
            var finisherCount = 0;

            for (var i = 0; i < _editingDeckCardIds.Count; i++)
            {
                var card = deckRuntime.FindCardById(_editingDeckCardIds[i]);
                if (card == null)
                {
                    continue;
                }

                totalSp += card.spCost;
                if (card.elementType == ElementType.None)
                {
                    neutralCount++;
                }
                else if (card.elementType == _primaryElement)
                {
                    primaryCount++;
                }
                else
                {
                    supportCount++;
                }

                if (card.damage >= 18)
                {
                    finisherCount++;
                }

                if (card.damage == 0 || HasEffect(card, BattleCardEffectType.Heal) || HasEffect(card, BattleCardEffectType.GainBarrier))
                {
                    defenseCount++;
                }
            }

            var averageSp = _editingDeckCardIds.Count > 0 ? totalSp / (float)_editingDeckCardIds.Count : 0f;
            return $"主属性 {primaryCount}/{_minPrimaryCards}  無 {neutralCount}/{_maxNeutralCards}  補助 {supportCount}/{_maxSupportCards}  差し替え {GetSwapCount(_editingDeckCardIds)}/{_maxDeckSwaps}\n平均SP {averageSp:0.0}  守り {defenseCount}  締め {finisherCount}";
        }

        private int GetSwapCount(List<string> deckCardIds)
        {
            var swapCount = 0;
            var count = Mathf.Min(deckCardIds.Count, _baseDeckCardIds.Count);
            for (var i = 0; i < count; i++)
            {
                if (deckCardIds[i] != _baseDeckCardIds[i])
                {
                    swapCount++;
                }
            }

            return swapCount;
        }

        private static int CountCopies(List<string> deckCardIds, string cardId)
        {
            var count = 0;
            for (var i = 0; i < deckCardIds.Count; i++)
            {
                if (deckCardIds[i] == cardId)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasEffect(BattleCardData card, BattleCardEffectType effectType)
        {
            if (card?.effects == null)
            {
                return false;
            }

            for (var i = 0; i < card.effects.Count; i++)
            {
                if (card.effects[i] != null && card.effects[i].effectType == effectType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
