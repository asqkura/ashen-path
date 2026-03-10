using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    [System.Serializable]
    public class BattleCardData
    {
        public string cardName = "Strike";
        public string description = "Basic attack.";
        public int damage = 8;
        public int spCost = 1;
        public ElementType elementType = ElementType.None;
        public bool exhaustAfterUse;
    }

    public class BattleController : MonoBehaviour
    {
        private enum BattleState
        {
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
        [SerializeField] private List<BattleCardData> cardPool = new()
        {
            new BattleCardData { cardName = "アタック", description = "無属性で 8 ダメージ", damage = 8, spCost = 2, elementType = ElementType.None },
            new BattleCardData { cardName = "ウィンド", description = "風で 7 ダメージ", damage = 7, spCost = 1, elementType = ElementType.Wind },
            new BattleCardData { cardName = "ファイア", description = "火で 10 ダメージ", damage = 10, spCost = 3, elementType = ElementType.Fire },
            new BattleCardData { cardName = "ウィンド+", description = "風で 6 ダメージ", damage = 6, spCost = 1, elementType = ElementType.Wind },
            new BattleCardData { cardName = "ウォーター", description = "水で 9 ダメージ", damage = 9, spCost = 2, elementType = ElementType.Water },
            new BattleCardData { cardName = "ファイア+", description = "火で 11 ダメージ。使い切り", damage = 11, spCost = 4, elementType = ElementType.Fire, exhaustAfterUse = true },
            new BattleCardData { cardName = "ウォーター+", description = "水で 5 ダメージ", damage = 5, spCost = 1, elementType = ElementType.Water },
        };

        private BattleState _state;
        private BattleUnit _playerUnit;
        private BattleUnit _enemyUnit;
        private BattleUI _battleUI;
        private Coroutine _enemyTurnCoroutine;
        private Coroutine _playerActionCoroutine;
        private int _turnCount;
        private readonly List<BattleCardData> _hand = new();
        private readonly List<int> _selectedCardIndices = new();

        public void Initialize(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);

            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _enemyUnit.SetWeakElement(GetRandomWeakElement());
            _enemyUnit.SetShieldCount(Random.Range(3, 6));
            _state = BattleState.PlayerTurn;
            _turnCount = 0;

            RefreshUi();
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.ClearBattleLog();
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} と {_enemyUnit.DisplayName} の戦闘開始");
            StartPlayerTurn("プレイヤーのターンです");
        }

        public void PerformPlayerCard(int cardIndex)
        {
            if (_state != BattleState.PlayerTurn)
            {
                return;
            }

            if (cardIndex < 0 || cardIndex >= _hand.Count)
            {
                return;
            }

            if (_selectedCardIndices.Contains(cardIndex))
            {
                _selectedCardIndices.Remove(cardIndex);
                UpdateCardState();
                return;
            }

            var selectedCard = _hand[cardIndex];
            if (GetSelectedSpCost() + selectedCard.spCost > _playerUnit.CurrentSp)
            {
                var message = $"SP不足: {selectedCard.cardName} を追加できません";
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
            _selectedCardIndices.Clear();
            UpdateCardState();
            _playerActionCoroutine = StartCoroutine(ResolvePlayerCards(selectedIndices));
        }

        private IEnumerator ExecuteEnemyTurn()
        {
            yield return new WaitForSeconds(enemyTurnDelay);

            if (_state != BattleState.EnemyTurn)
            {
                yield break;
            }

            if (_enemyUnit.IsBroken)
            {
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} は Break 中で行動不能ぬめ！");
                _enemyUnit.EndBreak();
                RefreshUi();
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} のシールドが回復したぬめ");
                StartPlayerTurn("プレイヤーのターンです");
                yield break;
            }

            switch (ChooseEnemyAction())
            {
                case EnemyAction.Attack:
                    PerformAttack(_enemyUnit, _playerUnit, _enemyUnit.AttackPower, "Claw", ElementType.None);
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

        private void StartPlayerTurn(string turnMessage)
        {
            _state = BattleState.PlayerTurn;
            _turnCount++;
            _selectedCardIndices.Clear();
            var previousSp = _playerUnit.CurrentSp;
            _playerUnit.RecoverSp(_playerUnit.SpRecoveryPerTurn);
            DrawHand();
            _battleUI.RefreshHand(_hand);
            _battleUI.SetTurnText(turnMessage);
            _battleUI.SetTurnCount(_turnCount);
            RefreshUi();
            UpdateCardState();
            var recoveredSp = _playerUnit.CurrentSp - previousSp;
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のターン: SP {recoveredSp} 回復");
        }

        private void DrawHand()
        {
            _hand.Clear();

            if (cardPool.Count == 0)
            {
                return;
            }

            for (var i = 0; i < 5; i++)
            {
                var card = cardPool[Random.Range(0, cardPool.Count)];
                _hand.Add(card);
            }
        }

        private void PerformAttack(BattleUnit attacker, BattleUnit defender, int damage, string attackName, ElementType elementType)
        {
            var wasBroken = defender.IsBroken;
            if (defender.TryBreakShield(elementType))
            {
                _battleUI.AddBattleLog("弱点を突いてシールドを削ったぬめ！");
                if (defender.IsBroken)
                {
                    _battleUI.AddBattleLog($"{defender.DisplayName} は Break 状態ぬめ！");
                }
            }

            var dealtDamage = wasBroken ? damage * 2 : damage;
            dealtDamage = defender.TakeDamage(dealtDamage);
            RefreshUi();
            if (defender == _enemyUnit)
            {
                _battleUI.PlayEnemyDamageEffect(dealtDamage);
            }

            var message = $"{attacker.DisplayName} の {attackName}！ {defender.DisplayName} に {dealtDamage} ダメージ";
            _battleUI.SetTurnText(message);
            _battleUI.AddBattleLog(message);
        }

        private IEnumerator ResolvePlayerCards(IReadOnlyList<int> selectedIndices)
        {
            for (var i = 0; i < selectedIndices.Count; i++)
            {
                var cardIndex = selectedIndices[i];
                if (cardIndex < 0 || cardIndex >= _hand.Count)
                {
                    continue;
                }

                var selectedCard = _hand[cardIndex];
                if (!_playerUnit.SpendSp(selectedCard.spCost))
                {
                    continue;
                }

                RefreshUi();
                _battleUI.AddBattleLog($"{_playerUnit.DisplayName} は {selectedCard.cardName} を使用");
                PerformAttack(_playerUnit, _enemyUnit, selectedCard.damage, selectedCard.cardName, selectedCard.elementType);

                if (selectedCard.exhaustAfterUse)
                {
                    cardPool.Remove(selectedCard);
                    _battleUI.AddBattleLog($"{selectedCard.cardName} は使い切りで消滅");
                }

                if (TryResolveBattleEnd())
                {
                    _playerActionCoroutine = null;
                    yield break;
                }

                if (i < selectedIndices.Count - 1)
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
            _battleUI.SetCardsInteractable(false, _hand, 0, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _hand, _playerUnit.CurrentSp);
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
            var remainingSp = Mathf.Max(0, _playerUnit.CurrentSp - GetSelectedSpCost());
            _battleUI.SetCardsInteractable(_state == BattleState.PlayerTurn, _hand, remainingSp, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(_state == BattleState.PlayerTurn && _selectedCardIndices.Count > 0, GetSelectedSpCost());
        }

        private int GetSelectedSpCost()
        {
            var total = 0;
            for (var i = 0; i < _selectedCardIndices.Count; i++)
            {
                var index = _selectedCardIndices[i];
                if (index < 0 || index >= _hand.Count)
                {
                    continue;
                }

                total += Mathf.Max(0, _hand[index].spCost);
            }

            return total;
        }

        private static ElementType GetRandomWeakElement()
        {
            return (ElementType)Random.Range(1, 4);
        }
    }
}
