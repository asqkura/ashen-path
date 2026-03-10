using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AshenPath.Battle
{
    public sealed class CardParallaxEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private const float MaxTiltDegrees = 16f;
        private const float RotationLerpSpeed = 18f;
        private const float HoverLift = 20f;
        private const float HoverLiftLerpSpeed = 14f;

        private RectTransform _rectTransform;
        private Quaternion _targetRotation = Quaternion.identity;
        private bool _isPointerInside;
        private float _targetHoverOffsetY;
        private float _currentHoverOffsetY;
        private Action _pointerEnterAction;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            transform.localRotation = Quaternion.Lerp(transform.localRotation, _targetRotation, Time.unscaledDeltaTime * RotationLerpSpeed);
            _currentHoverOffsetY = Mathf.Lerp(_currentHoverOffsetY, _targetHoverOffsetY, Time.unscaledDeltaTime * HoverLiftLerpSpeed);
            transform.localPosition = new Vector3(0f, _currentHoverOffsetY, 0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            _targetHoverOffsetY = HoverLift;
            UpdateRotation(eventData);
            _pointerEnterAction?.Invoke();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!_isPointerInside)
            {
                return;
            }

            UpdateRotation(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            _targetRotation = Quaternion.identity;
            _targetHoverOffsetY = 0f;
        }

        private void OnDisable()
        {
            _isPointerInside = false;
            _targetRotation = Quaternion.identity;
            _targetHoverOffsetY = 0f;
            _currentHoverOffsetY = 0f;
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
        }

        public void BindPointerEnterAction(Action pointerEnterAction)
        {
            _pointerEnterAction = pointerEnterAction;
        }

        private void UpdateRotation(PointerEventData eventData)
        {
            if (_rectTransform == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                return;
            }

            var halfWidth = _rectTransform.rect.width * 0.5f;
            var halfHeight = _rectTransform.rect.height * 0.5f;
            if (halfWidth <= 0f || halfHeight <= 0f)
            {
                _targetRotation = Quaternion.identity;
                return;
            }

            var normalizedX = Mathf.Clamp(localPoint.x / halfWidth, -1f, 1f);
            var normalizedY = Mathf.Clamp(localPoint.y / halfHeight, -1f, 1f);
            var xRotation = -normalizedY * MaxTiltDegrees;
            var yRotation = normalizedX * MaxTiltDegrees;
            _targetRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        }
    }
}
