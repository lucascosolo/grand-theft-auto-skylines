using UnityEngine;

namespace GtaSkylines.Mod.Rendering
{
    /// <summary>
    /// Our slot in CS1's manager rendering. <c>RenderManager.LateUpdate</c> (RenderManager.cs:572-687) clears the light
    /// buffer and resets <c>PrefabPool.m_canCreateInstances</c>, calls every registered manager's
    /// <c>BeginRendering(cameraInfo)</c>, renders the groups, calls every <c>EndRendering(cameraInfo)</c>, then
    /// <c>LightSystem.EndRendering</c>. Drawing from <see cref="EndRendering"/> therefore gets the frame's
    /// <c>CameraInfo</c> and lets <c>Vehicle.RenderInstance</c> effects and <c>LightSystem.DrawLight</c> lights land in
    /// this frame. <c>RegisterRenderableManager</c> (:475) appends to a static list that is never cleared and has no
    /// unregister, so the hook is registered once per process and forwards to <see cref="Target"/> (null = idle).
    /// </summary>
    internal sealed class RenderHook : IRenderableManager
    {
        private static RenderHook s_instance;

        /// <summary>What draws this frame; null when not playing.</summary>
        public static Renderer Target;

        /// <summary>Registers the hook (once per process).</summary>
        public static void Install()
        {
            if (s_instance != null) return;
            s_instance = new RenderHook();
            RenderManager.RegisterRenderableManager(s_instance);
            ModRuntime.Log.Info("render: hook registered with RenderManager");
        }

        public string GetName() { return "GtaSkylines"; }

        public DrawCallData GetDrawCallData() { return default(DrawCallData); }

        public void BeginRendering(RenderManager.CameraInfo cameraInfo) { }

        public void EndRendering(RenderManager.CameraInfo cameraInfo)
        {
            Renderer r = Target;
            if (r == null) return;
            try { r.RenderFromHook(cameraInfo); }
            catch (System.Exception e) { r.Fault("hook", e); }
        }

        public void BeginOverlay(RenderManager.CameraInfo cameraInfo) { }

        public void EndOverlay(RenderManager.CameraInfo cameraInfo) { }

        public void UndergroundOverlay(RenderManager.CameraInfo cameraInfo) { }

        public void CheckReferences() { }

        public void InitRenderData() { }

        public bool CalculateGroupData(int groupX, int groupZ, int layer, ref int vertexCount, ref int triangleCount, ref int objectCount, ref RenderGroup.VertexArrays vertexArrays)
        {
            return false;
        }

        public void PopulateGroupData(int groupX, int groupZ, int layer, ref int vertexIndex, ref int triangleIndex, Vector3 groupPosition, RenderGroup.MeshData data, ref Vector3 min, ref Vector3 max, ref float maxRenderDistance, ref float maxInstanceDistance, ref bool requireSurfaceMaps)
        {
        }
    }
}
