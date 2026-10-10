using System;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using Skylines.Host.Input;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.Input
{
    /// <summary>
    /// Builds an <see cref="InputFrame"/> from Unity input each frame through the key bindings, mouse sensitivity and
    /// invert setting. Reads nothing while the game window is unfocused (held buttons are reported released). No
    /// allocation per frame once built. Main thread only.
    /// </summary>
    public sealed class InputMapper
    {
        private ButtonMap _map;
        private KeySource[] _kinds = new KeySource[0];
        private KeyCode[] _codes = new KeyCode[0];
        private bool[] _down = new bool[0];
        private uint _held;

        /// <summary>Recompiles the bindings; key names Unity does not know are reported and left unbound.</summary>
        public void Rebuild(KeyBindings bindings, Action<string> warn)
        {
            _map = new ButtonMap(bindings);
            int n = _map.SourceCount;
            _kinds = new KeySource[n];
            _codes = new KeyCode[n];
            _down = new bool[n];
            for (int i = 0; i < n; i++)
            {
                string name = _map.SourceName(i);
                _kinds[i] = KeyNames.Classify(name);
                if (_kinds[i] != KeySource.Key) continue;
                try { _codes[i] = (KeyCode)Enum.Parse(typeof(KeyCode), name, false); }
                catch (ArgumentException)
                {
                    _kinds[i] = KeySource.None;
                    warn("input: Unity has no KeyCode '" + name + "'; left unbound");
                }
            }
            _held = 0;
        }

        /// <summary>This frame's input; mouse look comes from <paramref name="capture"/> (zero while the cursor is free).</summary>
        public InputFrame Read(InputCapture capture, GameSettings settings)
        {
            var f = new InputFrame();
            bool focused = Application.isFocused && _map != null;
            if (focused)
            {
                float wheel = UInput.mouseScrollDelta.y;
                for (int i = 0; i < _down.Length; i++)
                {
                    switch (_kinds[i])
                    {
                        case KeySource.Key: _down[i] = UInput.GetKey(_codes[i]); break;
                        case KeySource.WheelUp: _down[i] = wheel > 0f; break;
                        case KeySource.WheelDown: _down[i] = wheel < 0f; break;
                        default: _down[i] = false; break;
                    }
                }
            }
            uint held = focused ? _map.Held(_down) : 0u;
            ButtonMap.Edges(_held, held, out f.Pressed, out f.Released);
            f.Held = held;
            _held = held;
            if (!focused) return f;
            f.MoveX = _map.Axis(_down, MoveKey.Left, MoveKey.Right);
            f.MoveY = _map.Axis(_down, MoveKey.Back, MoveKey.Forward);
            float dx, dy;
            capture.ReadMouse(out dx, out dy);
            ButtonMap.Look(dx, dy, settings.MouseSensitivity, settings.InvertY, out f.LookYawDeg, out f.LookPitchDeg);
            Vector3 m = UInput.mousePosition;
            f.PointerX = m.x;
            f.PointerY = Screen.height - m.y;
            return f;
        }
    }
}
