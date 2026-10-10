using System;
using Skylines.Host;
using Skylines.Host.Ui;
using UnityEngine;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// The "drag the figure onto the city" entry (ported from the reference): a <see cref="MapDropButton"/> that exists
    /// while a city is loaded and is hidden while playing. A drop requests entry at the hit point.
    /// </summary>
    internal sealed class WalkInButton
    {
        private const string TooltipText = "Drag onto the map to start there (or Ctrl+Shift+G over a spot).\nClick to start at your last safehouse (Ctrl+Shift+H).";

        private readonly HostLog _log;
        private readonly Action<Vector3> _dropped;
        private readonly Action _clicked;
        private MapDropButton _button;

        /// <summary><paramref name="clicked"/> runs on a plain click; it returns false when it did nothing, and the tooltip is shown instead.</summary>
        public WalkInButton(HostLog log, Action<Vector3> dropped, Func<bool> clicked)
        {
            _log = log;
            _dropped = dropped;
            _clicked = () =>
            {
                if (!clicked() && _button != null) _button.ShowHint(TooltipText);
            };
        }

        public void Create()
        {
            Guard("create", () =>
            {
                var spec = new MapDropSpec
                {
                    Name = "GtaSkylines.WalkIn",
                    IconSprites = new[] { "InfoIconPopulation", "IconCitizenVehicle" },
                    BackgroundStems = new[] { "OptionBase", "RoundBackBig", "ButtonMenu" },
                    Tooltip = TooltipText,
                };
                _button = new MapDropButton(spec, _dropped, _clicked, _log);
                string problem = _button.Create();
                if (problem != null)
                {
                    _log.Warn("walk-in button not shown: " + problem);
                    _button = null;
                }
            });
        }

        /// <summary>Per frame.</summary>
        public void Update(bool playing)
        {
            if (_button == null) return;
            try
            {
                _button.Visible = !playing;
                _button.Update();
            }
            catch (Exception e)
            {
                _log.Error("walk-in button update", e);
            }
        }

        public void Dispose()
        {
            MapDropButton b = _button;
            _button = null;
            if (b != null) Guard("dispose", b.Dispose);
        }

        private void Guard(string what, Action step)
        {
            try { step(); }
            catch (Exception e) { _log.Error("walk-in button " + what, e); }
        }
    }
}
