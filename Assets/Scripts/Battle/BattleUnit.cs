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
            PrimaryWeakElement = Data.weakElement;
            SecondaryWeakElement = ElementType.None;
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

        public ElementType PrimaryWeakElement { get; private set; }

        public ElementType SecondaryWeakElement { get; private set; }

        public int ShieldCount { get; private set; }

        public int MaxShieldCount { get; private set; }

        public bool IsBroken => BreakTurnsRemaining > 0;

        public int BreakTurnsRemaining { get; private set; }

        public void Reset()
        {
            CurrentHp = MaxHp;
            CurrentSp = MaxSp;
            PrimaryWeakElement = Data.weakElement;
            SecondaryWeakElement = ElementType.None;
            ShieldCount = MaxShieldCount;
            BreakTurnsRemaining = 0;
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

        public void SetWeakElement(ElementType elementType)
        {
            PrimaryWeakElement = elementType;
            SecondaryWeakElement = ElementType.None;
        }

        public void SetWeakElements(ElementType primaryElement, ElementType secondaryElement)
        {
            PrimaryWeakElement = primaryElement;
            SecondaryWeakElement = secondaryElement == primaryElement ? ElementType.None : secondaryElement;
        }

        public void SetShieldCount(int shieldCount)
        {
            MaxShieldCount = Mathf.Max(0, shieldCount);
            ShieldCount = MaxShieldCount;
            BreakTurnsRemaining = 0;
        }

        public bool TryBreakShield(ElementType attackElement)
        {
            if (IsBroken || ShieldCount <= 0 || attackElement == ElementType.None || !IsWeakTo(attackElement))
            {
                return false;
            }

            ShieldCount = Mathf.Max(0, ShieldCount - 1);
            if (ShieldCount == 0)
            {
                BreakTurnsRemaining = 1;
            }

            return true;
        }

        public void EndBreak()
        {
            BreakTurnsRemaining = 0;
            ShieldCount = MaxShieldCount;
        }

        private bool IsWeakTo(ElementType attackElement)
        {
            return attackElement == PrimaryWeakElement || (SecondaryWeakElement != ElementType.None && attackElement == SecondaryWeakElement);
        }
    }
}
