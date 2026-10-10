using System;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Settings;
using Skylines.Host.Camera;
using UnityEngine;

namespace GtaSkylines.Mod.Lifecycle
{
    /// <summary>What runs while play mode is on. The lifecycle owns entering and leaving; the session owns the player.</summary>
    public interface IPlaySession
    {
        /// <summary>Starts at <paramref name="spawnFeet"/> facing <paramref name="yawDeg"/> (Unity euler y).</summary>
        void Begin(Vector3 spawnFeet, float yawDeg);
        /// <summary>Per frame from Update.</summary>
        void Tick(float dt, InputFrame input);
        /// <summary>Per frame from LateUpdate: drive the camera here.</summary>
        void LateTick();
        /// <summary>From OnGUI.</summary>
        void Gui();
        /// <summary>Play mode is ending (also on unload or errors).</summary>
        void End();
        /// <summary>True once the player chose to return to the city builder.</summary>
        bool WantsExit { get; }
        /// <summary>The profile as a <c>ProfileRecord</c>.</summary>
        byte[] SaveProfile();
        /// <summary>A <c>ProfileRecord</c>, or null for a fresh profile.</summary>
        void LoadProfile(byte[] data);
    }

    /// <summary>What the lifecycle lends a session.</summary>
    public sealed class SessionServices
    {
        /// <summary>The acquired city camera.</summary>
        public CameraTakeover Camera;
        /// <summary>Global settings (live; changes apply immediately).</summary>
        public GameSettings Settings;
        /// <summary>True while a menu is open: frees the cursor and pauses the simulation; false undoes it.</summary>
        public Action<bool> SetMenuOpen;
    }
}
