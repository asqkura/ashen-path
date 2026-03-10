using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public class BattleController : MonoBehaviour
    {
        private const string CardCatalogResourcePath = "Battle/card-catalog";
        private const int RequiredDeckSize = 15;
        private const int HandSize = 5;

        private enum BattleState
        {
            DeckEditing,
            PlayerTurn,
            EnemyTurn,
            BattleEnded
        }

        private enum EnemyAction
        {
            Attack
        }

        private sealed class PlayerTurnEffectState
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

        [SerializeField] private float enemyTurnDelay = 0.9f;
        [SerializeField] private float playerCardActionInterval = 0.35f;
        [SerializeField] private BattleUnitData playerUnitData = new()
        {
            displayName = "Player",
            maxHp = 40,
            attackPower = 8,
            maxSp = 5,
            spRecoveryPerTurn = 2
        };
        [SerializeField] private BattleUnitData enemyUnitData = new()
        {
            displayName = "Enemy",
            maxHp = 32,
            attackPower = 6
        };
        private BattleState _state;
        private BattleUnit _playerUnit;
        private BattleUnit _enemyUnit;
        private BattleUI _battleUI;
        private Coroutine _enemyTurnCoroutine;
        private Coroutine _playerActionCoroutine;
        private int _turnCount;
        private readonly BattleDeckRuntime _deckRuntime = new();
        private readonly List<int> _selectedCardIndices = new();

        public void Initialize(BattleUI battleUI)
        {
            InitializeDeckEditor(battleUI);
        }

        public void InitializeBattle(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);
            LoadCardCatalog();
            _deckRuntime.SetupDefaultDeck(RequiredDeckSize);
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _battleUI.HideDeckEditor();
            _battleUI.SetBattleScreenVisible(true);
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.ClearBattleLog();
            BeginBattle();
        }

        public void InitializeDeckEditor(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);
            LoadCardCatalog();
            _deckRuntime.SetupDefaultDeck(RequiredDeckSize);
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _state = BattleState.DeckEditing;

            _battleUI.SetBattleScreenVisible(false);
            _battleUI.ShowDeckEditor(_deckRuntime.AllCards, _deckRuntime.GetSelectedDeckIds(), RequiredDeckSize);
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.ClearBattleLog();
            _battleUI.SetTurnText("デッキを編成してください");
            _battleUI.SetTurnCount(1);
        }

        public void ToggleDeckCard(string cardId)
        {
            if (_state != BattleState.DeckEditing || string.IsNullOrWhiteSpace(cardId))
            {
                return;
            }

            var card = _deckRuntime.FindCardById(cardId);
            if (card == null)
            {
                return;
            }

            if (_deckRuntime.IsInSelectedDeck(card))
            {
                _deckRuntime.RemoveFromSelectedDeck(card);
            }
            else
            {
                if (!_deckRuntime.TryAddToSelectedDeck(card, RequiredDeckSize))
                {
                    _battleUI.SetDeckEditorHint($"デッキは {RequiredDeckSize} 枚までぬめ");
                    _battleUI.SetDeckEditorSelection(_deckRuntime.GetSelectedDeckIds(), RequiredDeckSize);
                    return;
                }
            }

            _battleUI.SetDeckEditorHint(_deckRuntime.SelectedDeckCount == RequiredDeckSize ? "戦闘開始できるぬめ" : $"あと {RequiredDeckSize - _deckRuntime.SelectedDeckCount} 枚必要ぬめ");
            _battleUI.SetDeckEditorSelection(_deckRuntime.GetSelectedDeckIds(), RequiredDeckSize);
        }

        public void ConfirmDeckSelection()
        {
            if (_state != BattleState.DeckEditing || _deckRuntime.SelectedDeckCount != RequiredDeckSize)
            {
                return;
            }

            BeginBattle();
        }

        public void PerformPlayerCard(int cardIndex)
        {
            if (_state != BattleState.PlayerTurn)
            {
                return;
            }

            if (cardIndex < 0 || cardIndex >= _deckRuntime.Hand.Count)
            {
                return;
            }

            if (_selectedCardIndices.Contains(cardIndex))
            {
                _selectedCardIndices.Remove(cardIndex);
                UpdateCardState();
                return;
            }

            var candidateIndices = new List<int>(_selectedCardIndices) { cardIndex };
            var projectedCost = GetSelectedSpCost(candidateIndices);
            if (projectedCost > _playerUnit.CurrentSp)
            {
                var message = $"SP不足: {_deckRuntime.Hand[cardIndex].cardName} を追加できません";
                _battleUI.SetTurnText(message);
                _battleUI.AddBattleLog(message);
                RefreshUi();
                UpdateCardState();
                return;
            }

            _selectedCardIndices.Add(cardIndex);
            UpdateCardState();
        }

        public void ConfirmSelectedCards()
        {
            if (_state != BattleState.PlayerTurn || _selectedCardIndices.Count == 0)
            {
                return;
            }

            var selectedIndices = new List<int>(_selectedCardIndices);
            var selectedCards = new List<BattleCardData>(selectedIndices.Count);
            for (var i = 0; i < selectedIndices.Count; i++)
            {
                var index = selectedIndices[i];
                if (index >= 0 && index < _deckRuntime.Hand.Count)
                {
                    selectedCards.Add(_deckRuntime.Hand[index]);
                }
            }

            _selectedCardIndices.Clear();
            UpdateCardState();
            _playerActionCoroutine = StartCoroutine(ResolvePlayerCards(selectedCards));
        }

        private IEnumerator ExecuteEnemyTurn()
        {
            yield return new WaitForSeconds(enemyTurnDelay);

            if (_state != BattleState.EnemyTurn)
            {
                yield break;
            }

            if (_enemyUnit.IsBroken)
            {
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} は Break 中で行動不能ぬめ！");
                _enemyUnit.EndBreak();
                RefreshUi();
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} のシールドが回復したぬめ");
                StartPlayerTurn("プレイヤーのターンです");
                yield break;
            }

            switch (ChooseEnemyAction())
            {
                case EnemyAction.Attack:
                    var attackPower = Mathf.Max(0, _enemyUnit.AttackPower + _enemyUnit.ConsumePendingAttackModifier());
                    attackPower = Mathf.RoundToInt(attackPower * (_enemyUnit.ConsumePendingAttackMultiplierPercent() / 100f));
                    PerformAttack(_enemyUnit, _playerUnit, attackPower, "Claw", ElementType.None);
                    break;
            }

            if (TryResolveBattleEnd())
            {
                yield break;
            }

            StartPlayerTurn("プレイヤーのターンです");
        }

        private EnemyAction ChooseEnemyAction()
        {
            return EnemyAction.Attack;
        }

        private void BeginBattle()
        {
            _state = BattleState.PlayerTurn;
            _turnCount = 0;
            _selectedCardIndices.Clear();

            _playerUnit.Reset();
            _enemyUnit.Reset();
            var weakElements = GetRandomWeakElements();
            _enemyUnit.SetWeakElements(weakElements.primary, weakElements.secondary);
            _enemyUnit.SetShieldCount(3);
            _deckRuntime.BeginBattle();

            _battleUI.SetBattleScreenVisible(true);
            _battleUI.HideDeckEditor();
            RefreshUi();
            _battleUI.ClearBattleLog();
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} と {_enemyUnit.DisplayName} の戦闘開始");
            StartPlayerTurn("プレイヤーのターンです");
        }

        private void StartPlayerTurn(string turnMessage)
        {
            _state = BattleState.PlayerTurn;
            _turnCount++;
            _selectedCardIndices.Clear();
            var previousSp = _playerUnit.CurrentSp;
            _playerUnit.RecoverSp(_playerUnit.SpRecoveryPerTurn);
            _deckRuntime.DrawNewTurnHand(HandSize);
            _battleUI.RefreshHand(_deckRuntime.Hand);
            _battleUI.SetTurnText(turnMessage);
            _battleUI.SetTurnCount(_turnCount);
            RefreshUi();
            UpdateCardState();
            var recoveredSp = _playerUnit.CurrentSp - previousSp;
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のターン: SP {recoveredSp} 回復");
        }

        private void PerformAttack(BattleUnit attacker, BattleUnit defender, int damage, string attackName, ElementType elementType, int extraShieldDamage = 0)
        {
            var wasBroken = defender.IsBroken;
            if (defender.TryBreakShield(elementType))
            {
                _battleUI.AddBattleLog("弱点を突いてシールドを削ったぬめ！");
                for (var i = 0; i < extraShieldDamage; i++)
                {
                    if (!defender.TryBreakShield(elementType))
                    {
                        break;
                    }

                    _battleUI.AddBattleLog("追撃でシールドを削ったぬめ！");
                }

                if (defender.IsBroken)
                {
                    _battleUI.AddBattleLog($"{defender.DisplayName} は Break 状態ぬめ！");
                }
            }

            var dealtDamage = wasBroken ? damage * 2 : damage;
            dealtDamage = defender.TakeDamage(dealtDamage, out var absorbedByBarrier);
            RefreshUi();
            if (defender == _enemyUnit && dealtDamage > 0)
            {
                _battleUI.PlayEnemyDamageEffect(dealtDamage, elementType);
            }

            var message = $"{attacker.DisplayName} の {attackName}！ {defender.DisplayName} に {dealtDamage} ダメージ";
            if (absorbedByBarrier > 0)
            {
                message += $" ({absorbedByBarrier} ガード)";
            }

            _battleUI.SetTurnText(message);
            _battleUI.AddBattleLog(message);
        }

        private IEnumerator ResolvePlayerCards(IReadOnlyList<BattleCardData> selectedCards)
        {
            var effectState = new PlayerTurnEffectState();

            for (var i = 0; i < selectedCards.Count; i++)
            {
                var selectedCard = selectedCards[i];
                if (selectedCard == null)
                {
                    continue;
                }

                var actualCost = Mathf.Max(0, selectedCard.spCost - effectState.ConsumeSpDiscount(selectedCard.elementType));
                if (!_playerUnit.SpendSp(actualCost))
                {
                    continue;
                }

                RefreshUi();
                _battleUI.AddBattleLog($"{_playerUnit.DisplayName} は {selectedCard.cardName} を使用");
                ResolveCardEffects(selectedCard, effectState, i + 1);
                FinalizePlayedCard(selectedCard);

                if (selectedCard.exhaustAfterUse)
                {
                    RemoveCardFromBattleDeck(selectedCard);
                    _battleUI.AddBattleLog($"{selectedCard.cardName} は使い切りで消滅");
                }

                if (TryResolveBattleEnd())
                {
                    _playerActionCoroutine = null;
                    yield break;
                }

                if (i < selectedCards.Count - 1)
                {
                    yield return new WaitForSeconds(playerCardActionInterval);
                }
            }

            _playerActionCoroutine = null;
            _state = BattleState.EnemyTurn;
            _battleUI.SetTurnText("敵のターンです");
            _battleUI.AddBattleLog("敵のターン");
            UpdateCardState();
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurn());
        }

        private void ResolveCardEffects(BattleCardData card, PlayerTurnEffectState effectState, int sequenceIndex)
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
                    currentDamage = DiscardHandAndCount() * 5;
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
                PerformAttack(_playerUnit, _enemyUnit, currentDamage, card.cardName, card.elementType, extraShieldDamage);
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
                    PerformAttack(_playerUnit, _enemyUnit, hitDamage, $"{card.cardName} 追撃", card.elementType);
                    if (_enemyUnit.IsDead)
                    {
                        break;
                    }
                }
            }

            ApplyPostCardEffects(card, effectState);

            switch (card.id)
            {
                case "neutral_draw":
                    DrawCardsIntoHand(2);
                    break;
                case "fire_burn_up":
                    effectState.AddTurnElementDamageBonus(ElementType.Fire, 4);
                    _battleUI.AddBattleLog("このターンの炎カードが強化されたぬめ");
                    break;
                case "fire_ash":
                    ReturnExhaustedCardToHand();
                    break;
                case "ice_freeze":
                    _enemyUnit.SetPendingAttackMultiplierPercent(50);
                    _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} の次の攻撃が半減するぬめ");
                    break;
                case "ice_crystal":
                    DrawCardsIntoHand(1);
                    break;
                case "wind_wind":
                    DrawCardsIntoHand(1);
                    break;
                case "wind_breeze":
                    DrawCardsIntoHand(2);
                    break;
                case "wind_step":
                    effectState.TurnWideSpDiscount += 1;
                    _battleUI.AddBattleLog("このターンの手札の消費SPが下がったぬめ");
                    break;
                case "wind_cyclone":
                    DrawCardsIntoHand(1);
                    break;
                case "wind_feather":
                    DiscardRandomHandCard();
                    DrawCardsIntoHand(3);
                    break;
                case "light_barrier":
                    _battleUI.AddBattleLog("このターンは弱体を防ぐぬめ");
                    break;
                case "light_sunlight":
                    var healFromDamage = Mathf.FloorToInt(Mathf.Max(0, currentDamage) * 0.5f);
                    var lifesteal = _playerUnit.Heal(healFromDamage);
                    _battleUI.AddBattleLog($"{_playerUnit.DisplayName} はHPを {lifesteal} 回復");
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

            RefreshUi();
        }

        private void ApplyPostCardEffects(BattleCardData card, PlayerTurnEffectState effectState)
        {
            if (card.effects == null)
            {
                return;
            }

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

            if (card.id == "dark_crow")
            {
                effectState.AddNextElementDamageMultiplierPercent(ElementType.Dark, 150);
                _battleUI.AddBattleLog("次の闇カードの威力が大きく上がったぬめ");
            }
        }

        private bool TryResolveBattleEnd()
        {
            if (!_playerUnit.IsDead && !_enemyUnit.IsDead)
            {
                return false;
            }

            _state = BattleState.BattleEnded;

            if (_enemyTurnCoroutine != null)
            {
                StopCoroutine(_enemyTurnCoroutine);
                _enemyTurnCoroutine = null;
            }

            if (_playerActionCoroutine != null)
            {
                StopCoroutine(_playerActionCoroutine);
                _playerActionCoroutine = null;
            }

            var resultMessage = _playerUnit.IsDead ? "敗北..." : "勝利！";
            _battleUI.SetResultText(resultMessage, true);
            _battleUI.SetTurnText("戦闘終了");
            _battleUI.AddBattleLog(resultMessage);
            _battleUI.SetCardsInteractable(false, _deckRuntime.Hand, 0, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(false, 0);
            return true;
        }

        private void RefreshUi()
        {
            _battleUI.RefreshUnits(_playerUnit, _enemyUnit);
            _battleUI.RefreshPlayerSp(_playerUnit.CurrentSp, _playerUnit.MaxSp);
        }

        private void UpdateCardState()
        {
            var remainingSp = Mathf.Max(0, _playerUnit.CurrentSp - GetSelectedSpCost(_selectedCardIndices));
            _battleUI.SetCardsInteractable(_state == BattleState.PlayerTurn, _deckRuntime.Hand, remainingSp, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(_state == BattleState.PlayerTurn && _selectedCardIndices.Count > 0, GetSelectedSpCost(_selectedCardIndices));
        }

        private int GetSelectedSpCost(IReadOnlyList<int> selectedIndices)
        {
            var total = 0;
            var effectState = new PlayerTurnEffectState();

            for (var i = 0; i < selectedIndices.Count; i++)
            {
                var index = selectedIndices[i];
                if (index < 0 || index >= _deckRuntime.Hand.Count)
                {
                    continue;
                }

                var card = _deckRuntime.Hand[index];
                total += Mathf.Max(0, card.spCost - effectState.ConsumeSpDiscount(card.elementType));
                ApplySelectionDiscountEffects(card, effectState);
            }

            return total;
        }

        private static void ApplySelectionDiscountEffects(BattleCardData card, PlayerTurnEffectState effectState)
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

        private static (ElementType primary, ElementType secondary) GetRandomWeakElements()
        {
            var primary = (ElementType)UnityEngine.Random.Range(1, 6);
            var secondary = primary;
            while (secondary == primary)
            {
                secondary = (ElementType)UnityEngine.Random.Range(1, 6);
            }

            return (primary, secondary);
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

        private void FinalizePlayedCard(BattleCardData card)
        {
            _deckRuntime.FinalizePlayedCard(card);
            if (card.id == "dark_abyss")
            {
                _battleUI.AddBattleLog($"{card.cardName} は手札に戻ったぬめ");
            }

            _battleUI.RefreshHand(_deckRuntime.Hand);
        }

        private void DrawCardsIntoHand(int count)
        {
            _deckRuntime.DrawCardsIntoHand(count);
            _battleUI.RefreshHand(_deckRuntime.Hand);
        }

        private int DiscardHandAndCount()
        {
            var discardCount = _deckRuntime.DiscardHandAndCount("dark_grim");
            _battleUI.RefreshHand(_deckRuntime.Hand);
            return discardCount;
        }

        private void DiscardRandomHandCard()
        {
            if (_deckRuntime.Hand.Count <= 1)
            {
                return;
            }

            if (_deckRuntime.DiscardFirstHandCardExcept("wind_feather"))
            {
                _battleUI.AddBattleLog("手札を1枚捨てたぬめ");
                _battleUI.RefreshHand(_deckRuntime.Hand);
            }
        }

        private void ReturnExhaustedCardToHand()
        {
            var recoveredCard = _deckRuntime.ReturnLastExhaustedCardToHand();
            if (recoveredCard == null)
            {
                return;
            }

            _battleUI.AddBattleLog($"{recoveredCard.cardName} が手札に戻ったぬめ");
            _battleUI.RefreshHand(_deckRuntime.Hand);
        }

        private void LoadCardCatalog()
        {
            _deckRuntime.ReplaceCatalog(BattleCardCatalogLoader.LoadFromResources(CardCatalogResourcePath));
        }

        private void RemoveCardFromBattleDeck(BattleCardData card)
        {
            _deckRuntime.RemoveCardFromBattleDeck(card);
        }
    }
}
