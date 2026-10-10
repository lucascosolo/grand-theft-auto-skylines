using GtaSkylines.Game.Physics;
using GtaSkylines.Game.Profile;
using UnityEngine;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>
    /// Intention-level sound calls for the gameplay wiring. Every call is safe before the clips are ready (it does nothing)
    /// and allocates nothing. Loops return a <see cref="VoiceHandle"/> the caller keeps, updates per frame and stops.
    /// </summary>
    public sealed class AudioEvents
    {
        private readonly AudioPlayer _p;
        private VoiceHandle _roomTone;
        private bool _indoors;

        /// <summary>Wraps a player.</summary>
        public AudioEvents(AudioPlayer player) { _p = player; }

        /// <summary>The underlying player.</summary>
        public AudioPlayer Player { get { return _p; } }

        /// <summary>A footstep on the surface under the foot.</summary>
        public void Footstep(SurfaceFlags surface, Vector3 position)
        {
            _p.PlayAt(SoundBank.Footstep(surface), position, WorldCategory.Foot, VolumeGroup.Effects, 1f, Random.Range(0.94f, 1.06f));
        }

        /// <summary>Jump take-off.</summary>
        public void Jump(Vector3 position) { _p.PlayAt(SoundId.Jump, position, WorldCategory.Foot, VolumeGroup.Effects, 1f, 1f); }

        /// <summary>Landing; louder with the downward speed in m/s (full at 8).</summary>
        public void Land(Vector3 position, float fallSpeed)
        {
            _p.PlayAt(SoundId.Land, position, WorldCategory.Foot, VolumeGroup.Effects, Mathf.Clamp(0.4f + fallSpeed / 8f, 0.4f, 1f), 1f);
        }

        /// <summary>Fist hit.</summary>
        public void Punch(Vector3 position) { _p.PlayAt(SoundId.Punch, position, WorldCategory.Foot, VolumeGroup.Effects, 1f, Random.Range(0.92f, 1.08f)); }

        /// <summary>Bat hit.</summary>
        public void BatHit(Vector3 position) { _p.PlayAt(SoundId.BatHit, position, WorldCategory.Foot, VolumeGroup.Effects, 1f, Random.Range(0.95f, 1.05f)); }

        /// <summary>A weapon's attack: a shot for firearms, a punch or bat hit for melee.</summary>
        public void Gunshot(WeaponId weapon, Vector3 position)
        {
            SoundId id = SoundBank.Gunshot(weapon);
            WorldCategory c = WeaponInfo.UsesAmmo(weapon) ? WorldCategory.Weapon : WorldCategory.Foot;
            _p.PlayAt(id, position, c, VolumeGroup.Effects, 1f, Random.Range(0.97f, 1.03f));
        }

        /// <summary>Trigger pulled on an empty weapon.</summary>
        public void DryFire(Vector3 position) { _p.PlayAt(SoundId.DryFire, position, WorldCategory.Foot, VolumeGroup.Effects, 1f, 1f); }

        /// <summary>Reload; the shotgun's is a little lower.</summary>
        public void Reload(WeaponId weapon, Vector3 position)
        {
            _p.PlayAt(SoundId.Reload, position, WorldCategory.Foot, VolumeGroup.Effects, 1f, weapon == WeaponId.Shotgun ? 0.85f : 1f);
        }

        /// <summary>A bullet hitting something.</summary>
        public void BulletImpact(Vector3 position) { _p.PlayAt(SoundId.BulletImpact, position, WorldCategory.Impact, VolumeGroup.Effects, 1f, Random.Range(0.9f, 1.1f)); }

        /// <summary>A vehicle crash at an impact speed in m/s (tier and loudness follow the speed).</summary>
        public void Crash(float impactSpeed, Vector3 position)
        {
            _p.PlayAt(SoundBank.CrashTier(impactSpeed), position, WorldCategory.Impact, VolumeGroup.Vehicles, SoundBank.CrashVolume(impactSpeed), Random.Range(0.93f, 1.07f));
        }

        /// <summary>An explosion.</summary>
        public void Explosion(Vector3 position) { _p.PlayAt(SoundId.Explosion, position, WorldCategory.Explosion, VolumeGroup.Effects, 1f, Random.Range(0.9f, 1.05f)); }

        /// <summary>A short horn tap.</summary>
        public void Horn(Vector3 position) { _p.PlayAt(SoundId.Horn, position, WorldCategory.Vehicle, VolumeGroup.Vehicles, 1f, 1f); }

        /// <summary>Starts a held horn; move it with <see cref="Move"/>, end it with <see cref="Stop"/>.</summary>
        public VoiceHandle StartHorn(Vector3 position)
        {
            return _p.StartLoop(SoundId.HornLoop, true, position, WorldCategory.Vehicle, VolumeGroup.Vehicles, 1f, 1f, false);
        }

        /// <summary>Starts a siren (Doppler on); move it every frame with <see cref="Move"/>.</summary>
        public VoiceHandle StartSiren(SirenKind kind, Vector3 position)
        {
            return _p.StartLoop(SoundBank.Siren(kind), true, position, WorldCategory.Siren, VolumeGroup.Vehicles, 1f, 1f, true);
        }

        /// <summary>Starts the procedural engine loop (use when <see cref="TryPlayVehicleEngine"/> returns false).</summary>
        public VoiceHandle StartEngine(Vector3 position)
        {
            return _p.StartLoop(SoundId.EngineLoop, true, position, WorldCategory.Vehicle, VolumeGroup.Vehicles, 0.5f, AudioMix.EnginePitch(0f), false);
        }

        /// <summary>Per frame: engine position, normalised rpm (pitch) and throttle 0..1 (loudness).</summary>
        public void UpdateEngine(VoiceHandle engine, Vector3 position, float rpm01, float throttle01)
        {
            _p.SetLoop(engine, position, 0.45f + 0.55f * Mathf.Clamp01(throttle01), AudioMix.EnginePitch(rpm01));
        }

        /// <summary>Starts the tyre screech loop silent; drive it with <see cref="UpdateScreech"/>.</summary>
        public VoiceHandle StartScreech(Vector3 position)
        {
            return _p.StartLoop(SoundId.ScreechLoop, true, position, WorldCategory.Vehicle, VolumeGroup.Vehicles, 0f, 1f, false);
        }

        /// <summary>Per frame: screech loudness from lateral slip 0..1 (silent below 0.15), pitch rising slightly with slip.</summary>
        public void UpdateScreech(VoiceHandle screech, Vector3 position, float slip01)
        {
            float s = Mathf.Clamp01((slip01 - 0.15f) / 0.85f);
            _p.SetLoop(screech, position, s, 0.9f + 0.2f * s);
        }

        /// <summary>Moves a loop without changing its volume or pitch.</summary>
        public void Move(VoiceHandle loop, Vector3 position) { _p.MoveLoop(loop, position); }

        /// <summary>Stops a loop and clears the handle.</summary>
        public void Stop(ref VoiceHandle loop) { _p.StopLoop(ref loop); }

        /// <summary>An interface sound.</summary>
        public void Ui(UiSound sound) { _p.PlayUi(SoundBank.Ui(sound), 1f); }

        /// <summary>
        /// Entering (true) or leaving an interior: CS1's city audio is ducked and the room tone runs while inside; leaving
        /// restores CS1 exactly.
        /// </summary>
        public void SetIndoors(bool indoors)
        {
            if (indoors == _indoors && (!indoors || _p.IsLive(_roomTone))) return;
            _indoors = indoors;
            _p.DuckCityAmbience(indoors);
            if (indoors) _roomTone = _p.StartLoop(SoundId.RoomToneLoop, false, Vector3.zero, WorldCategory.Foot, VolumeGroup.Effects, 1f, 1f, false);
            else _p.StopLoop(ref _roomTone);
        }

        /// <summary>Pauses (true) or resumes world sounds; UI sounds keep playing.</summary>
        public void SetPaused(bool paused) { _p.SetPaused(paused); }

        /// <summary>
        /// Per frame from Update: plays the CS1 vehicle's own engine effect for this pose (see <see cref="Cs1VehicleEngine"/>).
        /// False when the vehicle has no such effect; use <see cref="StartEngine"/> instead.
        /// </summary>
        public bool TryPlayVehicleEngine(VehicleInfo info, Vector3 position, Vector3 forward, Vector3 velocity, float acceleration)
        {
            return Cs1VehicleEngine.TryPlay(info, position, forward, velocity, acceleration);
        }
    }
}
