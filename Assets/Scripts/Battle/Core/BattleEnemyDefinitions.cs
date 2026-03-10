using System;
using System.Collections.Generic;

namespace AshenPath.Battle
{
    public enum BattleEnemyActionType
    {
        Attack,
        HeavyAttack,
        Guard,
        Flurry,
        Focus
    }

    [Serializable]
    public class BattleEnemyActionData
    {
        public BattleEnemyActionType actionType;
        public string intentLabel = string.Empty;
        public string attackName = string.Empty;
        public int baseWeight = 10;
        public int lowHpBonusWeight;
        public int chargedBonusWeight;
        public int attackPower;
        public int hitCount = 1;
        public int barrierGain;
        public int nextAttackMultiplierPercent = 100;
    }

    [Serializable]
    public class BattleEnemyData
    {
        public string id = "enemy";
        public BattleUnitData unitData = new();
        public List<BattleEnemyActionData> actions = new();
    }

    [Serializable]
    public class BattleEnemyActionJson
    {
        public string actionType;
        public string intentLabel;
        public string attackName;
        public int baseWeight = 10;
        public int lowHpBonusWeight;
        public int chargedBonusWeight;
        public int attackPower;
        public int hitCount = 1;
        public int barrierGain;
        public int nextAttackMultiplierPercent = 100;
    }

    [Serializable]
    public class BattleEnemyJson
    {
        public string id;
        public BattleUnitData unitData = new();
        public List<BattleEnemyActionJson> actions = new();
    }

    [Serializable]
    public class BattleEnemyCatalog
    {
        public List<BattleEnemyJson> enemies = new();
    }
}
