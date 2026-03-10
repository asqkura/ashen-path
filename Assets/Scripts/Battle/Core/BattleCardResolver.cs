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
            private bool _nextCardHasRevelation;

            public int NextCardDamageBonus { get; set; }

            public int NextCardSpDiscount { get; set; }

            public int TurnWideSpDiscount { get; set; }

            public bool PlayerLostHpThisTurn { get; private set; }

            public void AddRevelation()
            {
                _nextCardHasRevelation = true;
            }

            public bool ConsumeRevelationForCard(bool cardHasBlessing)
            {
                var hasRevelation = _nextCardHasRevelation;
                _nextCardHasRevelation = false;

                if (!hasRevelation || !cardHasBlessing)
                {
                    return false;
                }

                return true;
            }

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

            public int PeekSpDiscount(ElementType elementType)
            {
                var total = NextCardSpDiscount + TurnWideSpDiscount;
                if (_nextElementSpDiscount.TryGetValue(elementType, out var elementDiscount))
                {
                    total += elementDiscount;
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

            public void MarkPlayerLostHpThisTurn()
            {
                PlayerLostHpThisTurn = true;
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
        private readonly Dictionary<ElementType, int> _battleElementDamageBonus = new();

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

        public void ResetBattleState()
        {
            _battleElementDamageBonus.Clear();
        }

        public void ResolveCard(BattleCardData card, TurnEffectState effectState, int sequenceIndex)
        {
            var blessingTriggered = effectState.ConsumeRevelationForCard(HasBlessingKeyword(card));
            effectState.RegisterCardUse(card.elementType);
            var damageBonus = effectState.ConsumeDamageBonus(card.elementType) + GetBattleElementDamageBonus(card.elementType);
            var currentDamage = Mathf.Max(0, card.damage + damageBonus);
            var repeatEffects = new List<BattleCardEffectData>();
            var damageMultiplierPercent = effectState.ConsumeDamageMultiplierPercent(card.elementType);

            if (card.effects != null)
            {
                for (var i = 0; i < card.effects.Count; i++)
                {
                    var effect = card.effects[i];
                    switch (effect.effectType)
                    {
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
                        case BattleCardEffectType.MultiplyDamageIfCardSequenceAtLeast:
                            if (sequenceIndex >= Mathf.Max(1, effect.value))
                            {
                                damageMultiplierPercent = Mathf.RoundToInt(damageMultiplierPercent * (Mathf.Max(100, effect.secondaryValue) / 100f));
                            }
                            break;
                        case BattleCardEffectType.MultiplyDamageIfPlayerHpAtMostPercent:
                            if (_playerUnit.CurrentHp * 100 <= _playerUnit.MaxHp * Mathf.Max(0, effect.value))
                            {
                                damageMultiplierPercent = Mathf.RoundToInt(damageMultiplierPercent * (Mathf.Max(100, effect.secondaryValue) / 100f));
                            }
                            break;
                        case BattleCardEffectType.MultiplyDamageIfPlayerLostHpThisTurn:
                            if (effectState.PlayerLostHpThisTurn)
                            {
                                damageMultiplierPercent = Mathf.RoundToInt(damageMultiplierPercent * (Mathf.Max(100, effect.value) / 100f));
                            }
                            break;
                        case BattleCardEffectType.BonusDamageIfPlayerLostHpThisTurn:
                            if (effectState.PlayerLostHpThisTurn)
                            {
                                currentDamage += Mathf.Max(0, effect.value);
                            }
                            break;
                        case BattleCardEffectType.DiscardHandDamage:
                            currentDamage = _deckRuntime.DiscardHandAndCount(card.id) * Mathf.Max(0, effect.value);
                            break;
                        case BattleCardEffectType.RepeatAttackPerUsedElement:
                            repeatEffects.Add(new BattleCardEffectData
                            {
                                effectType = BattleCardEffectType.RepeatAttack,
                                value = Mathf.Max(0, effect.value),
                                secondaryValue = Mathf.Max(0, effectState.GetUsedElementCount(card.elementType) - 1)
                            });
                            break;
                        case BattleCardEffectType.BonusDamageFromBattleElementBonus:
                            currentDamage += GetBattleElementDamageBonus(card.elementType) * Mathf.Max(0, effect.value);
                            break;
                        case BattleCardEffectType.RepeatAttack:
                            repeatEffects.Add(effect);
                            break;
                    }
                }
            }

            switch (card.id)
            {
                case "dark_crow":
                    effectState.AddNextElementDamageMultiplierPercent(ElementType.Dark, 150);
                    break;
            }

            currentDamage = Mathf.RoundToInt(currentDamage * (damageMultiplierPercent / 100f));

            if (currentDamage > 0)
            {
                _performAttack(_playerUnit, _enemyUnit, currentDamage, card.cardName, card.elementType, 0);
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

            ApplyPostCardEffects(card, effectState, currentDamage, sequenceIndex, blessingTriggered);
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

        private void ApplyPostCardEffects(BattleCardData card, TurnEffectState effectState, int currentDamage, int sequenceIndex, bool blessingTriggered)
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
                            if (selfDamage > 0)
                            {
                                effectState.MarkPlayerLostHpThisTurn();
                            }
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
                        case BattleCardEffectType.DrawCards:
                            _deckRuntime.DrawCardsIntoHand(Mathf.Max(0, effect.value));
                            _battleUI.AddBattleLog($"カードを {Mathf.Max(0, effect.value)} 枚引いたぬめ");
                            break;
                        case BattleCardEffectType.DrawCardsIfPlayerLostHpThisTurn:
                            if (effectState.PlayerLostHpThisTurn)
                            {
                                _deckRuntime.DrawCardsIntoHand(Mathf.Max(0, effect.value));
                                _battleUI.AddBattleLog($"協約が満たされ、カードを {Mathf.Max(0, effect.value)} 枚引いたぬめ");
                            }
                            break;
                    }
                }
            }

            ApplyKeywordEffects(card, effectState, blessingTriggered);

            if (card.id == "dark_crow")
            {
                _battleUI.AddBattleLog("次の闇カードの威力が大きく上がったぬめ");
            }

            switch (card.id)
            {
                case "fire_ash":
                    var recoveredCard = _deckRuntime.ReturnLastExhaustedCardToHand();
                    if (recoveredCard != null)
                    {
                        _battleUI.AddBattleLog($"{recoveredCard.cardName} が手札に戻ったぬめ");
                    }
                    break;
                case "wind_step":
                    effectState.TurnWideSpDiscount += 1;
                    _battleUI.AddBattleLog("このターンの手札の消費SPが下がったぬめ");
                    break;
                case "wind_feather":
                    if (_deckRuntime.DiscardFirstHandCardExcept("wind_feather"))
                    {
                        _battleUI.AddBattleLog("手札を1枚捨てたぬめ");
                    }
                    _deckRuntime.DrawCardsIntoHand(3);
                    break;
                case "light_sunlight":
                    var healFromDamage = Mathf.FloorToInt(Mathf.Max(0, currentDamage) * 0.5f);
                    var lifeSteal = _playerUnit.Heal(healFromDamage);
                    _battleUI.AddBattleLog($"{_playerUnit.DisplayName} はHPを {lifeSteal} 回復");
                    break;
            }
        }

        private void ApplyKeywordEffects(BattleCardData card, TurnEffectState effectState, bool blessingTriggered)
        {
            if (card.keywords == null || card.keywords.Count == 0)
            {
                return;
            }

            if (TryGetKeywordValue(card, BattleCardKeywordType.Kindle, out var fireBonus))
            {
                AddBattleElementDamageBonus(ElementType.Fire, fireBonus);
                _battleUI.AddBattleLog($"熾火が燃え上がり、炎カードが {fireBonus} 強くなったぬめ");
            }

            if (TryGetKeywordValue(card, BattleCardKeywordType.Freeze, out var freezeAmount))
            {
                var totalFreeze = _enemyUnit.AddFreeze(freezeAmount);
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} に凍結 {freezeAmount} を付与 ({totalFreeze}/{_enemyUnit.FreezeThreshold})");
            }

            if (HasKeyword(card, BattleCardKeywordType.Revelation))
            {
                effectState.AddRevelation();
                _battleUI.AddBattleLog("啓示が灯り、次の祝福が開くぬめ");
            }

            if (TryGetKeywordValue(card, BattleCardKeywordType.Blessing, out var blessAmount))
            {
                if (blessingTriggered)
                {
                    effectState.NextCardDamageBonus += blessAmount;
                    _battleUI.AddBattleLog($"祝福が満ち、次のカードの威力が {blessAmount} 上がったぬめ");
                }
            }

            if (HasKeyword(card, BattleCardKeywordType.Tailwind) && card.elementType == ElementType.Wind && effectState.GetUsedElementCount(ElementType.Wind) == 1)
            {
                _deckRuntime.DrawCardsIntoHand(1);
                _battleUI.AddBattleLog("追風が吹き、カードを1枚引いたぬめ");
            }
        }

        private int GetBattleElementDamageBonus(ElementType elementType)
        {
            return _battleElementDamageBonus.TryGetValue(elementType, out var value) ? value : 0;
        }

        private void AddBattleElementDamageBonus(ElementType elementType, int value)
        {
            if (value <= 0)
            {
                return;
            }

            _battleElementDamageBonus[elementType] = GetBattleElementDamageBonus(elementType) + value;
        }

        private static bool HasKeyword(BattleCardData card, BattleCardKeywordType keywordType)
        {
            if (card?.keywords == null)
            {
                return false;
            }

            for (var i = 0; i < card.keywords.Count; i++)
            {
                var keyword = card.keywords[i];
                if (keyword != null && keyword.keywordType == keywordType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasBlessingKeyword(BattleCardData card)
        {
            return TryGetKeywordValue(card, BattleCardKeywordType.Blessing, out _);
        }

        private static bool TryGetKeywordValue(BattleCardData card, BattleCardKeywordType keywordType, out int value)
        {
            value = 0;
            if (card?.keywords == null)
            {
                return false;
            }

            for (var i = 0; i < card.keywords.Count; i++)
            {
                var keyword = card.keywords[i];
                if (keyword == null || keyword.keywordType != keywordType)
                {
                    continue;
                }

                value = keyword.value;
                return true;
            }

            return false;
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
