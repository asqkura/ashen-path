using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public class BattleController : MonoBehaviour
    {
        private const string CardCatalogResourcePath = "Battle/card-catalog";
        private const int RequiredDeckSize = 15;
        private const int MaxHandSize = 5;
        private const int OpeningHandSize = 5;
        private const int TurnDrawCount = 2;
        private const int MaxCardsPerTurn = 1;

        private enum BattleState
        {
            DeckEditing,
            PlayerTurn,
            EnemyTurn,
            BattleEnded
        }

        private enum EnemyAction
        {
            Attack,
            HeavyAttack,
            Guard
        }

        [SerializeField] private float enemyTurnDelay = 0.9f;
        [SerializeField] private float playerCardActionInterval = 0.2f;
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
            maxHp = 48,
            attackPower = 8
        };

        private BattleState _state;
        private BattleUnit _playerUnit;
        private BattleUnit _enemyUnit;
        private BattleUI _battleUI;
        private Coroutine _enemyTurnCoroutine;
        private Coroutine _playerActionCoroutine;
        private int _turnCount;
        private int _cardsPlayedThisTurn;
        private EnemyAction _nextEnemyAction;
        private readonly BattleDeckRuntime _deckRuntime = new();
        private BattleCardResolver _cardResolver;
        private BattleCardResolver.TurnEffectState _turnEffectState;
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
            _deckRuntime.HandLimit = MaxHandSize;
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
            _deckRuntime.HandLimit = MaxHandSize;
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
            _battleUI.SetEnemyIntent(string.Empty);
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
            else if (!_deckRuntime.TryAddToSelectedDeck(card, RequiredDeckSize))
            {
                _battleUI.SetDeckEditorHint($"デッキは {RequiredDeckSize} 枚までぬめ");
                _battleUI.SetDeckEditorSelection(_deckRuntime.GetSelectedDeckIds(), RequiredDeckSize);
                return;
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
            if (_state != BattleState.PlayerTurn || _playerActionCoroutine != null)
            {
                return;
            }

            if (cardIndex < 0 || cardIndex >= _deckRuntime.Hand.Count)
            {
                return;
            }

            if (_cardsPlayedThisTurn >= MaxCardsPerTurn)
            {
                _battleUI.SetTurnText("このターンはもうカードを使えないぬめ");
                return;
            }

            var card = _deckRuntime.Hand[cardIndex];
            var actualCost = Mathf.Max(0, card.spCost - _turnEffectState.PeekSpDiscount(card.elementType));
            if (actualCost > _playerUnit.CurrentSp)
            {
                var message = $"SP不足: {card.cardName} を使えません";
                _battleUI.SetTurnText(message);
                _battleUI.AddBattleLog(message);
                RefreshUi();
                UpdateCardState();
                return;
            }

            _playerActionCoroutine = StartCoroutine(ResolvePlayerCard(card, actualCost));
        }

        public void EndPlayerTurn()
        {
            if (_state != BattleState.PlayerTurn || _playerActionCoroutine != null)
            {
                return;
            }

            StartEnemyTurn();
        }

        private void BeginBattle()
        {
            _state = BattleState.PlayerTurn;
            _turnCount = 0;
            _cardsPlayedThisTurn = 0;
            _selectedCardIndices.Clear();
            _turnEffectState = new BattleCardResolver.TurnEffectState();

            _playerUnit.Reset();
            _enemyUnit.Reset();
            _deckRuntime.BeginBattle();
            _cardResolver.ResetBattleState();
            _deckRuntime.DrawCardsIntoHand(OpeningHandSize, MaxHandSize);
            _nextEnemyAction = RollEnemyAction();

            _battleUI.SetBattleScreenVisible(true);
            _battleUI.HideDeckEditor();
            RefreshUi();
            _battleUI.RefreshHand(_deckRuntime.Hand);
            _battleUI.ClearBattleLog();
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} と {_enemyUnit.DisplayName} の戦闘開始");
            StartPlayerTurn("プレイヤーのターンです");
        }

        private void StartPlayerTurn(string turnMessage)
        {
            _state = BattleState.PlayerTurn;
            _turnCount++;
            _cardsPlayedThisTurn = 0;
            _selectedCardIndices.Clear();
            _turnEffectState = new BattleCardResolver.TurnEffectState();

            var previousSp = _playerUnit.CurrentSp;
            _playerUnit.RecoverSp(_playerUnit.SpRecoveryPerTurn);
            if (_turnCount > 1)
            {
                _deckRuntime.DrawCardsIntoHand(TurnDrawCount, MaxHandSize);
            }

            _battleUI.RefreshHand(_deckRuntime.Hand);
            _battleUI.SetTurnText(turnMessage);
            _battleUI.SetTurnCount(_turnCount);
            _battleUI.SetEnemyIntent(GetEnemyIntentLabel(_nextEnemyAction));
            RefreshUi();
            UpdateCardState();

            var recoveredSp = _playerUnit.CurrentSp - previousSp;
            var drawCount = _turnCount == 1 ? OpeningHandSize : TurnDrawCount;
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のターン: SP {recoveredSp} 回復 / {drawCount} 枚ドロー");
        }

        private void StartEnemyTurn()
        {
            if (_state == BattleState.BattleEnded || _enemyTurnCoroutine != null)
            {
                return;
            }

            _state = BattleState.EnemyTurn;
            _battleUI.SetTurnText("敵のターンです");
            _battleUI.AddBattleLog($"敵のターン: {GetEnemyIntentLabel(_nextEnemyAction)}");
            UpdateCardState();
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurn());
        }

        private IEnumerator ResolvePlayerCard(BattleCardData card, int actualCost)
        {
            if (card == null)
            {
                _playerActionCoroutine = null;
                yield break;
            }

            var resolvedCost = Mathf.Max(0, card.spCost - _turnEffectState.ConsumeSpDiscount(card.elementType));
            if (!_playerUnit.SpendSp(resolvedCost))
            {
                _playerActionCoroutine = null;
                yield break;
            }

            RefreshUi();
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} は {card.cardName} を使用");
            FinalizePlayedCard(card);
            _cardResolver.ResolveCard(card, _turnEffectState, _cardsPlayedThisTurn + 1);
            _cardsPlayedThisTurn++;

            if (HasKeyword(card, BattleCardKeywordType.Exhaust))
            {
                RemoveCardFromBattleDeck(card);
                _battleUI.AddBattleLog($"{card.cardName} は使い切りで消滅");
            }

            if (TryResolveBattleEnd())
            {
                _playerActionCoroutine = null;
                yield break;
            }

            yield return new WaitForSeconds(playerCardActionInterval);
            _playerActionCoroutine = null;
            UpdateCardState();

            if (_cardsPlayedThisTurn >= MaxCardsPerTurn || !CanPlayAnyCard())
            {
                StartEnemyTurn();
            }
        }

        private IEnumerator ExecuteEnemyTurn()
        {
            yield return new WaitForSeconds(enemyTurnDelay);

            if (_state != BattleState.EnemyTurn)
            {
                yield break;
            }

            if (_enemyUnit.TryConsumeFrozenActionSkip())
            {
                RefreshUi();
                _battleUI.SetTurnText($"{_enemyUnit.DisplayName} は凍結で動けないぬめ");
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} は凍結で行動をスキップ");
            }
            else
            {
                switch (_nextEnemyAction)
                {
                    case EnemyAction.Attack:
                        PerformEnemyAttack(_enemyUnit.AttackPower, "Claw");
                        break;
                    case EnemyAction.HeavyAttack:
                        PerformEnemyAttack(_enemyUnit.AttackPower + 6, "Heavy Claw");
                        break;
                    case EnemyAction.Guard:
                        _enemyUnit.AddBarrier(8);
                        RefreshUi();
                        _battleUI.SetTurnText($"{_enemyUnit.DisplayName} は身構えたぬめ");
                        _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} はガードを 8 獲得");
                        break;
                }
            }

            _enemyTurnCoroutine = null;

            if (TryResolveBattleEnd())
            {
                yield break;
            }

            _nextEnemyAction = RollEnemyAction();
            StartPlayerTurn("プレイヤーのターンです");
        }

        private void PerformEnemyAttack(int basePower, string attackName)
        {
            var attackPower = Mathf.Max(0, basePower + _enemyUnit.ConsumePendingAttackModifier());
            attackPower = Mathf.RoundToInt(attackPower * (_enemyUnit.ConsumePendingAttackMultiplierPercent() / 100f));
            PerformAttack(_enemyUnit, _playerUnit, attackPower, attackName, ElementType.None);
        }

        private void PerformAttack(BattleUnit attacker, BattleUnit defender, int damage, string attackName, ElementType elementType, int extraShieldDamage = 0)
        {
            var dealtDamage = defender.TakeDamage(Mathf.Max(0, damage), out var absorbedByBarrier);
            RefreshUi();
            if (defender == _enemyUnit && dealtDamage > 0)
            {
                _battleUI.PlayEnemyDamageEffect(dealtDamage, elementType);
            }
            else if (defender == _playerUnit && dealtDamage > 0)
            {
                _battleUI.PlayPlayerDamageEffect(dealtDamage, elementType);
            }

            var message = $"{attacker.DisplayName} の {attackName}！ {defender.DisplayName} に {dealtDamage} ダメージ";
            if (absorbedByBarrier > 0)
            {
                message += $" ({absorbedByBarrier} ガード)";
            }

            _battleUI.SetTurnText(message);
            _battleUI.AddBattleLog(message);
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
            _battleUI.SetEnemyIntent(string.Empty);
            _battleUI.AddBattleLog(resultMessage);
            _battleUI.SetCardsInteractable(false, _deckRuntime.Hand, new List<int>());
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(false, "戦闘終了");
            return true;
        }

        private void RefreshUi()
        {
            _battleUI.RefreshUnits(_playerUnit, _enemyUnit);
            _battleUI.RefreshPlayerSp(_playerUnit.CurrentSp, _playerUnit.MaxSp);
            _battleUI.SetEnemyIntent(_state == BattleState.BattleEnded ? string.Empty : GetEnemyIntentLabel(_nextEnemyAction));
        }

        private void UpdateCardState()
        {
            var canUseCards = _state == BattleState.PlayerTurn && _playerActionCoroutine == null && _cardsPlayedThisTurn < MaxCardsPerTurn;
            _battleUI.SetCardsInteractable(canUseCards, _deckRuntime.Hand, GetPlayableCardIndices());
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(_state == BattleState.PlayerTurn && _playerActionCoroutine == null, $"ターン終了 ({_cardsPlayedThisTurn}/{MaxCardsPerTurn})");
        }

        private bool CanPlayAnyCard()
        {
            return GetPlayableCardIndices().Count > 0;
        }

        private List<int> GetPlayableCardIndices()
        {
            var playable = new List<int>();
            if (_state != BattleState.PlayerTurn)
            {
                return playable;
            }

            for (var i = 0; i < _deckRuntime.Hand.Count; i++)
            {
                var cost = Mathf.Max(0, _deckRuntime.Hand[i].spCost - _turnEffectState.PeekSpDiscount(_deckRuntime.Hand[i].elementType));
                if (_playerUnit.CurrentSp >= cost)
                {
                    playable.Add(i);
                }
            }

            return playable;
        }

        private static EnemyAction RollEnemyAction()
        {
            var roll = Random.Range(0, 100);
            if (roll < 40)
            {
                return EnemyAction.Attack;
            }

            if (roll < 75)
            {
                return EnemyAction.HeavyAttack;
            }

            return EnemyAction.Guard;
        }

        private string GetEnemyIntentLabel(EnemyAction action)
        {
            if (_enemyUnit != null && _enemyUnit.WillSkipNextAction)
            {
                return $"予告: 凍結停止 ({_enemyUnit.FreezeStack}/{_enemyUnit.FreezeThreshold})";
            }

            var baseLabel = action switch
            {
                EnemyAction.Attack => "予告: 通常攻撃",
                EnemyAction.HeavyAttack => "予告: 強攻撃",
                EnemyAction.Guard => "予告: 防御",
                _ => string.Empty
            };

            if (_enemyUnit != null && _enemyUnit.FreezeStack > 0)
            {
                return $"{baseLabel} / 凍結 {_enemyUnit.FreezeStack}";
            }

            return baseLabel;
        }

        private void FinalizePlayedCard(BattleCardData card)
        {
            _deckRuntime.FinalizePlayedCard(card);
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
