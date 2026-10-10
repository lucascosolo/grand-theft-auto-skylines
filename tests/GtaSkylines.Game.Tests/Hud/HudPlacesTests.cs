using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Hud;
using GtaSkylines.Game.Input;
using GtaSkylines.Game.Interiors;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.Tests.SessionTests;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.HudTests
{
    public class HudPlacesTests
    {
        private static BuildingDescriptor Shop(ushort startId, Vec3 at)
        {
            for (ushort id = startId; id < startId + 500; id++)
            {
                var b = new BuildingDescriptor { Id = id, Name = "Shop", InfoName = "Shop" + id, Service = ServiceKind.Commercial, SubService = "Low", Position = at };
                InteriorArchetype a = InteriorGenerator.ArchetypeFor(b);
                if (a == InteriorArchetype.ConvenienceStore || a == InteriorArchetype.ClothesShop || a == InteriorArchetype.GunStore) return b;
            }
            throw new System.InvalidOperationException("no shop archetype found");
        }

        private static int Count(List<HudBlip> blips, BlipKind kind)
        {
            int n = 0;
            for (int i = 0; i < blips.Count; i++) if (blips[i].Kind == kind) n++;
            return n;
        }

        [Fact]
        public void Heat_PlayerAndDistrictAreFractions()
        {
            var sim = new Sim(city: c => c.DistrictFunc = (x, z) => 3);
            sim.W.Wanted.PlayerHeat = 40f;
            sim.W.Wanted.SetDistrictHeat(3, 75f);
            sim.Run(0.1, In.Idle);
            Sim.Near(0.4f, sim.S.Hud.Heat, 1e-3f);
            Sim.Near(0.75f, sim.S.Hud.DistrictHeat, 1e-3f);
        }

        [Fact]
        public void ShopBlip_AppearsForNearbyShopArchetype_NotForFarOrResidential()
        {
            BuildingDescriptor near = Shop(1, new Vec3(40f, 0f, 20f));
            BuildingDescriptor far = Shop(1000, new Vec3(2000f, 0f, 0f));
            var house = new BuildingDescriptor { Id = 5000, InfoName = "Home", Service = ServiceKind.Residential, Position = new Vec3(10f, 0f, 10f) };
            var sim = new Sim(city: c => { c.AddBuilding(near); c.AddBuilding(far); c.AddBuilding(house); });
            sim.Run(0.1, In.Idle);
            List<HudBlip> b = sim.S.Hud.Blips;
            Assert.Equal(1, Count(b, BlipKind.Shop));
            Assert.Contains(b, x => x.Kind == BlipKind.Shop && (x.Position - near.Position).Length < 0.1f);
        }

        [Fact]
        public void SafehouseBlip_ComesFromOwnedProperties()
        {
            var home = new BuildingDescriptor { Id = 77, InfoName = "Home", Service = ServiceKind.Residential, Position = new Vec3(500f, 0f, 500f) };
            var sim = new Sim(city: c => c.AddBuilding(home), profile: p => p.Properties.Add(77));
            sim.Run(0.1, In.Idle);
            Assert.Contains(sim.S.Hud.Blips, x => x.Kind == BlipKind.Safehouse && (x.Position - home.Position).Length < 0.1f);
        }

        [Fact]
        public void CopBlips_OnlyWhileWanted()
        {
            var sim = new Sim();
            sim.W.SpawnPed(PedKind.Cop, 1, new Vec3(30f, 0f, 30f), 0f);
            sim.Run(0.1, In.Idle);
            Assert.Equal(0, Count(sim.S.Hud.Blips, BlipKind.Cop));
            sim.W.Wanted.Stars = 1;
            sim.Run(0.1, In.Idle);
            Assert.Equal(1, Count(sim.S.Hud.Blips, BlipKind.Cop));
            sim.W.Wanted.Stars = 0;
            sim.Run(0.1, In.Idle);
            Assert.Equal(0, Count(sim.S.Hud.Blips, BlipKind.Cop));
        }

        [Fact]
        public void ShopScan_RefreshesEveryFewSeconds_NotEveryStep()
        {
            var sim = new Sim();
            sim.Run(0.1, In.Idle);
            Assert.Equal(0, Count(sim.S.Hud.Blips, BlipKind.Shop));
            ((Fakes.FakeCity)sim.W.City).AddBuilding(Shop(1, new Vec3(30f, 0f, 30f)));
            sim.Run(1.0, In.Idle);
            Assert.Equal(0, Count(sim.S.Hud.Blips, BlipKind.Shop));
            sim.Run(HudSystem.PlacesRefreshSeconds, In.Idle);
            Assert.Equal(1, Count(sim.S.Hud.Blips, BlipKind.Shop));
        }
    }
}
