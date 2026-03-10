using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public static class BattleEnemyActionSelector
    {
        public static BattleEnemyActionData RollAction(BattleEnemyData enemyData, BattleUnit enemyUnit, BattleUnitData fallbackUnitData)
        {
            var actions = enemyData?.actions;
            if (actions == null || actions.Count == 0)
            {
                return CreateFallbackAction(enemyData?.unitData ?? fallbackUnitData);
            }

            var totalWeight = 0;
            for (var i = 0; i < actions.Count; i++)
            {
                totalWeight += GetActionWeight(actions[i], enemyUnit);
            }

            if (totalWeight <= 0)
            {
                return CreateFallbackAction(enemyData?.unitData ?? fallbackUnitData);
            }

            var roll = Random.Range(0, totalWeight);
            for (var i = 0; i < actions.Count; i++)
            {
                var weight = GetActionWeight(actions[i], enemyUnit);
                if (roll < weight)
                {
                    return actions[i];
                }

                roll -= weight;
            }

            return actions[actions.Count - 1];
        }

        public static string GetIntentLabel(BattleEnemyActionData action, BattleUnit enemyUnit)
        {
            if (enemyUnit != null && enemyUnit.WillSkipNextAction)
            {
                return $"予告: 凍結停止 ({enemyUnit.FreezeStack}/{enemyUnit.FreezeThreshold})";
            }

            var baseLabel = action == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(action.intentLabel)
                    ? GetFallbackIntentLabel(action)
                    : action.intentLabel;

            if (enemyUnit != null && enemyUnit.FreezeStack > 0)
            {
                return $"{baseLabel} / 凍結 {enemyUnit.FreezeStack}";
            }

            return baseLabel;
        }

        private static int GetActionWeight(BattleEnemyActionData action, BattleUnit enemyUnit)
        {
            if (action == null)
            {
                return 0;
            }

            var weight = Mathf.Max(0, action.baseWeight);
            if (enemyUnit != null && enemyUnit.MaxHp > 0 && enemyUnit.CurrentHp <= Mathf.RoundToInt(enemyUnit.MaxHp * 0.4f))
            {
                weight += Mathf.Max(0, action.lowHpBonusWeight);
            }

            if (enemyUnit != null && enemyUnit.PendingAttackMultiplierPercent > 100)
            {
                weight += Mathf.Max(0, action.chargedBonusWeight);
            }

            return weight;
        }

        private static string GetFallbackIntentLabel(BattleEnemyActionData action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            return action.actionType switch
            {
                BattleEnemyActionType.Attack => $"予告: 通常攻撃 {Mathf.Max(0, action.attackPower)}",
                BattleEnemyActionType.HeavyAttack => $"予告: 強攻撃 {Mathf.Max(0, action.attackPower)}",
                BattleEnemyActionType.Guard => $"予告: 防御 {Mathf.Max(0, action.barrierGain)}",
                BattleEnemyActionType.Flurry => $"予告: 連撃 {Mathf.Max(0, action.attackPower)}x{Mathf.Max(1, action.hitCount)}",
                BattleEnemyActionType.Focus => $"予告: 溜め / 次攻撃 {Mathf.Max(100, action.nextAttackMultiplierPercent)}% + ガード{Mathf.Max(0, action.barrierGain)}",
                _ => string.Empty
            };
        }

        private static BattleEnemyActionData CreateFallbackAction(BattleUnitData unitData)
        {
            return new BattleEnemyActionData
            {
                actionType = BattleEnemyActionType.Attack,
                intentLabel = $"予告: 通常攻撃 {Mathf.Max(0, unitData.attackPower)}",
                attackName = "Claw",
                attackPower = Mathf.Max(0, unitData.attackPower),
                baseWeight = 1,
                hitCount = 1
            };
        }
    }
}
