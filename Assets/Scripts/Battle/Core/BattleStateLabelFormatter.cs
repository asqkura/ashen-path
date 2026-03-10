using System.Collections.Generic;

namespace AshenPath.Battle
{
    public static class BattleStateLabelFormatter
    {
        public static string GetPlayerStateLabel(BattleCardResolver resolver, BattleCardResolver.TurnEffectState turnEffectState)
        {
            var parts = new List<string>();

            if (resolver != null)
            {
                var kindle = resolver.GetBattleElementDamageBonus(ElementType.Fire);
                if (kindle > 0)
                {
                    parts.Add($"熾火 {kindle}");
                }
            }

            if (turnEffectState != null && turnEffectState.HasPendingRevelation())
            {
                parts.Add("啓示");
            }

            if (turnEffectState != null && turnEffectState.PlayerLostHpThisTurn)
            {
                parts.Add("協約");
            }

            return parts.Count > 0 ? $"状態: {string.Join(" / ", parts)}" : string.Empty;
        }

        public static string GetEnemyStateLabel(BattleUnit enemyUnit)
        {
            var parts = new List<string>();
            if (enemyUnit != null && enemyUnit.FreezeStack > 0)
            {
                parts.Add($"凍結 {enemyUnit.FreezeStack}/{enemyUnit.FreezeThreshold}");
            }

            return parts.Count > 0 ? $"状態: {string.Join(" / ", parts)}" : string.Empty;
        }
    }
}
