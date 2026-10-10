using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using GtaSkylines.Mod.Input;
using ICities;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace GtaSkylines.Mod.Options
{
    /// <summary>
    /// The mod's page in CS1's options (DESIGN F-SET): every setting with its range, and key rebinding (click a slot,
    /// press a key or mouse button; Esc cancels; conflicts shown on the buttons). Every change is saved at once.
    /// </summary>
    internal static class OptionsPage
    {
        private static readonly string[] Views = { "First person", "Third person near", "Third person mid", "Third person far" };
        private static readonly string[] Rates = { "0.5x (default)", "0.75x", "1x" };
        private static readonly string[] Police = { "Easy", "Normal", "Hard" };

        private sealed class Slot
        {
            public bool Move;
            public int Index;
            public int SlotIndex;
            public UIButton Button;
        }

        private static readonly List<Slot> s_slots = new List<Slot>();
        private static KeyListener s_listener;

        public static void Build(UIHelperBase helper)
        {
            GameSettings s = ModRuntime.Settings;
            s_slots.Clear();

            UIHelperBase g = helper.AddGroup("Camera and look");
            Slider(g, "Mouse sensitivity", 0.1f, 5f, 0.05f, s.MouseSensitivity, "0.00", v => s.MouseSensitivity = v);
            g.AddCheckbox("Invert mouse Y", s.InvertY, v => Changed(() => s.InvertY = v));
            Slider(g, "Field of view (degrees)", 50f, 100f, 1f, s.FieldOfViewDeg, "0", v => s.FieldOfViewDeg = v);
            g.AddDropdown("Default view on foot", Views, s.FootView, i => Changed(() => s.FootView = i));
            g.AddDropdown("Default view in vehicles", Views, s.VehicleView, i => Changed(() => s.VehicleView = i));

            g = helper.AddGroup("HUD");
            Slider(g, "HUD scale", 0.5f, 2f, 0.05f, s.HudScale, "0.00", v => s.HudScale = v);
            g.AddCheckbox("Minimap", s.MinimapEnabled, v => Changed(() => s.MinimapEnabled = v));
            g.AddCheckbox("Subtitles", s.Subtitles, v => Changed(() => s.Subtitles = v));

            g = helper.AddGroup("City");
            g.AddDropdown("Simulation speed while playing", Rates, s.SimulationRate - 1, i => Changed(() => s.SimulationRate = i + 1));
            g.AddDropdown("Police difficulty", Police, (int)s.Police, i => Changed(() => s.Police = (PoliceDifficulty)i));

            g = helper.AddGroup("Audio");
            Slider(g, "Master volume", 0f, 1f, 0.05f, s.MasterVolume, "0.00", v => s.MasterVolume = v);
            Slider(g, "Effects volume", 0f, 1f, 0.05f, s.EffectsVolume, "0.00", v => s.EffectsVolume = v);
            Slider(g, "Music and radio volume", 0f, 1f, 0.05f, s.MusicVolume, "0.00", v => s.MusicVolume = v);
            Slider(g, "Voice volume", 0f, 1f, 0.05f, s.VoiceVolume, "0.00", v => s.VoiceVolume = v);

            g = helper.AddGroup("Keys: click a slot, then press a key or mouse button (Esc cancels)");
            foreach (MoveKey m in Enum.GetValues(typeof(MoveKey))) AddSlots(g, true, (int)m);
            foreach (GameButton b in Enum.GetValues(typeof(GameButton))) AddSlots(g, false, (int)b);
            g.AddButton("Reset all keys to defaults", () => Changed(() =>
            {
                s.Bindings.ResetToDefaults();
                RefreshSlots();
            }));
            RefreshSlots();
        }

        private static void Slider(UIHelperBase g, string text, float min, float max, float step, float value, string format, Action<float> set)
        {
            // The slider template's own label (UIHelper.AddSlider) shows the current value.
            UILabel label = null;
            var slider = g.AddSlider(text, min, max, step, value, v =>
            {
                Changed(() => set(v));
                if (label != null) label.text = text + ": " + v.ToString(format);
            }) as UISlider;
            label = slider == null || slider.parent == null ? null : slider.parent.Find<UILabel>("Label");
            if (label != null) label.text = text + ": " + value.ToString(format);
        }

        private static void AddSlots(UIHelperBase g, bool move, int index)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                var entry = new Slot { Move = move, Index = index, SlotIndex = slot };
                entry.Button = g.AddButton(BindingText.ActionName(move, index), () => StartRebind(entry)) as UIButton;
                if (entry.Button == null) continue;
                s_slots.Add(entry);
                if (s_listener == null) s_listener = entry.Button.gameObject.AddComponent<KeyListener>();
            }
        }

        private static void StartRebind(Slot entry)
        {
            if (s_listener == null) return;
            entry.Button.text = Label(entry) + ": press a key... (Esc cancels)";
            s_listener.Listen(name =>
            {
                if (name != null)
                {
                    KeyBindings kb = ModRuntime.Settings.Bindings;
                    Changed(() =>
                    {
                        if (entry.Move) kb.SetMove((MoveKey)entry.Index, entry.SlotIndex, name);
                        else kb.Set((GameButton)entry.Index, entry.SlotIndex, name);
                    });
                    List<string> conflicts = BindingText.Conflicts(kb, entry.Move, entry.Index, entry.SlotIndex);
                    if (conflicts.Count > 0)
                        ModRuntime.Log.Info("options: " + name + " is also bound to " + string.Join(", ", conflicts.ToArray()));
                }
                RefreshSlots();
            });
        }

        private static void RefreshSlots()
        {
            KeyBindings kb = ModRuntime.Settings.Bindings;
            foreach (Slot e in s_slots)
                if (e.Button != null) e.Button.text = Label(e) + ": " + BindingText.Describe(kb, e.Move, e.Index, e.SlotIndex);
        }

        private static string Label(Slot e)
        {
            return BindingText.ActionName(e.Move, e.Index) + (e.SlotIndex == 0 ? "" : " (2nd)");
        }

        private static void Changed(Action change)
        {
            try
            {
                change();
                ModRuntime.SettingsChanged();
            }
            catch (Exception e)
            {
                ModRuntime.Log.Error("options change", e);
            }
        }

        /// <summary>Polls Unity input while a rebind is pending; reports the first key, mouse button or wheel, or null on Esc.</summary>
        private sealed class KeyListener : MonoBehaviour
        {
            private KeyCode[] _codes;
            private string[] _names;
            private Action<string> _done;
            private int _armedFrame;

            public void Listen(Action<string> done)
            {
                if (_done != null) _done(null);
                _done = done;
                _armedFrame = Time.frameCount;
            }

            private void Update()
            {
                // The options page has no other per-frame hook outside a city: write changed settings once they settle.
                ModRuntime.Guard("settings flush", () => ModRuntime.FlushSettings(false));
                if (_done == null || Time.frameCount <= _armedFrame) return;
                try
                {
                    if (_codes == null) Prepare();
                    if (UInput.GetKeyDown(KeyCode.Escape))
                    {
                        Finish(null);
                        return;
                    }
                    float wheel = UInput.mouseScrollDelta.y;
                    if (wheel > 0f) { Finish("WheelUp"); return; }
                    if (wheel < 0f) { Finish("WheelDown"); return; }
                    for (int i = 0; i < _codes.Length; i++)
                        if (UInput.GetKeyDown(_codes[i])) { Finish(_names[i]); return; }
                }
                catch (Exception e)
                {
                    ModRuntime.Log.Error("options key listener", e);
                    _done = null;
                }
            }

            private void OnDestroy()
            {
                if (s_listener == this) s_listener = null;
            }

            private void Finish(string name)
            {
                Action<string> done = _done;
                _done = null;
                done(name);
            }

            private void Prepare()
            {
                var codes = new List<KeyCode>();
                var names = new List<string>();
                foreach (string n in KeyNames.All)
                {
                    if (KeyNames.Classify(n) != KeySource.Key || n == "Escape") continue;
                    try
                    {
                        codes.Add((KeyCode)Enum.Parse(typeof(KeyCode), n, false));
                        names.Add(n);
                    }
                    catch (ArgumentException)
                    {
                        ModRuntime.Log.Warn("options: Unity has no KeyCode '" + n + "'");
                    }
                }
                _codes = codes.ToArray();
                _names = names.ToArray();
            }
        }
    }
}
