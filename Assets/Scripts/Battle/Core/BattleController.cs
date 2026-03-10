using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AshenPath.Battle
{
    public class BattleController : MonoBehaviour
    {
        private const string CardCatalogResourcePath = "Battle/card-catalog";
        private const string EnemyCatalogResourcePath = "Battle/enemy-catalog";
        private const int RequiredDeckSize = 15;
        private const int MaxHandSize = 5;
        private const int OpeningHandSize = 5;
        private const int TurnDrawCount = 2;
        private const int MaxCardsPerTurn = 3;
        private const int MaxDeckSwaps = 5;
        private const int MaxNeutralCards = 4;
        private const int MaxSupportCards = 3;
        private const int MinPrimaryCards = 6;
        private const int MaxCopiesPerCard = 2;

        private enum BattleState
        {
            DeckEditing,
            PlayerTurn,
            EnemyTurn,
            BattleEnded
        }

        [SerializeField] private float enemyTurnDelay = 0.9f;
        [SerializeField] private float playerCardActionInterval = 0.2f;
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
            maxHp = 48,
            attackPower = 8
        };

        private BattleState _state;
        private BattleUnit _playerUnit;
        private BattleUnit _enemyUnit;
        private BattleEnemyData _enemyData;
        private BattleUI _battleUI;
        private Coroutine _enemyTurnCoroutine;
        private Coroutine _playerActionCoroutine;
        private int _turnCount;
        private int _cardsPlayedThisTurn;
        private BattleEnemyActionData _nextEnemyAction;
        private readonly BattleDeckRuntime _deckRuntime = new();
        private readonly List<BattleDeckPresetData> _deckPresets = new();
        private BattleCardResolver _cardResolver;
        private BattleCardResolver.TurnEffectState _turnEffectState;
        private readonly List<int> _selectedCardIndices = new();
        private readonly List<string> _editingDeckCardIds = new();
        private readonly List<string> _baseDeckCardIds = new();
        private int _selectedDeckPresetIndex;
        private int _selectedDeckSlotIndex = -1;

        public void Initialize(BattleUI battleUI)
        {
            InitializeDeckEditor(battleUI);
        }

        public void InitializeBattle(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);
            LoadCardCatalog();
            LoadEnemyCatalog();
            BuildDeckPresets();
            _deckRuntime.HandLimit = MaxHandSize;
            ApplySelectedDeckPreset();
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(GetEnemyUnitData());
            _cardResolver = new BattleCardResolver(_battleUI, _deckRuntime, _playerUnit, _enemyUnit, PerformAttack, RefreshUi);
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
            LoadEnemyCatalog();
            BuildDeckPresets();
            _deckRuntime.HandLimit = MaxHandSize;
            ApplySelectedDeckPreset();
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(GetEnemyUnitData());
            _cardResolver = new BattleCardResolver(_battleUI, _deckRuntime, _playerUnit, _enemyUnit, PerformAttack, RefreshUi);
            _state = BattleState.DeckEditing;

            _battleUI.SetBattleScreenVisible(false);
            RefreshDeckEditorUi("ぬめの想定デッキを選んで、5枚まで差し替えられるぬめ");
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.ClearBattleLog();
            _battleUI.SetTurnText("デッキを選んでください");
            _battleUI.SetTurnCount(1);
            _battleUI.SetEnemyIntent(string.Empty);
        }

        public void SelectDeckPreset(int presetIndex)
        {
            if (_state != BattleState.DeckEditing || presetIndex < 0 || presetIndex >= _deckPresets.Count)
            {
                return;
            }

            _selectedDeckPresetIndex = presetIndex;
            ApplySelectedDeckPreset();
            RefreshDeckEditorUi("差し替える枠を選んで、下の候補から入れ替えるぬめ");
        }

        public void SelectDeckSlot(int slotIndex)
        {
            if (_state != BattleState.DeckEditing || slotIndex < 0 || slotIndex >= _editingDeckCardIds.Count)
            {
                return;
            }

            _selectedDeckSlotIndex = slotIndex;
            RefreshDeckEditorUi("下の候補カードを押すと、この枠と差し替えるぬめ");
        }

        public void SelectCatalogCard(int cardIndex)
        {
            if (_state != BattleState.DeckEditing || _selectedDeckSlotIndex < 0 || _selectedDeckSlotIndex >= _editingDeckCardIds.Count)
            {
                return;
            }

            if (cardIndex < 0 || cardIndex >= _deckRuntime.AllCards.Count)
            {
                return;
            }

            var selectedCard = _deckRuntime.AllCards[cardIndex];
            if (selectedCard == null)
            {
                return;
            }

            if (!TryReplaceEditingDeckCard(_selectedDeckSlotIndex, selectedCard.id, out var message))
            {
                RefreshDeckEditorUi(message);
                return;
            }

            RefreshDeckEditorUi($"{selectedCard.cardName} を編成したぬめ");
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
            if (_state != BattleState.PlayerTurn || _playerActionCoroutine != null)
            {
                return;
            }

            if (cardIndex < 0 || cardIndex >= _deckRuntime.Hand.Count)
            {
                return;
            }

            if (_cardsPlayedThisTurn >= MaxCardsPerTurn)
            {
                _battleUI.SetTurnText("このターンはもうカードを使えないぬめ");
                return;
            }

            var card = _deckRuntime.Hand[cardIndex];
            var actualCost = Mathf.Max(0, card.spCost - _turnEffectState.PeekSpDiscount(card.elementType));
            if (actualCost > _playerUnit.CurrentSp)
            {
                var message = $"SP不足: {card.cardName} を使えません";
                _battleUI.SetTurnText(message);
                _battleUI.AddBattleLog(message);
                RefreshUi();
                UpdateCardState();
                return;
            }

            _playerActionCoroutine = StartCoroutine(ResolvePlayerCard(card, actualCost));
        }

        public void EndPlayerTurn()
        {
            if (_state != BattleState.PlayerTurn || _playerActionCoroutine != null)
            {
                return;
            }

            StartEnemyTurn();
        }

        private void BeginBattle()
        {
            _state = BattleState.PlayerTurn;
            _turnCount = 0;
            _cardsPlayedThisTurn = 0;
            _selectedCardIndices.Clear();
            _turnEffectState = new BattleCardResolver.TurnEffectState();

            _playerUnit.Reset();
            _enemyUnit.Reset();
            _deckRuntime.BeginBattle();
            _cardResolver.ResetBattleState();
            _deckRuntime.DrawCardsIntoHand(OpeningHandSize, MaxHandSize);
            _nextEnemyAction = RollEnemyAction();

            _battleUI.SetBattleScreenVisible(true);
            _battleUI.HideDeckEditor();
            RefreshUi();
            _battleUI.RefreshHand(_deckRuntime.Hand);
            _battleUI.ClearBattleLog();
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} と {_enemyUnit.DisplayName} の戦闘開始");
            StartPlayerTurn("プレイヤーのターンです");
        }

        private void StartPlayerTurn(string turnMessage)
        {
            _state = BattleState.PlayerTurn;
            _turnCount++;
            _cardsPlayedThisTurn = 0;
            _selectedCardIndices.Clear();
            _turnEffectState = new BattleCardResolver.TurnEffectState();

            var previousSp = _playerUnit.CurrentSp;
            _playerUnit.RecoverSp(_playerUnit.SpRecoveryPerTurn);
            if (_turnCount > 1)
            {
                _deckRuntime.DrawCardsIntoHand(TurnDrawCount, MaxHandSize);
            }

            _battleUI.RefreshHand(_deckRuntime.Hand);
            _battleUI.SetTurnText(turnMessage);
            _battleUI.SetTurnCount(_turnCount);
            _battleUI.SetEnemyIntent(GetEnemyIntentLabel(_nextEnemyAction));
            RefreshUi();
            UpdateCardState();

            var recoveredSp = _playerUnit.CurrentSp - previousSp;
            var drawCount = _turnCount == 1 ? OpeningHandSize : TurnDrawCount;
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} のターン: SP {recoveredSp} 回復 / {drawCount} 枚ドロー");
        }

        private void StartEnemyTurn()
        {
            if (_state == BattleState.BattleEnded || _enemyTurnCoroutine != null)
            {
                return;
            }

            _state = BattleState.EnemyTurn;
            _battleUI.SetTurnText("敵のターンです");
            _battleUI.AddBattleLog($"敵のターン: {GetEnemyIntentLabel(_nextEnemyAction)}");
            UpdateCardState();
            _enemyTurnCoroutine = StartCoroutine(ExecuteEnemyTurn());
        }

        private IEnumerator ResolvePlayerCard(BattleCardData card, int actualCost)
        {
            if (card == null)
            {
                _playerActionCoroutine = null;
                yield break;
            }

            var resolvedCost = Mathf.Max(0, card.spCost - _turnEffectState.ConsumeSpDiscount(card.elementType));
            if (!_playerUnit.SpendSp(resolvedCost))
            {
                _playerActionCoroutine = null;
                yield break;
            }

            RefreshUi();
            _battleUI.AddBattleLog($"{_playerUnit.DisplayName} は {card.cardName} を使用");
            FinalizePlayedCard(card);
            _cardResolver.ResolveCard(card, _turnEffectState, _cardsPlayedThisTurn + 1);
            _battleUI.RefreshHand(_deckRuntime.Hand);
            _cardsPlayedThisTurn++;

            if (HasKeyword(card, BattleCardKeywordType.Exhaust))
            {
                RemoveCardFromBattleDeck(card);
                _battleUI.AddBattleLog($"{card.cardName} は使い切りで消滅");
            }

            if (TryResolveBattleEnd())
            {
                _playerActionCoroutine = null;
                yield break;
            }

            yield return new WaitForSeconds(playerCardActionInterval);
            _playerActionCoroutine = null;
            UpdateCardState();

            if (_cardsPlayedThisTurn >= MaxCardsPerTurn || !CanPlayAnyCard())
            {
                StartEnemyTurn();
            }
        }

        private IEnumerator ExecuteEnemyTurn()
        {
            yield return new WaitForSeconds(enemyTurnDelay);

            if (_state != BattleState.EnemyTurn)
            {
                yield break;
            }

            if (_enemyUnit.TryConsumeFrozenActionSkip())
            {
                RefreshUi();
                _battleUI.SetTurnText($"{_enemyUnit.DisplayName} は凍結で動けないぬめ");
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} は凍結で行動をスキップ");
            }
            else
            {
                switch (_nextEnemyAction.actionType)
                {
                    case BattleEnemyActionType.Attack:
                    case BattleEnemyActionType.HeavyAttack:
                    case BattleEnemyActionType.Flurry:
                        PerformEnemyActionAttack(_nextEnemyAction);
                        break;
                    case BattleEnemyActionType.Guard:
                    case BattleEnemyActionType.Focus:
                        ApplyEnemyActionState(_nextEnemyAction);
                        break;
                }
            }

            _enemyTurnCoroutine = null;

            if (TryResolveBattleEnd())
            {
                yield break;
            }

            _nextEnemyAction = RollEnemyAction();
            StartPlayerTurn("プレイヤーのターンです");
        }

        private void PerformEnemyActionAttack(BattleEnemyActionData actionData)
        {
            if (actionData == null)
            {
                return;
            }

            var hitCount = Mathf.Max(1, actionData.hitCount);
            for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                var attackPower = Mathf.Max(0, actionData.attackPower);
                attackPower = Mathf.Max(0, attackPower + _enemyUnit.ConsumePendingAttackModifier());
                attackPower = Mathf.RoundToInt(attackPower * (_enemyUnit.ConsumePendingAttackMultiplierPercent() / 100f));
                var attackName = string.IsNullOrWhiteSpace(actionData.attackName) ? "Claw" : actionData.attackName;
                PerformAttack(_enemyUnit, _playerUnit, attackPower, attackName, ElementType.None);
                if (_playerUnit.IsDead)
                {
                    break;
                }
            }
        }

        private void ApplyEnemyActionState(BattleEnemyActionData actionData)
        {
            if (actionData == null)
            {
                return;
            }

            if (actionData.barrierGain > 0)
            {
                _enemyUnit.AddBarrier(actionData.barrierGain);
            }

            if (actionData.nextAttackMultiplierPercent > 100)
            {
                _enemyUnit.SetPendingAttackMultiplierPercent(actionData.nextAttackMultiplierPercent);
            }

            RefreshUi();
            if (actionData.actionType == BattleEnemyActionType.Focus)
            {
                _battleUI.SetTurnText($"{_enemyUnit.DisplayName} は力を溜めているぬめ");
                _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} は次の攻撃を強化し、ガードを {Mathf.Max(0, actionData.barrierGain)} 獲得");
                return;
            }

            _battleUI.SetTurnText($"{_enemyUnit.DisplayName} は身構えたぬめ");
            _battleUI.AddBattleLog($"{_enemyUnit.DisplayName} はガードを {Mathf.Max(0, actionData.barrierGain)} 獲得");
        }

        private void PerformAttack(BattleUnit attacker, BattleUnit defender, int damage, string attackName, ElementType elementType, int extraShieldDamage = 0)
        {
            var dealtDamage = defender.TakeDamage(Mathf.Max(0, damage), out var absorbedByBarrier);
            RefreshUi();
            if (defender == _enemyUnit && dealtDamage > 0)
            {
                _battleUI.PlayEnemyDamageEffect(dealtDamage, elementType);
            }
            else if (defender == _playerUnit && dealtDamage > 0)
            {
                _battleUI.PlayPlayerDamageEffect(dealtDamage, elementType);
            }

            var message = $"{attacker.DisplayName} の {attackName}！ {defender.DisplayName} に {dealtDamage} ダメージ";
            if (absorbedByBarrier > 0)
            {
                message += $" ({absorbedByBarrier} ガード)";
            }

            _battleUI.SetTurnText(message);
            _battleUI.AddBattleLog(message);
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
            _battleUI.SetEnemyIntent(string.Empty);
            _battleUI.AddBattleLog(resultMessage);
            _battleUI.SetCardsInteractable(false, _deckRuntime.Hand, new List<int>());
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(false, "戦闘終了");
            return true;
        }

        private void RefreshUi()
        {
            _battleUI.RefreshUnits(_playerUnit, _enemyUnit);
            _battleUI.RefreshPlayerSp(_playerUnit.CurrentSp, _playerUnit.MaxSp);
            _battleUI.SetEnemyIntent(_state == BattleState.BattleEnded ? string.Empty : GetEnemyIntentLabel(_nextEnemyAction));
            _battleUI.SetBattleStates(GetPlayerStateLabel(), GetEnemyStateLabel());
        }

        private void UpdateCardState()
        {
            var canUseCards = _state == BattleState.PlayerTurn && _playerActionCoroutine == null && _cardsPlayedThisTurn < MaxCardsPerTurn;
            _battleUI.SetCardsInteractable(canUseCards, _deckRuntime.Hand, GetPlayableCardIndices());
            _battleUI.SetSelectedCards(_selectedCardIndices, _deckRuntime.Hand, _playerUnit.CurrentSp);
            _battleUI.SetConfirmButtonState(_state == BattleState.PlayerTurn && _playerActionCoroutine == null, $"ターン終了 ({_cardsPlayedThisTurn}/{MaxCardsPerTurn})");
        }

        private bool CanPlayAnyCard()
        {
            return GetPlayableCardIndices().Count > 0;
        }

        private List<int> GetPlayableCardIndices()
        {
            var playable = new List<int>();
            if (_state != BattleState.PlayerTurn)
            {
                return playable;
            }

            for (var i = 0; i < _deckRuntime.Hand.Count; i++)
            {
                var cost = Mathf.Max(0, _deckRuntime.Hand[i].spCost - _turnEffectState.PeekSpDiscount(_deckRuntime.Hand[i].elementType));
                if (_playerUnit.CurrentSp >= cost)
                {
                    playable.Add(i);
                }
            }

            return playable;
        }

        private BattleEnemyActionData RollEnemyAction()
        {
            var actions = _enemyData?.actions;
            if (actions == null || actions.Count == 0)
            {
                return CreateFallbackEnemyAction();
            }

            var totalWeight = 0;
            for (var i = 0; i < actions.Count; i++)
            {
                totalWeight += GetEnemyActionWeight(actions[i]);
            }

            if (totalWeight <= 0)
            {
                return CreateFallbackEnemyAction();
            }

            var roll = Random.Range(0, totalWeight);
            for (var i = 0; i < actions.Count; i++)
            {
                var weight = GetEnemyActionWeight(actions[i]);
                if (roll < weight)
                {
                    return actions[i];
                }

                roll -= weight;
            }

            return actions[actions.Count - 1];
        }

        private string GetEnemyIntentLabel(BattleEnemyActionData action)
        {
            if (_enemyUnit != null && _enemyUnit.WillSkipNextAction)
            {
                return $"予告: 凍結停止 ({_enemyUnit.FreezeStack}/{_enemyUnit.FreezeThreshold})";
            }

            var baseLabel = action == null
                ? string.Empty
                : string.IsNullOrWhiteSpace(action.intentLabel)
                    ? GetFallbackIntentLabel(action)
                    : action.intentLabel;

            if (_enemyUnit != null && _enemyUnit.FreezeStack > 0)
            {
                return $"{baseLabel} / 凍結 {_enemyUnit.FreezeStack}";
            }

            return baseLabel;
        }

        private void FinalizePlayedCard(BattleCardData card)
        {
            _deckRuntime.FinalizePlayedCard(card);
            _battleUI.RefreshHand(_deckRuntime.Hand);
        }

        private void LoadCardCatalog()
        {
            _deckRuntime.ReplaceCatalog(BattleCardCatalogLoader.LoadFromResources(CardCatalogResourcePath));
        }

        private void BuildDeckPresets()
        {
            _deckPresets.Clear();
            _deckPresets.Add(CreateDeckPreset(
                "starter_blaze",
                "灰都の残火",
                "熾火を育て、最後は獄炎で焼き切るぬめ。",
                ElementType.Fire,
                "fire_heat", "fire_ember", "fire_kindle_pile", "fire_spark_flare", "fire_backdraft",
                "fire_ash_run", "fire_blaze", "fire_flare", "fire_inferno", "fire_volcano",
                "fire_caldera", "fire_cremation", "neutral_chain", "neutral_brave", "dark_bloodletter"));
            _deckPresets.Add(CreateDeckPreset(
                "frost_lock",
                "白霜の棺",
                "凍てつく拘束で時を奪い、静かに仕留めるぬめ。",
                ElementType.Ice,
                "ice_cold_mist", "ice_ice_edge", "ice_freeze", "ice_hush", "ice_seal",
                "ice_frostbite", "ice_thin_ice", "ice_crystal", "ice_glacia", "ice_shatterfrost",
                "ice_icicle_fall", "ice_avalanche", "neutral_draw", "light_protect", "wind_step"));
            _deckPresets.Add(CreateDeckPreset(
                "wind_loop",
                "宵風の刃",
                "宵風のように手を巡らせ、連撃で裂き続けるぬめ。",
                ElementType.Wind,
                "wind_wind", "wind_breeze", "wind_step", "wind_sway", "wind_replace",
                "wind_gust", "wind_tail_chase", "wind_spiral", "wind_aero", "wind_rapid",
                "wind_cyclone", "wind_feather", "wind_zephyr", "wind_storm_call", "light_omen"));
            _deckPresets.Add(CreateDeckPreset(
                "light_reversal",
                "聖痕の灯",
                "か細い光を繋ぎ、傷を癒やしながら反撃へ転じるぬめ。",
                ElementType.Light,
                "light_guide", "light_omen", "light_prayer", "light_lumina", "light_shine",
                "light_ray", "light_echo", "light_barrier", "light_protect", "light_shelter",
                "light_redemption", "light_sunlight", "light_holy", "light_seraph", "light_judge"));
            _deckPresets.Add(CreateDeckPreset(
                "dark_gamble",
                "黒血の盟約",
                "血を捧げて協約を満たし、禁じ手で叩き伏せるぬめ。",
                ElementType.Dark,
                "dark_sacrifice", "dark_bloodletter", "dark_forbidden", "dark_crow", "dark_dark",
                "dark_grim", "dark_pain_share", "dark_nox", "dark_black_rain", "dark_abyss",
                "dark_night", "dark_soul_eat", "dark_reaper", "neutral_reload", "wind_step"));
            _deckPresets.Add(CreateDeckPreset(
                "balanced_path",
                "灰の旅路",
                "各属性の力を継ぎ合わせた、試練を渡るための道ぬめ。",
                ElementType.None,
                "neutral_attack", "neutral_chain", "neutral_setup", "neutral_draw", "neutral_brave",
                "neutral_reload", "neutral_bandage", "neutral_follow_up", "fire_backdraft", "ice_hush",
                "ice_icicle_fall", "wind_spiral", "wind_storm_call", "light_omen", "dark_reaper"));

            _selectedDeckPresetIndex = Mathf.Clamp(_selectedDeckPresetIndex, 0, Mathf.Max(0, _deckPresets.Count - 1));
        }

        private void LoadEnemyCatalog()
        {
            try
            {
                var enemies = BattleEnemyCatalogLoader.LoadFromResources(EnemyCatalogResourcePath);
                _enemyData = enemies.Count > 0 ? enemies[0] : CreateFallbackEnemyData();
            }
            catch
            {
                _enemyData = CreateFallbackEnemyData();
            }
        }

        private void RemoveCardFromBattleDeck(BattleCardData card)
        {
            _deckRuntime.RemoveCardFromBattleDeck(card);
        }

        private BattleUnitData GetEnemyUnitData()
        {
            return _enemyData?.unitData ?? enemyUnitData;
        }

        private int GetEnemyActionWeight(BattleEnemyActionData action)
        {
            if (action == null)
            {
                return 0;
            }

            var weight = Mathf.Max(0, action.baseWeight);
            if (_enemyUnit != null && _enemyUnit.MaxHp > 0 && _enemyUnit.CurrentHp <= Mathf.RoundToInt(_enemyUnit.MaxHp * 0.4f))
            {
                weight += Mathf.Max(0, action.lowHpBonusWeight);
            }

            if (_enemyUnit != null && _enemyUnit.PendingAttackMultiplierPercent > 100)
            {
                weight += Mathf.Max(0, action.chargedBonusWeight);
            }

            return weight;
        }

        private static string GetFallbackIntentLabel(BattleEnemyActionData action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            return action.actionType switch
            {
                BattleEnemyActionType.Attack => $"予告: 通常攻撃 {Mathf.Max(0, action.attackPower)}",
                BattleEnemyActionType.HeavyAttack => $"予告: 強攻撃 {Mathf.Max(0, action.attackPower)}",
                BattleEnemyActionType.Guard => $"予告: 防御 {Mathf.Max(0, action.barrierGain)}",
                BattleEnemyActionType.Flurry => $"予告: 連撃 {Mathf.Max(0, action.attackPower)}x{Mathf.Max(1, action.hitCount)}",
                BattleEnemyActionType.Focus => $"予告: 溜め / 次攻撃{x(action.nextAttackMultiplierPercent)} + ガード{Mathf.Max(0, action.barrierGain)}",
                _ => string.Empty
            };
        }

        private static string x(int percent)
        {
            return $"{Mathf.Max(100, percent)}%";
        }

        private BattleEnemyActionData CreateFallbackEnemyAction()
        {
            return new BattleEnemyActionData
            {
                actionType = BattleEnemyActionType.Attack,
                intentLabel = $"予告: 通常攻撃 {Mathf.Max(0, GetEnemyUnitData().attackPower)}",
                attackName = "Claw",
                attackPower = Mathf.Max(0, GetEnemyUnitData().attackPower),
                baseWeight = 1,
                hitCount = 1
            };
        }

        private BattleEnemyData CreateFallbackEnemyData()
        {
            return new BattleEnemyData
            {
                id = "fallback_enemy",
                unitData = enemyUnitData,
                actions = new List<BattleEnemyActionData>
                {
                    new()
                    {
                        actionType = BattleEnemyActionType.Attack,
                        intentLabel = $"予告: 通常攻撃 {Mathf.Max(0, enemyUnitData.attackPower)}",
                        attackName = "Claw",
                        attackPower = Mathf.Max(0, enemyUnitData.attackPower),
                        baseWeight = 10
                    }
                }
            };
        }

        private static bool HasKeyword(BattleCardData card, BattleCardKeywordType keywordType)
        {
            if (card?.keywords == null)
            {
                return false;
            }

            for (var i = 0; i < card.keywords.Count; i++)
            {
                if (card.keywords[i] != null && card.keywords[i].keywordType == keywordType)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetPlayerStateLabel()
        {
            var parts = new List<string>();

            if (_cardResolver != null)
            {
                var kindle = _cardResolver.GetBattleElementDamageBonus(ElementType.Fire);
                if (kindle > 0)
                {
                    parts.Add($"熾火 {kindle}");
                }
            }

            if (_turnEffectState != null && _turnEffectState.HasPendingRevelation())
            {
                parts.Add("啓示");
            }

            if (_turnEffectState != null && _turnEffectState.PlayerLostHpThisTurn)
            {
                parts.Add("協約");
            }

            return parts.Count > 0 ? $"状態: {string.Join(" / ", parts)}" : string.Empty;
        }

        private string GetEnemyStateLabel()
        {
            var parts = new List<string>();
            if (_enemyUnit != null && _enemyUnit.FreezeStack > 0)
            {
                parts.Add($"凍結 {_enemyUnit.FreezeStack}/{_enemyUnit.FreezeThreshold}");
            }

            return parts.Count > 0 ? $"状態: {string.Join(" / ", parts)}" : string.Empty;
        }

        private void ApplySelectedDeckPreset()
        {
            if (_deckPresets.Count == 0)
            {
                _deckRuntime.SetupDefaultDeck(RequiredDeckSize);
                return;
            }

            _baseDeckCardIds.Clear();
            _editingDeckCardIds.Clear();

            var presetCardIds = _deckPresets[_selectedDeckPresetIndex].cardIds;
            for (var i = 0; i < presetCardIds.Count; i++)
            {
                _baseDeckCardIds.Add(presetCardIds[i]);
                _editingDeckCardIds.Add(presetCardIds[i]);
            }

            _selectedDeckSlotIndex = _editingDeckCardIds.Count > 0 ? 0 : -1;
            _deckRuntime.ReplaceSelectedDeckByIds(_editingDeckCardIds);
        }

        private string GetSelectedDeckPresetId()
        {
            if (_deckPresets.Count == 0 || _selectedDeckPresetIndex < 0 || _selectedDeckPresetIndex >= _deckPresets.Count)
            {
                return string.Empty;
            }

            return _deckPresets[_selectedDeckPresetIndex].id;
        }

        private void RefreshDeckEditorUi(string hintMessage)
        {
            if (_battleUI == null)
            {
                return;
            }

            _battleUI.ShowDeckEditor(
                _deckPresets,
                GetSelectedDeckPresetId(),
                GetEditingDeckCards(),
                _editingDeckCardIds,
                _selectedDeckSlotIndex,
                _deckRuntime.AllCards,
                GetCatalogInteractableIndices(),
                GetDeckSummaryText());
            _battleUI.SetDeckEditorHint(hintMessage);
            _battleUI.SetDeckEditorSelection(GetSelectedDeckPresetId(), RequiredDeckSize, GetRemainingSwapCount(), CanStartBattleFromEditor());
        }

        private List<BattleCardData> GetEditingDeckCards()
        {
            var cards = new List<BattleCardData>(_editingDeckCardIds.Count);
            for (var i = 0; i < _editingDeckCardIds.Count; i++)
            {
                var card = _deckRuntime.FindCardById(_editingDeckCardIds[i]);
                if (card != null)
                {
                    cards.Add(card);
                }
            }

            return cards;
        }

        private List<int> GetCatalogInteractableIndices()
        {
            var indices = new List<int>();
            for (var i = 0; i < _deckRuntime.AllCards.Count; i++)
            {
                var card = _deckRuntime.AllCards[i];
                if (card == null)
                {
                    continue;
                }

                if (_selectedDeckSlotIndex < 0 || CanReplaceEditingDeckCard(_selectedDeckSlotIndex, card.id))
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private bool TryReplaceEditingDeckCard(int slotIndex, string cardId, out string message)
        {
            message = string.Empty;
            if (!CanReplaceEditingDeckCard(slotIndex, cardId, out message))
            {
                return false;
            }

            _editingDeckCardIds[slotIndex] = cardId;
            _deckRuntime.ReplaceSelectedDeckByIds(_editingDeckCardIds);
            return true;
        }

        private bool CanReplaceEditingDeckCard(int slotIndex, string cardId)
        {
            return CanReplaceEditingDeckCard(slotIndex, cardId, out _);
        }

        private bool CanReplaceEditingDeckCard(int slotIndex, string cardId, out string message)
        {
            message = string.Empty;
            if (slotIndex < 0 || slotIndex >= _editingDeckCardIds.Count)
            {
                message = "差し替える枠を先に選ぶぬめ";
                return false;
            }

            var primaryElement = _deckPresets[_selectedDeckPresetIndex].primaryElement;
            var candidateDeck = new List<string>(_editingDeckCardIds);
            candidateDeck[slotIndex] = cardId;

            if (CountCopies(candidateDeck, cardId) > MaxCopiesPerCard)
            {
                message = "同じカードは2枚までぬめ";
                return false;
            }

            var neutralCount = 0;
            var primaryCount = 0;
            var supportCount = 0;
            for (var i = 0; i < candidateDeck.Count; i++)
            {
                var card = _deckRuntime.FindCardById(candidateDeck[i]);
                if (card == null)
                {
                    continue;
                }

                if (card.elementType == ElementType.None)
                {
                    neutralCount++;
                }
                else if (card.elementType == primaryElement)
                {
                    primaryCount++;
                }
                else
                {
                    supportCount++;
                }
            }

            if (neutralCount > MaxNeutralCards)
            {
                message = "無属性は4枚までぬめ";
                return false;
            }

            if (supportCount > MaxSupportCards)
            {
                message = "補助属性は3枚までぬめ";
                return false;
            }

            if (primaryElement != ElementType.None && primaryCount < MinPrimaryCards)
            {
                message = "主属性カードは6枚以上ほしいぬめ";
                return false;
            }

            if (GetSwapCount(candidateDeck) > MaxDeckSwaps)
            {
                message = "差し替えは5枚までぬめ";
                return false;
            }

            return true;
        }

        private int GetRemainingSwapCount()
        {
            return Mathf.Max(0, MaxDeckSwaps - GetSwapCount(_editingDeckCardIds));
        }

        private int GetSwapCount(List<string> deckCardIds)
        {
            var swapCount = 0;
            var count = Mathf.Min(deckCardIds.Count, _baseDeckCardIds.Count);
            for (var i = 0; i < count; i++)
            {
                if (deckCardIds[i] != _baseDeckCardIds[i])
                {
                    swapCount++;
                }
            }

            return swapCount;
        }

        private bool CanStartBattleFromEditor()
        {
            if (_editingDeckCardIds.Count != RequiredDeckSize)
            {
                return false;
            }

            return GetRemainingSwapCount() >= 0;
        }

        private string GetDeckSummaryText()
        {
            var primaryElement = _deckPresets.Count > 0 ? _deckPresets[_selectedDeckPresetIndex].primaryElement : ElementType.None;
            var primaryCount = 0;
            var neutralCount = 0;
            var supportCount = 0;
            var totalSp = 0;
            var defenseCount = 0;
            var finisherCount = 0;

            for (var i = 0; i < _editingDeckCardIds.Count; i++)
            {
                var card = _deckRuntime.FindCardById(_editingDeckCardIds[i]);
                if (card == null)
                {
                    continue;
                }

                totalSp += card.spCost;
                if (card.elementType == ElementType.None)
                {
                    neutralCount++;
                }
                else if (card.elementType == primaryElement)
                {
                    primaryCount++;
                }
                else
                {
                    supportCount++;
                }

                if (card.damage >= 18)
                {
                    finisherCount++;
                }

                if (card.damage == 0 || CardHasEffect(card, BattleCardEffectType.Heal) || CardHasEffect(card, BattleCardEffectType.GainBarrier))
                {
                    defenseCount++;
                }
            }

            var averageSp = _editingDeckCardIds.Count > 0 ? totalSp / (float)_editingDeckCardIds.Count : 0f;
            return $"主属性 {primaryCount}/{MinPrimaryCards}  無 {neutralCount}/{MaxNeutralCards}  補助 {supportCount}/{MaxSupportCards}  差し替え {GetSwapCount(_editingDeckCardIds)}/{MaxDeckSwaps}\n平均SP {averageSp:0.0}  守り {defenseCount}  締め {finisherCount}";
        }

        private static int CountCopies(List<string> deckCardIds, string cardId)
        {
            var count = 0;
            for (var i = 0; i < deckCardIds.Count; i++)
            {
                if (deckCardIds[i] == cardId)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CardHasEffect(BattleCardData card, BattleCardEffectType effectType)
        {
            if (card?.effects == null)
            {
                return false;
            }

            for (var i = 0; i < card.effects.Count; i++)
            {
                if (card.effects[i] != null && card.effects[i].effectType == effectType)
                {
                    return true;
                }
            }

            return false;
        }

        private static BattleDeckPresetData CreateDeckPreset(string id, string displayName, string description, ElementType primaryElement, params string[] cardIds)
        {
            return new BattleDeckPresetData
            {
                id = id,
                displayName = displayName,
                description = description,
                primaryElement = primaryElement,
                cardIds = new List<string>(cardIds)
            };
        }
    }
}
