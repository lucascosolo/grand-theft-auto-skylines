using System;
using GtaSkylines.Game.Mathx;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>Weapons with a first-person view model.</summary>
    public enum ViewWeapon
    {
        /// <summary>Nothing drawn.</summary>
        Fists,
        /// <summary>Baseball bat.</summary>
        Bat,
        /// <summary>Pistol.</summary>
        Pistol,
        /// <summary>Sub-machine gun.</summary>
        Smg,
        /// <summary>Pump shotgun.</summary>
        Shotgun,
        /// <summary>Carbine rifle.</summary>
        Carbine,
    }

    /// <summary>View model offset from its rest pose.</summary>
    public struct ViewPose
    {
        /// <summary>Offset in view space (x right, y up, z forward), metres.</summary>
        public Vec3 Offset;
        /// <summary>Muzzle-up rotation, degrees.</summary>
        public float PitchUpDeg;
    }

    /// <summary>Procedural low-poly weapon meshes (grip at the origin, barrel along +z) and their recoil/reload motion.</summary>
    public static class WeaponModels
    {
        private const float RecoilWindow = 0.4f;
        private const float RecoilDecay = 0.08f;
        private const float SwingTime = 0.35f;

        /// <summary>The weapon's mesh; empty for fists.</summary>
        public static SimpleMesh Build(ViewWeapon w)
        {
            var b = new MeshBuilder();
            switch (w)
            {
                case ViewWeapon.Bat:
                    b.AddCylinderZ(new Vec3(0f, 0f, 0.15f), 0.017f, 0.3f, 8);
                    b.AddCylinderZ(new Vec3(0f, 0f, 0.575f), 0.03f, 0.55f, 8);
                    break;
                case ViewWeapon.Pistol:
                    b.AddBox(new Vec3(0f, 0.03f, 0.05f), new Vec3(0.03f, 0.035f, 0.2f));
                    b.AddBox(new Vec3(0f, -0.03f, -0.02f), new Vec3(0.028f, 0.1f, 0.04f));
                    break;
                case ViewWeapon.Smg:
                    b.AddBox(new Vec3(0f, 0.02f, 0.05f), new Vec3(0.04f, 0.06f, 0.3f));
                    b.AddCylinderZ(new Vec3(0f, 0.03f, 0.25f), 0.01f, 0.1f, 6);
                    b.AddBox(new Vec3(0f, -0.06f, 0.06f), new Vec3(0.03f, 0.12f, 0.04f));
                    b.AddBox(new Vec3(0f, -0.04f, -0.06f), new Vec3(0.03f, 0.08f, 0.035f));
                    break;
                case ViewWeapon.Shotgun:
                    b.AddBox(new Vec3(0f, 0f, -0.2f), new Vec3(0.04f, 0.07f, 0.28f));
                    b.AddBox(new Vec3(0f, 0.02f, 0.04f), new Vec3(0.045f, 0.06f, 0.2f));
                    b.AddCylinderZ(new Vec3(0f, 0.035f, 0.415f), 0.012f, 0.55f, 6);
                    b.AddCylinderZ(new Vec3(0f, 0.005f, 0.35f), 0.02f, 0.2f, 6);
                    break;
                case ViewWeapon.Carbine:
                    b.AddBox(new Vec3(0f, 0f, -0.2f), new Vec3(0.04f, 0.07f, 0.22f));
                    b.AddBox(new Vec3(0f, 0.02f, 0.06f), new Vec3(0.045f, 0.07f, 0.3f));
                    b.AddCylinderZ(new Vec3(0f, 0.03f, 0.41f), 0.01f, 0.4f, 6);
                    b.AddBox(new Vec3(0f, -0.08f, 0.08f), new Vec3(0.03f, 0.15f, 0.05f));
                    b.AddBox(new Vec3(0f, 0.07f, 0.05f), new Vec3(0.02f, 0.03f, 0.08f));
                    break;
            }
            return b.ToMesh();
        }

        /// <summary>Muzzle position in the weapon's frame (zero for fists and bat).</summary>
        public static Vec3 Muzzle(ViewWeapon w)
        {
            switch (w)
            {
                case ViewWeapon.Pistol: return new Vec3(0f, 0.03f, 0.15f);
                case ViewWeapon.Smg: return new Vec3(0f, 0.03f, 0.3f);
                case ViewWeapon.Shotgun: return new Vec3(0f, 0.035f, 0.69f);
                case ViewWeapon.Carbine: return new Vec3(0f, 0.03f, 0.61f);
                default: return Vec3.Zero;
            }
        }

        /// <summary>Recoil (decaying kick and climb), bat swing and reload dip.</summary>
        public static ViewPose Pose(ViewWeapon w, float sinceShotS, float reloadProgress)
        {
            var pose = new ViewPose();
            if (w == ViewWeapon.Fists) return pose;
            if (w == ViewWeapon.Bat)
            {
                if (sinceShotS >= 0f && sinceShotS < SwingTime) pose.PitchUpDeg = -80f * (float)Math.Sin(Math.PI * sinceShotS / SwingTime);
                return pose;
            }
            if (sinceShotS >= 0f && sinceShotS < RecoilWindow)
            {
                float kick, climb;
                Recoil(w, out kick, out climb);
                float e = (float)Math.Exp(-sinceShotS / RecoilDecay);
                pose.Offset = new Vec3(0f, 0f, -kick * e);
                pose.PitchUpDeg = climb * e;
            }
            if (reloadProgress >= 0f && reloadProgress <= 1f)
            {
                float s = (float)Math.Sin(Math.PI * reloadProgress);
                pose.Offset = pose.Offset + new Vec3(0f, -0.2f * s, 0f);
                pose.PitchUpDeg -= 35f * s;
            }
            return pose;
        }

        private static void Recoil(ViewWeapon w, out float kick, out float climb)
        {
            switch (w)
            {
                case ViewWeapon.Pistol: kick = 0.04f; climb = 6f; return;
                case ViewWeapon.Smg: kick = 0.025f; climb = 3f; return;
                case ViewWeapon.Shotgun: kick = 0.09f; climb = 12f; return;
                default: kick = 0.035f; climb = 4f; return;
            }
        }
    }
}
