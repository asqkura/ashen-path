using System;

namespace AshenPath.Battle
{
    [Serializable]
    public class BattleUnitData
    {
        public string displayName = "Unit";
        public int maxHp = 30;
        public int attackPower = 8;
        public int maxSp = 3;
        public int spRecoveryPerTurn = 2;
    }
}
