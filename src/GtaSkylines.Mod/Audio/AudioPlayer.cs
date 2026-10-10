using System;
using ColossalFramework;
using UnityEngine;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>A looping voice. Default (unset) and stale handles are ignored by every call.</summary>
    public struct VoiceHandle
    {
        internal int Index;
        internal int Generation;

        /// <summary>True once returned by a start call (it may have been stopped since).</summary>
        public bool IsSet { get { return Generation != 0; } }
    }

    /// <summary>
    /// Plays the <see cref="ClipBank"/> through pooled <see cref="AudioSource"/>s on child objects: 2D one-shots for UI, 3D
    /// one-shots for the world (linear rolloff, per-category range), and looping voices with handles. The listener is CS1's.
    /// Applies the settings volumes when <see cref="ModRuntime.SettingsVersion"/> moves, pauses world sounds while the
    /// simulation is paused (menus) or <see cref="SetPaused"/> asks, and owns the CS1 ambience ducker. Main thread only;
    /// nothing allocates per frame once the clips exist.
    /// </summary>
    public sealed class AudioPlayer : MonoBehaviour
    {
        private const int UiVoices = 6, WorldVoices = 20, LoopVoices = 10, MaxLoggedErrors = 20;

        private sealed class Voice
        {
            public AudioSource Src;
            public SoundId Id;
            public VolumeGroup Group;
            public float Volume;
            public bool Pausable;
            public bool Busy;
            public bool PausedByUs;
            public int Generation;
        }

        private readonly ClipBank _bank = new ClipBank();
        private readonly AmbienceDucker _ducker = new AmbienceDucker();
        private readonly float[] _gain = new float[Enum.GetValues(typeof(VolumeGroup)).Length];
        private readonly System.Random _rng = new System.Random(20261010);
        private Voice[] _ui, _world, _loops;
        private int _uiNext, _worldNext, _settingsVersion = -1, _errors;
        private bool _pauseRequested, _paused;

        /// <summary>Creates the player under <paramref name="parent"/> and starts background synthesis.</summary>
        public static AudioPlayer Create(Transform parent)
        {
            var go = new GameObject("GtaSkylines.Audio");
            go.transform.parent = parent;
            AudioPlayer p = go.AddComponent<AudioPlayer>();
            p.Init();
            return p;
        }

        /// <summary>True once the clips exist; until then every play call is a no-op and start calls return unset handles.</summary>
        public bool Ready { get { return _bank.Ready; } }

        /// <summary>True while world sounds are paused.</summary>
        public bool Paused { get { return _paused; } }

        private void Init()
        {
            _ui = new Voice[UiVoices];
            for (int i = 0; i < UiVoices; i++) _ui[i] = NewVoice(gameObject, false, false);
            _world = new Voice[WorldVoices];
            for (int i = 0; i < WorldVoices; i++) _world[i] = NewVoice(Child("World" + i), true, false);
            _loops = new Voice[LoopVoices];
            for (int i = 0; i < LoopVoices; i++) _loops[i] = NewVoice(Child("Loop" + i), true, true);
            _bank.Start();
        }

        private GameObject Child(string name)
        {
            var go = new GameObject(name);
            go.transform.parent = transform;
            return go;
        }

        private static Voice NewVoice(GameObject host, bool pausable, bool loop)
        {
            AudioSource s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            return new Voice { Src = s, Pausable = pausable };
        }

        /// <summary>Asks for world sounds to pause (true) or resume; a paused simulation pauses them as well.</summary>
        public void SetPaused(bool paused) { _pauseRequested = paused; }

        /// <summary>Ducks CS1's own world audio (true) or restores it exactly.</summary>
        public void DuckCityAmbience(bool duck) { _ducker.Set(duck); }

        /// <summary>Plays a UI one-shot (never paused).</summary>
        public void PlayUi(SoundId id, float volume) { PlayUi(id, VolumeGroup.Ui, volume); }

        /// <summary>Plays a UI one-shot on <paramref name="group"/> (never paused).</summary>
        public void PlayUi(SoundId id, VolumeGroup group, float volume)
        {
            AudioClip clip = Pick(id);
            if (clip == null) return;
            Voice v = _ui[_uiNext];
            _uiNext = (_uiNext + 1) % _ui.Length;
            Start(v, id, clip, group, AudioMix.BaseVolume(id) * volume, 1f);
        }

        /// <summary>Plays a world one-shot at <paramref name="position"/>; dropped while paused.</summary>
        public void PlayAt(SoundId id, Vector3 position, WorldCategory category, VolumeGroup group, float volume, float pitch)
        {
            AudioClip clip = Pick(id);
            if (clip == null || _paused) return;
            Voice v = FreeWorldVoice();
            v.Src.transform.position = position;
            Spatial(v.Src, category, false);
            Start(v, id, clip, group, AudioMix.BaseVolume(id) * volume, pitch);
        }

        /// <summary>
        /// Starts a looping voice: 3D at <paramref name="position"/> when <paramref name="spatial"/>, else 2D (room tone).
        /// <paramref name="doppler"/> enables Unity's Doppler shift (sirens). Returns an unset handle when no voice is free.
        /// </summary>
        public VoiceHandle StartLoop(SoundId id, bool spatial, Vector3 position, WorldCategory category, VolumeGroup group, float volume, float pitch, bool doppler)
        {
            AudioClip clip = Pick(id);
            if (clip == null) return default(VoiceHandle);
            for (int i = 0; i < _loops.Length; i++)
            {
                Voice v = _loops[i];
                if (v.Busy) continue;
                v.Busy = true;
                v.Generation++;
                if (v.Generation == 0) v.Generation = 1;
                v.Src.transform.position = position;
                if (spatial) Spatial(v.Src, category, doppler);
                else
                {
                    v.Src.spatialBlend = 0f;
                    v.Src.dopplerLevel = 0f;
                }
                Start(v, id, clip, group, AudioMix.BaseVolume(id) * volume, pitch);
                if (_paused)
                {
                    v.Src.Pause();
                    v.PausedByUs = true;
                }
                return new VoiceHandle { Index = i, Generation = v.Generation };
            }
            return default(VoiceHandle);
        }

        /// <summary>Moves a loop and sets its volume (before group gain) and pitch. Per frame; no allocation.</summary>
        public void SetLoop(VoiceHandle h, Vector3 position, float volume, float pitch)
        {
            Voice v = Live(h);
            if (v == null) return;
            v.Src.transform.position = position;
            v.Volume = AudioMix.BaseVolume(v.Id) * volume;
            v.Src.volume = v.Volume * _gain[(int)v.Group];
            v.Src.pitch = pitch;
        }

        /// <summary>Moves a loop, keeping its volume and pitch.</summary>
        public void MoveLoop(VoiceHandle h, Vector3 position)
        {
            Voice v = Live(h);
            if (v != null) v.Src.transform.position = position;
        }

        /// <summary>Stops a loop and clears the handle.</summary>
        public void StopLoop(ref VoiceHandle h)
        {
            Voice v = Live(h);
            h = default(VoiceHandle);
            if (v == null) return;
            v.Src.Stop();
            v.Busy = false;
            v.PausedByUs = false;
        }

        /// <summary>True while the handle names a playing loop.</summary>
        public bool IsLive(VoiceHandle h) { return Live(h) != null; }

        private Voice Live(VoiceHandle h)
        {
            if (!h.IsSet || _loops == null || h.Index < 0 || h.Index >= _loops.Length) return null;
            Voice v = _loops[h.Index];
            return v.Busy && v.Generation == h.Generation ? v : null;
        }

        private AudioClip Pick(SoundId id)
        {
            if (!_bank.Ready) return null;
            return _bank.Get(id, _rng.Next(SoundBank.VariantCount(id)));
        }

        private Voice FreeWorldVoice()
        {
            for (int i = 0; i < _world.Length; i++)
            {
                Voice v = _world[(_worldNext + i) % _world.Length];
                if (!v.Src.isPlaying && !v.PausedByUs)
                {
                    _worldNext = (_worldNext + i + 1) % _world.Length;
                    return v;
                }
            }
            Voice oldest = _world[_worldNext];
            _worldNext = (_worldNext + 1) % _world.Length;
            return oldest;
        }

        private static void Spatial(AudioSource s, WorldCategory c, bool doppler)
        {
            s.spatialBlend = 1f;
            s.minDistance = AudioMix.MinDistance(c);
            s.maxDistance = AudioMix.MaxDistance(c);
            s.dopplerLevel = doppler ? 1f : 0f;
        }

        private void Start(Voice v, SoundId id, AudioClip clip, VolumeGroup group, float volume, float pitch)
        {
            v.Id = id;
            v.Group = group;
            v.Volume = volume;
            v.PausedByUs = false;
            v.Src.clip = clip;
            v.Src.volume = volume * _gain[(int)group];
            v.Src.pitch = pitch;
            v.Src.Play();
        }

        private void Update()
        {
            try
            {
                _bank.Poll(ModRuntime.Log);
                if (_settingsVersion != ModRuntime.SettingsVersion) ApplySettings();
                bool simPaused = Singleton<SimulationManager>.exists
                                 && (Singleton<SimulationManager>.instance.SimulationPaused || Singleton<SimulationManager>.instance.ForcedSimulationPaused);
                bool paused = _pauseRequested || simPaused;
                if (paused != _paused) ApplyPause(paused);
                _ducker.Tick();
            }
            catch (Exception e)
            {
                if (_errors++ < MaxLoggedErrors) ModRuntime.Log.Error("AudioPlayer.Update", e);
            }
        }

        private void ApplySettings()
        {
            _settingsVersion = ModRuntime.SettingsVersion;
            for (int g = 0; g < _gain.Length; g++) _gain[g] = AudioMix.GroupGain(ModRuntime.Settings, (VolumeGroup)g);
            Reapply(_ui);
            Reapply(_world);
            Reapply(_loops);
        }

        private void Reapply(Voice[] voices)
        {
            for (int i = 0; i < voices.Length; i++) voices[i].Src.volume = voices[i].Volume * _gain[(int)voices[i].Group];
        }

        private void ApplyPause(bool paused)
        {
            _paused = paused;
            PauseAll(_world, paused);
            PauseAll(_loops, paused);
        }

        private static void PauseAll(Voice[] voices, bool paused)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (!v.Pausable) continue;
                if (paused && v.Src.isPlaying)
                {
                    v.Src.Pause();
                    v.PausedByUs = true;
                }
                else if (!paused && v.PausedByUs)
                {
                    v.Src.UnPause();
                    v.PausedByUs = false;
                }
            }
        }

        /// <summary>
        /// Stops every voice, restores CS1's ambience and destroys the clips. Called by the session on every exit path and
        /// again from OnDestroy; idempotent, never throws.
        /// </summary>
        public void Dispose()
        {
            Try("ducker", _ducker.Dispose);
            Try("voices", () =>
            {
                StopAll(_ui);
                StopAll(_world);
                StopAll(_loops);
            });
            Try("clips", _bank.Dispose);
        }

        private static void StopAll(Voice[] voices)
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice v = voices[i];
                if (v.Src == null) continue;
                v.Src.Stop();
                v.Src.clip = null;
                v.Busy = false;
                v.PausedByUs = false;
            }
        }

        private static void Try(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { ModRuntime.Log.Error("AudioPlayer.Dispose " + what, e); }
        }

        private void OnDestroy()
        {
            try { Dispose(); }
            catch (Exception e) { ModRuntime.Log.Error("AudioPlayer.OnDestroy", e); }
        }
    }
}
