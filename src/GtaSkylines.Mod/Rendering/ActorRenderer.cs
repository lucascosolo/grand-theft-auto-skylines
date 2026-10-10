using System;
using System.Collections.Generic;
using ColossalFramework;
using GtaSkylines.Game.Mathx;
using UnityEngine;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Peds as CS1 citizen prefab instances and our cars through the static <c>Vehicle.RenderInstance</c>; called from
    /// <see cref="RenderHook"/> each frame. A prefab instance stays ours only while it is obtained every frame:
    /// <c>PrefabInfo.LateUpdate</c> (PrefabInfo.cs:190-200) releases an instance whose <c>m_instanceNeeded</c> was not set
    /// since its last LateUpdate, so a despawned ped is released by simply not drawing it. Main thread only.
    /// </summary>
    internal sealed class ActorRenderer
    {
        private readonly Func<int, CitizenInfo> _citizen;
        private readonly Func<int, VehicleInfo> _vehicle;
        private readonly Dictionary<int, int> _slotOfKey = new Dictionary<int, int>();
        private readonly Stack<int> _freeSlots = new Stack<int>();
        private readonly int[] _slotFrame = new int[PedInstanceIds.MaxSlots];
        private readonly int[] _slotKey = new int[PedInstanceIds.MaxSlots];
        private readonly List<int> _stale = new List<int>();
        private int _frame;

        public ActorRenderer(Func<int, CitizenInfo> citizen, Func<int, VehicleInfo> vehicle)
        {
            _citizen = citizen;
            _vehicle = vehicle;
            for (int i = PedInstanceIds.MaxSlots - 1; i >= 0; i--) _freeSlots.Push(i);
        }

        public void DrawPeds(RenderFrame f)
        {
            _frame++;
            for (int i = 0; i < f.Peds.Count; i++)
            {
                PedDraw p = f.Peds[i];
                if (!p.Visible) continue;
                CitizenInfo info = _citizen(p.ModelId);
                int slot = Slot(p.Key);
                if (info == null || slot < 0) continue;
                _slotFrame[slot] = _frame;
                var id = new InstanceID { RawData = PedInstanceIds.Raw(slot) };
                CitizenInfo inst = info.ObtainPrefabInstance<CitizenInfo>(id, 255);
                if (inst == null) continue; // created this frame (PrefabPool.cs:57-70 returns null) or pool full
                PedPose pose = PedAnimMap.Map(p.Anim, p.Velocity.LengthXZ);
                Vec3 pos = Interp.Position(p.PrevPosition, p.Position, f.Alpha);
                float yaw = Interp.YawDeg(p.PrevYawDeg, p.YawDeg, f.Alpha);
                Quat rot = pose.LieOnBack ? PedAnimMap.LyingRotation(yaw) : Quat.Yaw(yaw);
                if (pose.LieOnBack) pos = pos + new Vec3(0f, 0.15f, 0f);
                Vector3 vel = MaterialKit.Q(Quat.Yaw(yaw)) * new Vector3(0f, 0f, pose.Speed);
                inst.SetRenderParameters(MaterialKit.V(pos), MaterialKit.Q(rot), vel, info.m_color0,
                    (CitizenInfo.AnimationState)pose.Cs1State, false, true);
            }
            FreeStaleSlots();
        }

        public void DrawCars(RenderManager.CameraInfo cam, RenderFrame f)
        {
            for (int i = 0; i < f.Cars.Count; i++)
            {
                CarDraw c = f.Cars[i];
                VehicleInfo info = _vehicle(c.ModelId);
                if (info == null || info.m_mesh == null) continue;
                Quat rot = Quat.Slerp(c.PrevRotation, c.Rotation, Mathf.Clamp01(f.Alpha));
                Vec3 centre = Interp.Position(c.PrevCentre, c.Centre, f.Alpha);
                Bounds b = info.m_mesh.bounds;
                Vec3 origin = VehicleMath.OriginFromBoundsCentre(centre, rot, new Vec3(b.center.x, b.center.y, b.center.z));
                float gauge = info.m_generatedInfo != null ? info.m_generatedInfo.m_wheelGauge : 1.6f;
                float wheelBase = info.m_generatedInfo != null ? info.m_generatedInfo.m_wheelBase : 2.6f;
                Vec3 sway = VehicleMath.SwayFromWheels(c.CompressionFL, c.CompressionFR, c.CompressionRL, c.CompressionRR, gauge, wheelBase);
                float lx, ly, lz, lw;
                VehicleMath.LightState(c.Headlights && c.Damage != CarDamage.Exploded, c.Brake, 0, false, out lx, out ly, out lz, out lw);
                Vehicle.Flags flags = Vehicle.Flags.Created | Vehicle.Flags.Spawned;
                if (c.Siren && c.Damage != CarDamage.Exploded) flags |= Vehicle.Flags.Emergency2;
                int rgb = c.Damage == CarDamage.Exploded ? VehicleMath.ExplodedColour(c.ColourRgb) : c.ColourRgb;
                Vector3 vel = MaterialKit.V(c.Velocity);
                // InstanceID.Empty: RenderInstance indexes VehicleManager's buffer with id.Vehicle (Vehicle.cs:380), which
                // is 0 (CS1's unused slot) for a non-vehicle id, so no live vehicle's data is read or written.
                Vehicle.RenderInstance(cam, info, MaterialKit.V(origin), MaterialKit.Q(rot), MaterialKit.V(sway),
                    new Vector4(lx, ly, lz, lw), new Vector4(c.SteerRad, c.WheelTravelMetres, 0f, 0f), vel, c.Acceleration,
                    MaterialKit.Rgb(rgb, 0f), flags, (Vehicle.Flags2)0, ~1, InstanceID.Empty, false, true);
            }
        }

        public void Reset()
        {
            _slotOfKey.Clear();
            _freeSlots.Clear();
            for (int i = PedInstanceIds.MaxSlots - 1; i >= 0; i--) _freeSlots.Push(i);
        }

        private int Slot(int key)
        {
            int slot;
            if (_slotOfKey.TryGetValue(key, out slot)) return slot;
            if (_freeSlots.Count == 0) return -1;
            slot = _freeSlots.Pop();
            _slotOfKey[key] = slot;
            _slotKey[slot] = key;
            _slotFrame[slot] = _frame;
            return slot;
        }

        // A slot unused for 2 frames goes back to the free list; by then CS1 has released its instance.
        private void FreeStaleSlots()
        {
            if ((_frame & 31) != 0 || _slotOfKey.Count == 0) return;
            _stale.Clear();
            foreach (KeyValuePair<int, int> kv in _slotOfKey)
            {
                if (_frame - _slotFrame[kv.Value] > 2) _stale.Add(kv.Key);
            }
            for (int i = 0; i < _stale.Count; i++)
            {
                int slot = _slotOfKey[_stale[i]];
                _slotOfKey.Remove(_stale[i]);
                _freeSlots.Push(slot);
            }
        }
    }
}
