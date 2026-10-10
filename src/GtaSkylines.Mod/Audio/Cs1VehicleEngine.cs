using ColossalFramework;
using UnityEngine;

namespace GtaSkylines.Mod.Audio
{
    /// <summary>
    /// Plays a CS1 vehicle's own engine sound for an arbitrary pose, the way <c>Vehicle.PlayAudio</c> does (Vehicle.cs:837-852):
    /// each <see cref="EngineSoundEffect"/> in <c>VehicleInfo.m_effects</c> is fed into <c>VehicleManager.m_audioGroup</c>, whose
    /// players CS1 updates in <c>AudioManager.LateUpdate</c>. Call it every frame from Update while the car should be heard.
    /// </summary>
    internal static class Cs1VehicleEngine
    {
        // Vehicle index 0 is never a real vehicle, so this player id cannot collide with CS1's own traffic.
        private static readonly InstanceID s_id = new InstanceID { Vehicle = 0 };

        /// <summary>
        /// Feeds this frame's engine sound. Returns false when CS1 is not ready or the vehicle has no unconditional engine
        /// effect; the caller then uses the procedural engine loop.
        /// </summary>
        public static bool TryPlay(VehicleInfo info, Vector3 position, Vector3 forward, Vector3 velocity, float acceleration)
        {
            if (info == null || info.m_effects == null || !Singleton<VehicleManager>.exists || !Singleton<AudioManager>.exists) return false;
            AudioManager.ListenerInfo listener = Singleton<AudioManager>.instance.CurrentListenerInfo;
            AudioGroup group = Singleton<VehicleManager>.instance.m_audioGroup;
            if (listener == null || group == null) return false;
            var area = new EffectInfo.SpawnArea(position, forward, 1f);
            bool played = false;
            for (int i = 0; i < info.m_effects.Length; i++)
            {
                VehicleInfo.Effect e = info.m_effects[i];
                if (!(e.m_effect is EngineSoundEffect) || e.m_vehicleFlagsRequired != 0) continue;
                e.m_effect.PlayEffect(s_id, area, velocity, acceleration, 1f, listener, group);
                played = true;
            }
            return played;
        }
    }
}
