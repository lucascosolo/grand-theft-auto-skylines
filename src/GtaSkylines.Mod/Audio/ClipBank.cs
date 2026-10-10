using System;
using System.Diagnostics;
using System.Threading;
using Skylines.Host;
using UnityEngine;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>
    /// Every <see cref="SoundBank"/> take as a Unity clip. The pure synthesis runs once on a background thread (it touches no
    /// Unity API); the main thread then creates the clips with <c>AudioClip.Create</c> + <c>SetData</c> and drops the buffers.
    /// </summary>
    internal sealed class ClipBank
    {
        private readonly float[][][] _buffers = new float[SoundBank.Count][][];
        private AudioClip[][] _clips;
        private Thread _thread;
        private volatile bool _synthDone;
        private Exception _synthError;
        private double _synthMs;
        private bool _disposed;

        /// <summary>True once every clip exists.</summary>
        public bool Ready { get { return _clips != null; } }

        /// <summary>Starts synthesis in the background. Main thread, once.</summary>
        public void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(Synthesise) { IsBackground = true, Name = "GtaSkylines.AudioSynth" };
            _thread.Start();
        }

        private void Synthesise()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                for (int i = 0; i < SoundBank.Count; i++)
                {
                    var id = (SoundId)i;
                    var takes = new float[SoundBank.VariantCount(id)][];
                    for (int v = 0; v < takes.Length; v++) takes[v] = SoundBank.Render(id, v);
                    _buffers[i] = takes;
                }
            }
            catch (Exception e) { _synthError = e; }
            _synthMs = sw.Elapsed.TotalMilliseconds;
            _synthDone = true;
        }

        /// <summary>Main thread, per frame until <see cref="Ready"/>: creates the clips once synthesis has finished.</summary>
        public void Poll(HostLog log)
        {
            if (_disposed || _clips != null || !_synthDone) return;
            if (_synthError != null)
            {
                log.Error("audio synthesis failed; the mod plays no sound", _synthError);
                _synthError = null;
                _thread = null;
                return;
            }
            if (_buffers[0] == null) return;
            var sw = Stopwatch.StartNew();
            var clips = new AudioClip[SoundBank.Count][];
            long samples = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                float[][] takes = _buffers[i];
                clips[i] = new AudioClip[takes.Length];
                for (int v = 0; v < takes.Length; v++)
                {
                    AudioClip c = AudioClip.Create("GtaSkylines." + (SoundId)i + "." + v, takes[v].Length, 1, SoundBank.SampleRate, false);
                    c.SetData(takes[v], 0);
                    clips[i][v] = c;
                    samples += takes[v].Length;
                }
                _buffers[i] = null;
            }
            _clips = clips;
            log.Info("audio: synthesised " + samples + " samples in " + _synthMs.ToString("F0") + " ms (background), clips created in "
                     + sw.Elapsed.TotalMilliseconds.ToString("F0") + " ms (main thread)");
        }

        /// <summary>Destroys every clip (the bank is per play session) and stops creating them. Main thread; idempotent.</summary>
        public void Dispose()
        {
            _disposed = true;
            AudioClip[][] clips = _clips;
            _clips = null;
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
                for (int v = 0; v < clips[i].Length; v++)
                    if (clips[i][v] != null) UnityEngine.Object.Destroy(clips[i][v]);
        }

        /// <summary>A take of <paramref name="id"/>; null before <see cref="Ready"/>.</summary>
        public AudioClip Get(SoundId id, int variant)
        {
            if (_clips == null) return null;
            AudioClip[] takes = _clips[(int)id];
            return takes[variant % takes.Length];
        }
    }
}
