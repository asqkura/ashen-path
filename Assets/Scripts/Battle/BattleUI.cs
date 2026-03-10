using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace AshenPath.Battle
{
    public class BattleUI : MonoBehaviour
    {
        private static readonly Vector2 EnemyPanelSize = new(440f, 136f);
        private static readonly Vector2 PlayerPanelSize = new(440f, 176f);
        private static readonly Vector2 HpSectionSize = new(384f, 64f);
        private static readonly Vector2 ArenaSize = new(1360f, 500f);
        private static readonly Vector2 HandRootSize = new(1520f, 220f);
        private static readonly Color BackgroundColor = new(0.08f, 0.09f, 0.12f, 1f);
        private static readonly Color PanelColor = new(0.14f, 0.16f, 0.2f, 0.92f);
        private static readonly Color EnemyAccent = new(0.76f, 0.32f, 0.32f, 1f);
        private static readonly Color PlayerAccent = new(0.32f, 0.72f, 0.58f, 1f);
        private static readonly Color SpAccent = new(0.36f, 0.64f, 0.96f, 1f);
        private static readonly Color TextColor = new(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color ArenaColor = new(0.12f, 0.13f, 0.16f, 0.92f);
        private static readonly Color CardColor = new(0.88f, 0.82f, 0.68f, 1f);
        private static readonly Color CardDisabledColor = new(0.35f, 0.35f, 0.35f, 0.95f);
        private static readonly Color DarkTextColor = new(0.17f, 0.13f, 0.09f, 1f);

        private TMP_FontAsset _font;
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
        private readonly List<Button> _cardButtons = new();
        private readonly List<TextMeshProUGUI> _cardTitleTexts = new();
        private readonly List<TextMeshProUGUI> _cardDescriptionTexts = new();
        private readonly List<TextMeshProUGUI> _cardCostTexts = new();
        private readonly List<Image> _cardBackgrounds = new();

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

            var background = CreateImage("Background", canvasObject.transform, BackgroundColor);
            StretchFullScreen(background.rectTransform);

            CreateArena(canvasObject.transform);

            var enemyPanel = CreatePanel("EnemyPanel", canvasObject.transform, new Vector2(-48f, -44f), EnemyPanelSize, new Vector2(1f, 1f));
            _enemyNameText = CreateText("EnemyName", enemyPanel.transform, 34, TextAnchor.MiddleLeft, TextColor);
            ConfigureRect(_enemyNameText.rectTransform, new Vector2(28f, -28f), new Vector2(384f, 36f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _enemyHpFill = CreateHpSection(enemyPanel.transform, new Vector2(28f, -88f), HpSectionSize, EnemyAccent, out _enemyHpText);

            var playerPanel = CreatePanel("PlayerPanel", canvasObject.transform, new Vector2(48f, 272f), PlayerPanelSize, new Vector2(0f, 0f));
            _playerNameText = CreateText("PlayerName", playerPanel.transform, 34, TextAnchor.MiddleLeft, TextColor);
            ConfigureRect(_playerNameText.rectTransform, new Vector2(28f, -28f), new Vector2(384f, 36f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _playerHpFill = CreateHpSection(playerPanel.transform, new Vector2(28f, -88f), HpSectionSize, PlayerAccent, out _playerHpText);
            _playerSpFill = CreateResourceSection(playerPanel.transform, "SpSection", new Vector2(28f, -130f), HpSectionSize, SpAccent, "SP", out _playerSpText);

            _turnText = CreateText("TurnText", canvasObject.transform, 30, TextAnchor.MiddleCenter, TextColor);
            ConfigureRect(_turnText.rectTransform, new Vector2(0f, 224f), new Vector2(1120f, 60f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            _resultText = CreateText("ResultText", canvasObject.transform, 54, TextAnchor.MiddleCenter, TextColor);
            ConfigureRect(_resultText.rectTransform, new Vector2(0f, 80f), new Vector2(960f, 80f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _resultText.gameObject.SetActive(false);

            CreateCardHand(canvasObject.transform);
        }

        public void Bind(BattleController controller)
        {
            for (var i = 0; i < _cardButtons.Count; i++)
            {
                var cardIndex = i;
                _cardButtons[i].onClick.RemoveAllListeners();
                _cardButtons[i].onClick.AddListener(() => controller.PerformPlayerCard(cardIndex));
            }
        }

        public void RefreshUnits(BattleUnit player, BattleUnit enemy)
        {
            UpdateUnitDisplay(player, _playerNameText, _playerHpText, _playerHpFill);
            UpdateUnitDisplay(enemy, _enemyNameText, _enemyHpText, _enemyHpFill);
        }

        public void RefreshPlayerSp(int currentSp, int maxSp)
        {
            _playerSpText.text = $"SP {currentSp} / {maxSp}";
            _playerSpFill.fillAmount = maxSp > 0 ? currentSp / (float)maxSp : 0f;
        }

        public void SetTurnText(string message)
        {
            _turnText.text = message;
        }

        public void SetResultText(string message, bool visible)
        {
            _resultText.text = message;
            _resultText.gameObject.SetActive(visible);
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
                _cardCostTexts[i].text = $"{hand[i].spCost} SP";
            }
        }

        public void SetCardsInteractable(bool interactable, IReadOnlyList<BattleCardData> hand, int currentSp)
        {
            for (var i = 0; i < _cardButtons.Count; i++)
            {
                var canAfford = hand != null && i < hand.Count && currentSp >= hand[i].spCost;
                _cardButtons[i].interactable = interactable && _cardButtons[i].gameObject.activeSelf && canAfford;
                _cardBackgrounds[i].color = _cardButtons[i].interactable ? CardColor : CardDisabledColor;
                _cardTitleTexts[i].color = _cardButtons[i].interactable ? DarkTextColor : new Color(0.78f, 0.78f, 0.78f, 1f);
                _cardDescriptionTexts[i].color = _cardButtons[i].interactable ? DarkTextColor : new Color(0.72f, 0.72f, 0.72f, 1f);
                _cardCostTexts[i].color = _cardButtons[i].interactable ? DarkTextColor : new Color(0.82f, 0.82f, 0.82f, 1f);
            }
        }

        private void UpdateUnitDisplay(BattleUnit unit, TextMeshProUGUI nameText, TextMeshProUGUI hpText, Image hpFill)
        {
            nameText.text = unit.DisplayName;
            hpText.text = $"HP {unit.CurrentHp} / {unit.MaxHp}";
            hpFill.fillAmount = unit.CurrentHp / (float)unit.MaxHp;
        }

        private Image CreateHpSection(Transform parent, Vector2 anchoredPosition, Vector2 size, Color fillColor, out TextMeshProUGUI hpText)
        {
            var root = new GameObject("HpSection", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            ConfigureRect(rootRect, anchoredPosition, size, new Vector2(0f, 1f), new Vector2(0f, 1f));

            var barBackground = CreateImage("HpBarBackground", root.transform, new Color(0.12f, 0.13f, 0.16f, 1f));
            ConfigureRect(barBackground.rectTransform, new Vector2(0f, -8f), new Vector2(size.x, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var fill = CreateImage("HpFill", barBackground.transform, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            StretchFullScreen(fill.rectTransform);

            hpText = CreateText("HpText", root.transform, 24, TextAnchor.MiddleLeft, TextColor);
            ConfigureRect(hpText.rectTransform, new Vector2(0f, -46f), new Vector2(size.x, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            return fill;
        }

        private Image CreateResourceSection(Transform parent, string sectionName, Vector2 anchoredPosition, Vector2 size, Color fillColor, string label, out TextMeshProUGUI valueText)
        {
            var root = new GameObject(sectionName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            ConfigureRect(root.GetComponent<RectTransform>(), anchoredPosition, size, new Vector2(0f, 1f), new Vector2(0f, 1f));

            var labelText = CreateText("Label", root.transform, 18, TextAnchor.MiddleLeft, new Color(0.76f, 0.82f, 0.94f, 1f));
            labelText.text = label;
            ConfigureRect(labelText.rectTransform, new Vector2(0f, -10f), new Vector2(52f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var barBackground = CreateImage("BarBackground", root.transform, new Color(0.1f, 0.12f, 0.16f, 1f));
            ConfigureRect(barBackground.rectTransform, new Vector2(60f, -8f), new Vector2(size.x - 60f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var fill = CreateImage("Fill", barBackground.transform, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            StretchFullScreen(fill.rectTransform);

            valueText = CreateText("Value", root.transform, 20, TextAnchor.MiddleLeft, TextColor);
            ConfigureRect(valueText.rectTransform, new Vector2(60f, -34f), new Vector2(size.x - 60f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            return fill;
        }

        private void CreateArena(Transform parent)
        {
            var arena = CreatePanel("Arena", parent, new Vector2(0f, 44f), ArenaSize, new Vector2(0.5f, 0.5f));
            arena.GetComponent<Image>().color = ArenaColor;

            CreateActor(arena.transform, "EnemyActor", new Vector2(420f, 30f), EnemyAccent, "ENEMY");
            CreateActor(arena.transform, "PlayerActor", new Vector2(-420f, 30f), PlayerAccent, "PLAYER");

            var divider = CreateImage("Divider", arena.transform, new Color(0.22f, 0.24f, 0.28f, 1f));
            ConfigureRect(divider.rectTransform, new Vector2(0f, 40f), new Vector2(2f, 320f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            var vsText = CreateText("VersusText", arena.transform, 40, TextAnchor.MiddleCenter, new Color(0.82f, 0.83f, 0.86f, 1f));
            vsText.text = "VS";
            ConfigureRect(vsText.rectTransform, new Vector2(0f, 0f), new Vector2(320f, 60f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private void CreateCardHand(Transform parent)
        {
            var handRoot = new GameObject("CardHand", typeof(RectTransform));
            handRoot.transform.SetParent(parent, false);
            ConfigureRect(handRoot.GetComponent<RectTransform>(), new Vector2(0f, 30f), HandRootSize, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));

            const float cardWidth = 260f;
            const float cardHeight = 180f;
            const float spacing = 18f;
            var totalWidth = (cardWidth * 5f) + (spacing * 4f);
            var startX = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (var i = 0; i < 5; i++)
            {
                var x = startX + i * (cardWidth + spacing);
                var button = CreateCardButton(handRoot.transform, new Vector2(x, 0f), new Vector2(cardWidth, cardHeight));
                _cardButtons.Add(button);
                _cardBackgrounds.Add(button.GetComponent<Image>());

                var title = CreateText("Title", button.transform, 28, TextAnchor.UpperLeft, DarkTextColor);
                ConfigureRect(title.rectTransform, new Vector2(18f, -18f), new Vector2(cardWidth - 36f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                title.fontStyle = FontStyles.Bold;
                title.textWrappingMode = TextWrappingModes.Normal;
                title.overflowMode = TextOverflowModes.Truncate;
                _cardTitleTexts.Add(title);

                var cost = CreateText("Cost", button.transform, 20, TextAnchor.UpperRight, DarkTextColor);
                ConfigureRect(cost.rectTransform, new Vector2(-18f, -18f), new Vector2(96f, 28f), new Vector2(1f, 1f), new Vector2(1f, 1f));
                cost.fontStyle = FontStyles.Bold;
                _cardCostTexts.Add(cost);

                var description = CreateText("Description", button.transform, 22, TextAnchor.UpperLeft, DarkTextColor);
                ConfigureRect(description.rectTransform, new Vector2(18f, -62f), new Vector2(cardWidth - 36f, 86f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                description.textWrappingMode = TextWrappingModes.Normal;
                description.overflowMode = TextOverflowModes.Truncate;
                _cardDescriptionTexts.Add(description);
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
            ConfigureRect(image.rectTransform, anchoredPosition, size, anchor, anchor);
            return panel;
        }

        private Button CreateCardButton(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            var buttonObject = new GameObject("CardButton", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = CardColor;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = CardColor;
            colors.highlightedColor = new Color(0.95f, 0.89f, 0.75f, 1f);
            colors.pressedColor = new Color(0.79f, 0.72f, 0.58f, 1f);
            colors.disabledColor = CardDisabledColor;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            ConfigureRect(image.rectTransform, anchoredPosition, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            return button;
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
