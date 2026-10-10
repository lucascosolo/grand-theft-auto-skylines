using GtaSkylines.Game.City;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Missions;
using GtaSkylines.Game.Tests.CityTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.Missions
{
    public class CivilianModelTests
    {
        [Fact]
        public void Mission_peds_without_a_fixed_model_never_wear_police_or_service_models()
        {
            // Only model 5 is a civilian: an unfiltered seeded pick hits a uniform or costume with probability 7/8.
            TestCity t = TestCity.Full();
            Roles.Apply(t.City, new[] { 0, 1, 3 }, new[] { 2, 4, 6, 7 });
            Scene s = Scene.Create(t);
            MissionContext ctx = s.Begin(new StealVehicleStep(VehicleKind.Car));
            for (uint i = 0; i < 12; i++)
            {
                var spec = new MissionActorSpec { Kind = ActorKind.Ped, Position = new Vec3(120f + 5f * i, 0f, 100f), Seed = i + 1 };
                Assert.True(ctx.SpawnActor(spec, 0));
                Ped ped = s.World.FindPed(ctx.ActorIds[0]);
                Assert.True(Roles.IsCivilian(s.City, ped.Model), "ped model " + ped.Model);
            }
        }
    }
}
