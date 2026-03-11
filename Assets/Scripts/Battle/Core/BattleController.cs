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
        private readonly BattleDeckEditSession _deckEditSession = new(RequiredDeckSize, 5, 4, 3, 6, 2);
        private readonly List<BattleDeckPresetData> _deckPresets = new();
        private BattleCardResolver _cardResolver;
        private BattleCardResolver.TurnEffectState _turnEffectState;
        private readonly List<int> _selectedCardIndices = new();
        private int _selectedDeckPresetIndex;

        public void Initialize(BattleUI battleUI)
        {
            InitializeDeckEditor(battleUI);
        }

        public void InitializeBattle(BattleUI battleUI)
        {
            InitializeRuntime(battleUI);
            _battleUI.HideDeckEditor();
            _battleUI.SetBattleScreenVisible(true);
            _battleUI.SetResultText(string.Empty, false);
            _battleUI.ClearBattleLog();
            BeginBattle();
        }

        public void InitializeDeckEditor(BattleUI battleUI)
        {
            InitializeRuntime(battleUI);
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
            if (_state != BattleState.DeckEditing)
            {
                return;
            }

            _deckEditSession.SelectSlot(slotIndex);
            RefreshDeckEditorUi("下の候補カードを押すと、この枠と差し替えるぬめ");
        }

        public void SelectCatalogCard(int cardIndex)
        {
            if (_state != BattleState.DeckEditing)
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

            if (!_deckEditSession.TryReplaceCard(selectedCard.id, _deckRuntime, out var message))
            {
                RefreshDeckEditorUi(message);
                return;
            }

            RefreshDeckEditorUi($"{selectedCard.cardName} を編成したぬめ");
        }

        public void ConfirmDeckSelection()
        {
            if (_state != BattleState.DeckEditing || !_deckEditSession.CanStartBattle())
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

            ResetBattleRuntime();
            _cardResolver = new BattleCardResolver(_battleUI, _deckRuntime, _playerUnit, _enemyUnit, PerformAttack, RefreshUi);
            _deckRuntime.BeginBattle();
            _cardResolver.ResetBattleState();
            _deckRuntime.DrawCardsIntoHand(OpeningHandSize, MaxHandSize);
            _nextEnemyAction = BattleEnemyActionSelector.RollAction(_enemyData, _enemyUnit, enemyUnitData);

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
            _battleUI.SetEnemyIntent(BattleEnemyActionSelector.GetIntentLabel(_nextEnemyAction, _enemyUnit));
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
            _battleUI.AddBattleLog($"敵のターン: {BattleEnemyActionSelector.GetIntentLabel(_nextEnemyAction, _enemyUnit)}");
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

            _nextEnemyAction = BattleEnemyActionSelector.RollAction(_enemyData, _enemyUnit, enemyUnitData);
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
            _battleUI.SetEnemyIntent(_state == BattleState.BattleEnded ? string.Empty : BattleEnemyActionSelector.GetIntentLabel(_nextEnemyAction, _enemyUnit));
            _battleUI.SetBattleStates(
                BattleStateLabelFormatter.GetPlayerStateLabel(_cardResolver, _turnEffectState),
                BattleStateLabelFormatter.GetEnemyStateLabel(_enemyUnit));
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

        private void InitializeRuntime(BattleUI battleUI)
        {
            _battleUI = battleUI;
            _battleUI.Bind(this);
            LoadCardCatalog();
            LoadEnemyCatalog();
            BuildDeckPresets();
            _deckRuntime.HandLimit = MaxHandSize;
            ApplySelectedDeckPreset();
            ResetBattleRuntime();
            _cardResolver = new BattleCardResolver(_battleUI, _deckRuntime, _playerUnit, _enemyUnit, PerformAttack, RefreshUi);
        }

        private void ResetBattleRuntime()
        {
            _playerUnit = new BattleUnit(playerUnitData);
            _enemyUnit = new BattleUnit(GetEnemyUnitData());
        }

        private BattleUnitData GetEnemyUnitData()
        {
            return _enemyData?.unitData ?? enemyUnitData;
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

        private void ApplySelectedDeckPreset()
        {
            if (_deckPresets.Count == 0)
            {
                _deckRuntime.SetupDefaultDeck(RequiredDeckSize);
                return;
            }

            _deckEditSession.ApplyPreset(_deckPresets[_selectedDeckPresetIndex]);
            _deckRuntime.ReplaceSelectedDeckByIds(_deckEditSession.EditingDeckCardIds);
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
                _deckEditSession.GetEditingDeckCards(_deckRuntime),
                _deckEditSession.EditingDeckCardIds,
                _deckEditSession.SelectedSlotIndex,
                _deckRuntime.AllCards,
                _deckEditSession.GetCatalogInteractableIndices(_deckRuntime),
                _deckEditSession.BuildSummary(_deckRuntime));
            _battleUI.SetDeckEditorHint(hintMessage);
            _battleUI.SetDeckEditorSelection(GetSelectedDeckPresetId(), RequiredDeckSize, _deckEditSession.GetRemainingSwapCount(), _deckEditSession.CanStartBattle());
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
