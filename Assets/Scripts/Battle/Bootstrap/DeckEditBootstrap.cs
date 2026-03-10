using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AshenPath.Battle
{
    public class DeckEditBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureEventSystem();
            BuildDeckEditRuntime();
            ConfigureCamera();
        }

        private void BuildDeckEditRuntime()
        {
            if (FindFirstObjectByType<BattleController>() != null)
            {
                return;
            }

            var runtimeRoot = new GameObject("DeckEditRuntime");
            var battleUi = runtimeRoot.AddComponent<BattleUI>();
            battleUi.Build();

            var battleController = runtimeRoot.AddComponent<BattleController>();
            battleController.InitializeDeckEditor(battleUi);
        }

        private void EnsureEventSystem()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                var inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
                return;
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                var inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }
        }

        private void ConfigureCamera()
        {
            var targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                return;
            }

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 1f);
        }
    }
}
