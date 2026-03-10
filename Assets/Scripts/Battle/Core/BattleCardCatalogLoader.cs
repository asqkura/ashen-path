using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public static class BattleCardCatalogLoader
    {
        public static List<BattleCardData> LoadFromResources(string resourcePath)
        {
            var catalogAsset = Resources.Load<TextAsset>(resourcePath);
            if (catalogAsset == null || string.IsNullOrWhiteSpace(catalogAsset.text))
            {
                throw new InvalidOperationException($"カードカタログが見つからないぬめ: Resources/{resourcePath}.json");
            }

            var catalog = JsonUtility.FromJson<BattleCardCatalog>(catalogAsset.text);
            if (catalog?.cards == null || catalog.cards.Count == 0)
            {
                throw new InvalidOperationException($"カードカタログが空か不正ぬめ: Resources/{resourcePath}.json");
            }

            var cards = new List<BattleCardData>(catalog.cards.Count);
            for (var i = 0; i < catalog.cards.Count; i++)
            {
                cards.Add(ConvertCard(catalog.cards[i]));
            }

            return cards;
        }

        private static BattleCardData ConvertCard(BattleCardJson source)
        {
            if (source == null)
            {
                throw new InvalidOperationException("カード定義が不正ぬめ");
            }

            return new BattleCardData
            {
                id = string.IsNullOrWhiteSpace(source.id) ? source.cardName : source.id,
                cardName = source.cardName,
                description = source.description,
                keywords = ConvertKeywords(source.keywords),
                damage = source.damage,
                spCost = source.spCost,
                elementType = ParseElementType(source.elementType),
                effects = ConvertEffects(source.effects)
            };
        }

        private static List<BattleCardEffectData> ConvertEffects(List<BattleCardEffectJson> source)
        {
            var effects = new List<BattleCardEffectData>();
            if (source == null)
            {
                return effects;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var effect = source[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.effectType))
                {
                    continue;
                }

                effects.Add(new BattleCardEffectData
                {
                    effectType = ParseEffectType(effect.effectType),
                    value = effect.value,
                    secondaryValue = effect.secondaryValue
                });
            }

            return effects;
        }

        private static List<BattleCardKeywordData> ConvertKeywords(List<BattleCardKeywordJson> source)
        {
            var keywords = new List<BattleCardKeywordData>();
            if (source == null)
            {
                return keywords;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var keyword = source[i];
                if (keyword == null || string.IsNullOrWhiteSpace(keyword.keywordType))
                {
                    continue;
                }

                keywords.Add(new BattleCardKeywordData
                {
                    keywordType = ParseKeywordType(keyword.keywordType),
                    value = keyword.value
                });
            }

            return keywords;
        }

        private static ElementType ParseElementType(string elementType)
        {
            if (Enum.TryParse(elementType, true, out ElementType parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"不明な属性ぬめ: {elementType}");
        }

        private static BattleCardEffectType ParseEffectType(string effectType)
        {
            if (Enum.TryParse(effectType, true, out BattleCardEffectType parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"不明なカード効果ぬめ: {effectType}");
        }

        private static BattleCardKeywordType ParseKeywordType(string keywordType)
        {
            if (Enum.TryParse(keywordType, true, out BattleCardKeywordType parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"不明なキーワードぬめ: {keywordType}");
        }
    }
}
