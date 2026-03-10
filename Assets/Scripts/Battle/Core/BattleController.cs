using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public class BattleController : MonoBehaviour
    {
        private const string CardCatalogResourcePath = "Battle/card-catalog";
        private const int RequiredDeckSize = 15;
        private const int HandSize = 5;

        private enum BattleState
        {
            DeckEditing,
            PlayerTurn,
            EnemyTurn,
            BattleEnded
        }

        private enum EnemyAction
        {
            Attack
        }

        [SerializeField] private float enemyTurnDelay = 0.9f;
        [SerializeField] private float playerCardActionInterval = 0.35f;
        [SerializeField] private BattleUnitData playerUnitData = new()
        {
            displayName = "Player",
            maxHp = 40,
            attackPower = 8,
            maxSp = 5,
            spRecoveryPerTurn = 2
        };
        [SerializeField] private BattleUnitData enemyUnitData = new()
        {
            displayName = "Enemy",
            maxHp = 32,
            attackPower = 6
        };
        private BattleState _state;
        private BattleUnit _playerUnit;
        private BattleUnit _enemyUnit;
        private BattleUI _battleUI;
        private Coroutine _enemyTurnCoroutine;
        private Coroutine _playerActionCoroutine;
        private int _turnCount;
        private readonly BattleDeckRuntime _deckRuntime = new();
        private BattleCardResolver _cardResolver;
        private readonly List<int> _selectedCardIndices = new();

        public void Initialize(BattleUI battleUI)
        {
            InitializeDeckEditor(battleUI);
        }

        public void InitializeBattle(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);
            LoadCardCatalog();
            _deckRuntime.SetupDefaultDeck(RequiredDeckSize);
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _cardResolver = new BattleCardResolver(_battleUI, _deckRuntime, _playerUnit, _enemyUnit, PerformAttack, RefreshUi);
            _battleUI.HideDeckEditor();
            _battleUI.SetBattleScreenVisible(true);
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.ClearBattleLog();
            BeginBattle();
        }

        public void InitializeDeckEditor(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);
            LoadCardCatalog();
            _deckRuntime.SetupDefaultDeck(RequiredDeckSize);
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _cardResolver = new BattleCardResolver(_battleUI, _deckRuntime, _playerUnit, _enemyUnit, PerformAttack, RefreshUi);
            _state = BattleState.DeckEditing;

            _battleUI.SetBattleScreenVisible(false);
            _battleUI.ShowDeckEditor(_deckRuntime.AllCards, _deckRuntime.GetSelectedDeckIds(), RequiredDeckSize);
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.ClearBattleLog();
            _battleUI.SetTurnText("デッキを編成してください");
            _battleUI.SetTurnCount(1);
        }

        public void ToggleDeckCard(string cardId)
        {
            if (_state != BattleState.DeckEditing || string.IsNullOrWhiteSpace(cardId))
            {
                return;
            }

            var card = _deckRuntime.FindCardById(cardId);
            if (card == null)
            {
                return;
            }

            if (_deckRuntime.IsInSelectedDeck(card))
            {
                _deckRuntime.RemoveFromSelectedDeck(card);
            }
            else
            {
                if (!_deckRuntime.TryAddToSelectedDeck(card, RequiredDeckSize))
                {
                    _battleUI.SetDeckEditorHint($"デッキは {RequiredDeckSize} 枚までぬめ");
                    _battleUI.SetDeckEditorSelection(_deckRuntime.GetSelectedDeckIds(), RequiredDeckSize);
                    return;
                }
            }

            _battleUI.SetDeckEditorHint(_deckRuntime.SelectedDeckCount == RequiredDeckSize ? "戦闘開始できるぬめ" : $"あと {RequiredDeckSize - _deckRuntime.SelectedDeckCount} 枚必要ぬめ");
            _battleUI.SetDeckEditorSelection(_deckRuntime.GetSelectedDeckIds(), RequiredDeckSize);
        }

        public void ConfirmDeckSelection()
        {
            if (_state != BattleState.DeckEditing || _deckRuntime.SelectedDeckCount != RequiredDeckSize)
            {
                return;
            }

            BeginBattle();
        }

        public void PerformPlayerCard(int cardIndex)
        {
            if (_state != BattleState.PlayerTurn)
            {
                return;
            }

            if (cardIndex < 0 || cardIndex >= _deckRuntime.Hand.Count)
            {
                return;
            }

            if (_selectedCardIndices.Contains(cardIndex))
            {
                _selectedCardIndices.Remove(cardIndex);
                UpdateCardState();
                return;
            }

            var candidateIndices = new List<int>(_selectedCardIndices) { cardIndex };
            var projectedCost = GetSelectedSpCost(candidateIndices);
            if (projectedCost > _playerUnit.CurrentSp)
            {
                var message = $"SP不足: {_deckRuntime.Hand[cardIndex].cardName} を追加できません";
                _battleUI.SetTurnText(message);
                _battleUI.AddBattleLog(message);
                RefreshUi();
                UpdateCardState();
                return;
            }

            _selectedCardIndices.Add(cardIndex);
            UpdateCardState();
        }

        public void ConfirmSelectedCards()
        {
            if (_state != BattleState.PlayerTurn || _selectedCardIndices.Count == 0)
            {
                return;
            }

            var selectedIndices = new List<int>(_selectedCardIndices);
            var selectedCards = new List<BattleCardData>(selectedIndices.Count);
            for (var i = 0; i < selectedIndices.Count; i++)
            {
                var index = selectedIndices[i];
                if (index >= 0 && index < _deckRuntime.Hand.Count)
                {
                    selectedCards.Add(_deckRuntime.Hand[index]);
                }
            }

            _selectedCardIndices.Clear();
            UpdateCardState();
            _playerActionCoroutine = StartCoroutine(ResolvePlayerCards(selectedCards));
        }

        private IEnumerator ExecuteEnemyTurn()
        {
            yield return new WaitForSeconds(enemyTurnDelay);

            if (_state != BattleState.EnemyTurn)
            {
                yield break;
            }

            switch (ChooseEnemyAction())
            {
                case EnemyAction.Attack:
                    var attackPower = Mathf.Max(0, _enemyUnit.AttackPower + _enemyUnit.ConsumePendingAttackModifier());
                    attackPower = Mathf.RoundToInt(attackPower * (_enemyUnit.ConsumePendingAttackMultiplierPercent() / 100f));
                    PerformAttack(_enemyUnit, _playerUnit, attackPower, "Claw", ElementType.None);
                    break;
            }

            if (TryResolveBattleEnd())
            {
                yield break;
            }

            StartPlayerTurn("プレイヤーのターンです");
        }

        private EnemyAction ChooseEnemyAction()
        {
            return EnemyAction.Attack;
        }

        private void BeginBattle()
        {
            _state = BattleState.PlayerTurn;
            _turnCount = 0;
            _selectedCardIndices.Clear();

            _playerUnit.Reset();
            _enemyUnit.Reset();
            _deckRuntime.BeginBattle();

            _battleUI.SetBattleScreenVisible(true);
            _battleUI.HideDeckEditor();
            RefreshUi();
            _battleUI.ClearBattleLog();
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} と {_enemyUnit.DisplayName} の戦闘開始");
            StartPlayerTurn("プレイヤーのターンです");
        }

        private void StartPlayerTurn(string turnMessage)
        {
            _state = BattleState.PlayerTurn;
            _turnCount++;
            _selectedCardIndices.Clear();
            var previousSp = _playerUnit.CurrentSp;
            _playerUnit.RecoverSp(_playerUnit.SpRecoveryPerTurn);
            _deckRuntime.RefillHand(HandSize);
            _battleUI.RefreshHand(_deckRuntime.Hand);
            _battleUI.SetTurnText(turnMessage);
            _battleUI.SetTurnCount(_turnCount);
            RefreshUi();
            UpdateCardState();
            var recoveredSp = _playerUnit.CurrentSp - previousSp;
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のターン: SP {recoveredSp} 回復");
        }

        private void PerformAttack(BattleUnit attacker, BattleUnit defender, int damage, string attackName, ElementType elementType, int extraShieldDamage = 0)
        {
            var dealtDamage = damage;
            dealtDamage = defender.TakeDamage(dealtDamage, out var absorbedByBarrier);
            RefreshUi();
            if (defender == _enemyUnit && dealtDamage > 0)
            {
                _battleUI.PlayEnemyDamageEffect(dealtDamage, elementType);
            }

            var message = $"{attacker.DisplayName} の {attackName}！ {defender.DisplayName} に {dealtDamage} ダメージ";
            if (absorbedByBarrier > 0)
            {
                message += $" ({absorbedByBarrier} ガード)";
            }

            _battleUI.SetTurnText(message);
            _battleUI.AddBattleLog(message);
        }

        private IEnumerator ResolvePlayerCards(IReadOnlyList<BattleCardData> selectedCards)
        {
            var effectState = new BattleCardResolver.TurnEffectState();

            for (var i = 0; i < selectedCards.Count; i++)
            {
                var selectedCard = selectedCards[i];
                if (selectedCard == null)
                {
                    continue;
                }

                var actualCost = Mathf.Max(0, selectedCard.spCost - effectState.ConsumeSpDiscount(selectedCard.elementType));
                if (!_playerUnit.SpendSp(actualCost))
                {
                    continue;
                }

                RefreshUi();
                _battleUI.AddBattleLog($"{_playerUnit.DisplayName} は {selectedCard.cardName} を使用");
                _cardResolver.ResolveCard(selectedCard, effectState, i + 1);
                FinalizePlayedCard(selectedCard);

                if (selectedCard.exhaustAfterUse)
                {
                    RemoveCardFromBattleDeck(selectedCard);
                    _battleUI.AddBattleLog($"{selectedCard.cardName} は使い切りで消滅");
                }

                if (TryResolveBattleEnd())
                {
                    _playerActionCoroutine = null;
                    yield break;
                }

                if (i < selectedCards.Count - 1)
                {
                    yield return new WaitForSeconds(playerCardActionInterval);
                }
            }

            _playerActionCoroutine = null;
            _state = BattleState.EnemyTurn;
            _battleUI.SetTurnText("敵のターンです");
            _battleUI.AddBattleLog("敵のターン");
            UpdateCardState();
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurn());
        }

        private bool TryResolveBattleEnd()
        {
            if (!_playerUnit.IsDead && !_enemyUnit.IsDead)
            {
                return false;
            }

            _state = BattleState.BattleEnded;

            if (_enemyTurnCoroutine != null)
            {
                StopCoroutine(_enemyTurnCoroutine);
                _enemyTurnCoroutine = null;
            }

            if (_playerActionCoroutine != null)
            {
                StopCoroutine(_playerActionCoroutine);
                _playerActionCoroutine = null;
            }

            var resultMessage = _playerUnit.IsDead ? "敗北..." : "勝利！";
            _battleUI.SetResultText(resultMessage, true);
            _battleUI.SetTurnText("戦闘終了");
            _battleUI.AddBattleLog(resultMessage);
            _battleUI.SetCardsInteractable(false, _deckRuntime.Hand, 0, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(false, 0);
            return true;
        }

        private void RefreshUi()
        {
            _battleUI.RefreshUnits(_playerUnit, _enemyUnit);
            _battleUI.RefreshPlayerSp(_playerUnit.CurrentSp, _playerUnit.MaxSp);
        }

        private void UpdateCardState()
        {
            var remainingSp = Mathf.Max(0, _playerUnit.CurrentSp - GetSelectedSpCost(_selectedCardIndices));
            _battleUI.SetCardsInteractable(_state == BattleState.PlayerTurn, _deckRuntime.Hand, remainingSp, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(_state == BattleState.PlayerTurn && _selectedCardIndices.Count > 0, GetSelectedSpCost(_selectedCardIndices));
        }

        private int GetSelectedSpCost(IReadOnlyList<int> selectedIndices)
        {
            var total = 0;
            var effectState = new BattleCardResolver.TurnEffectState();

            for (var i = 0; i < selectedIndices.Count; i++)
            {
                var index = selectedIndices[i];
                if (index < 0 || index >= _deckRuntime.Hand.Count)
                {
                    continue;
                }

                var card = _deckRuntime.Hand[index];
                total += Mathf.Max(0, card.spCost - effectState.ConsumeSpDiscount(card.elementType));
                BattleCardResolver.ApplySelectionDiscountEffects(card, effectState);
            }

            return total;
        }

        private void FinalizePlayedCard(BattleCardData card)
        {
            _deckRuntime.FinalizePlayedCard(card);
            if (card.id == "dark_abyss")
            {
                _battleUI.AddBattleLog($"{card.cardName} は手札に戻ったぬめ");
            }

            _battleUI.RefreshHand(_deckRuntime.Hand);
        }

        private void LoadCardCatalog()
        {
            _deckRuntime.ReplaceCatalog(BattleCardCatalogLoader.LoadFromResources(CardCatalogResourcePath));
        }

        private void RemoveCardFromBattleDeck(BattleCardData card)
        {
            _deckRuntime.RemoveCardFromBattleDeck(card);
        }
    }
}
