using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public sealed class BattleCardResolver
    {
        public sealed class TurnEffectState
        {
            private readonly Dictionary<ElementType, int> _nextElementDamageBonus = new();
            private readonly Dictionary<ElementType, int> _nextElementSpDiscount = new();
            private readonly Dictionary<ElementType, int> _turnElementDamageBonus = new();
            private readonly Dictionary<ElementType, int> _nextElementDamageMultiplierPercent = new();
            private readonly Dictionary<ElementType, int> _usedElementCounts = new();

            public int NextCardDamageBonus { get; set; }

            public int NextCardSpDiscount { get; set; }

            public int TurnWideSpDiscount { get; set; }

            public int ConsumeDamageBonus(ElementType elementType)
            {
                var total = NextCardDamageBonus;
                NextCardDamageBonus = 0;

                if (_nextElementDamageBonus.TryGetValue(elementType, out var elementBonus))
                {
                    total += elementBonus;
                    _nextElementDamageBonus.Remove(elementType);
                }

                total += GetDictionaryValue(_turnElementDamageBonus, elementType);
                return total;
            }

            public int ConsumeSpDiscount(ElementType elementType)
            {
                var total = NextCardSpDiscount + TurnWideSpDiscount;
                NextCardSpDiscount = 0;

                if (_nextElementSpDiscount.TryGetValue(elementType, out var elementDiscount))
                {
                    total += elementDiscount;
                    _nextElementSpDiscount.Remove(elementType);
                }

                return total;
            }

            public int ConsumeDamageMultiplierPercent(ElementType elementType)
            {
                if (_nextElementDamageMultiplierPercent.TryGetValue(elementType, out var percent))
                {
                    _nextElementDamageMultiplierPercent.Remove(elementType);
                    return Mathf.Max(0, percent);
                }

                return 100;
            }

            public void AddNextElementDamageBonus(ElementType elementType, int value)
            {
                if (value <= 0)
                {
                    return;
                }

                _nextElementDamageBonus[elementType] = GetDictionaryValue(_nextElementDamageBonus, elementType) + value;
            }

            public void AddNextElementSpDiscount(ElementType elementType, int value)
            {
                if (value <= 0)
                {
                    return;
                }

                _nextElementSpDiscount[elementType] = GetDictionaryValue(_nextElementSpDiscount, elementType) + value;
            }

            public void AddTurnElementDamageBonus(ElementType elementType, int value)
            {
                if (value <= 0)
                {
                    return;
                }

                _turnElementDamageBonus[elementType] = GetDictionaryValue(_turnElementDamageBonus, elementType) + value;
            }

            public void AddNextElementDamageMultiplierPercent(ElementType elementType, int percent)
            {
                if (percent <= 0)
                {
                    return;
                }

                _nextElementDamageMultiplierPercent[elementType] = percent;
            }

            public void RegisterCardUse(ElementType elementType)
            {
                if (elementType == ElementType.None)
                {
                    return;
                }

                _usedElementCounts[elementType] = GetDictionaryValue(_usedElementCounts, elementType) + 1;
            }

            public int GetUsedElementCount(ElementType elementType)
            {
                return GetDictionaryValue(_usedElementCounts, elementType);
            }

            private static int GetDictionaryValue(Dictionary<ElementType, int> dictionary, ElementType key)
            {
                return dictionary.TryGetValue(key, out var value) ? value : 0;
            }
        }

        private readonly BattleUI _battleUI;
        private readonly BattleDeckRuntime _deckRuntime;
        private readonly BattleUnit _playerUnit;
        private readonly BattleUnit _enemyUnit;
        private readonly Action<BattleUnit, BattleUnit, int, string, ElementType, int> _performAttack;
        private readonly Action _refreshUi;

        public BattleCardResolver(
            BattleUI battleUI,
            BattleDeckRuntime deckRuntime,
            BattleUnit playerUnit,
            BattleUnit enemyUnit,
            Action<BattleUnit, BattleUnit, int, string, ElementType, int> performAttack,
            Action refreshUi)
        {
            _battleUI = battleUI;
            _deckRuntime = deckRuntime;
            _playerUnit = playerUnit;
            _enemyUnit = enemyUnit;
            _performAttack = performAttack;
            _refreshUi = refreshUi;
        }

        public void ResolveCard(BattleCardData card, TurnEffectState effectState, int sequenceIndex)
        {
            effectState.RegisterCardUse(card.elementType);
            var damageBonus = effectState.ConsumeDamageBonus(card.elementType);
            var currentDamage = Mathf.Max(0, card.damage + damageBonus);
            var extraShieldDamage = 0;
            var repeatEffects = new List<BattleCardEffectData>();
            var damageMultiplierPercent = effectState.ConsumeDamageMultiplierPercent(card.elementType);
            var ignoreDefaultAttack = false;
            var skipDamageIfNotBroken = false;

            if (card.effects != null)
            {
                for (var i = 0; i < card.effects.Count; i++)
                {
                    var effect = card.effects[i];
                    switch (effect.effectType)
                    {
                        case BattleCardEffectType.ExtraShieldDamage:
                            extraShieldDamage += Mathf.Max(0, effect.value);
                            break;
                        case BattleCardEffectType.BonusDamageIfTargetBroken:
                            if (_enemyUnit.IsBroken)
                            {
                                currentDamage += Mathf.Max(0, effect.value);
                            }
                            break;
                        case BattleCardEffectType.BonusDamageIfCardSequenceAtLeast:
                            if (sequenceIndex >= Mathf.Max(1, effect.value))
                            {
                                currentDamage += Mathf.Max(0, effect.secondaryValue);
                            }
                            break;
                        case BattleCardEffectType.BonusDamageIfHandCountAtMost:
                            if (Mathf.Max(0, _deckRuntime.Hand.Count - sequenceIndex) <= Mathf.Max(0, effect.value))
                            {
                                currentDamage += Mathf.Max(0, effect.secondaryValue);
                            }
                            break;
                        case BattleCardEffectType.RepeatAttack:
                            repeatEffects.Add(effect);
                            break;
                    }
                }
            }

            switch (card.id)
            {
                case "neutral_finish":
                    if (_enemyUnit.IsBroken)
                    {
                        damageMultiplierPercent *= 2;
                    }
                    break;
                case "fire_ignition":
                    if (sequenceIndex >= 2)
                    {
                        damageMultiplierPercent *= 2;
                    }
                    break;
                case "fire_inferno":
                    if (_playerUnit.CurrentHp * 2 <= _playerUnit.MaxHp)
                    {
                        damageMultiplierPercent = Mathf.RoundToInt(damageMultiplierPercent * 1.5f);
                    }
                    break;
                case "fire_volcano":
                    if (_enemyUnit.IsBroken)
                    {
                        currentDamage += 20;
                    }
                    break;
                case "ice_avalanche":
                    extraShieldDamage += Mathf.Max(0, _enemyUnit.ShieldCount);
                    break;
                case "wind_zephyr":
                    repeatEffects.Add(new BattleCardEffectData
                    {
                        effectType = BattleCardEffectType.RepeatAttack,
                        value = 5,
                        secondaryValue = Mathf.Max(0, effectState.GetUsedElementCount(ElementType.Wind) - 1)
                    });
                    break;
                case "light_shine":
                    if (_playerUnit.CurrentHp >= _playerUnit.MaxHp)
                    {
                        damageMultiplierPercent *= 2;
                    }
                    break;
                case "light_judge":
                    currentDamage = _playerUnit.CurrentHp;
                    break;
                case "dark_grim":
                    currentDamage = _deckRuntime.DiscardHandAndCount("dark_grim") * 5;
                    break;
                case "dark_nox":
                    if (_deckRuntime.Hand.Count == 1)
                    {
                        damageMultiplierPercent *= 3;
                    }
                    break;
                case "dark_reaper":
                    skipDamageIfNotBroken = true;
                    break;
            }

            currentDamage = Mathf.RoundToInt(currentDamage * (damageMultiplierPercent / 100f));

            if (skipDamageIfNotBroken && !_enemyUnit.IsBroken)
            {
                ignoreDefaultAttack = true;
                _battleUI.AddBattleLog($"{card.cardName} は Break 中の敵にしか使えないぬめ");
            }

            if (!ignoreDefaultAttack && (currentDamage > 0 || (extraShieldDamage > 0 && card.elementType != ElementType.None)))
            {
                _performAttack(_playerUnit, _enemyUnit, currentDamage, card.cardName, card.elementType, extraShieldDamage);
            }
            else
            {
                _battleUI.SetTurnText($"{_playerUnit.DisplayName} の {card.cardName}！");
            }

            for (var i = 0; i < repeatEffects.Count; i++)
            {
                var repeatEffect = repeatEffects[i];
                var hitDamage = Mathf.Max(0, repeatEffect.value);
                var hitCount = Mathf.Max(1, repeatEffect.secondaryValue);
                for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    _performAttack(_playerUnit, _enemyUnit, hitDamage, $"{card.cardName} 追撃", card.elementType, 0);
                    if (_enemyUnit.IsDead)
                    {
                        break;
                    }
                }
            }

            ApplyPostCardEffects(card, effectState, currentDamage);
            _refreshUi();
        }

        public static void ApplySelectionDiscountEffects(BattleCardData card, TurnEffectState effectState)
        {
            if (card.effects == null)
            {
                if (card.id == "wind_step")
                {
                    effectState.TurnWideSpDiscount += 1;
                }

                return;
            }

            for (var i = 0; i < card.effects.Count; i++)
            {
                var effect = card.effects[i];
                switch (effect.effectType)
                {
                    case BattleCardEffectType.DiscountNextCardSp:
                        effectState.NextCardSpDiscount += Mathf.Max(0, effect.value);
                        break;
                    case BattleCardEffectType.DiscountNextElementSp:
                        effectState.AddNextElementSpDiscount(card.elementType, effect.value);
                        break;
                }
            }

            if (card.id == "wind_step")
            {
                effectState.TurnWideSpDiscount += 1;
            }
        }

        private void ApplyPostCardEffects(BattleCardData card, TurnEffectState effectState, int currentDamage)
        {
            if (card.effects != null)
            {
                for (var i = 0; i < card.effects.Count; i++)
                {
                    var effect = card.effects[i];
                    switch (effect.effectType)
                    {
                        case BattleCardEffectType.RecoverSp:
                            var previousSp = _playerUnit.CurrentSp;
                            _playerUnit.RecoverSp(effect.value);
                            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のSPが {_playerUnit.CurrentSp - previousSp} 回復");
                            break;
                        case BattleCardEffectType.Heal:
                            var healed = _playerUnit.Heal(effect.value);
                            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} はHPを {healed} 回復");
                            break;
                        case BattleCardEffectType.SelfDamage:
                            var selfDamage = _playerUnit.TakeDamage(effect.value);
                            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} は反動で {selfDamage} ダメージ");
                            break;
                        case BattleCardEffectType.GainBarrier:
                            _playerUnit.AddBarrier(effect.value);
                            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} はガードを {Mathf.Max(0, effect.value)} 獲得");
                            break;
                        case BattleCardEffectType.BuffNextCardDamage:
                            effectState.NextCardDamageBonus += Mathf.Max(0, effect.value);
                            _battleUI.AddBattleLog("次のカードの威力が上がったぬめ");
                            break;
                        case BattleCardEffectType.BuffNextElementDamage:
                            effectState.AddNextElementDamageBonus(card.elementType, effect.value);
                            _battleUI.AddBattleLog($"次の{GetElementLabel(card.elementType)}カードの威力が上がったぬめ");
                            break;
                        case BattleCardEffectType.DiscountNextCardSp:
                            effectState.NextCardSpDiscount += Mathf.Max(0, effect.value);
                            _battleUI.AddBattleLog("次のカードの消費SPが下がったぬめ");
                            break;
                        case BattleCardEffectType.DiscountNextElementSp:
                            effectState.AddNextElementSpDiscount(card.elementType, effect.value);
                            _battleUI.AddBattleLog($"次の{GetElementLabel(card.elementType)}カードの消費SPが下がったぬめ");
                            break;
                        case BattleCardEffectType.EnemyAttackDown:
                            _enemyUnit.AddPendingAttackModifier(-Mathf.Max(0, effect.value));
                            _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} の次の攻撃が弱まったぬめ");
                            break;
                    }
                }
            }

            if (card.id == "dark_crow")
            {
                effectState.AddNextElementDamageMultiplierPercent(ElementType.Dark, 150);
                _battleUI.AddBattleLog("次の闇カードの威力が大きく上がったぬめ");
            }

            switch (card.id)
            {
                case "neutral_draw":
                    _deckRuntime.DrawCardsIntoHand(2);
                    break;
                case "fire_burn_up":
                    effectState.AddTurnElementDamageBonus(ElementType.Fire, 4);
                    _battleUI.AddBattleLog("このターンの炎カードが強化されたぬめ");
                    break;
                case "fire_ash":
                    var recoveredCard = _deckRuntime.ReturnLastExhaustedCardToHand();
                    if (recoveredCard != null)
                    {
                        _battleUI.AddBattleLog($"{recoveredCard.cardName} が手札に戻ったぬめ");
                    }
                    break;
                case "ice_freeze":
                    _enemyUnit.SetPendingAttackMultiplierPercent(50);
                    _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} の次の攻撃が半減するぬめ");
                    break;
                case "ice_crystal":
                    _deckRuntime.DrawCardsIntoHand(1);
                    break;
                case "wind_wind":
                    _deckRuntime.DrawCardsIntoHand(1);
                    break;
                case "wind_breeze":
                    _deckRuntime.DrawCardsIntoHand(2);
                    break;
                case "wind_step":
                    effectState.TurnWideSpDiscount += 1;
                    _battleUI.AddBattleLog("このターンの手札の消費SPが下がったぬめ");
                    break;
                case "wind_cyclone":
                    _deckRuntime.DrawCardsIntoHand(1);
                    break;
                case "wind_feather":
                    if (_deckRuntime.DiscardFirstHandCardExcept("wind_feather"))
                    {
                        _battleUI.AddBattleLog("手札を1枚捨てたぬめ");
                    }
                    _deckRuntime.DrawCardsIntoHand(3);
                    break;
                case "light_barrier":
                    _battleUI.AddBattleLog("このターンは弱体を防ぐぬめ");
                    break;
                case "light_sunlight":
                    var healFromDamage = Mathf.FloorToInt(Mathf.Max(0, currentDamage) * 0.5f);
                    var lifeSteal = _playerUnit.Heal(healFromDamage);
                    _battleUI.AddBattleLog($"{_playerUnit.DisplayName} はHPを {lifeSteal} 回復");
                    break;
                case "dark_reaper":
                    if (_enemyUnit.IsBroken)
                    {
                        var hpToLose = Mathf.Max(0, _playerUnit.CurrentHp - 1);
                        if (hpToLose > 0)
                        {
                            _playerUnit.TakeDamage(hpToLose);
                            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のHPは 1 になったぬめ");
                        }
                    }
                    break;
            }
        }

        private static string GetElementLabel(ElementType elementType)
        {
            return elementType switch
            {
                ElementType.Fire => "炎",
                ElementType.Ice => "氷",
                ElementType.Wind => "風",
                ElementType.Light => "光",
                ElementType.Dark => "闇",
                _ => "無"
            };
        }
    }
}
