using System;
using System.Collections.Generic;

namespace AshenPath.Battle
{
    public enum BattleCardEffectType
    {
        RepeatAttack,
        RecoverSp,
        Heal,
        SelfDamage,
        GainBarrier,
        BuffNextCardDamage,
        BuffNextElementDamage,
        DiscountNextCardSp,
        DiscountNextElementSp,
        EnemyAttackDown,
        ExtraShieldDamage,
        BonusDamageIfTargetBroken,
        BonusDamageIfCardSequenceAtLeast,
        BonusDamageIfHandCountAtMost,
        MultiplyDamageIfCardSequenceAtLeast,
        MultiplyDamageIfPlayerHpAtMostPercent,
        MultiplyDamageIfPlayerLostHpThisTurn,
        BonusDamageIfPlayerLostHpThisTurn,
        DrawCards,
        DrawCardsIfPlayerLostHpThisTurn,
        DiscardHandDamage,
        RepeatAttackPerUsedElement,
        BonusDamageFromBattleElementBonus
    }

    public enum BattleCardKeywordType
    {
        Kindle,
        Freeze,
        Tailwind,
        Revelation,
        Blessing,
        Covenant,
        Exhaust
    }

    [Serializable]
    public class BattleCardEffectData
    {
        public BattleCardEffectType effectType;
        public int value;
        public int secondaryValue;
    }

    [Serializable]
    public class BattleCardKeywordData
    {
        public BattleCardKeywordType keywordType;
        public int value;
    }

    [Serializable]
    public class BattleCardData
    {
        public string id = "attack";
        public string cardName = "Strike";
        public string description = "Basic attack.";
        public List<BattleCardKeywordData> keywords = new();
        public int damage = 8;
        public int spCost = 1;
        public ElementType elementType = ElementType.None;
        public List<BattleCardEffectData> effects = new();
    }

    [Serializable]
    public class BattleCardEffectJson
    {
        public string effectType;
        public int value;
        public int secondaryValue;
    }

    [Serializable]
    public class BattleCardKeywordJson
    {
        public string keywordType;
        public int value;
    }

    [Serializable]
    public class BattleCardJson
    {
        public string id;
        public string cardName;
        public string description;
        public List<BattleCardKeywordJson> keywords = new();
        public int damage;
        public int spCost;
        public string elementType;
        public List<BattleCardEffectJson> effects = new();
    }

    [Serializable]
    public class BattleCardCatalog
    {
        public List<BattleCardJson> cards = new();
    }
}
