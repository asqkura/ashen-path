using UnityEngine;

namespace AshenPath.Battle
{
    public class BattleUnit
    {
        public BattleUnit(BattleUnitData data)
        {
            Data = data ?? new BattleUnitData();
            CurrentHp = Mathf.Clamp(Data.maxHp, 0, Data.maxHp);
        }

        public BattleUnitData Data { get; }

        public int CurrentHp { get; private set; }

        public bool IsDead => CurrentHp <= 0;

        public int MaxHp => Mathf.Max(1, Data.maxHp);

        public string DisplayName => string.IsNullOrWhiteSpace(Data.displayName) ? "Unit" : Data.displayName;

        public int AttackPower => Mathf.Max(0, Data.attackPower);

        public void Reset()
        {
            CurrentHp = MaxHp;
        }

        public int TakeDamage(int amount)
        {
            var damage = Mathf.Max(0, amount);
            CurrentHp = Mathf.Clamp(CurrentHp - damage, 0, MaxHp);
            return damage;
        }
    }
}
