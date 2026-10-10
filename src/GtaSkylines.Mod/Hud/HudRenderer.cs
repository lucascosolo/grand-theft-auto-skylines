using System;
using System.Collections.Generic;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Settings;
using GtaSkylines.Game.World;
using UnityEngine;

namespace GtaSkylines.Mod.Hud
{
    /// <summary>
    /// Draws the GTA V style HUD from the Game's <see cref="HudModel"/> (DESIGN "HUD", contract M4). Strings come from the
    /// model (formatted on change by G6) or from value caches here, so a steady frame allocates nothing.
    /// </summary>
    public sealed class HudRenderer : IDisposable
    {
        private readonly HudStyle _st;
        private readonly MinimapRenderer _minimap = new MinimapRenderer();
        private readonly IntTextCache _speed = new IntTextCache(HudFormat.Speed);
        private readonly PairTextCache _ammo = new PairTextCache(HudFormat.Ammo);
        private readonly RichTextCache _help = new RichTextCache();
        private readonly RichTextCache _objective = new RichTextCache();
        private readonly RichTextCache _subtitle = new RichTextCache();
        private readonly RichTextCache[] _notes = { new RichTextCache(), new RichTextCache(), new RichTextCache(), new RichTextCache() };
        private readonly Func<int, float> _objectiveOffset;
        private readonly Func<int, float> _subtitleOffset;
        private readonly List<TextSpan> _speakerSpans = new List<TextSpan>(1);
        private readonly string[] _card = new string[4];
        private uint _cardCitizen = uint.MaxValue;
        private int _cardHappiness = -1;
        private string _helpShown;
        private float _helpAlpha;
        private string _subSpeaker, _subText, _subCombined;
        private float _lastTime = -1f;
        private int _generation;
        private int _lastFontSize;

        /// <summary>Creates the renderer around a style.</summary>
        public HudRenderer(HudStyle style)
        {
            _st = style;
            _objectiveOffset = l => (_objective.MaxLineWidth - _objective.LineWidths[l]) * 0.5f;
            _subtitleOffset = l => (_subtitle.MaxLineWidth - _subtitle.LineWidths[l]) * 0.5f;
        }

        /// <summary>Minimap north-up instead of rotating with the camera (not yet a GameSettings field).</summary>
        public bool NorthUp { get; set; }

        /// <summary>Enlarged minimap (Z).</summary>
        public bool BigMap { get; set; }

        /// <summary>Layout of the last frame.</summary>
        public HudLayout Layout { get; private set; }

