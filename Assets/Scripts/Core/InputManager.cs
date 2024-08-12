using UnityEngine;
using UnityEngine.InputSystem;

namespace Arknights.Core
{
    [AddComponentMenu("Arknights/Core/Input Manager")]
    public class InputManager : MonoBehaviour
    {
        public InputAction Click { get; private set; }
        public InputAction ClickHold { get; private set; }

        [SerializeField]
        InputActionAsset inputSettings;

        void Awake()
        {
            Click = inputSettings.FindActionMap("Operator").FindAction("Click");
            ClickHold = inputSettings.FindActionMap("Operator").FindAction("ClickHold");
        }

        void OnEnable()
        {
            inputSettings.Enable();
        }

        void OnDestroy()
        {
            inputSettings.Disable();
        }

    }
}
