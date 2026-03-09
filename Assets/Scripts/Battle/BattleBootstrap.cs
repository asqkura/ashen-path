using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AshenPath.Battle
{
    public class BattleBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureEventSystem();
            BuildBattleRuntime();
            ConfigureCamera();
        }

        private void BuildBattleRuntime()
        {
            if (FindFirstObjectByType<BattleController>() != null)
            {
                return;
            }

            var runtimeRoot = new GameObject("BattleRuntime");
            var battleUi = runtimeRoot.AddComponent<BattleUI>();
            battleUi.Build();

            var battleController = runtimeRoot.AddComponent<BattleController>();
            battleController.Initialize(battleUi);
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
