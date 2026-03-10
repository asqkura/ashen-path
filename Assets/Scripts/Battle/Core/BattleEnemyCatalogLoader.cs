using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public static class BattleEnemyCatalogLoader
    {
        public static List<BattleEnemyData> LoadFromResources(string resourcePath)
        {
            var catalogAsset = Resources.Load<TextAsset>(resourcePath);
            if (catalogAsset == null || string.IsNullOrWhiteSpace(catalogAsset.text))
            {
                throw new InvalidOperationException($"敵カタログが見つからないぬめ: Resources/{resourcePath}.json");
            }

            var catalog = JsonUtility.FromJson<BattleEnemyCatalog>(catalogAsset.text);
            if (catalog?.enemies == null || catalog.enemies.Count == 0)
            {
                throw new InvalidOperationException($"敵カタログが空か不正ぬめ: Resources/{resourcePath}.json");
            }

            var enemies = new List<BattleEnemyData>(catalog.enemies.Count);
            for (var i = 0; i < catalog.enemies.Count; i++)
            {
                enemies.Add(ConvertEnemy(catalog.enemies[i]));
            }

            return enemies;
        }

        private static BattleEnemyData ConvertEnemy(BattleEnemyJson source)
        {
            if (source == null)
            {
                throw new InvalidOperationException("敵定義が不正ぬめ");
            }

            return new BattleEnemyData
            {
                id = string.IsNullOrWhiteSpace(source.id) ? source.unitData?.displayName : source.id,
                unitData = source.unitData ?? new BattleUnitData(),
                actions = ConvertActions(source.actions)
            };
        }

        private static List<BattleEnemyActionData> ConvertActions(List<BattleEnemyActionJson> source)
        {
            var actions = new List<BattleEnemyActionData>();
            if (source == null)
            {
                return actions;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var action = source[i];
                if (action == null || string.IsNullOrWhiteSpace(action.actionType))
                {
                    continue;
                }

                actions.Add(new BattleEnemyActionData
                {
                    actionType = ParseActionType(action.actionType),
                    intentLabel = action.intentLabel ?? string.Empty,
                    attackName = action.attackName ?? string.Empty,
                    baseWeight = action.baseWeight,
                    lowHpBonusWeight = action.lowHpBonusWeight,
                    chargedBonusWeight = action.chargedBonusWeight,
                    attackPower = action.attackPower,
                    hitCount = action.hitCount,
                    barrierGain = action.barrierGain,
                    nextAttackMultiplierPercent = action.nextAttackMultiplierPercent
                });
            }

            return actions;
        }

        private static BattleEnemyActionType ParseActionType(string actionType)
        {
            if (Enum.TryParse(actionType, true, out BattleEnemyActionType parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"不明な敵行動ぬめ: {actionType}");
        }
    }
}
