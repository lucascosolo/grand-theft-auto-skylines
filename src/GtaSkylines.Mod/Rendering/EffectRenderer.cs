using GtaSkylines.Game.Mathx;
using UnityEngine;
using UnityEngine.Rendering;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Markers, tracers, muzzle flashes (billboard plus a CS1 point light via <c>LightSystem.DrawLight</c>, LightSystem.cs:223),
    /// impact puffs, explosions, car smoke/fire and the first-person view model. Unlit translucent billboards with
    /// Internal-Colored (Unity's particle shaders are not in CS1's build). Transient effects live in a fixed pool fed
    /// from each frame's event lists; nothing allocates per frame. Main thread only.
    /// </summary>
    internal sealed class EffectRenderer
    {
        private enum Kind : byte { None, Tracer, Flash, Impact, Explosion }

        private struct Fx
        {
            public Kind Kind;
            public Vector3 A, B;
            public float Age;
        }

        private const int PoolSize = 256;
        private static readonly Color FireColour = new Color(1f, 0.55f, 0.15f, 0.85f);
        private static readonly Color SmokeColour = new Color(0.25f, 0.25f, 0.25f, 0.55f);
        private static readonly Color BeamColour = new Color(1f, 0.97f, 0.8f, 0.14f);
        private static readonly Color FlashColour = new Color(1f, 0.85f, 0.45f, 1f);

        private readonly MaterialKit _kit;
        private readonly Fx[] _pool = new Fx[PoolSize];
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly Mesh[] _weapons = new Mesh[6];
        private Material _weaponMaterial;
        private Mesh _quad, _cylinder, _ring, _arrow, _cone;
        private int _next;

        public EffectRenderer(MaterialKit kit)
        {
            _kit = kit;
        }

        /// <summary>Takes this frame's events into the pool and ages everything (Update phase).</summary>
        public void Ingest(RenderFrame f)
        {
            float dt = f.DeltaTime > 0f ? f.DeltaTime : Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                if (_pool[i].Kind != Kind.None) _pool[i].Age += dt;
            }
            for (int i = 0; i < f.Shots.Count; i++) Spawn(Kind.Tracer, MaterialKit.V(f.Shots[i].From), MaterialKit.V(f.Shots[i].To));
            for (int i = 0; i < f.MuzzleFlashes.Count; i++) Spawn(Kind.Flash, MaterialKit.V(f.MuzzleFlashes[i].Position), Vector3.zero);
            for (int i = 0; i < f.Impacts.Count; i++) Spawn(Kind.Impact, MaterialKit.V(f.Impacts[i].Position), Vector3.zero);
            for (int i = 0; i < f.Explosions.Count; i++) Spawn(Kind.Explosion, MaterialKit.V(f.Explosions[i].Position), Vector3.zero);
        }

        public void Draw(RenderFrame f, Camera cam)
        {
            Material mat = _kit.Translucent;
            if (mat == null || cam == null) return;
            EnsureMeshes();
            Transform ct = cam.transform;
            for (int i = 0; i < f.Markers.Count; i++) DrawMarker(f.Markers[i], mat);
            for (int i = 0; i < PoolSize; i++) DrawFx(ref _pool[i], ct, mat);
            for (int i = 0; i < f.Cars.Count; i++) DrawDamage(f.Cars[i], f.Alpha, ct, mat);
            DrawBeam(f.Heli, f.Alpha, mat);
        }

        public void DrawViewModel(ViewModelDraw v, Camera cam)
        {
            if (!v.Visible || v.Weapon == ViewWeapon.Fists || cam == null) return;
            int w = (int)v.Weapon;
            if (_weapons[w] == null) _weapons[w] = _kit.Mesh(WeaponModels.Build(v.Weapon), "GtaSkylines.Weapon." + v.Weapon);
            if (_weaponMaterial == null)
            {
                Texture2D grey = _kit.Own(Skylines.Host.Rendering.TextureUtil.Solid(new Color32(60, 60, 64, 255), false, "GtaSkylines.WeaponGrey"));
                _weaponMaterial = _kit.Lit(grey, "GtaSkylines.Weapon");
                if (_weaponMaterial == null) return;
            }
            ViewPose pose = WeaponModels.Pose(v.Weapon, v.SinceShotS, v.ReloadProgress);
            Transform t = cam.transform;
            Vector3 local = new Vector3(0.16f, -0.17f, 0.32f) + MaterialKit.V(pose.Offset);
            Matrix4x4 m = Matrix4x4.TRS(t.TransformPoint(local), t.rotation * Quaternion.Euler(-pose.PitchUpDeg, 0f, 0f), Vector3.one);
            Graphics.DrawMesh(_weapons[w], m, _weaponMaterial, _kit.PropLayer, cam, 0, null, ShadowCastingMode.Off, false);
        }

        public void Clear()
        {
            for (int i = 0; i < PoolSize; i++) _pool[i].Kind = Kind.None;
        }

        /// <summary>Forget meshes and materials (the kit destroys them).</summary>
        public void Release()
        {
            Clear();
            _quad = _cylinder = _ring = _arrow = _cone = null;
            for (int i = 0; i < _weapons.Length; i++) _weapons[i] = null;
            _weaponMaterial = null;
        }

        private void Spawn(Kind k, Vector3 a, Vector3 b)
        {
            _pool[_next] = new Fx { Kind = k, A = a, B = b, Age = 0f };
            _next = (_next + 1) % PoolSize;
        }

        private void DrawMarker(MarkerDraw mk, Material mat)
        {
            Color c = MaterialKit.Rgb(mk.ColourRgb, mk.Alpha);
            Vector3 p = MaterialKit.V(mk.Position);
            Quaternion yaw = Quaternion.Euler(0f, mk.YawDeg, 0f);
            float r = mk.Radius > 0f ? mk.Radius : 1f;
            switch (mk.Kind)
            {
                case MarkerKind.Door:
                    float bob = 0.1f * Mathf.Sin(Time.time * 3f);
                    Quad(_arrow, Matrix4x4.TRS(p + new Vector3(0f, 0.05f + bob * 0.2f, 0f), yaw, new Vector3(r, 1f, r)), c, mat);
                    break;
                case MarkerKind.Cylinder:
                    Quad(_cylinder, Matrix4x4.TRS(p, Quaternion.identity, new Vector3(r, 1f, r)), c, mat);
                    break;
                case MarkerKind.Ring:
                    Quad(_ring, Matrix4x4.TRS(p, yaw, new Vector3(r, r, 1f)), c, mat);
                    break;
            }
        }

        private void DrawFx(ref Fx fx, Transform cam, Material mat)
        {
            float scale, alpha;
            switch (fx.Kind)
            {
                case Kind.Tracer:
                    alpha = EffectMath.TracerAlpha(fx.Age);
                    if (alpha <= 0f) { fx.Kind = Kind.None; return; }
                    Vector3 d = fx.B - fx.A;
                    float len = d.magnitude;
                    if (len < 0.01f) return;
                    Vector3 mid = (fx.A + fx.B) * 0.5f;
                    Vector3 dir = d / len;
                    Vector3 toCam = Vector3.ProjectOnPlane(mid - cam.position, dir);
                    if (toCam.sqrMagnitude < 1e-6f) toCam = cam.forward;
                    Quad(_quad, Matrix4x4.TRS(mid, Quaternion.LookRotation(toCam, dir), new Vector3(0.025f, len, 1f)),
                        new Color(1f, 0.92f, 0.6f, alpha), mat);
                    return;
                case Kind.Flash:
                    if (fx.Age > 0f) { fx.Kind = Kind.None; return; }
                    Billboard(fx.A, 0.35f, cam, FlashColour, mat);
                    if (RenderManager.exists)
                        RenderManager.instance.lightSystem.DrawLight(LightType.Point, fx.A, Vector3.down, Vector3.zero, FlashColour, 3f, 8f, 0f, 0f, false);
                    return;
                case Kind.Impact:
                    EffectMath.Impact(fx.Age, out scale, out alpha);
                    if (alpha <= 0f) { fx.Kind = Kind.None; return; }
                    Billboard(fx.A, scale, cam, new Color(0.6f, 0.56f, 0.5f, alpha), mat);
                    return;
                case Kind.Explosion:
                    float fs, fa, ss, sa;
                    EffectMath.ExplosionFlash(fx.Age, out fs, out fa);
                    EffectMath.ExplosionSmoke(fx.Age, out ss, out sa);
                    if (sa <= 0f && fa <= 0f) { fx.Kind = Kind.None; return; }
                    if (sa > 0f) Billboard(fx.A + new Vector3(0f, 1f + fx.Age, 0f), ss, cam, new Color(0.2f, 0.2f, 0.2f, sa), mat);
                    if (fa > 0f)
                    {
                        Billboard(fx.A, fs, cam, new Color(1f, 0.7f, 0.3f, fa), mat);
                        if (RenderManager.exists)
                            RenderManager.instance.lightSystem.DrawLight(LightType.Point, fx.A + Vector3.up, Vector3.down, Vector3.zero, FlashColour, 6f * fa, 30f, 0f, 0f, false);
                    }
                    return;
            }
        }

        private void DrawDamage(CarDraw c, float alpha, Transform cam, Material mat)
        {
            bool fire = c.Damage == CarDamage.Burning || (c.Damage == CarDamage.Exploded && EffectMath.WreckBurning(c.SecondsSinceExplosion));
            bool smoke = fire || c.Damage == CarDamage.Smoking;
            if (!smoke) return;
            Vector3 p = MaterialKit.V(Interp.Position(c.PrevCentre, c.Centre, alpha));
            float t = Time.time;
            float column = c.Damage == CarDamage.Exploded ? 2f : 1f;
            for (int k = 0; k < 3; k++)
            {
                float phase = (t * 0.6f + k / 3f) % 1f;
                Billboard(p + new Vector3(0f, 0.8f + phase * 3f * column, 0f), (0.6f + phase * 1.6f) * column, cam,
                    new Color(SmokeColour.r, SmokeColour.g, SmokeColour.b, SmokeColour.a * (1f - phase)), mat);
            }
            if (fire)
            {
                float flicker = 0.85f + 0.15f * Mathf.Sin(t * 23f);
                Billboard(p + new Vector3(0f, 0.6f * column, 0f), 1.1f * column * flicker, cam, FireColour, mat);
                if (RenderManager.exists)
                    RenderManager.instance.lightSystem.DrawLight(LightType.Point, p + Vector3.up, Vector3.down, Vector3.zero, FireColour, 2f * flicker, 12f, 0f, 0f, false);
            }
        }

        private void DrawBeam(HeliDraw h, float alpha, Material mat)
        {
            if (!h.Visible || !h.Spotlight) return;
            Vec3 from = Interp.Position(h.PrevPosition, h.Position, alpha) + new Vec3(0f, -1.5f, 0f);
            Quat rot;
            float radius, length;
            if (!HeliMath.Beam(from, h.SpotlightTarget, out rot, out radius, out length)) return;
            Vector3 origin = MaterialKit.V(from);
            Quad(_cone, Matrix4x4.TRS(origin, MaterialKit.Q(rot), new Vector3(radius, radius, length)), BeamColour, mat);
            if (RenderManager.exists)
                RenderManager.instance.lightSystem.DrawLight(LightType.Spot, origin, MaterialKit.V((h.SpotlightTarget - from).Normalized), Vector3.zero,
                    BeamColour, 4f, length * 1.2f, HeliMath.BeamHalfAngleDeg * 2f, 0.3f, false);
        }

        private void Billboard(Vector3 p, float size, Transform cam, Color c, Material mat)
        {
            Quad(_quad, Matrix4x4.TRS(p, cam.rotation, new Vector3(size, size, 1f)), c, mat);
        }

        private void Quad(Mesh mesh, Matrix4x4 m, Color c, Material mat)
        {
            _block.Clear();
            _block.SetColor("_Color", c);
            Graphics.DrawMesh(mesh, m, mat, _kit.PropLayer, null, 0, _block, ShadowCastingMode.Off, false);
        }

        private void EnsureMeshes()
        {
            if (_quad != null) return;
            _quad = _kit.Mesh(MeshShapes.Quad(1f, 1f), "GtaSkylines.Quad");
            _cylinder = _kit.Mesh(MeshShapes.Cylinder(24, 1f, 3f), "GtaSkylines.MarkerCylinder");
            _ring = _kit.Mesh(MeshShapes.Ring(32, 0.85f, 1f), "GtaSkylines.Ring");
            _arrow = _kit.Mesh(MeshShapes.Arrow(1f), "GtaSkylines.Arrow");
            _cone = _kit.Mesh(MeshShapes.Cone(24), "GtaSkylines.HeliBeam");
        }
    }
}
