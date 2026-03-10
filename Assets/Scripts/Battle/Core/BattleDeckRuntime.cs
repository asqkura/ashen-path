using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public class BattleDeckRuntime
    {
        private readonly List<BattleCardData> _allCards = new();
        private readonly List<BattleCardData> _selectedDeck = new();
        private readonly List<BattleCardData> _drawPile = new();
        private readonly List<BattleCardData> _discardPile = new();
        private readonly List<BattleCardData> _exhaustedPile = new();
        private readonly List<BattleCardData> _hand = new();

        public int HandLimit { get; set; } = int.MaxValue;

        public IReadOnlyList<BattleCardData> AllCards => _allCards;

        public IReadOnlyList<BattleCardData> Hand => _hand;

        public int SelectedDeckCount => _selectedDeck.Count;

        public void ReplaceCatalog(IReadOnlyList<BattleCardData> cards)
        {
            _allCards.Clear();
            _selectedDeck.Clear();
            _drawPile.Clear();
            _discardPile.Clear();
            _exhaustedPile.Clear();
            _hand.Clear();

            if (cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Count; i++)
            {
                _allCards.Add(cards[i]);
            }
        }

        public void SetupDefaultDeck(int requiredDeckSize)
        {
            _selectedDeck.Clear();
            for (var i = 0; i < Mathf.Min(requiredDeckSize, _allCards.Count); i++)
            {
                _selectedDeck.Add(_allCards[i]);
            }
        }

        public BattleCardData FindCardById(string cardId)
        {
            for (var i = 0; i < _allCards.Count; i++)
            {
                if (_allCards[i].id == cardId)
                {
                    return _allCards[i];
                }
            }

            return null;
        }

        public bool IsInSelectedDeck(BattleCardData card)
        {
            return card != null && _selectedDeck.Contains(card);
        }

        public bool TryAddToSelectedDeck(BattleCardData card, int requiredDeckSize)
        {
            if (card == null || _selectedDeck.Count >= requiredDeckSize || _selectedDeck.Contains(card))
            {
                return false;
            }

            _selectedDeck.Add(card);
            return true;
        }

        public bool RemoveFromSelectedDeck(BattleCardData card)
        {
            return card != null && _selectedDeck.Remove(card);
        }

        public List<string> GetSelectedDeckIds()
        {
            var ids = new List<string>(_selectedDeck.Count);
            for (var i = 0; i < _selectedDeck.Count; i++)
            {
                ids.Add(_selectedDeck[i].id);
            }

            return ids;
        }

        public void BeginBattle()
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _exhaustedPile.Clear();
            _hand.Clear();
            _drawPile.AddRange(_selectedDeck);
            ShuffleCards(_drawPile);
        }

        public void RefillHand(int handSize)
        {
            var drawCount = Mathf.Max(0, handSize - _hand.Count);
            DrawCardsIntoHand(drawCount, handSize);
        }

        public void FinalizePlayedCard(BattleCardData card)
        {
            _hand.Remove(card);

            if (card == null)
            {
                return;
            }

            if (HasKeyword(card, BattleCardKeywordType.Exhaust))
            {
                _exhaustedPile.Add(card);
                return;
            }

            _discardPile.Add(card);
        }

        public void RemoveCardFromBattleDeck(BattleCardData card)
        {
            _selectedDeck.Remove(card);
            _drawPile.Remove(card);
            _discardPile.Remove(card);
        }

        public void DrawCardsIntoHand(int count)
        {
            DrawCardsIntoHand(count, HandLimit);
        }

        public void DrawCardsIntoHand(int count, int maxHandSize)
        {
            for (var i = 0; i < Mathf.Max(0, count); i++)
            {
                if (_hand.Count >= Mathf.Max(0, maxHandSize))
                {
                    break;
                }

                if (_drawPile.Count == 0)
                {
                    if (_discardPile.Count == 0)
                    {
                        break;
                    }

                    _drawPile.AddRange(_discardPile);
                    _discardPile.Clear();
                    ShuffleCards(_drawPile);
                }

                var drawIndex = _drawPile.Count - 1;
                _hand.Add(_drawPile[drawIndex]);
                _drawPile.RemoveAt(drawIndex);
            }
        }

        public int DiscardHandAndCount(string excludedCardId)
        {
            var discardCount = 0;
            for (var i = _hand.Count - 1; i >= 0; i--)
            {
                if (_hand[i].id == excludedCardId)
                {
                    continue;
                }

                _discardPile.Add(_hand[i]);
                _hand.RemoveAt(i);
                discardCount++;
            }

            return discardCount;
        }

        public bool DiscardFirstHandCardExcept(string excludedCardId)
        {
            for (var i = 0; i < _hand.Count; i++)
            {
                if (_hand[i].id == excludedCardId)
                {
                    continue;
                }

                _discardPile.Add(_hand[i]);
                _hand.RemoveAt(i);
                return true;
            }

            return false;
        }

        public BattleCardData ReturnLastExhaustedCardToHand()
        {
            if (_exhaustedPile.Count == 0)
            {
                return null;
            }

            var recoveredIndex = _exhaustedPile.Count - 1;
            var recoveredCard = _exhaustedPile[recoveredIndex];
            _exhaustedPile.RemoveAt(recoveredIndex);
            _selectedDeck.Add(recoveredCard);
            _hand.Add(recoveredCard);
            return recoveredCard;
        }

        private static void ShuffleCards(List<BattleCardData> cards)
        {
            for (var i = cards.Count - 1; i > 0; i--)
            {
                var swapIndex = Random.Range(0, i + 1);
                (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
            }
        }

        private static bool HasKeyword(BattleCardData card, BattleCardKeywordType keywordType)
        {
            if (card?.keywords == null)
            {
                return false;
            }

            for (var i = 0; i < card.keywords.Count; i++)
            {
                if (card.keywords[i] != null && card.keywords[i].keywordType == keywordType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
