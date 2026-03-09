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
            new BattleCardData { cardName = "Slash", description = "斬撃で 8 ダメージ", damage = 8, spCost = 2 },
            new BattleCardData { cardName = "Pierce", description = "貫通攻撃で 7 ダメージ", damage = 7, spCost = 1 },
            new BattleCardData { cardName = "Smash", description = "重い一撃で 10 ダメージ", damage = 10, spCost = 3 },
            new BattleCardData { cardName = "Twin Fang", description = "素早い連撃で 6 ダメージ", damage = 6, spCost = 1 },
            new BattleCardData { cardName = "Moon Edge", description = "深い斬り込みで 9 ダメージ", damage = 9, spCost = 2 },
            new BattleCardData { cardName = "Ash Burst", description = "灰の爆ぜで 11 ダメージ", damage = 11, spCost = 4, exhaustAfterUse = true },
            new BattleCardData { cardName = "Needle", description = "細い突きで 5 ダメージ", damage = 5, spCost = 1 },
        };

        private BattleState _state;
        private BattleUnit _playerUnit;
        private BattleUnit _enemyUnit;
        private BattleUI _battleUI;
        private Coroutine _enemyTurnCoroutine;
        private readonly List<BattleCardData> _hand = new();

        public void Initialize(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);

            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _state = BattleState.PlayerTurn;

            RefreshUi();
            _battleUI.SetResultText(string.Empty, false);
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

            var selectedCard = _hand[cardIndex];
            if (!_playerUnit.SpendSp(selectedCard.spCost))
            {
                _battleUI.SetTurnText($"SP不足: {selectedCard.cardName} には {selectedCard.spCost} SP 必要");
                RefreshUi();
                UpdateCardState();
                return;
            }

            RefreshUi();
            _battleUI.SetCardsInteractable(false, _hand, _playerUnit.CurrentSp);
            PerformAttack(_playerUnit, _enemyUnit, selectedCard.damage, selectedCard.cardName);

            if (selectedCard.exhaustAfterUse)
            {
                cardPool.Remove(selectedCard);
            }

            if (TryResolveBattleEnd())
            {
                return;
            }

            _state = BattleState.EnemyTurn;
            _battleUI.SetTurnText("敵のターンです");
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurn());
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
                    PerformAttack(_enemyUnit, _playerUnit, _enemyUnit.AttackPower, "Claw");
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
            _playerUnit.RecoverSp(_playerUnit.SpRecoveryPerTurn);
            DrawHand();
            _battleUI.RefreshHand(_hand);
            _battleUI.SetTurnText(turnMessage);
            RefreshUi();
            UpdateCardState();
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

        private void PerformAttack(BattleUnit attacker, BattleUnit defender, int damage, string attackName)
        {
            defender.TakeDamage(damage);
            RefreshUi();
            _battleUI.SetTurnText($"{attacker.DisplayName} の {attackName}！ {defender.DisplayName} に {damage} ダメージ");
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

            var resultMessage = _playerUnit.IsDead ? "敗北..." : "勝利！";
            _battleUI.SetResultText(resultMessage, true);
            _battleUI.SetTurnText("戦闘終了");
            _battleUI.SetCardsInteractable(false, _hand, _playerUnit.CurrentSp);
            return true;
        }

        private void RefreshUi()
        {
            _battleUI.RefreshUnits(_playerUnit, _enemyUnit);
            _battleUI.RefreshPlayerSp(_playerUnit.CurrentSp, _playerUnit.MaxSp);
        }

        private void UpdateCardState()
        {
            _battleUI.SetCardsInteractable(_state == BattleState.PlayerTurn, _hand, _playerUnit.CurrentSp);
        }
    }
}
