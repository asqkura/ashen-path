using System.Collections.Generic;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine;
using UnityEngine.UI;
namespace AshenPath.Battle
{
    public class BattleUI : MonoBehaviour
    {
        private static readonly Vector2 EnemyPanelSize = new(440f, 182f);
        private static readonly Vector2 PlayerPanelSize = new(440f, 182f);
        private static readonly Vector2 StatusBarSize = new(392f, 52f);
        private static readonly Vector2 ArenaSize = new(1360f, 500f);
        private static readonly Vector2 HandRootSize = new(1520f, 220f);
        private static readonly Vector2 LogPanelSize = new(520f, 160f);
        private static readonly Vector2 ConfirmButtonSize = new(220f, 68f);
        private const float CardAspectRatio = 1.4f;
        private const float PanelPadding = 24f;
        private const float StatusSectionSpacing = 4f;
        private const float StatusSectionTopOffset = 46f;
        private const float TopPanelMargin = 32f;
        private const float SelectedCardHop = 18f;
        private const float BarFillAnimationSpeed = 2.8f;
        private const float CursorSeCooldown = 0.12f;
        private const string CursorSeAddress = "Assets/Audios/SE/cursor.mp3";
        private const string CursorSeGuid = "54db76d7baca3e84d8ade6f541a9f026";
        private const string DamageSeAddress = "Assets/Audios/SE/damage.mp3";
        private const string DamageSeGuid = "06aa2d0e4b3e1ae489cf29f1b91b7062";
        private static readonly Color BackgroundColor = new(0.08f, 0.09f, 0.12f, 1f);
        private static readonly Color PanelColor = new(0.14f, 0.16f, 0.2f, 0.92f);
        private static readonly Color EnemyAccent = new(0.76f, 0.32f, 0.32f, 1f);
        private static readonly Color PlayerAccent = new(0.32f, 0.72f, 0.58f, 1f);
        private static readonly Color SpAccent = new(0.36f, 0.64f, 0.96f, 1f);
        private static readonly Color ShieldAccent = new(0.42f, 0.7f, 0.98f, 1f);
        private static readonly Color TextColor = new(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color ArenaColor = new(0.12f, 0.13f, 0.16f, 0.92f);
        private static readonly Color CardColor = new(0.92f, 0.92f, 0.92f, 1f);
        private static readonly Color CardSelectedColor = new(1f, 1f, 1f, 1f);
        private static readonly Color CardDisabledColor = new(0.35f, 0.35f, 0.35f, 0.95f);
        private static readonly Color DarkTextColor = new(0.17f, 0.13f, 0.09f, 1f);
        private static readonly Color DamagePopupColor = new(0.96f, 0.96f, 0.96f, 1f);

        private TMP_FontAsset _font;
        private RectTransform _battleContentRoot;
        private GameObject _deckEditorPanel;
        private TextMeshProUGUI _deckEditorCountText;
        private TextMeshProUGUI _deckEditorHintText;
        private Button _deckEditorStartButton;
        private TextMeshProUGUI _deckEditorStartButtonText;
        private TextMeshProUGUI _turnText;
        private TextMeshProUGUI _resultText;
        private TextMeshProUGUI _playerNameText;
        private TextMeshProUGUI _playerHpText;
        private Image _playerHpFill;
        private TextMeshProUGUI _playerSpText;
        private Image _playerSpFill;
        private TextMeshProUGUI _enemyNameText;
        private TextMeshProUGUI _enemyHpText;
        private Image _enemyHpFill;
        private TextMeshProUGUI _enemyWeakText;
        private TextMeshProUGUI _enemyShieldText;
        private Image _enemyShieldFill;
        private TextMeshProUGUI _logText;
        private TextMeshProUGUI _turnCountText;
        private Button _confirmButton;
        private TextMeshProUGUI _confirmButtonText;
        private readonly Queue<string> _battleLogs = new();
        private readonly List<Button> _cardButtons = new();
        private readonly List<RectTransform> _cardRects = new();
        private readonly List<Vector2> _cardAnchoredPositions = new();
        private readonly List<TextMeshProUGUI> _cardElementTexts = new();
        private readonly List<TextMeshProUGUI> _cardTitleTexts = new();
        private readonly List<TextMeshProUGUI> _cardDescriptionTexts = new();
        private readonly List<TextMeshProUGUI> _cardCostTexts = new();
        private readonly List<Image> _cardBackgrounds = new();
        private readonly List<Button> _deckCardButtons = new();
        private readonly List<string> _deckCardIds = new();
        private readonly List<TextMeshProUGUI> _deckCardTitleTexts = new();
        private readonly List<TextMeshProUGUI> _deckCardDescriptionTexts = new();
        private readonly List<TextMeshProUGUI> _deckCardCostTexts = new();
        private readonly List<Image> _deckCardBackgrounds = new();
        private readonly List<Outline> _deckCardOutlines = new();
        private readonly StringBuilder _logBuilder = new();
        private Sprite _roundedPanelSprite;
        private Sprite _solidFillSprite;
        private RectTransform _enemyActorRect;
        private Vector2 _enemyActorBasePosition;
        private Coroutine _enemyShakeCoroutine;
        private AudioSource _uiAudioSource;
        private AsyncOperationHandle<AudioClip> _cursorSeHandle;
        private bool _cursorSeHandleInitialized;
        private bool _cursorSeLoadRequested;
        private AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> _cursorSeLocationHandle;
        private bool _cursorSeLocationHandleInitialized;
        private AsyncOperationHandle<AudioClip> _damageSeHandle;
        private bool _damageSeHandleInitialized;
        private bool _damageSeLoadRequested;
        private AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> _damageSeLocationHandle;
        private bool _damageSeLocationHandleInitialized;
        private int _pendingDamageSePlayCount;
        private float _lastCursorSePlayTime = -10f;
        private float _playerHpTargetFill = 1f;
        private float _playerSpTargetFill = 1f;
        private float _enemyHpTargetFill = 1f;
        private float _enemyShieldTargetFill = 1f;

        public void Build()
        {
            _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
            {
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            var canvasObject = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _uiAudioSource = canvasObject.AddComponent<AudioSource>();
            _uiAudioSource.playOnAwake = false;
            _uiAudioSource.loop = false;
            _uiAudioSource.spatialBlend = 0f;

            var background = CreateImage("Background", canvasObject.transform, BackgroundColor);
            StretchFullScreen(background.rectTransform);

            var battleContent = new GameObject("BattleContent", typeof(RectTransform));
            battleContent.transform.SetParent(canvasObject.transform, false);
            _battleContentRoot = battleContent.GetComponent<RectTransform>();
            StretchFullScreen(_battleContentRoot);

            CreateArena(_battleContentRoot);

            var enemyPanel = CreateStatusPanel("EnemyPanel", _battleContentRoot, new Vector2(TopPanelMargin, -TopPanelMargin), EnemyPanelSize, new Vector2(0f, 1f), "EnemyName", out _enemyNameText);
            _enemyWeakText = CreateText("EnemyWeakText", enemyPanel.transform, 20, TextAnchor.MiddleRight, new Color(0.98f, 0.88f, 0.62f, 1f));
            _enemyWeakText.fontStyle = FontStyles.Bold;
            ConfigureRect(_enemyWeakText.rectTransform, new Vector2(-PanelPadding, -PanelPadding - 2f), new Vector2(180f, 28f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            _enemyHpFill = CreateStatusBarSection(enemyPanel.transform, "HpSection", new Vector2(PanelPadding, -(PanelPadding + StatusSectionTopOffset)), StatusBarSize, EnemyAccent, out _enemyHpText);
            _enemyShieldFill = CreateStatusBarSection(enemyPanel.transform, "ShieldSection", new Vector2(PanelPadding, -(PanelPadding + StatusSectionTopOffset + StatusBarSize.y + StatusSectionSpacing)), StatusBarSize, ShieldAccent, out _enemyShieldText);

            var playerPanel = CreateStatusPanel("PlayerPanel", _battleContentRoot, new Vector2(-TopPanelMargin, -TopPanelMargin), PlayerPanelSize, new Vector2(1f, 1f), "PlayerName", out _playerNameText);
            _playerHpFill = CreateStatusBarSection(playerPanel.transform, "HpSection", new Vector2(PanelPadding, -(PanelPadding + StatusSectionTopOffset)), StatusBarSize, PlayerAccent, out _playerHpText);
            _playerSpFill = CreateStatusBarSection(playerPanel.transform, "SpSection", new Vector2(PanelPadding, -(PanelPadding + StatusSectionTopOffset + StatusBarSize.y + StatusSectionSpacing)), StatusBarSize, SpAccent, out _playerSpText);

            _resultText = CreateText("ResultText", _battleContentRoot, 54, TextAnchor.MiddleCenter, TextColor);
            ConfigureRect(_resultText.rectTransform, new Vector2(0f, 80f), new Vector2(960f, 80f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _resultText.gameObject.SetActive(false);

            CreateBattleLog(_battleContentRoot);
            CreateCardHand(_battleContentRoot);
            CreateConfirmButton(_battleContentRoot);
            CreateDeckEditor(canvasObject.transform);
        }

        public void Bind(BattleController controller)
        {
            for (var i = 0; i < _cardButtons.Count; i++)
            {
                var cardIndex = i;
                _cardButtons[i].onClick.RemoveAllListeners();
                _cardButtons[i].onClick.AddListener(() => controller.PerformPlayerCard(cardIndex));
            }

            if (_confirmButton != null)
            {
                _confirmButton.onClick.RemoveAllListeners();
                _confirmButton.onClick.AddListener(controller.ConfirmSelectedCards);
            }

            for (var i = 0; i < _deckCardButtons.Count; i++)
            {
                var deckCardIndex = i;
                _deckCardButtons[i].onClick.RemoveAllListeners();
                _deckCardButtons[i].onClick.AddListener(() => controller.ToggleDeckCard(_deckCardIds[deckCardIndex]));
            }

            if (_deckEditorStartButton != null)
            {
                _deckEditorStartButton.onClick.RemoveAllListeners();
                _deckEditorStartButton.onClick.AddListener(controller.ConfirmDeckSelection);
            }
        }

        private void Update()
        {
            AnimateBarFill(_playerHpFill, _playerHpTargetFill);
            AnimateBarFill(_playerSpFill, _playerSpTargetFill);
            AnimateBarFill(_enemyHpFill, _enemyHpTargetFill);
            AnimateBarFill(_enemyShieldFill, _enemyShieldTargetFill);
        }

        public void RefreshUnits(BattleUnit player, BattleUnit enemy)
        {
            UpdateUnitDisplay(player, _playerNameText, _playerHpText, _playerHpFill);
            UpdateUnitDisplay(enemy, _enemyNameText, _enemyHpText, _enemyHpFill);
            if (_enemyWeakText != null)
            {
                _enemyWeakText.text = $"Weak: {GetWeakElementLabel(enemy.PrimaryWeakElement, enemy.SecondaryWeakElement)}";
            }

            if (_enemyShieldText != null)
            {
                _enemyShieldText.text = enemy.IsBroken ? "BREAK" : $"{enemy.ShieldCount} / {enemy.MaxShieldCount}";
            }

            if (_enemyShieldFill != null)
            {
                _enemyShieldTargetFill = enemy.MaxShieldCount > 0 ? enemy.ShieldCount / (float)enemy.MaxShieldCount : 0f;
            }
        }

        public void RefreshPlayerSp(int currentSp, int maxSp)
        {
            _playerSpText.text = $"SP {currentSp} / {maxSp}";
            _playerSpTargetFill = maxSp > 0 ? currentSp / (float)maxSp : 0f;
        }

        public void SetTurnText(string message)
        {
            if (_turnText != null)
            {
                _turnText.text = message;
            }
        }

        public void SetTurnCount(int turnCount)
        {
            if (_turnCountText != null)
            {
                _turnCountText.text = $"TURN {Mathf.Max(1, turnCount)}";
            }
        }

        public void SetResultText(string message, bool visible)
        {
            _resultText.text = message;
            _resultText.gameObject.SetActive(visible);
        }

        public void ClearBattleLog()
        {
            _battleLogs.Clear();
            if (_logText != null)
            {
                _logText.text = string.Empty;
            }
        }

        public void AddBattleLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            const int maxLogEntries = 6;
            _battleLogs.Enqueue(message);
            while (_battleLogs.Count > maxLogEntries)
            {
                _battleLogs.Dequeue();
            }

            if (_logText == null)
            {
                return;
            }

            _logBuilder.Clear();
            foreach (var entry in _battleLogs)
            {
                if (_logBuilder.Length > 0)
                {
                    _logBuilder.Append('\n');
                }

                _logBuilder.Append("・");
                _logBuilder.Append(entry);
            }

            _logText.text = _logBuilder.ToString();
        }

        public void RefreshHand(IReadOnlyList<BattleCardData> hand)
        {
            for (var i = 0; i < _cardButtons.Count; i++)
            {
                var hasCard = hand != null && i < hand.Count;
                _cardButtons[i].gameObject.SetActive(hasCard);

                if (!hasCard)
                {
                    continue;
                }

                _cardTitleTexts[i].text = hand[i].cardName;
                _cardDescriptionTexts[i].text = hand[i].description;
                _cardCostTexts[i].text = $"[{hand[i].spCost}]{GetElementLabel(hand[i].elementType)}";
                _cardElementTexts[i].text = string.Empty;
            }
        }

        public void SetCardsInteractable(bool interactable, IReadOnlyList<BattleCardData> hand, int currentSp, IReadOnlyCollection<int> selectedIndices)
        {
            for (var i = 0; i < _cardButtons.Count; i++)
            {
                var isSelected = IsCardSelected(selectedIndices, i);
                var canAfford = hand != null && i < hand.Count && currentSp >= hand[i].spCost;
                _cardButtons[i].interactable = interactable && _cardButtons[i].gameObject.activeSelf && (canAfford || isSelected);
                _cardBackgrounds[i].color = _cardButtons[i].interactable ? GetCardBaseColor(hand, i) : CardDisabledColor;
                _cardTitleTexts[i].color = _cardButtons[i].interactable ? DarkTextColor : new Color(0.78f, 0.78f, 0.78f, 1f);
                _cardDescriptionTexts[i].color = _cardButtons[i].interactable ? DarkTextColor : new Color(0.72f, 0.72f, 0.72f, 1f);
                _cardCostTexts[i].color = _cardButtons[i].interactable ? DarkTextColor : new Color(0.82f, 0.82f, 0.82f, 1f);
                _cardElementTexts[i].color = _cardButtons[i].interactable ? DarkTextColor : new Color(0.82f, 0.82f, 0.82f, 1f);
            }
        }

        public void SetSelectedCards(IReadOnlyCollection<int> selectedIndices, IReadOnlyList<BattleCardData> hand, int currentSp)
        {
            for (var i = 0; i < _cardButtons.Count; i++)
            {
                var isSelected = IsCardSelected(selectedIndices, i);
                var isActive = _cardButtons[i].gameObject.activeSelf;
                var canAfford = hand != null && i < hand.Count && currentSp >= hand[i].spCost;

                _cardRects[i].anchoredPosition = _cardAnchoredPositions[i] + new Vector2(0f, isSelected ? SelectedCardHop : 0f);
                _cardBackgrounds[i].color = _cardButtons[i].interactable ? GetCardBaseColor(hand, i) : CardDisabledColor;
                var parallaxEffect = _cardButtons[i].GetComponent<CardParallaxEffect>();
                if (parallaxEffect != null)
                {
                    parallaxEffect.SetSelected(isSelected);
                }

                var titleColor = isSelected || _cardButtons[i].interactable ? DarkTextColor : new Color(0.78f, 0.78f, 0.78f, 1f);
                var bodyColor = isSelected || _cardButtons[i].interactable ? DarkTextColor : new Color(0.72f, 0.72f, 0.72f, 1f);
                var costColor = isSelected || _cardButtons[i].interactable || (isActive && canAfford) ? DarkTextColor : new Color(0.82f, 0.82f, 0.82f, 1f);
                var elementColor = isSelected || _cardButtons[i].interactable ? DarkTextColor : new Color(0.82f, 0.82f, 0.82f, 1f);

                _cardTitleTexts[i].color = titleColor;
                _cardDescriptionTexts[i].color = bodyColor;
                _cardCostTexts[i].color = costColor;
                _cardElementTexts[i].color = elementColor;
            }
        }

        public void SetConfirmButtonState(bool enabled, int selectedSpCost)
        {
            if (_confirmButton == null || _confirmButtonText == null)
            {
                return;
            }

            _confirmButton.interactable = enabled;
            _confirmButton.gameObject.SetActive(true);
            _confirmButtonText.text = enabled ? $"確定 ({selectedSpCost} SP)" : "確定";
        }

        public void SetBattleScreenVisible(bool visible)
        {
            if (_battleContentRoot != null)
            {
                _battleContentRoot.gameObject.SetActive(visible);
            }
        }

        public void ShowDeckEditor(IReadOnlyList<BattleCardData> cards, IReadOnlyCollection<string> selectedCardIds, int requiredDeckSize)
        {
            if (_deckEditorPanel == null)
            {
                return;
            }

            _deckEditorPanel.SetActive(true);
            RefreshDeckEditor(cards);
            SetDeckEditorHint($"あと {Mathf.Max(0, requiredDeckSize - (selectedCardIds?.Count ?? 0))} 枚必要ぬめ");
            SetDeckEditorSelection(selectedCardIds, requiredDeckSize);
        }

        public void HideDeckEditor()
        {
            if (_deckEditorPanel != null)
            {
                _deckEditorPanel.SetActive(false);
            }
        }

        public void SetDeckEditorSelection(IReadOnlyCollection<string> selectedCardIds, int requiredDeckSize)
        {
            var selectedCount = 0;
            for (var i = 0; i < _deckCardButtons.Count; i++)
            {
                var isSelected = ContainsCardId(selectedCardIds, _deckCardIds[i]);
                if (isSelected)
                {
                    selectedCount++;
                }

                _deckCardOutlines[i].enabled = isSelected;
                _deckCardBackgrounds[i].transform.localScale = isSelected ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
            }

            if (_deckEditorCountText != null)
            {
                _deckEditorCountText.text = $"DECK {selectedCount} / {requiredDeckSize}";
            }

            if (_deckEditorStartButton != null)
            {
                var canStart = selectedCount == requiredDeckSize;
                _deckEditorStartButton.interactable = canStart;
                _deckEditorStartButtonText.text = canStart ? "戦闘開始" : $"戦闘開始 ({selectedCount}/{requiredDeckSize})";
            }
        }

        public void SetDeckEditorHint(string message)
        {
            if (_deckEditorHintText != null)
            {
                _deckEditorHintText.text = message;
            }
        }

        public void PlayEnemyDamageEffect(int damage, ElementType elementType)
        {
            if (_enemyActorRect == null || damage <= 0)
            {
                return;
            }

            PlayDamageSe();

            if (_enemyShakeCoroutine != null)
            {
                StopCoroutine(_enemyShakeCoroutine);
                _enemyActorRect.anchoredPosition = _enemyActorBasePosition;
            }

            _enemyShakeCoroutine = StartCoroutine(ShakeRect(_enemyActorRect, _enemyActorBasePosition, 0.24f, 20f));
            StartCoroutine(AnimateDamagePopup(_enemyActorRect, damage, elementType));
        }

        private static bool IsCardSelected(IReadOnlyCollection<int> selectedIndices, int index)
        {
            if (selectedIndices == null)
            {
                return false;
            }

            foreach (var selectedIndex in selectedIndices)
            {
                if (selectedIndex == index)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCardId(IReadOnlyCollection<string> selectedCardIds, string cardId)
        {
            if (selectedCardIds == null || string.IsNullOrWhiteSpace(cardId))
            {
                return false;
            }

            foreach (var selectedCardId in selectedCardIds)
            {
                if (selectedCardId == cardId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetElementLabel(ElementType elementType)
        {
            return elementType switch
            {
                ElementType.Fire => "[炎]",
                ElementType.Ice => "[氷]",
                ElementType.Wind => "[風]",
                ElementType.Light => "[光]",
                ElementType.Dark => "[闇]",
                _ => "[無]"
            };
        }

        private static string GetWeakElementLabel(ElementType primaryElement, ElementType secondaryElement)
        {
            var primaryLabel = GetElementLabel(primaryElement);
            if (secondaryElement == ElementType.None || secondaryElement == primaryElement)
            {
                return primaryLabel;
            }

            return $"{primaryLabel}{GetElementLabel(secondaryElement)}";
        }

        private static Color GetElementColor(ElementType elementType)
        {
            return elementType switch
            {
                ElementType.Fire => new Color(0.95f, 0.74f, 0.7f, 1f),
                ElementType.Ice => new Color(0.72f, 0.9f, 1f, 1f),
                ElementType.Wind => new Color(0.74f, 0.92f, 0.76f, 1f),
                ElementType.Light => new Color(1f, 0.95f, 0.74f, 1f),
                ElementType.Dark => new Color(0.72f, 0.62f, 0.88f, 1f),
                _ => CardColor
            };
        }

        private static Color GetSelectedElementColor(ElementType elementType)
        {
            return elementType switch
            {
                ElementType.Fire => new Color(0.99f, 0.64f, 0.56f, 1f),
                ElementType.Ice => new Color(0.58f, 0.86f, 1f, 1f),
                ElementType.Wind => new Color(0.62f, 0.88f, 0.66f, 1f),
                ElementType.Light => new Color(1f, 0.9f, 0.6f, 1f),
                ElementType.Dark => new Color(0.62f, 0.5f, 0.86f, 1f),
                _ => CardSelectedColor
            };
        }

        private static Color GetCardBaseColor(IReadOnlyList<BattleCardData> hand, int index)
        {
            if (hand == null || index < 0 || index >= hand.Count)
            {
                return CardColor;
            }

            return GetElementColor(hand[index].elementType);
        }

        private static Color GetSelectedCardColor(IReadOnlyList<BattleCardData> hand, int index)
        {
            if (hand == null || index < 0 || index >= hand.Count)
            {
                return CardSelectedColor;
            }

            return GetSelectedElementColor(hand[index].elementType);
        }

        private void UpdateUnitDisplay(BattleUnit unit, TextMeshProUGUI nameText, TextMeshProUGUI hpText, Image hpFill)
        {
            nameText.text = unit.DisplayName;
            hpText.text = unit.CurrentBarrier > 0
                ? $"HP {unit.CurrentHp} / {unit.MaxHp}  G {unit.CurrentBarrier}"
                : $"HP {unit.CurrentHp} / {unit.MaxHp}";
            var targetFill = unit.CurrentHp / (float)unit.MaxHp;
            if (hpFill == _playerHpFill)
            {
                _playerHpTargetFill = targetFill;
                return;
            }

            if (hpFill == _enemyHpFill)
            {
                _enemyHpTargetFill = targetFill;
            }
        }

        private Image CreateStatusBarSection(Transform parent, string sectionName, Vector2 anchoredPosition, Vector2 size, Color fillColor, out TextMeshProUGUI valueText)
        {
            var root = new GameObject(sectionName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            ConfigureRect(rootRect, anchoredPosition, size, new Vector2(0f, 1f), new Vector2(0f, 1f));

            var barBackground = CreateRoundedImage("BarBackground", root.transform, new Color(0.1f, 0.12f, 0.16f, 1f));
            ConfigureRect(barBackground.rectTransform, new Vector2(0f, -12f), new Vector2(size.x, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            barBackground.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var fill = CreateSolidFillImage("Fill", barBackground.transform, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            StretchFullScreen(fill.rectTransform);

            valueText = CreateText("Value", barBackground.transform, 20, TextAnchor.MiddleRight, TextColor);
            valueText.fontStyle = FontStyles.Bold;
            ConfigureRect(valueText.rectTransform, new Vector2(-12f, 0f), new Vector2(size.x - 24f, 24f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));

            return fill;
        }

        private void CreateArena(Transform parent)
        {
            var arena = CreatePanel("Arena", parent, new Vector2(0f, 44f), ArenaSize, new Vector2(0.5f, 0.5f));
            arena.GetComponent<Image>().color = ArenaColor;

            _enemyActorRect = CreateActor(arena.transform, "EnemyActor", new Vector2(-420f, 30f), EnemyAccent, "ENEMY").GetComponent<RectTransform>();
            _enemyActorBasePosition = _enemyActorRect.anchoredPosition;
            CreateActor(arena.transform, "PlayerActor", new Vector2(420f, 30f), PlayerAccent, "PLAYER");

        }

        private void CreateBattleLog(Transform parent)
        {
            var logPanel = CreatePanel("BattleLogPanel", parent, new Vector2(0f, -TopPanelMargin), LogPanelSize, new Vector2(0.5f, 1f));
            logPanel.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.94f);

            var title = CreateText("BattleLogTitle", logPanel.transform, 24, TextAnchor.MiddleLeft, new Color(0.82f, 0.84f, 0.9f, 1f));
            title.text = "LOG";
            title.fontStyle = FontStyles.Bold;
            ConfigureRect(title.rectTransform, new Vector2(20f, -18f), new Vector2(100f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _turnCountText = CreateText("TurnCountText", logPanel.transform, 22, TextAnchor.MiddleRight, new Color(0.82f, 0.84f, 0.9f, 1f));
            _turnCountText.fontStyle = FontStyles.Bold;
            ConfigureRect(_turnCountText.rectTransform, new Vector2(-20f, -18f), new Vector2(160f, 28f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            _turnText = CreateText("TurnText", logPanel.transform, 18, TextAnchor.MiddleLeft, new Color(0.9f, 0.92f, 0.96f, 1f));
            _turnText.textWrappingMode = TextWrappingModes.Normal;
            _turnText.overflowMode = TextOverflowModes.Ellipsis;
            ConfigureRect(_turnText.rectTransform, new Vector2(20f, -48f), new Vector2(LogPanelSize.x - 200f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _logText = CreateText("BattleLogText", logPanel.transform, 20, TextAnchor.UpperLeft, TextColor);
            _logText.textWrappingMode = TextWrappingModes.Normal;
            _logText.overflowMode = TextOverflowModes.Truncate;
            ConfigureRect(_logText.rectTransform, new Vector2(20f, -78f), new Vector2(LogPanelSize.x - 40f, LogPanelSize.y - 98f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        }

        private void CreateCardHand(Transform parent)
        {
            var handRoot = new GameObject("CardHand", typeof(RectTransform));
            handRoot.transform.SetParent(parent, false);
            ConfigureRect(handRoot.GetComponent<RectTransform>(), new Vector2(0f, 30f), HandRootSize, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            const float cardWidth = 200f;
            var cardHeight = cardWidth * CardAspectRatio;
            const float spacing = 22f;
            var totalWidth = (cardWidth * 5f) + (spacing * 4f);
            var startX = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (var i = 0; i < 5; i++)
            {
                var x = startX + i * (cardWidth + spacing);
                var cardRoot = new GameObject($"CardRoot{i}", typeof(RectTransform));
                cardRoot.transform.SetParent(handRoot.transform, false);
                var cardRootRect = cardRoot.GetComponent<RectTransform>();
                ConfigureRect(cardRootRect, new Vector2(x, 0f), new Vector2(cardWidth, cardHeight), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

                var button = CreateCardButton(cardRoot.transform, Vector2.zero, new Vector2(cardWidth, cardHeight), true);
                var visual = CreateCardVisual(button.transform, new Vector2(cardWidth, cardHeight));
                _cardButtons.Add(button);
                _cardRects.Add(cardRootRect);
                _cardAnchoredPositions.Add(new Vector2(x, 0f));
                _cardBackgrounds.Add(visual);

                button.targetGraphic = visual;

                var title = CreateText("Title", visual.transform, 28, TextAnchor.UpperLeft, DarkTextColor);
                ConfigureRect(title.rectTransform, new Vector2(18f, -18f), new Vector2(cardWidth - 36f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                title.fontStyle = FontStyles.Bold;
                title.textWrappingMode = TextWrappingModes.Normal;
                title.overflowMode = TextOverflowModes.Truncate;
                _cardTitleTexts.Add(title);

                var elementText = CreateText("Element", visual.transform, 18, TextAnchor.MiddleRight, DarkTextColor);
                elementText.fontStyle = FontStyles.Bold;
                ConfigureRect(elementText.rectTransform, new Vector2(-18f, -18f), new Vector2(72f, 24f), new Vector2(1f, 1f), new Vector2(1f, 1f));
                _cardElementTexts.Add(elementText);

                var cost = CreateText("Cost", visual.transform, 20, TextAnchor.UpperLeft, DarkTextColor);
                ConfigureRect(cost.rectTransform, new Vector2(18f, -52f), new Vector2(cardWidth - 36f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                cost.fontStyle = FontStyles.Bold;
                _cardCostTexts.Add(cost);

                var description = CreateText("Description", visual.transform, 22, TextAnchor.UpperLeft, DarkTextColor);
                ConfigureRect(description.rectTransform, new Vector2(18f, -92f), new Vector2(cardWidth - 36f, 102f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                description.textWrappingMode = TextWrappingModes.Normal;
                description.overflowMode = TextOverflowModes.Truncate;
                _cardDescriptionTexts.Add(description);

                var parallaxEffect = button.GetComponent<CardParallaxEffect>();
                if (parallaxEffect != null)
                {
                    parallaxEffect.BindPointerEnterAction(PlayCursorHoverSe);
                    parallaxEffect.BindVisualTarget(visual.rectTransform);
                }
            }
        }

        private void CreateDeckEditor(Transform parent)
        {
            _deckEditorPanel = CreatePanel("DeckEditorPanel", parent, Vector2.zero, new Vector2(1820f, 980f), new Vector2(0.5f, 0.5f));
            ConfigureRect(_deckEditorPanel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1820f, 980f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _deckEditorPanel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.1f, 0.97f);

            var title = CreateText("DeckEditorTitle", _deckEditorPanel.transform, 42, TextAnchor.MiddleLeft, TextColor);
            title.text = "DECK EDIT";
            title.fontStyle = FontStyles.Bold;
            ConfigureRect(title.rectTransform, new Vector2(32f, -28f), new Vector2(280f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _deckEditorCountText = CreateText("DeckEditorCount", _deckEditorPanel.transform, 28, TextAnchor.MiddleRight, new Color(0.96f, 0.88f, 0.68f, 1f));
            _deckEditorCountText.fontStyle = FontStyles.Bold;
            ConfigureRect(_deckEditorCountText.rectTransform, new Vector2(-240f, -32f), new Vector2(280f, 36f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            _deckEditorHintText = CreateText("DeckEditorHint", _deckEditorPanel.transform, 24, TextAnchor.MiddleLeft, new Color(0.82f, 0.86f, 0.92f, 1f));
            ConfigureRect(_deckEditorHintText.rectTransform, new Vector2(32f, -78f), new Vector2(720f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _deckEditorStartButton = CreateCardButton(_deckEditorPanel.transform, Vector2.zero, new Vector2(220f, 68f));
            ConfigureRect(_deckEditorStartButton.GetComponent<RectTransform>(), new Vector2(-32f, 32f), new Vector2(220f, 68f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            _deckEditorStartButton.GetComponent<Image>().color = new Color(0.84f, 0.74f, 0.52f, 1f);
            _deckEditorStartButtonText = CreateText("DeckEditorStartLabel", _deckEditorStartButton.transform, 24, TextAnchor.MiddleCenter, DarkTextColor);
            _deckEditorStartButtonText.fontStyle = FontStyles.Bold;
            _deckEditorStartButtonText.text = "戦闘開始";
            ConfigureRect(_deckEditorStartButtonText.rectTransform, Vector2.zero, new Vector2(220f, 68f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var gridRoot = new GameObject("DeckGrid", typeof(RectTransform));
            gridRoot.transform.SetParent(_deckEditorPanel.transform, false);
            ConfigureRect(gridRoot.GetComponent<RectTransform>(), new Vector2(0f, -28f), new Vector2(1720f, 760f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            const int columns = 10;
            const float cardWidth = 156f;
            const float cardHeight = 116f;
            const float spacingX = 14f;
            const float spacingY = 14f;
            var totalWidth = columns * cardWidth + (columns - 1) * spacingX;
            var startX = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (var i = 0; i < 60; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var x = startX + column * (cardWidth + spacingX);
                var y = 300f - row * (cardHeight + spacingY);

                var button = CreateCardButton(gridRoot.transform, new Vector2(x, y), new Vector2(cardWidth, cardHeight));
                var visual = CreateCardVisual(button.transform, new Vector2(cardWidth, cardHeight));
                button.targetGraphic = visual;

                var outline = visual.GetComponent<Outline>();
                outline.effectColor = new Color(1f, 0.92f, 0.58f, 1f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.enabled = false;

                var titleText = CreateText("DeckTitle", visual.transform, 20, TextAnchor.UpperLeft, DarkTextColor);
                titleText.fontStyle = FontStyles.Bold;
                titleText.textWrappingMode = TextWrappingModes.Normal;
                titleText.overflowMode = TextOverflowModes.Truncate;
                ConfigureRect(titleText.rectTransform, new Vector2(10f, -8f), new Vector2(cardWidth - 20f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                var costText = CreateText("DeckCost", visual.transform, 16, TextAnchor.UpperLeft, DarkTextColor);
                costText.fontStyle = FontStyles.Bold;
                ConfigureRect(costText.rectTransform, new Vector2(10f, -34f), new Vector2(cardWidth - 20f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                var descriptionText = CreateText("DeckDescription", visual.transform, 14, TextAnchor.UpperLeft, DarkTextColor);
                descriptionText.textWrappingMode = TextWrappingModes.Normal;
                descriptionText.overflowMode = TextOverflowModes.Truncate;
                ConfigureRect(descriptionText.rectTransform, new Vector2(10f, -56f), new Vector2(cardWidth - 20f, 48f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                button.gameObject.SetActive(false);
                _deckCardButtons.Add(button);
                _deckCardIds.Add(string.Empty);
                _deckCardTitleTexts.Add(titleText);
                _deckCardCostTexts.Add(costText);
                _deckCardDescriptionTexts.Add(descriptionText);
                _deckCardBackgrounds.Add(visual);
                _deckCardOutlines.Add(outline);
            }
        }

        private void RefreshDeckEditor(IReadOnlyList<BattleCardData> cards)
        {
            for (var i = 0; i < _deckCardButtons.Count; i++)
            {
                var hasCard = cards != null && i < cards.Count;
                _deckCardButtons[i].gameObject.SetActive(hasCard);
                _deckCardIds[i] = hasCard ? cards[i].id : string.Empty;

                if (!hasCard)
                {
                    continue;
                }

                _deckCardTitleTexts[i].text = cards[i].cardName;
                _deckCardCostTexts[i].text = $"[{cards[i].spCost}]{GetElementLabel(cards[i].elementType)}";
                _deckCardDescriptionTexts[i].text = cards[i].description;
                _deckCardBackgrounds[i].color = GetElementColor(cards[i].elementType);
                _deckCardOutlines[i].enabled = false;
            }
        }

        private GameObject CreateActor(Transform parent, string actorName, Vector2 anchoredPosition, Color accentColor, string label)
        {
            var actorRoot = new GameObject(actorName, typeof(RectTransform));
            actorRoot.transform.SetParent(parent, false);
            ConfigureRect(actorRoot.GetComponent<RectTransform>(), anchoredPosition, new Vector2(220f, 320f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var body = CreateImage("Body", actorRoot.transform, accentColor);
            ConfigureRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(160f, 220f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var labelText = CreateText("ActorLabel", actorRoot.transform, 26, TextAnchor.MiddleCenter, TextColor);
            labelText.text = label;
            ConfigureRect(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(140f, 32f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            return actorRoot;
        }

        private GameObject CreatePanel(string panelName, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var panel = new GameObject(panelName, typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.color = PanelColor;
            image.sprite = GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
            ConfigureRect(image.rectTransform, anchoredPosition, size, anchor, anchor);
            return panel;
        }

        private GameObject CreateStatusPanel(string panelName, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, string nameTextName, out TextMeshProUGUI nameText)
        {
            var panel = CreatePanel(panelName, parent, anchoredPosition, size, anchor);
            nameText = CreateText(nameTextName, panel.transform, 34, TextAnchor.MiddleLeft, TextColor);
            nameText.fontStyle = FontStyles.Bold;
            ConfigureRect(nameText.rectTransform, new Vector2(PanelPadding, -PanelPadding), new Vector2(size.x - (PanelPadding * 2f), 32f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            return panel;
        }

        private void CreateConfirmButton(Transform parent)
        {
            _confirmButton = CreateCardButton(parent, new Vector2(-32f, 32f), ConfirmButtonSize);
            ConfigureRect(_confirmButton.GetComponent<RectTransform>(), new Vector2(-32f, 32f), ConfirmButtonSize, new Vector2(1f, 0f), new Vector2(1f, 0f));
            _confirmButton.gameObject.name = "ConfirmButton";
            _confirmButton.GetComponent<Image>().color = new Color(0.84f, 0.74f, 0.52f, 1f);

            _confirmButtonText = CreateText("ConfirmLabel", _confirmButton.transform, 24, TextAnchor.MiddleCenter, DarkTextColor);
            _confirmButtonText.fontStyle = FontStyles.Bold;
            _confirmButtonText.text = "確定";
            ConfigureRect(_confirmButtonText.rectTransform, Vector2.zero, ConfirmButtonSize, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private Button CreateCardButton(Transform parent, Vector2 anchoredPosition, Vector2 size, bool enableParallax = false)
        {
            var buttonObject = new GameObject("CardButton", typeof(Image), typeof(Button), typeof(Outline));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var outline = buttonObject.GetComponent<Outline>();
            outline.enabled = false;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = CardColor;
            colors.highlightedColor = colors.normalColor;
            colors.pressedColor = colors.normalColor;
            colors.disabledColor = CardDisabledColor;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;

            if (enableParallax)
            {
                buttonObject.AddComponent<CardParallaxEffect>();
            }

            ConfigureRect(image.rectTransform, anchoredPosition, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            return button;
        }

        private Image CreateCardVisual(Transform parent, Vector2 size)
        {
            var visualObject = new GameObject("CardVisual", typeof(Image), typeof(Outline));
            visualObject.transform.SetParent(parent, false);

            var image = visualObject.GetComponent<Image>();
            image.color = CardColor;
            image.sprite = GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            var outline = visualObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.26f, 0.19f, 0.1f, 0.95f);
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = true;

            ConfigureRect(image.rectTransform, Vector2.zero, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            return image;
        }

        private TextMeshProUGUI CreateText(string textName, Transform parent, int fontSize, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(textName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = ConvertAlignment(alignment);
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            return text;
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Midline,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.TopLeft
            };
        }

        private Image CreateImage(string imageName, Transform parent, Color color)
        {
            var imageObject = new GameObject(imageName, typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Image CreateRoundedImage(string imageName, Transform parent, Color color)
        {
            var image = CreateImage(imageName, parent, color);
            image.sprite = GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
            return image;
        }

        private Image CreateSolidFillImage(string imageName, Transform parent, Color color)
        {
            var image = CreateImage(imageName, parent, color);
            image.sprite = GetSolidFillSprite();
            image.type = Image.Type.Simple;
            return image;
        }

        private void PlayCursorHoverSe()
        {
            if (_uiAudioSource == null)
            {
                return;
            }

            if (Time.unscaledTime - _lastCursorSePlayTime < CursorSeCooldown)
            {
                return;
            }

            if (_cursorSeHandleInitialized && _cursorSeHandle.IsValid())
            {
                if (_cursorSeHandle.Status == AsyncOperationStatus.Succeeded && _cursorSeHandle.Result != null)
                {
                    _uiAudioSource.PlayOneShot(_cursorSeHandle.Result);
                    _lastCursorSePlayTime = Time.unscaledTime;
                }

                return;
            }

            _cursorSeHandleInitialized = false;

            if (_cursorSeLoadRequested)
            {
                return;
            }

            _cursorSeLoadRequested = true;
            _cursorSeLocationHandle = Addressables.LoadResourceLocationsAsync(CursorSeAddress, typeof(AudioClip));
            _cursorSeLocationHandle.Completed += handle =>
            {
                _cursorSeLocationHandleInitialized = true;
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null && handle.Result.Count > 0)
                {
                    LoadCursorSeFromKey(CursorSeAddress);
                    return;
                }

                LoadCursorSeFromKey(CursorSeGuid);
            };
        }

        private void PlayDamageSe()
        {
            if (_uiAudioSource == null)
            {
                return;
            }

            if (_damageSeHandleInitialized && _damageSeHandle.IsValid())
            {
                if (_damageSeHandle.Status == AsyncOperationStatus.Succeeded && _damageSeHandle.Result != null)
                {
                    PlayDamageSeClip(1);
                }

                return;
            }

            _damageSeHandleInitialized = false;
            _pendingDamageSePlayCount++;

            if (_damageSeLoadRequested)
            {
                return;
            }

            _damageSeLoadRequested = true;
            _damageSeLocationHandle = Addressables.LoadResourceLocationsAsync(DamageSeAddress, typeof(AudioClip));
            _damageSeLocationHandle.Completed += handle =>
            {
                _damageSeLocationHandleInitialized = true;
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null && handle.Result.Count > 0)
                {
                    LoadDamageSeFromKey(DamageSeAddress);
                    return;
                }

                LoadDamageSeFromKey(DamageSeGuid);
            };
        }

        private void LoadCursorSeFromKey(object key)
        {
            _cursorSeHandle = Addressables.LoadAssetAsync<AudioClip>(key);
            _cursorSeHandle.Completed += handle =>
            {
                _cursorSeHandleInitialized = true;
                _cursorSeLoadRequested = false;
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null && _uiAudioSource != null)
                {
                    _uiAudioSource.PlayOneShot(handle.Result);
                    _lastCursorSePlayTime = Time.unscaledTime;
                }
            };
        }

        private void LoadDamageSeFromKey(object key)
        {
            _damageSeHandle = Addressables.LoadAssetAsync<AudioClip>(key);
            _damageSeHandle.Completed += handle =>
            {
                _damageSeHandleInitialized = true;
                _damageSeLoadRequested = false;
                if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null && _uiAudioSource != null)
                {
                    PlayDamageSeClip(_pendingDamageSePlayCount);
                }

                _pendingDamageSePlayCount = 0;
            };
        }

        private void PlayDamageSeClip(int playCount)
        {
            if (_uiAudioSource == null || !_damageSeHandle.IsValid() || _damageSeHandle.Result == null)
            {
                return;
            }

            for (var i = 0; i < Mathf.Max(1, playCount); i++)
            {
                _uiAudioSource.PlayOneShot(_damageSeHandle.Result);
            }
        }

        private void OnDestroy()
        {
            if (_cursorSeHandleInitialized && _cursorSeHandle.IsValid())
            {
                Addressables.Release(_cursorSeHandle);
            }

            if (_cursorSeLocationHandleInitialized && _cursorSeLocationHandle.IsValid())
            {
                Addressables.Release(_cursorSeLocationHandle);
            }

            if (_damageSeHandleInitialized && _damageSeHandle.IsValid())
            {
                Addressables.Release(_damageSeHandle);
            }

            if (_damageSeLocationHandleInitialized && _damageSeLocationHandle.IsValid())
            {
                Addressables.Release(_damageSeLocationHandle);
            }

            _cursorSeHandleInitialized = false;
            _cursorSeLocationHandleInitialized = false;
            _damageSeHandleInitialized = false;
            _damageSeLocationHandleInitialized = false;
            _pendingDamageSePlayCount = 0;
        }

        private static void AnimateBarFill(Image fillImage, float targetFill)
        {
            if (fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, Mathf.Clamp01(targetFill), Time.unscaledDeltaTime * BarFillAnimationSpeed);
        }

        private IEnumerator ShakeRect(RectTransform rectTransform, Vector2 basePosition, float duration, float magnitude)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var strength = 1f - Mathf.Clamp01(elapsed / duration);
                var offset = Random.insideUnitCircle * magnitude * strength;
                rectTransform.anchoredPosition = basePosition + offset;
                yield return null;
            }

            rectTransform.anchoredPosition = basePosition;
            _enemyShakeCoroutine = null;
        }

        private IEnumerator AnimateDamagePopup(RectTransform targetRect, int damage, ElementType elementType)
        {
            var popupColor = GetDamagePopupColor(elementType);
            var popup = CreateText("DamagePopup", targetRect, 42, TextAnchor.MiddleCenter, popupColor);
            popup.text = damage.ToString();
            popup.fontStyle = FontStyles.Bold;
            popup.outlineWidth = 0.18f;
            popup.outlineColor = new Color(0.25f, 0.08f, 0.02f, 1f);

            var popupRect = popup.rectTransform;
            ConfigureRect(popupRect, new Vector2(0f, 88f), new Vector2(180f, 56f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var startPosition = popupRect.anchoredPosition;
            var endPosition = startPosition + new Vector2(0f, 84f);
            var startScale = new Vector3(0.72f, 0.72f, 1f);
            var peakScale = new Vector3(1.18f, 1.18f, 1f);
            const float duration = 0.6f;
            var elapsed = 0f;
            var color = popupColor;
            var outlineColor = popup.outlineColor;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                popupRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
                popupRect.localScale = Vector3.Lerp(t < 0.2f ? startScale : peakScale, Vector3.one, Mathf.InverseLerp(0.2f, 1f, t));

                color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, t));
                outlineColor.a = (byte)Mathf.RoundToInt(color.a * byte.MaxValue);
                popup.color = color;
                popup.outlineColor = outlineColor;
                yield return null;
            }

            Destroy(popup.gameObject);
        }

        private static Color GetDamagePopupColor(ElementType elementType)
        {
            return elementType == ElementType.None ? DamagePopupColor : GetSelectedElementColor(elementType);
        }

        private Sprite GetRoundedPanelSprite()
        {
            if (_roundedPanelSprite != null)
            {
                return _roundedPanelSprite;
            }

            const int textureSize = 32;
            const int radius = 6;
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "RoundedPanelTexture"
            };

            var clear = new Color32(255, 255, 255, 0);
            var fill = new Color32(255, 255, 255, 255);
            var center = new Vector2(textureSize * 0.5f, textureSize * 0.5f);
            var innerHalf = (textureSize * 0.5f) - radius;

            for (var y = 0; y < textureSize; y++)
            {
                for (var x = 0; x < textureSize; x++)
                {
                    var local = new Vector2(Mathf.Abs((x + 0.5f) - center.x), Mathf.Abs((y + 0.5f) - center.y));
                    var cornerDelta = new Vector2(Mathf.Max(0f, local.x - innerHalf), Mathf.Max(0f, local.y - innerHalf));
                    var inside = cornerDelta.sqrMagnitude <= radius * radius;
                    texture.SetPixel(x, y, inside ? fill : clear);
                }
            }

            texture.Apply();
            _roundedPanelSprite = Sprite.Create(texture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _roundedPanelSprite.name = "RoundedPanelSprite";
            return _roundedPanelSprite;
        }

        private Sprite GetSolidFillSprite()
        {
            if (_solidFillSprite != null)
            {
                return _solidFillSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "SolidFillTexture"
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _solidFillSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _solidFillSprite.name = "SolidFillSprite";
            return _solidFillSprite;
        }

        private static void StretchFullScreen(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void ConfigureRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.localScale = Vector3.one;
        }
    }
}
