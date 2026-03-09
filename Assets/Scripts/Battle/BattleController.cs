using System.Collections;
using UnityEngine;

namespace AshenPath.Battle
{
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
            attackPower = 8
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

        public void Initialize(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);

            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _state = BattleState.PlayerTurn;

            RefreshUi();
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.SetTurnText("プレイヤーのターンです");
            _battleUI.SetAttackButtonInteractable(true);
        }

        public void PerformPlayerAttack()
        {
            if (_state != BattleState.PlayerTurn)
            {
                return;
            }

            _battleUI.SetAttackButtonInteractable(false);
            PerformAttack(_playerUnit, _enemyUnit);

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
                    PerformAttack(_enemyUnit, _playerUnit);
                    break;
            }

            if (TryResolveBattleEnd())
            {
                yield break;
            }

            _state = BattleState.PlayerTurn;
            _battleUI.SetTurnText("プレイヤーのターンです");
            _battleUI.SetAttackButtonInteractable(true);
        }

        private EnemyAction ChooseEnemyAction()
        {
            return EnemyAction.Attack;
        }

        private void PerformAttack(BattleUnit attacker, BattleUnit defender)
        {
            defender.TakeDamage(attacker.AttackPower);
            RefreshUi();
            _battleUI.SetTurnText($"{attacker.DisplayName} の攻撃！ {defender.DisplayName} に {attacker.AttackPower} ダメージ");
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
            _battleUI.SetAttackButtonInteractable(false);
            return true;
        }

        private void RefreshUi()
        {
            _battleUI.RefreshUnits(_playerUnit, _enemyUnit);
        }
    }
}
