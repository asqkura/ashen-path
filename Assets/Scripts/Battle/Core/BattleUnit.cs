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

        public int CurrentBarrier { get; private set; }

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

        public int PendingAttackModifier { get; private set; }

        public int PendingAttackMultiplierPercent { get; private set; } = 100;

        public void Reset()
        {
            CurrentHp = MaxHp;
            CurrentSp = MaxSp;
            CurrentBarrier = 0;
            PendingAttackModifier = 0;
            PendingAttackMultiplierPercent = 100;
            PrimaryWeakElement = Data.weakElement;
            SecondaryWeakElement = ElementType.None;
            ShieldCount = MaxShieldCount;
            BreakTurnsRemaining = 0;
        }

        public int TakeDamage(int amount)
        {
            return TakeDamage(amount, out _);
        }

        public int TakeDamage(int amount, out int absorbedByBarrier)
        {
            var damage = Mathf.Max(0, amount);
            absorbedByBarrier = Mathf.Min(CurrentBarrier, damage);
            CurrentBarrier = Mathf.Max(0, CurrentBarrier - absorbedByBarrier);
            var remainingDamage = Mathf.Max(0, damage - absorbedByBarrier);
            CurrentHp = Mathf.Clamp(CurrentHp - remainingDamage, 0, MaxHp);
            return remainingDamage;
        }

        public void RecoverSp(int amount)
        {
            var recovery = Mathf.Max(0, amount);
            CurrentSp = Mathf.Clamp(CurrentSp + recovery, 0, MaxSp);
        }

        public int Heal(int amount)
        {
            var recovery = Mathf.Max(0, amount);
            var previousHp = CurrentHp;
            CurrentHp = Mathf.Clamp(CurrentHp + recovery, 0, MaxHp);
            return CurrentHp - previousHp;
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

        public void AddBarrier(int amount)
        {
            CurrentBarrier = Mathf.Max(0, CurrentBarrier + Mathf.Max(0, amount));
        }

        public void AddPendingAttackModifier(int amount)
        {
            PendingAttackModifier += amount;
        }

        public int ConsumePendingAttackModifier()
        {
            var modifier = PendingAttackModifier;
            PendingAttackModifier = 0;
            return modifier;
        }

        public void SetPendingAttackMultiplierPercent(int percent)
        {
            PendingAttackMultiplierPercent = Mathf.Clamp(percent, 0, 1000);
        }

        public int ConsumePendingAttackMultiplierPercent()
        {
            var percent = PendingAttackMultiplierPercent;
            PendingAttackMultiplierPercent = 100;
            return percent;
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
