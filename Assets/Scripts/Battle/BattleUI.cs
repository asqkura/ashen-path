using UnityEngine;
using UnityEngine.UI;

namespace AshenPath.Battle
{
    public class BattleUI : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new(0.08f, 0.09f, 0.12f, 1f);
        private static readonly Color PanelColor = new(0.14f, 0.16f, 0.2f, 0.92f);
        private static readonly Color EnemyAccent = new(0.76f, 0.32f, 0.32f, 1f);
        private static readonly Color PlayerAccent = new(0.32f, 0.72f, 0.58f, 1f);
        private static readonly Color TextColor = new(0.95f, 0.96f, 0.98f, 1f);

        private Font _font;
        private Button _attackButton;
        private Text _attackButtonText;
        private Text _turnText;
        private Text _resultText;
        private Text _playerNameText;
        private Text _playerHpText;
        private Image _playerHpFill;
        private Text _enemyNameText;
        private Text _enemyHpText;
        private Image _enemyHpFill;

        public void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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

            var enemyPanel = CreatePanel("EnemyPanel", canvasObject.transform, new Vector2(0f, -64f), new Vector2(760f, 220f), new Vector2(0.5f, 1f));
            _enemyNameText = CreateText("EnemyName", enemyPanel.transform, 34, TextAnchor.MiddleLeft, TextColor);
            ConfigureRect(_enemyNameText.rectTransform, new Vector2(40f, -36f), new Vector2(680f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _enemyHpFill = CreateHpSection(enemyPanel.transform, new Vector2(40f, -110f), EnemyAccent, out _enemyHpText);

            var playerPanel = CreatePanel("PlayerPanel", canvasObject.transform, new Vector2(0f, 64f), new Vector2(760f, 220f), new Vector2(0.5f, 0f));
            _playerNameText = CreateText("PlayerName", playerPanel.transform, 34, TextAnchor.MiddleLeft, TextColor);
            ConfigureRect(_playerNameText.rectTransform, new Vector2(40f, -36f), new Vector2(680f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _playerHpFill = CreateHpSection(playerPanel.transform, new Vector2(40f, -110f), PlayerAccent, out _playerHpText);

            _turnText = CreateText("TurnText", canvasObject.transform, 30, TextAnchor.MiddleCenter, TextColor);
            ConfigureRect(_turnText.rectTransform, new Vector2(0f, -20f), new Vector2(960f, 60f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            _resultText = CreateText("ResultText", canvasObject.transform, 54, TextAnchor.MiddleCenter, TextColor);
            ConfigureRect(_resultText.rectTransform, new Vector2(0f, -100f), new Vector2(960f, 80f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _resultText.gameObject.SetActive(false);

            _attackButton = CreateButton(canvasObject.transform, new Vector2(0f, 320f), new Vector2(320f, 90f), new Vector2(0.5f, 0.5f));
            _attackButtonText = _attackButton.GetComponentInChildren<Text>();
            _attackButtonText.text = "攻撃";
        }

        public void Bind(BattleController controller)
        {
            _attackButton.onClick.RemoveAllListeners();
            _attackButton.onClick.AddListener(controller.PerformPlayerAttack);
        }

        public void RefreshUnits(BattleUnit player, BattleUnit enemy)
        {
            UpdateUnitDisplay(player, _playerNameText, _playerHpText, _playerHpFill);
            UpdateUnitDisplay(enemy, _enemyNameText, _enemyHpText, _enemyHpFill);
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

        public void SetAttackButtonInteractable(bool interactable)
        {
            _attackButton.interactable = interactable;
            _attackButtonText.color = interactable ? TextColor : new Color(0.6f, 0.64f, 0.68f, 1f);
        }

        private void UpdateUnitDisplay(BattleUnit unit, Text nameText, Text hpText, Image hpFill)
        {
            nameText.text = unit.DisplayName;
            hpText.text = $"HP {unit.CurrentHp} / {unit.MaxHp}";
            hpFill.fillAmount = unit.CurrentHp / (float)unit.MaxHp;
        }

        private Image CreateHpSection(Transform parent, Vector2 anchoredPosition, Color fillColor, out Text hpText)
        {
            var root = new GameObject("HpSection", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            ConfigureRect(rootRect, anchoredPosition, new Vector2(680f, 72f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var barBackground = CreateImage("HpBarBackground", root.transform, new Color(0.12f, 0.13f, 0.16f, 1f));
            ConfigureRect(barBackground.rectTransform, new Vector2(0f, -8f), new Vector2(680f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            var fill = CreateImage("HpFill", barBackground.transform, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            StretchFullScreen(fill.rectTransform);

            hpText = CreateText("HpText", root.transform, 24, TextAnchor.MiddleLeft, TextColor);
            ConfigureRect(hpText.rectTransform, new Vector2(0f, -46f), new Vector2(420f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            return fill;
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

        private Button CreateButton(Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
        {
            var buttonObject = new GameObject("AttackButton", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = PlayerAccent;

            var button = buttonObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = PlayerAccent;
            colors.highlightedColor = new Color(0.4f, 0.8f, 0.66f, 1f);
            colors.pressedColor = new Color(0.24f, 0.58f, 0.46f, 1f);
            colors.disabledColor = new Color(0.24f, 0.28f, 0.32f, 0.9f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            ConfigureRect(image.rectTransform, anchoredPosition, size, anchor, anchor);

            var label = CreateText("Label", buttonObject.transform, 30, TextAnchor.MiddleCenter, TextColor);
            StretchFullScreen(label.rectTransform);

            return button;
        }

        private Text CreateText(string textName, Transform parent, int fontSize, TextAnchor alignment, Color color)
        {
            var textObject = new GameObject(textName, typeof(Text));
            textObject.transform.SetParent(parent, false);

            var text = textObject.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
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
