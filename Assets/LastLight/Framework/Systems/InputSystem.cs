using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace LastLight
{
    public sealed class InputSystem : GameSystemBase, IGameTick
    {
        private InputAction cancel;
        public event Action CancelPressed;
        public bool GameplayEnabled { get; set; }
        public override Task InitializeAsync(SystemRegistry systems, CancellationToken cancellation)
        {
            cancel = new InputAction("LastLight/UI/Cancel", InputActionType.Button, "<Keyboard>/escape");
            cancel.Enable();
            return Task.CompletedTask;
        }
        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (cancel.WasPressedThisFrame()) CancelPressed?.Invoke();
        }
        public override Task ShutdownAsync()
        {
            cancel?.Dispose(); cancel = null; CancelPressed = null; GameplayEnabled = false;
            return Task.CompletedTask;
        }
    }
}
