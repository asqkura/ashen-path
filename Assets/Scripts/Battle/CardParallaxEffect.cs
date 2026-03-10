using UnityEngine;
using UnityEngine.EventSystems;

namespace AshenPath.Battle
{
    public sealed class CardParallaxEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private const float MaxTiltDegrees = 16f;
        private const float RotationLerpSpeed = 18f;

        private RectTransform _rectTransform;
        private Quaternion _targetRotation = Quaternion.identity;
        private bool _isPointerInside;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            transform.localRotation = Quaternion.Lerp(transform.localRotation, _targetRotation, Time.unscaledDeltaTime * RotationLerpSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            UpdateRotation(eventData);
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
        }

        private void OnDisable()
        {
            _isPointerInside = false;
            _targetRotation = Quaternion.identity;
            transform.localRotation = Quaternion.identity;
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
