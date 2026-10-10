using System;
using System.Collections.Generic;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;

namespace GtaSkylines.Mod.Input
{
    /// <summary>
    /// A snapshot of <see cref="KeyBindings"/> compiled for per-frame reading: the distinct bound key names become
    /// sources, the caller fills one bool per source each frame and gets button masks and move axes back without
    /// allocating. Unity-free.
    /// </summary>
    public sealed class ButtonMap
    {
        /// <summary>Look degrees per Unity mouse axis unit at sensitivity 1 (the reference project's default).</summary>
        public const float DegreesPerUnit = 1.5f;

        private static readonly int ButtonCount = Enum.GetValues(typeof(GameButton)).Length;
        private static readonly int MoveCount = Enum.GetValues(typeof(MoveKey)).Length;

        private readonly string[] _sources;
        private readonly int[] _buttonSlots;
        private readonly int[] _moveSlots;

        /// <summary>Compiles <paramref name="bindings"/>; later changes to it do not affect this map.</summary>
        public ButtonMap(KeyBindings bindings)
        {
            var names = new List<string>();
            _buttonSlots = new int[ButtonCount * 2];
            _moveSlots = new int[MoveCount * 2];
            for (int b = 0; b < ButtonCount; b++)
                for (int s = 0; s < 2; s++)
                    _buttonSlots[b * 2 + s] = SourceOf(names, bindings.Get((GameButton)b, s));
            for (int m = 0; m < MoveCount; m++)
                for (int s = 0; s < 2; s++)
                    _moveSlots[m * 2 + s] = SourceOf(names, bindings.GetMove((MoveKey)m, s));
            _sources = names.ToArray();
        }

        /// <summary>Number of distinct bound key names.</summary>
        public int SourceCount { get { return _sources.Length; } }

        /// <summary>The key name of source <paramref name="i"/>.</summary>
        public string SourceName(int i) { return _sources[i]; }

        /// <summary>Bit per <see cref="GameButton"/> with any slot's source down.</summary>
        public uint Held(bool[] sourceDown)
        {
            uint held = 0;
            for (int b = 0; b < ButtonCount; b++)
                if (Down(sourceDown, _buttonSlots[b * 2]) || Down(sourceDown, _buttonSlots[b * 2 + 1])) held |= 1u << b;
            return held;
        }

        /// <summary>−1 when only <paramref name="negative"/> is down, +1 when only <paramref name="positive"/> is, else 0.</summary>
        public float Axis(bool[] sourceDown, MoveKey negative, MoveKey positive)
        {
            float v = 0f;
            if (MoveDown(sourceDown, negative)) v -= 1f;
            if (MoveDown(sourceDown, positive)) v += 1f;
            return v;
        }

        /// <summary>Buttons that went down and up between two held masks.</summary>
        public static void Edges(uint previousHeld, uint held, out uint pressed, out uint released)
        {
            pressed = held & ~previousHeld;
            released = previousHeld & ~held;
        }

        /// <summary>Mouse axis deltas to look degrees (+yaw right, +pitch up unless inverted).</summary>
        public static void Look(float mouseDx, float mouseDy, float sensitivity, bool invertY, out float yawDeg, out float pitchDeg)
        {
            float k = sensitivity * DegreesPerUnit;
            yawDeg = mouseDx * k;
            pitchDeg = (invertY ? -mouseDy : mouseDy) * k;
        }

        private bool MoveDown(bool[] sourceDown, MoveKey key)
        {
            int m = (int)key;
            return Down(sourceDown, _moveSlots[m * 2]) || Down(sourceDown, _moveSlots[m * 2 + 1]);
        }

        private static bool Down(bool[] sourceDown, int source) { return source >= 0 && sourceDown[source]; }

        private static int SourceOf(List<string> names, string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int i = names.IndexOf(name);
            if (i >= 0) return i;
            names.Add(name);
            return names.Count - 1;
        }
    }
}
