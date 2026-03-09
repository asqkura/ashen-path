using UnityEngine;

namespace AshenPath.Battle
{
    public class BattleUnit
    {
        public BattleUnit(BattleUnitData data)
        {
            Data = data ?? new BattleUnitData();
            CurrentHp = Mathf.Clamp(Data.maxHp, 0, Data.maxHp);
            CurrentSp = Mathf.Clamp(MaxSp, 0, MaxSp);
        }

        public BattleUnitData Data { get; }

        public int CurrentHp { get; private set; }

        public int CurrentSp { get; private set; }

        public bool IsDead => CurrentHp <= 0;

        public int MaxHp => Mathf.Max(1, Data.maxHp);

        public string DisplayName => string.IsNullOrWhiteSpace(Data.displayName) ? "Unit" : Data.displayName;

        public int AttackPower => Mathf.Max(0, Data.attackPower);

        public int MaxSp => Mathf.Max(0, Data.maxSp);

        public int SpRecoveryPerTurn => Mathf.Max(0, Data.spRecoveryPerTurn);

        public void Reset()
        {
            CurrentHp = MaxHp;
            CurrentSp = MaxSp;
        }

        public int TakeDamage(int amount)
        {
            var damage = Mathf.Max(0, amount);
            CurrentHp = Mathf.Clamp(CurrentHp - damage, 0, MaxHp);
            return damage;
        }

        public void RecoverSp(int amount)
        {
            var recovery = Mathf.Max(0, amount);
            CurrentSp = Mathf.Clamp(CurrentSp + recovery, 0, MaxSp);
        }

        public bool CanSpendSp(int amount)
        {
            return CurrentSp >= Mathf.Max(0, amount);
        }

        public bool SpendSp(int amount)
        {
            var cost = Mathf.Max(0, amount);
            if (CurrentSp < cost)
            {
                return false;
            }

            CurrentSp -= cost;
            return true;
        }
    }
}
