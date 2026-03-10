using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        BonusDamageIfHandCountAtMost
    }

    [Serializable]
    public class BattleCardEffectData
    {
        public BattleCardEffectType effectType;
        public int value;
        public int secondaryValue;
    }

    [Serializable]
    public class BattleCardData
    {
        public string id = "attack";
        public string cardName = "Strike";
        public string description = "Basic attack.";
        public int damage = 8;
        public int spCost = 1;
        public ElementType elementType = ElementType.None;
        public bool exhaustAfterUse;
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
    public class BattleCardJson
    {
        public string id;
        public string cardName;
        public string description;
        public int damage;
        public int spCost;
        public string elementType;
        public bool exhaustAfterUse;
        public List<BattleCardEffectJson> effects = new();
    }

    [Serializable]
    public class BattleCardCatalog
    {
        public List<BattleCardJson> cards = new();
    }

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

            public int NextCardDamageBonus { get; set; }

            public int NextCardSpDiscount { get; set; }

            public int ConsumeDamageBonus(ElementType elementType)
            {
                var total = NextCardDamageBonus;
                NextCardDamageBonus = 0;

                if (_nextElementDamageBonus.TryGetValue(elementType, out var elementBonus))
                {
                    total += elementBonus;
                    _nextElementDamageBonus.Remove(elementType);
                }

                return total;
            }

            public int ConsumeSpDiscount(ElementType elementType)
            {
                var total = NextCardSpDiscount;
                NextCardSpDiscount = 0;

                if (_nextElementSpDiscount.TryGetValue(elementType, out var elementDiscount))
                {
                    total += elementDiscount;
                    _nextElementSpDiscount.Remove(elementType);
                }

                return total;
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
        [SerializeField] private List<BattleCardData> cardPool = new();

        private BattleState _state;
        private BattleUnit _playerUnit;
        private BattleUnit _enemyUnit;
        private BattleUI _battleUI;
        private Coroutine _enemyTurnCoroutine;
        private Coroutine _playerActionCoroutine;
        private int _turnCount;
        private readonly List<BattleCardData> _allCards = new();
        private readonly List<BattleCardData> _selectedDeck = new();
        private readonly List<BattleCardData> _drawPile = new();
        private readonly List<BattleCardData> _discardPile = new();
        private readonly List<BattleCardData> _hand = new();
        private readonly List<int> _selectedCardIndices = new();

        public void Initialize(BattleUI battleUI)
        {
            InitializeBattle(battleUI);
        }

        public void InitializeBattle(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);
            LoadCardCatalog();
            SetupDefaultDeck();
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
            SetupDefaultDeck();
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(enemyUnitData);
            _state = BattleState.DeckEditing;

            _battleUI.SetBattleScreenVisible(false);
            _battleUI.ShowDeckEditor(_allCards, GetSelectedDeckIds(), RequiredDeckSize);
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

            var card = FindCardById(cardId);
            if (card == null)
            {
                return;
            }

            if (_selectedDeck.Contains(card))
            {
                _selectedDeck.Remove(card);
            }
            else
            {
                if (_selectedDeck.Count >= RequiredDeckSize)
                {
                    _battleUI.SetDeckEditorHint($"デッキは {RequiredDeckSize} 枚までぬめ");
                    _battleUI.SetDeckEditorSelection(GetSelectedDeckIds(), RequiredDeckSize);
                    return;
                }

                _selectedDeck.Add(card);
            }

            _battleUI.SetDeckEditorHint(_selectedDeck.Count == RequiredDeckSize ? "戦闘開始できるぬめ" : $"あと {RequiredDeckSize - _selectedDeck.Count} 枚必要ぬめ");
            _battleUI.SetDeckEditorSelection(GetSelectedDeckIds(), RequiredDeckSize);
        }

        public void ConfirmDeckSelection()
        {
            if (_state != BattleState.DeckEditing || _selectedDeck.Count != RequiredDeckSize)
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

            if (cardIndex < 0 || cardIndex >= _hand.Count)
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
                var message = $"SP不足: {_hand[cardIndex].cardName} を追加できません";
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
            _selectedCardIndices.Clear();
            UpdateCardState();
            _playerActionCoroutine = StartCoroutine(ResolvePlayerCards(selectedIndices));
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
            _drawPile.Clear();
            _discardPile.Clear();
            _hand.Clear();

            _playerUnit.Reset();
            _enemyUnit.Reset();
            var weakElements = GetRandomWeakElements();
            _enemyUnit.SetWeakElements(weakElements.primary, weakElements.secondary);
            _enemyUnit.SetShieldCount(3);
            BuildDrawPile();

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
            DrawHand();
            _battleUI.RefreshHand(_hand);
            _battleUI.SetTurnText(turnMessage);
            _battleUI.SetTurnCount(_turnCount);
            RefreshUi();
            UpdateCardState();
            var recoveredSp = _playerUnit.CurrentSp - previousSp;
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のターン: SP {recoveredSp} 回復");
        }

        private void DrawHand()
        {
            for (var i = 0; i < _hand.Count; i++)
            {
                _discardPile.Add(_hand[i]);
            }

            _hand.Clear();

            for (var i = 0; i < HandSize; i++)
            {
                if (_drawPile.Count == 0)
                {
                    if (_discardPile.Count == 0)
                    {
                        break;
                    }

                    _drawPile.AddRange(_discardPile);
                    _discardPile.Clear();
                    ShuffleCards(_drawPile);
                }

                var drawIndex = _drawPile.Count - 1;
                _hand.Add(_drawPile[drawIndex]);
                _drawPile.RemoveAt(drawIndex);
            }
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

        private IEnumerator ResolvePlayerCards(IReadOnlyList<int> selectedIndices)
        {
            var effectState = new PlayerTurnEffectState();

            for (var i = 0; i < selectedIndices.Count; i++)
            {
                var cardIndex = selectedIndices[i];
                if (cardIndex < 0 || cardIndex >= _hand.Count)
                {
                    continue;
                }

                var selectedCard = _hand[cardIndex];
                var actualCost = Mathf.Max(0, selectedCard.spCost - effectState.ConsumeSpDiscount(selectedCard.elementType));
                if (!_playerUnit.SpendSp(actualCost))
                {
                    continue;
                }

                RefreshUi();
                _battleUI.AddBattleLog($"{_playerUnit.DisplayName} は {selectedCard.cardName} を使用");
                ResolveCardEffects(selectedCard, effectState, i + 1);

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

                if (i < selectedIndices.Count - 1)
                {
                    yield return new WaitForSeconds(playerCardActionInterval);
                }
            }

            MovePlayedCardsToDiscard(selectedIndices);

            _playerActionCoroutine = null;
            _state = BattleState.EnemyTurn;
            _battleUI.SetTurnText("敵のターンです");
            _battleUI.AddBattleLog("敵のターン");
            UpdateCardState();
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurn());
        }

        private void ResolveCardEffects(BattleCardData card, PlayerTurnEffectState effectState, int sequenceIndex)
        {
            var damageBonus = effectState.ConsumeDamageBonus(card.elementType);
            var currentDamage = Mathf.Max(0, card.damage + damageBonus);
            var extraShieldDamage = 0;
            var repeatEffects = new List<BattleCardEffectData>();

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
                            if (Mathf.Max(0, _hand.Count - sequenceIndex) <= Mathf.Max(0, effect.value))
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

            if (currentDamage > 0 || (extraShieldDamage > 0 && card.elementType != ElementType.None))
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
            _battleUI.SetCardsInteractable(false, _hand, 0, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _hand, _playerUnit.CurrentSp);
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
            _battleUI.SetCardsInteractable(_state == BattleState.PlayerTurn, _hand, remainingSp, _selectedCardIndices);
            _battleUI.SetSelectedCards(_selectedCardIndices, _hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(_state == BattleState.PlayerTurn && _selectedCardIndices.Count > 0, GetSelectedSpCost(_selectedCardIndices));
        }

        private int GetSelectedSpCost(IReadOnlyList<int> selectedIndices)
        {
            var total = 0;
            var effectState = new PlayerTurnEffectState();

            for (var i = 0; i < selectedIndices.Count; i++)
            {
                var index = selectedIndices[i];
                if (index < 0 || index >= _hand.Count)
                {
                    continue;
                }

                var card = _hand[index];
                total += Mathf.Max(0, card.spCost - effectState.ConsumeSpDiscount(card.elementType));
                ApplySelectionDiscountEffects(card, effectState);
            }

            return total;
        }

        private static void ApplySelectionDiscountEffects(BattleCardData card, PlayerTurnEffectState effectState)
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
                    case BattleCardEffectType.DiscountNextCardSp:
                        effectState.NextCardSpDiscount += Mathf.Max(0, effect.value);
                        break;
                    case BattleCardEffectType.DiscountNextElementSp:
                        effectState.AddNextElementSpDiscount(card.elementType, effect.value);
                        break;
                }
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

        private void LoadCardCatalog()
        {
            var catalogAsset = Resources.Load<TextAsset>(CardCatalogResourcePath);
            if (catalogAsset == null || string.IsNullOrWhiteSpace(catalogAsset.text))
            {
                throw new InvalidOperationException($"カードカタログが見つからないぬめ: Resources/{CardCatalogResourcePath}.json");
            }

            var catalog = JsonUtility.FromJson<BattleCardCatalog>(catalogAsset.text);
            if (catalog?.cards == null || catalog.cards.Count == 0)
            {
                throw new InvalidOperationException($"カードカタログが空か不正ぬめ: Resources/{CardCatalogResourcePath}.json");
            }

            cardPool = new List<BattleCardData>(catalog.cards.Count);
            for (var i = 0; i < catalog.cards.Count; i++)
            {
                cardPool.Add(ConvertCard(catalog.cards[i]));
            }

            _allCards.Clear();
            _allCards.AddRange(cardPool);
        }

        private static BattleCardData ConvertCard(BattleCardJson source)
        {
            if (source == null)
            {
                throw new InvalidOperationException("カード定義が不正ぬめ");
            }

            return new BattleCardData
            {
                id = string.IsNullOrWhiteSpace(source.id) ? source.cardName : source.id,
                cardName = source.cardName,
                description = source.description,
                damage = source.damage,
                spCost = source.spCost,
                elementType = ParseElementType(source.elementType),
                exhaustAfterUse = source.exhaustAfterUse,
                effects = ConvertEffects(source.effects)
            };
        }

        private static List<BattleCardEffectData> ConvertEffects(List<BattleCardEffectJson> source)
        {
            var effects = new List<BattleCardEffectData>();
            if (source == null)
            {
                return effects;
            }

            for (var i = 0; i < source.Count; i++)
            {
                var effect = source[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.effectType))
                {
                    continue;
                }

                effects.Add(new BattleCardEffectData
                {
                    effectType = ParseEffectType(effect.effectType),
                    value = effect.value,
                    secondaryValue = effect.secondaryValue
                });
            }

            return effects;
        }

        private static ElementType ParseElementType(string elementType)
        {
            if (Enum.TryParse(elementType, true, out ElementType parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"不明な属性ぬめ: {elementType}");
        }

        private static BattleCardEffectType ParseEffectType(string effectType)
        {
            if (Enum.TryParse(effectType, true, out BattleCardEffectType parsed))
            {
                return parsed;
            }

            throw new InvalidOperationException($"不明なカード効果ぬめ: {effectType}");
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

        private void SetupDefaultDeck()
        {
            _selectedDeck.Clear();
            for (var i = 0; i < Mathf.Min(RequiredDeckSize, _allCards.Count); i++)
            {
                _selectedDeck.Add(_allCards[i]);
            }
        }

        private BattleCardData FindCardById(string cardId)
        {
            for (var i = 0; i < _allCards.Count; i++)
            {
                if (string.Equals(_allCards[i].id, cardId, StringComparison.Ordinal))
                {
                    return _allCards[i];
                }
            }

            return null;
        }

        private List<string> GetSelectedDeckIds()
        {
            var ids = new List<string>(_selectedDeck.Count);
            for (var i = 0; i < _selectedDeck.Count; i++)
            {
                ids.Add(_selectedDeck[i].id);
            }

            return ids;
        }

        private void BuildDrawPile()
        {
            _drawPile.Clear();
            _discardPile.Clear();
            _drawPile.AddRange(_selectedDeck);
            ShuffleCards(_drawPile);
        }

        private static void ShuffleCards(List<BattleCardData> cards)
        {
            for (var i = cards.Count - 1; i > 0; i--)
            {
                var swapIndex = UnityEngine.Random.Range(0, i + 1);
                (cards[i], cards[swapIndex]) = (cards[swapIndex], cards[i]);
            }
        }

        private void MovePlayedCardsToDiscard(IReadOnlyList<int> selectedIndices)
        {
            for (var i = selectedIndices.Count - 1; i >= 0; i--)
            {
                var index = selectedIndices[i];
                if (index < 0 || index >= _hand.Count)
                {
                    continue;
                }

                var playedCard = _hand[index];
                if (!playedCard.exhaustAfterUse)
                {
                    _discardPile.Add(playedCard);
                }

                _hand.RemoveAt(index);
            }

            _battleUI.RefreshHand(_hand);
        }

        private void RemoveCardFromBattleDeck(BattleCardData card)
        {
            _selectedDeck.Remove(card);
            _drawPile.Remove(card);
            _discardPile.Remove(card);
        }
    }
}
