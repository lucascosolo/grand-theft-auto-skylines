using GtaSkylines.Game.Input;
using GtaSkylines.Game.Profile;
using UnityEngine;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>
    /// Temporary session until the real game session is wired: a WASD fly camera (Space up, Sneak down, Sprint faster)
    /// and a minimal IMGUI pause box (Esc/P) with Resume and Return to City Builder.
    /// </summary>
    public sealed class FreeCameraSession : IPlaySession
    {
        private const float EyeHeight = 1.7f;
        private const float Speed = 12f;
        private const float SprintFactor = 5f;
        private const float NearClip = 0.3f;

        private readonly SessionServices _s;
        private PlayerProfile _profile = new PlayerProfile();
        private Vector3 _pos;
        private float _yaw;
        private float _pitch;
        private bool _menu;

        /// <summary>A session using <paramref name="services"/>.</summary>
        public FreeCameraSession(SessionServices services)
        {
            _s = services;
        }

        /// <inheritdoc />
        public bool WantsExit { get; private set; }

        /// <inheritdoc />
        public void Begin(Vector3 spawnFeet, float yawDeg)
        {
            _pos = spawnFeet + new Vector3(0f, EyeHeight, 0f);
            _yaw = yawDeg;
            _pitch = 0f;
        }

        /// <inheritdoc />
        public void Tick(float dt, InputFrame input)
        {
            if (input.WasPressed(GameButton.Pause)) SetMenu(!_menu);
            if (_menu) return;
            _profile.PlayTimeSeconds += dt;
            _yaw = Mathf.Repeat(_yaw + input.LookYawDeg, 360f);
            _pitch = Mathf.Clamp(_pitch + input.LookPitchDeg, -89f, 89f);
            float up = (input.IsHeld(GameButton.Jump) ? 1f : 0f) - (input.IsHeld(GameButton.Sneak) ? 1f : 0f);
            Vector3 move = Rotation() * new Vector3(input.MoveX, 0f, input.MoveY) + new Vector3(0f, up, 0f);
            float speed = Speed * (input.IsHeld(GameButton.Sprint) ? SprintFactor : 1f);
            _pos += move * (speed * dt);
        }

        /// <inheritdoc />
        public void LateTick()
        {
            _s.Camera.Drive(_pos, Rotation(), _s.Settings.FieldOfViewDeg, NearClip);
        }

        /// <inheritdoc />
        public void Gui()
        {
            if (!_menu) return;
            const float w = 260f, h = 130f;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(box, "Paused");
            if (GUI.Button(new Rect(box.x + 20f, box.y + 35f, w - 40f, 32f), "Resume")) SetMenu(false);
            if (GUI.Button(new Rect(box.x + 20f, box.y + 77f, w - 40f, 32f), "Return to City Builder")) WantsExit = true;
        }

        /// <inheritdoc />
        public void End()
        {
            SetMenu(false);
        }

        /// <inheritdoc />
        public byte[] SaveProfile()
        {
            return ProfileRecord.Encode(_profile);
        }

        /// <inheritdoc />
        public void LoadProfile(byte[] data)
        {
            ProfileDecodeResult r = data == null ? null : ProfileRecord.Decode(data);
            _profile = r != null && r.Ok ? r.Profile : new PlayerProfile();
        }

        private Quaternion Rotation()
        {
            return Quaternion.Euler(-_pitch, _yaw, 0f);
        }

        private void SetMenu(bool open)
        {
            if (_menu == open) return;
            _menu = open;
            _s.SetMenuOpen(open);
        }
    }
}