        /// <summary>Draws the HUD. <paramref name="hideWorldHud"/> keeps only fades and big screens (pause menu open).</summary>
        public void Draw(HudModel m, GameSettings s, CityMapTexture map, bool hideWorldHud, bool hasWaypoint, float wx, float wz)
        {
            float now = Time.realtimeSinceStartup;
            float dt = _lastTime < 0f ? 0f : Mathf.Clamp(now - _lastTime, 0f, 0.25f);
            if (Event.current.type == EventType.Repaint) _lastTime = now;

            HudLayout L = HudLayout.Compute(Screen.width, Screen.height, s.HudScale, BigMap);
            Layout = L;
            _st.Scale = L.Scale;
            if (_st.Px(20f) != _lastFontSize)
            {
                _lastFontSize = _st.Px(20f);
                _generation++;
            }

            bool big = m.BigScreen != BigScreenKind.None;
            if (!hideWorldHud && !big)
            {
                if (s.MinimapEnabled)
                {
                    _minimap.Draw(_st, map, m, ToRect(L.Minimap), NorthUp, now, hasWaypoint, wx, wz);
                    Bars(m, L);
                }
                TopRight(m, L);
                Help(m, L, dt);
                BottomCentre(m, s, L);
                BottomRight(m, L);
                Notifications(m, L);
                Crosshair(m, L);
            }
            if (big) BigScreen(m, L);
            if (m.FadeAlpha > 0f) _st.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, Mathf.Clamp01(m.FadeAlpha)));
        }

        private static Rect ToRect(HudRect r)
        {
            return new Rect(r.X, r.Y, r.W, r.H);
        }

        private void Bars(HudModel m, HudLayout L)
        {
            Bar(L.HealthBar, m.Health, HudStyle.Health, m.Health < 0.25f);
            Bar(L.ArmourBar, m.Armour, HudStyle.Armour, false);
            if (m.ShowBreath) Bar(L.BreathBar, m.Breath, HudStyle.Breath, false);
        }

        private void Bar(HudRect r, float fraction, Color c, bool flash)
        {
            Rect rect = ToRect(r);
            _st.Fill(rect, new Color(0f, 0f, 0f, 0.6f));
            _st.Fill(rect, HudStyle.A(c * 0.45f, 0.9f));
            if (flash && (Time.realtimeSinceStartup * 2f) % 1f < 0.5f) c = HudStyle.Red;
            _st.Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), c);
        }

        private void TopRight(HudModel m, HudLayout L)
        {
            float x = L.TopRightX, y = L.TopRightY;
            float star = _st.Px(30f), gap = _st.Px(2f);
            if (m.Stars > 0 || m.StarsFlashing)
            {
                float pulse = HudAnim.StarPulse(Time.realtimeSinceStartup);
                Color filled = m.StarsFlashing ? Color.Lerp(HudStyle.StarGrey, Color.white, pulse) : Color.white;
                for (int i = 0; i < 5; i++)
                {
                    var r = new Rect(x - (5 - i) * (star + gap), y, star, star);
                    bool on = i >= 5 - m.Stars;
                    _st.Icon(new Rect(r.x + 1, r.y + 1, star, star), Icon.Star, new Color(0f, 0f, 0f, on ? 0.6f : 0.25f));
                    _st.Icon(r, on ? Icon.Star : Icon.StarOutline, on ? filled : new Color(1f, 1f, 1f, 0.55f));
                }
            }
            y += star + gap * 2;
            float line = _st.Px(38f);
            _st.Text(new Rect(x - 600f, y, 600f, line), m.CashText, 34f, TextAnchor.MiddleRight, Color.white, 1f);
            y += line;
            float deltaAlpha = HudAnim.Window(m.CashDeltaAge, HudModel.CashDeltaSeconds, 0.3f);
            if (deltaAlpha > 0f && m.CashDeltaText != null)
            {
                _st.Text(new Rect(x - 600f, y, 600f, _st.Px(30f)), m.CashDeltaText, 28f, TextAnchor.MiddleRight,
                    m.CashDelta < 0 ? HudStyle.Red : HudStyle.CashPlus, deltaAlpha);
                y += _st.Px(30f);
            }
            if (m.WeaponName != null)
            {
                y += _st.Px(4f);
                _st.Text(new Rect(x - 600f, y, 600f, _st.Px(26f)), m.WeaponName, 22f, TextAnchor.MiddleRight, Color.white, 1f);
                y += _st.Px(26f);
                if (m.ShowAmmo)
                    _st.Text(new Rect(x - 600f, y, 600f, _st.Px(26f)), _ammo.Get(m.AmmoInMagazine, m.AmmoReserve), 22f,
                        TextAnchor.MiddleRight, Color.white, 1f);
            }
        }

        private void Help(HudModel m, HudLayout L, float dt)
        {
            if (m.HelpText != null) _helpShown = m.HelpText;
            _helpAlpha = HudAnim.Approach(_helpAlpha, m.HelpText != null ? 1f : 0f, 1f / 0.25f, dt);
            if (_helpAlpha <= 0f || _helpShown == null) return;
            float pad = _st.Px(14f), lineH = _st.Px(27f);
            _st.MeasureSize = _st.Px(20f);
            _help.Update(_helpShown, null, L.HelpMaxW - 2 * pad, _st.Measurer, _st.Px(6f), _generation);
            var box = new Rect(L.HelpX, L.HelpY, _help.MaxLineWidth + 2 * pad, _help.Lines * lineH + 2 * pad);
            _st.Fill(box, new Color(0f, 0f, 0f, 0.7f * _helpAlpha));
            _st.Runs(_help, box.x + pad, box.y + pad, lineH, 20f, _helpAlpha, false, null);
        }

        private void BottomCentre(HudModel m, GameSettings s, HudLayout L)
        {
            float lineH = _st.Px(30f);
            float maxW = L.ScreenW * 0.6f;
            float y = L.BottomCentreY;
            _st.MeasureSize = _st.Px(22f);
            if (m.Objective != null)
            {
                _objective.Update(m.Objective, m.ObjectiveSpans, maxW, _st.Measurer, _st.Px(6f), _generation);
                y -= _objective.Lines * lineH;
                _st.Runs(_objective, L.ScreenW * 0.5f - _objective.MaxLineWidth * 0.5f, y, lineH, 22f, 1f, true, _objectiveOffset);
            }
            if (s.Subtitles && m.SubtitleText != null)
            {
                if (!ReferenceEquals(m.SubtitleText, _subText) || !ReferenceEquals(m.SubtitleSpeaker, _subSpeaker))
                {
                    _subText = m.SubtitleText;
                    _subSpeaker = m.SubtitleSpeaker;
                    _subCombined = _subSpeaker == null ? _subText : _subSpeaker + ": " + _subText;
                    _speakerSpans.Clear();
                    if (_subSpeaker != null) _speakerSpans.Add(new TextSpan { Start = 0, Length = _subSpeaker.Length + 1, Kind = HighlightKind.Friendly });
                }
                _subtitle.Update(_subCombined, _speakerSpans, maxW, _st.Measurer, _st.Px(6f), _generation);
                y -= _subtitle.Lines * lineH + _st.Px(6f);
                _st.Runs(_subtitle, L.ScreenW * 0.5f - _subtitle.MaxLineWidth * 0.5f, y, lineH, 22f, 1f, true, _subtitleOffset);
            }
        }

        private void BottomRight(HudModel m, HudLayout L)
        {
            float x = L.BottomRightX, y = L.BottomRightY;
            float a = HudAnim.Window(m.LocationBannerAge, HudModel.LocationBannerSeconds, 0.5f);
            if (a > 0f)
            {
                float h1 = _st.Px(34f), h2 = _st.Px(26f);
                y -= h2;
                _st.Text(new Rect(x - 800f, y, 800f, h2), m.StreetName, 21f, TextAnchor.MiddleRight, Color.white, a);
                y -= h1;
                _st.Text(new Rect(x - 800f, y, 800f, h1), m.DistrictName, 30f, TextAnchor.MiddleRight, Color.white, a);
            }
            float va = HudAnim.Window(m.VehicleBannerAge, HudModel.VehicleBannerSeconds, 0.5f);
            if (va > 0f && m.VehicleName != null)
            {
                y -= _st.Px(36f);
                _st.Text(new Rect(x - 800f, y, 800f, _st.Px(36f)), m.VehicleName, 30f, TextAnchor.MiddleRight, HudStyle.Mission, va);
            }
            if (m.ShowSpeed)
            {
                y -= _st.Px(26f);
                _st.Text(new Rect(x - 400f, y, 400f, _st.Px(26f)), _speed.Get(m.SpeedKmh), 20f, TextAnchor.MiddleRight, Color.white, 0.9f);
            }
            if (m.TimerText != null) y = InfoBar(x, y, "TIME", m.TimerText);
            if (m.StatusLine != null) InfoBar(x, y, null, m.StatusLine);
        }

        private float InfoBar(float right, float bottom, string label, string value)
        {
            float h = _st.Px(34f), w = _st.Px(260f);
            var r = new Rect(right - w, bottom - h - _st.Px(4f), w, h);
            _st.Fill(r, new Color(0f, 0f, 0f, 0.6f));
            float pad = _st.Px(10f);
            if (label != null) _st.Plain(new Rect(r.x + pad, r.y, w, h), label, 18f, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.8f), 1f);
            _st.Plain(new Rect(r.x, r.y, w - pad, h), value, 24f, TextAnchor.MiddleRight, Color.white, 1f);
            return r.y;
        }

        private void Notifications(HudModel m, HudLayout L)
        {
            float y = L.Notifications.Y, w = L.Notifications.W, pad = _st.Px(8f), lineH = _st.Px(21f), icon = _st.Px(28f);
            _st.MeasureSize = _st.Px(16f);
            for (int i = 0; i < m.Notifications.Count && i < _notes.Length; i++)
            {
                GtaSkylines.Game.World.Notification n = m.Notifications[i];
                float a = HudAnim.Window(n.Age, HudMessages.NotificationSeconds, 0.35f);
                if (a <= 0f) continue;
                RichTextCache c = _notes[i];
                c.Update(n.Text, null, w - icon - 3 * pad, _st.Measurer, _st.Px(4f), _generation);
                float h = pad * 2 + lineH * (c.Lines + (n.Sender != null ? 1 : 0));
                y -= h + _st.Px(4f);
                var box = new Rect(L.Notifications.X, y, w, h);
                _st.Fill(box, new Color(0f, 0f, 0f, 0.75f * a));
                Color kc = NotificationColour(n.Kind);
                _st.Fill(new Rect(box.x, box.y, _st.Px(4f), h), HudStyle.A(kc, a));
                _st.Icon(new Rect(box.x + pad + _st.Px(2f), box.y + pad, icon, icon), NotificationIcon(n.Kind), HudStyle.A(kc, a));
                float tx = box.x + icon + 2 * pad, ty = box.y + pad;
                if (n.Sender != null)
                {
                    _st.Plain(new Rect(tx, ty, w, lineH), n.Sender, 17f, TextAnchor.MiddleLeft, kc, a);
                    ty += lineH;
                }
                _st.Runs(c, tx, ty, lineH, 16f, a, false, null);
            }
        }

        private static Color NotificationColour(NotificationKind k)
        {
            switch (k)
            {
                case NotificationKind.Text: return HudStyle.CashPlus;
                case NotificationKind.Contact: return HudStyle.Mission;
                case NotificationKind.Chirper: return HudStyle.Chirper;
                default: return new Color(0.8f, 0.8f, 0.8f, 1f);
            }
        }

        private static Icon NotificationIcon(NotificationKind k)
        {
            switch (k)
            {
                case NotificationKind.Chirper: return Icon.Chirp;
                case NotificationKind.System: return Icon.Square;
                default: return Icon.Bubble;
            }
        }

        private void Crosshair(HudModel m, HudLayout L)
        {
            if (m.Crosshair == CrosshairKind.None) return;
            float cx = L.ScreenW * 0.5f, cy = L.ScreenH * 0.5f, d = _st.Px(6f), o = d + 2f;
            _st.Icon(new Rect(cx - o / 2, cy - o / 2, o, o), Icon.Disc, new Color(0f, 0f, 0f, 0.6f));
            _st.Icon(new Rect(cx - d / 2, cy - d / 2, d, d), Icon.Disc, m.Crosshair == CrosshairKind.Hostile ? HudStyle.Red : Color.white);
            if (!m.HasPedCard) return;
            PedCard(m.PedCard);
            float pad = _st.Px(10f), lineH = _st.Px(22f), w = _st.Px(300f);
            var box = new Rect(cx + _st.Px(40f), cy - _st.Px(30f), w, pad * 2 + lineH * 5);
            _st.Fill(box, new Color(0f, 0f, 0f, 0.7f));
            _st.Fill(new Rect(box.x, box.y, w, _st.Px(3f)), HudStyle.Blue);
            _st.Plain(new Rect(box.x + pad, box.y + pad, w, lineH), m.PedCard.Name, 20f, TextAnchor.MiddleLeft, Color.white, 1f);
            for (int i = 0; i < _card.Length; i++)
                _st.Plain(new Rect(box.x + pad, box.y + pad + lineH * (i + 1), w - pad, lineH), _card[i], 16f, TextAnchor.MiddleLeft,
                    new Color(0.85f, 0.85f, 0.85f, 1f), 1f);
        }

        private void PedCard(PedCardData p)
        {
            if (p.Citizen == _cardCitizen && p.Happiness == _cardHappiness) return;
            _cardCitizen = p.Citizen;
            _cardHappiness = p.Happiness;
            _card[0] = "Age " + p.Age + (p.Tourist ? ", tourist" : "");
            _card[1] = p.Retired ? "Retired" : p.WorkName == null ? "Unemployed" : (p.Student ? "Studies at " : "Works at ") + p.WorkName;
            _card[2] = p.HomeName == null ? "No fixed address" : "Lives at " + p.HomeName;
            _card[3] = "Happiness " + p.Happiness + "%";
        }

        private void BigScreen(HudModel m, HudLayout L)
        {
            float a = HudAnim.BigScreenAlpha(m.BigScreenProgress);
            if (a <= 0f) return;
            var full = new Rect(0, 0, L.ScreenW, L.ScreenH);
            string title, sub = null;
            Color titleColour, tint;
            switch (m.BigScreen)
            {
                case BigScreenKind.Wasted:
                    title = "wasted"; titleColour = HudStyle.Red; tint = new Color(0.5f, 0.05f, 0.05f, 0.35f);
                    break;
                case BigScreenKind.Busted:
                    title = "busted"; titleColour = Color.white; tint = new Color(0.08f, 0.15f, 0.45f, 0.35f);
                    break;
                case BigScreenKind.MissionPassed:
                    title = "mission passed"; titleColour = HudStyle.Mission; tint = new Color(0f, 0f, 0f, 0f);
                    sub = _rewardText(m.BigScreenReward);
                    break;
                default:
                    title = "mission failed"; titleColour = HudStyle.Red; tint = new Color(0f, 0f, 0f, 0.2f);
                    sub = m.BigScreenText;
                    break;
            }
            bool death = m.BigScreen == BigScreenKind.Wasted || m.BigScreen == BigScreenKind.Busted;
            if (death)
            {
                _st.Fill(full, new Color(0.35f, 0.35f, 0.35f, 0.35f * a));
                _st.Icon(new Rect(-L.ScreenW * 0.1f, -L.ScreenH * 0.1f, L.ScreenW * 1.2f, L.ScreenH * 1.2f), Icon.Vignette, new Color(0f, 0f, 0f, 0.85f * a));
            }
            _st.Fill(full, HudStyle.A(tint, tint.a * a));
            float bandH = _st.Px(death ? 150f : 190f), cy = L.ScreenH * 0.45f;
            _st.Fill(new Rect(0, cy - bandH / 2, L.ScreenW, bandH), new Color(0f, 0f, 0f, 0.55f * a));
            int size = (int)(_st.Px(death ? 120f : 84f) * HudAnim.BigScreenTitleScale(m.BigScreenProgress));
            float th = size * 1.4f;
            _st.Title(new Rect(0, cy - th / 2 - (sub != null ? _st.Px(18f) : 0f), L.ScreenW, th), title, size, TextAnchor.MiddleCenter, titleColour, a);
            if (sub != null)
                _st.Text(new Rect(0, cy + _st.Px(32f), L.ScreenW, _st.Px(36f)), sub, 28f, TextAnchor.MiddleCenter, Color.white, a);
        }

        private long _rewardValue = long.MinValue;
        private string _rewardString;

        private string _rewardText(long reward)
        {
            if (reward != _rewardValue)
            {
                _rewardValue = reward;
                _rewardString = reward > 0 ? "+" + HudFormat.Money(reward) : null;
            }
            return _rewardString;
        }

        /// <summary>Frees the minimap render texture.</summary>
        public void Dispose()
        {
            _minimap.Dispose();
        }
    }
}
