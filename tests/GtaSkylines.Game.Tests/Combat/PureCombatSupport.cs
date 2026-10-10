using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Physics;
using GtaSkylines.Game.World;

namespace GtaSkylines.Game.Tests.Combat
{
    /// <summary>Peds that need no GameWorld (the constructor is internal to the Game assembly).</summary>
    internal static class Bare
    {
        public static Ped Ped(float x = 0f, float y = 0f, float z = 0f, PedKind kind = PedKind.Civilian)
        {
            var p = new Ped(7, kind, 0, new CharacterMotor(new MotorSettings()));
            p.Motor.Teleport(new Vec3(x, y, z));
            return p;
        }
    }
}
