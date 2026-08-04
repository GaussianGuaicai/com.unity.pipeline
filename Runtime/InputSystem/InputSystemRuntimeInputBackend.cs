#if ENABLE_INPUT_SYSTEM
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Scripting;

namespace Unity.Pipeline.Runtime.Commands
{
    /// <summary>Input System-backed implementation loaded by <see cref="RuntimeInputCommand"/>.</summary>
    [Preserve]
    public sealed class InputSystemRuntimeInputBackend : IRuntimeInputBackend
    {
        public InputSimulationResponse SimulateKey(string key, string action)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return InputSimulationResponse.Fail("simulate_key", "No keyboard device is present.");

            if (!Enum.TryParse<Key>(key, ignoreCase: true, out var parsedKey) || parsedKey == Key.None)
                return InputSimulationResponse.Fail("simulate_key", $"Unknown key '{key}'. Use an Input System Key name (e.g. Space, W, Enter).");

            var control = keyboard[parsedKey];
            var act = Normalize(action);
            switch (act)
            {
                case "down":
                    QueueButton(control, true);
                    break;
                case "up":
                    QueueButton(control, false);
                    break;
                case "press":
                    QueueButton(control, true);
                    QueueButton(control, false);
                    break;
                default:
                    return InputSimulationResponse.Fail("simulate_key", $"Unknown action '{action}'. Use down | up | press.");
            }

            InputSystem.Update();
            return InputSimulationResponse.Ok("simulate_key", $"key={parsedKey} action={act}");
        }

        public InputSimulationResponse SimulatePointer(float x, float y, string action, string button)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return InputSimulationResponse.Fail("simulate_pointer", "No mouse/pointer device is present.");

            var position = new Vector2(x, y);
            var act = Normalize(action);

            ButtonControl btn;
            switch (Normalize(button))
            {
                case "left": btn = mouse.leftButton; break;
                case "right": btn = mouse.rightButton; break;
                case "middle": btn = mouse.middleButton; break;
                default:
                    return InputSimulationResponse.Fail("simulate_pointer", $"Unknown button '{button}'. Use left | right | middle.");
            }

            switch (act)
            {
                case "move":
                    QueuePointer(mouse, position, null, false);
                    break;
                case "down":
                    QueuePointer(mouse, position, btn, true);
                    break;
                case "up":
                    QueuePointer(mouse, position, btn, false);
                    break;
                case "click":
                    QueuePointer(mouse, position, btn, true);
                    QueuePointer(mouse, position, btn, false);
                    break;
                default:
                    return InputSimulationResponse.Fail("simulate_pointer", $"Unknown action '{action}'. Use move | down | up | click.");
            }

            InputSystem.Update();
            return InputSimulationResponse.Ok("simulate_pointer", $"pos=({x},{y}) action={act} button={Normalize(button)}");
        }

        private static void QueueButton(InputControl<float> control, bool pressed)
        {
            using (StateEvent.From(control.device, out var eventPtr))
            {
                control.WriteValueIntoEvent(pressed ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }

        private static void QueuePointer(Mouse mouse, Vector2 position, ButtonControl button, bool pressed)
        {
            using (StateEvent.From(mouse, out var eventPtr))
            {
                mouse.position.WriteValueIntoEvent(position, eventPtr);
                if (button != null)
                    button.WriteValueIntoEvent(pressed ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }

        private static string Normalize(string value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
#endif
