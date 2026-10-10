using System.Collections.Generic;
using GtaSkylines.Game.City;
using GtaSkylines.Game.Crime;
using GtaSkylines.Game.Mathx;
using GtaSkylines.Game.World;
using Xunit;

namespace GtaSkylines.Game.Tests.CrimeTests
{
    public class CrimeSystemTests
    {
        private static Scene NoPoliceScene(uint seed = 7u) { return new Scene(seed, policeCars: false); }

        // ---- knowledge: only witnessed crimes count ----------------------------------------------------------------

        [Fact]
        public void An_unwitnessed_crime_adds_no_stars_no_heat_and_no_calls()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(10, 0);
            s.Punch(civ);
            s.Run(15f, police: false);
            Assert.Equal(0, s.Wanted.Stars);
            Assert.Equal(0f, s.Wanted.PlayerHeat, 3);
            Assert.Equal(0f, s.Wanted.GetDistrictHeat(0), 3);
            Assert.Equal(1, s.Crime.CrimeCount);
            Assert.Equal(CrimeState.Unwitnessed, s.Crime.GetCrime(0).State);
            Assert.Empty(s.Peds.Calls);
            Assert.Equal(0, s.Crime.ActiveCallCount);
        }

        [Fact]
        public void A_civilian_call_gives_stars_only_when_it_completes_inside_the_four_to_ten_second_window()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(10, 0, witness: true, seesPlayer: true);
            s.Punch(civ);
            s.Step(police: false);
            Assert.Single(s.Peds.Calls);
            float callSeconds = s.Peds.Calls[0].CallSeconds;
            Assert.InRange(callSeconds, 4f, 10f);
            Assert.Equal(CrimeState.Reported, s.Crime.GetCrime(0).State);
            Assert.Equal(0, s.Wanted.Stars);

            float t = s.RunUntil(() => s.Wanted.Stars > 0, 12f);
            Assert.True(t > 0f, "the call never produced stars");
            Assert.InRange(t, callSeconds - 0.25f, callSeconds + 0.5f);
            Assert.Equal(1, s.Wanted.Stars);
            Assert.Equal(CrimeState.Known, s.Crime.GetCrime(0).State);
        }

        [Fact]
        public void Call_length_comes_from_the_seeded_rng_of_world_seed_crime_id_and_caller_index()
        {
            var s = NoPoliceScene(1234u);
            Ped a = s.Civilian(10, 0, witness: true, seesPlayer: true);
            Ped b = s.Civilian(14, 0, witness: true, seesPlayer: true);
            s.Punch(a);
            s.Step(police: false);
            Assert.Equal(2, s.Peds.Calls.Count);
            int id = s.Crime.GetCrime(0).Id;
            Assert.Equal(new Rng(Rng.Seed(1234u, (uint)id, 0u)).Range(4f, 10f), s.Peds.Calls[0].CallSeconds, 4);
            Assert.Equal(new Rng(Rng.Seed(1234u, (uint)id, 1u)).Range(4f, 10f), s.Peds.Calls[1].CallSeconds, 4);
        }

        [Fact]
        public void At_most_two_nearest_witnesses_make_calls_and_ties_go_to_the_lower_tag()
        {
            var s = NoPoliceScene();
            Ped far = s.Civilian(30, 0, witness: true, seesPlayer: true);
            Ped near1 = s.Civilian(6, 0, witness: true, seesPlayer: true);
            Ped near2 = s.Civilian(-6, 0, witness: true, seesPlayer: true);
            s.Punch(s.Civilian(0, 0));
            s.Step(police: false);
            Assert.Equal(2, s.Peds.Calls.Count);
            var called = new HashSet<int> { s.Peds.Calls[0].PedId, s.Peds.Calls[1].PedId };
            Assert.Contains(near1.Id, called);
            Assert.Contains(near2.Id, called);
            Assert.DoesNotContain(far.Id, called);
            Assert.Equal(2, s.Crime.ActiveCallCount);
        }

        [Fact]
        public void Interrupting_the_only_call_loses_the_crime_for_good()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(10, 0, witness: true, seesPlayer: true);
            s.Punch(civ);
            s.Run(1f, police: false);
            s.Peds.InterruptAll();
            s.Run(15f, police: false);
            Assert.Equal(0, s.Wanted.Stars);
            Assert.Equal(0f, s.Wanted.PlayerHeat, 3);
            Assert.Equal(CrimeState.Lost, s.Crime.GetCrime(0).State);
            Assert.Equal(0, s.Crime.ActiveCallCount);
        }

        [Fact]
        public void A_second_caller_still_makes_the_crime_known_when_the_first_is_interrupted()
        {
            var s = NoPoliceScene();
            s.Civilian(10, 0, witness: true, seesPlayer: true);
            s.Civilian(12, 0, witness: true, seesPlayer: true);
            s.Punch(s.Civilian(0, 3));
            s.Step(police: false);
            s.Peds.Calls[0].Brain.Interrupted = true;
            s.Run(12f, police: false);
            Assert.Equal(1, s.Wanted.Stars);
            Assert.Equal(CrimeState.Known, s.Crime.GetCrime(0).State);
        }

        [Fact]
        public void A_caller_who_lost_the_player_and_is_over_sixty_metres_away_drops_the_call()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(10, 0, witness: true, seesPlayer: true);
            s.Punch(civ);
            s.Run(1f, police: false);
            s.Peds.Seeing.Clear();
            s.PlacePlayer(-100f, 0f);
            s.Run(15f, police: false);
            Assert.Equal(0, s.Wanted.Stars);
            Assert.Equal(CrimeState.Lost, s.Crime.GetCrime(0).State);
            Assert.Equal(0, s.Crime.ActiveCallCount);
        }

        [Fact]
        public void A_dead_caller_drops_the_call()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(10, 0, witness: true, seesPlayer: true);
            s.Punch(civ);
            s.Run(1f, police: false);
            civ.Flags |= PedFlags.Dead;
            s.Run(15f, police: false);
            Assert.Equal(0, s.Wanted.Stars);
            Assert.Equal(CrimeState.Lost, s.Crime.GetCrime(0).State);
        }

        [Fact]
        public void The_call_reports_where_and_as_whom_the_witness_last_saw_the_player()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(10, 0, witness: true, seesPlayer: true);
            s.Punch(civ);
            s.Run(2f, police: false);                     // witness sees the player at the origin in outfit 3
            s.Peds.Seeing.Clear();
            s.PlacePlayer(40f, 0f);                       // 30 m from the witness: unseen but not lost
            s.Player.Model = 5;                           // new outfit, unseen
            float t = s.RunUntil(() => s.Wanted.Stars > 0, 12f);
            Assert.True(t > 0f);
            Assert.True(s.Wanted.HasLastKnownPosition);
            Near.Equal(Vec3.Zero, s.Wanted.LastKnownPosition, 1.5f);
            Assert.Equal(3, s.Wanted.Description.OutfitModel);
            double now = s.World.Clock.GameSeconds;
            Assert.InRange(s.Wanted.SecondsSinceSeen, (float)(now - 2.5), (float)(now - 1.5));
        }

        // ---- cops witnessing -----------------------------------------------------------------------------------------

        [Fact]
        public void A_cop_who_sees_the_crime_makes_it_known_at_once_with_the_players_position()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(3, 0);
            Ped cop = s.Cop(0, 25f);
            s.Punch(civ);
            s.Step(police: false);
            Assert.Equal(1, s.Wanted.Stars);
            CrimeRecord r = s.Crime.GetCrime(0);
            Assert.Equal(CrimeState.Known, r.State);
            Assert.Equal(cop.Tag, r.SeenByCopTag);
            Assert.True(s.Wanted.HasLastKnownPosition);
            Near.Equal(Vec3.Zero, s.Wanted.LastKnownPosition, 0.5f);
            Assert.Equal(0f, s.Wanted.SecondsSinceSeen, 3);
            Assert.Empty(s.Peds.Calls);
        }

        [Fact]
        public void A_cop_beyond_sixty_metres_by_day_does_not_see_it()
        {
            var s = NoPoliceScene();
            s.Cop(0, 70f);
            s.Punch(s.Civilian(3, 0));
            s.Step(police: false);
            Assert.Equal(0, s.Wanted.Stars);
            Assert.Equal(CrimeState.Unwitnessed, s.Crime.GetCrime(0).State);
        }

        [Fact]
        public void At_night_a_cop_sees_at_thirty_metres_but_not_at_forty()
        {
            var s = NoPoliceScene();
            s.World.Clock.Hour = 23f;
            s.Cop(0, 40f);
            s.Punch(s.Civilian(3, 0));
            s.Step(police: false);
            Assert.Equal(0, s.Wanted.Stars);

            var s2 = NoPoliceScene();
            s2.World.Clock.Hour = 23f;
            s2.Cop(0, 30f);
            s2.Punch(s2.Civilian(3, 0));
            s2.Step(police: false);
            Assert.Equal(1, s2.Wanted.Stars);
        }

        [Fact]
        public void A_cop_facing_away_does_not_see_a_quiet_crime_but_hears_a_gunshot_within_twenty_metres()
        {
            var s = NoPoliceScene();
            s.Cop(0, 15f, yaw: 0f);                      // facing +z, away from the crime
            s.Punch(s.Civilian(3, 0));
            s.Step(police: false);
            Assert.Equal(0, s.Wanted.Stars);

            var shot = NoPoliceScene();
            shot.Cop(0, 15f, yaw: 0f);
            shot.Gunshot(Vec3.Zero);
            shot.Step(police: false);
            Assert.Equal(1, shot.Wanted.Stars);

            var far = NoPoliceScene();
            far.Cop(0, 25f, yaw: 0f);
            far.Gunshot(Vec3.Zero);
            far.Step(police: false);
            Assert.Equal(0, far.Wanted.Stars);
        }

        [Fact]
        public void A_dead_cop_witnesses_nothing()
        {
            var s = NoPoliceScene();
            Ped cop = s.Cop(0, 20f);
            cop.Flags |= PedFlags.Dead;
            s.Punch(s.Civilian(3, 0));
            s.Step(police: false);
            Assert.Equal(0, s.Wanted.Stars);
        }

        [Fact]
        public void Three_reckless_events_seen_by_a_cop_make_one_star_and_one_record()
        {
            var s = NoPoliceScene();
            Car car = s.PlayerInCar(0, 0);
            s.Cop(0, 25f);
            for (int i = 0; i < 3; i++)
            {
                s.World.Emit(GameEventType.RecklessDriving, car.Position, car.Tag, 0, 0f, null);
                s.Step(0.5f, police: false);
            }
            Assert.Equal(1, s.Wanted.Stars);
            Assert.Equal(1, s.Crime.CrimeCount);
            Assert.Equal(CrimeType.RecklessDriving, s.Crime.GetCrime(0).Type);
        }

        // ---- stars and heat --------------------------------------------------------------------------------------------

        [Fact]
        public void Reporting_a_murder_by_a_cop_gives_three_stars_and_cop_kills_reach_the_cap_of_five()
        {
            var s = NoPoliceScene();
            Ped cop = s.Cop(0, 25f);
            s.Crime.ReportByCop(s.World, CrimeType.PedKilled, Vec3.Zero, 0, cop.Tag);
            Assert.Equal(3, s.Wanted.Stars);
            for (int i = 0; i < 4; i++) s.Crime.ReportByCop(s.World, CrimeType.CopKilled, Vec3.Zero, 0, cop.Tag);
            Assert.Equal(5, s.Wanted.Stars);
        }

        [Fact]
        public void Repeated_minor_crimes_escalate_one_star_once_accumulated_severity_exceeds_three()
        {
            var s = NoPoliceScene();
            Ped cop = s.Cop(0, 25f);
            var stars = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                s.Crime.ReportByCop(s.World, CrimeType.Gunshot, Vec3.Zero, 0, cop.Tag);
                stars.Add(s.Wanted.Stars);
            }
            Assert.Equal(new[] { 1, 1, 1, 1, 2 }, stars.ToArray());
        }

        [Fact]
        public void A_known_crime_heats_the_player_and_its_district()
        {
            var s = NoPoliceScene();
            s.City.DistrictFunc = (x, z) => x >= 100f ? (byte)7 : (byte)0;
            Ped cop = s.Cop(0, 25f);
            s.Crime.ReportByCop(s.World, CrimeType.PedKilled, new Vec3(150f, 0f, 0f), 0, cop.Tag);
            Assert.Equal(18f, s.Wanted.PlayerHeat, 2);
            Assert.Equal(30f, s.Wanted.GetDistrictHeat(7), 2);
            Assert.Equal(0f, s.Wanted.GetDistrictHeat(0), 2);
            Assert.Equal(7, s.Crime.GetCrime(0).District);
        }

        [Fact]
        public void Heat_caps_at_one_hundred()
        {
            var s = NoPoliceScene();
            Ped cop = s.Cop(0, 25f);
            for (int i = 0; i < 7; i++) s.Crime.ReportByCop(s.World, CrimeType.PedKilled, Vec3.Zero, 0, cop.Tag);
            Assert.Equal(100f, s.Wanted.PlayerHeat, 2);
            Assert.Equal(100f, s.Wanted.GetDistrictHeat(0), 2);
        }

        [Fact]
        public void Player_heat_lingers_after_the_stars_go_and_decays_one_per_minute()
        {
            var s = NoPoliceScene();
            s.Wanted.PlayerHeat = 30f;
            s.Run(120f, dt: 0.5f, police: false);
            Assert.Equal(28f, s.Wanted.PlayerHeat, 1);
        }

        [Fact]
        public void Player_heat_does_not_decay_while_wanted()
        {
            var s = NoPoliceScene();
            s.Wanted.PlayerHeat = 30f;
            s.MakeWanted(1);
            s.Run(60f, dt: 0.5f, police: false);
            Assert.Equal(30f, s.Wanted.PlayerHeat, 2);
        }

        [Fact]
        public void An_owned_safehouse_triples_the_player_heat_decay()
        {
            var s = NoPoliceScene();
            s.Wanted.PlayerHeat = 30f;
            s.World.Interior.Building = new BuildingDescriptor { Id = 5, OwnedByPlayer = true };
            s.World.Interior.Phase = InteriorPhase.Inside;
            s.Run(60f, dt: 0.5f, police: false);
            Assert.Equal(27f, s.Wanted.PlayerHeat, 1);
        }

        [Fact]
        public void District_heat_decays_two_per_minute_even_while_wanted()
        {
            var s = NoPoliceScene();
            s.Wanted.SetDistrictHeat(3, 10f);
            s.Wanted.SetDistrictHeat(0, 20f);
            s.MakeWanted(1);
            s.Run(60f, dt: 0.5f, police: false);
            Assert.Equal(8f, s.Wanted.GetDistrictHeat(3), 1);
            Assert.Equal(18f, s.Wanted.GetDistrictHeat(0), 1);
        }

        [Fact]
        public void The_episode_memory_resets_when_the_player_is_not_wanted()
        {
            var s = NoPoliceScene();
            Ped cop = s.Cop(0, 25f);
            s.Crime.ReportByCop(s.World, CrimeType.Gunshot, Vec3.Zero, 0, cop.Tag);
            s.Crime.ReportByCop(s.World, CrimeType.Gunshot, Vec3.Zero, 0, cop.Tag);
            Assert.True(s.Crime.Episode.SeveritySinceStar > 0f);
            s.Wanted.Reset();
            s.Step(police: false);
            Assert.Equal(0f, s.Crime.Episode.SeveritySinceStar, 3);
            Assert.Equal(0, s.Crime.Episode.CopKills);
        }

        // ---- records -----------------------------------------------------------------------------------------------------

        [Fact]
        public void Crime_ids_start_at_one_and_the_record_keeps_type_position_victim_and_description()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(3, 0);
            s.Punch(civ);
            s.Step(police: false);
            CrimeRecord r = s.Crime.GetCrime(0);
            Assert.Equal(1, r.Id);
            Assert.Equal(CrimeType.Assault, r.Type);
            Assert.Equal(1f, r.Severity, 3);
            Assert.Equal(civ.Tag, r.VictimTag);
            Near.Equal(civ.Position, r.Position, 0.01f);
            Assert.Equal(3, r.Description.OutfitModel);
            Assert.True(s.Crime.TryGetCrime(1, out CrimeRecord same));
            Assert.Equal(CrimeType.Assault, same.Type);
            Assert.False(s.Crime.TryGetCrime(99, out _));
        }

        [Fact]
        public void Only_the_newest_thirty_two_records_are_kept()
        {
            var s = NoPoliceScene();
            for (int i = 0; i < 33; i++) { s.Gunshot(new Vec3(0, 0, 100 + i)); s.Step(police: false); }
            Assert.Equal(CrimeSystem.CrimeCapacity, s.Crime.CrimeCount);
            Assert.False(s.Crime.TryGetCrime(1, out _));
            Assert.True(s.Crime.TryGetCrime(33, out _));
            Assert.Equal(33, s.Crime.GetCrime(0).Id);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => s.Crime.GetCrime(32));
        }

        [Fact]
        public void A_worse_crime_on_the_same_victim_within_ten_seconds_escalates_the_earlier_record()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(3, 0);
            s.Cop(0, 25f);
            s.Punch(civ);
            s.Step(police: false);
            Assert.Equal(6f, s.Wanted.PlayerHeat, 2);
            s.Run(3f, police: false);
            s.Kill(civ);
            s.Step(police: false);
            Assert.Equal(1, s.Crime.CrimeCount);
            CrimeRecord r = s.Crime.GetCrime(0);
            Assert.Equal(CrimeType.PedKilled, r.Type);
            Assert.Equal(3f, r.Severity, 3);
            Assert.Equal(18f, s.Wanted.PlayerHeat, 2);            // only the difference was added
            Assert.Equal(30f, s.Wanted.GetDistrictHeat(0), 1);
            Assert.Equal(3, s.Wanted.Stars);
        }

        [Fact]
        public void A_later_crime_on_the_same_victim_after_ten_seconds_is_a_new_record()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(3, 0);
            s.Cop(0, 25f);
            s.Punch(civ);
            s.Run(11f, police: false);
            s.Kill(civ);
            s.Step(police: false);
            Assert.Equal(2, s.Crime.CrimeCount);
            Assert.Equal(24f, s.Wanted.PlayerHeat, 1);
        }

        [Fact]
        public void A_lesser_crime_on_the_same_victim_is_dropped()
        {
            var s = NoPoliceScene();
            Ped civ = s.Civilian(3, 0);
            s.Cop(0, 25f);
            s.Kill(civ);
            s.Step(police: false);
            s.Run(2f, police: false);
            s.Punch(civ);
            s.Step(police: false);
            Assert.Equal(1, s.Crime.CrimeCount);
            Assert.Equal(CrimeType.PedKilled, s.Crime.GetCrime(0).Type);
            Assert.Equal(18f, s.Wanted.PlayerHeat, 2);
        }

        [Fact]
        public void A_cop_reported_resisting_arrest_is_known_at_once_and_makes_two_stars()
        {
            var s = NoPoliceScene();
            Ped cop = s.Cop(0, 5f);
            int id = s.Crime.ReportByCop(s.World, CrimeType.ResistingArrest, s.Player.Position, cop.Tag, cop.Tag);
            Assert.True(id >= 1);
            Assert.True(s.Crime.TryGetCrime(id, out CrimeRecord r));
            Assert.Equal(CrimeState.Known, r.State);
            Assert.Equal(cop.Tag, r.SeenByCopTag);
            Assert.Equal(2, s.Wanted.Stars);
        }

        [Fact]
        public void A_cop_counts_as_having_seen_a_crime_for_one_second_only()
        {
            var s = NoPoliceScene();
            Ped cop = s.Cop(0, 25f);
            Assert.False(s.Crime.CopSawCrimeRecently(s.World, cop.Tag));
            s.Punch(s.Civilian(3, 0));
            s.Step(police: false);
            Assert.True(s.Crime.CopSawCrimeRecently(s.World, cop.Tag));
            s.Run(1.5f, police: false);
            Assert.False(s.Crime.CopSawCrimeRecently(s.World, cop.Tag));
        }

        [Fact]
        public void The_same_seed_gives_the_same_call_lengths_and_different_seeds_differ()
        {
            float[] Lengths(uint seed)
            {
                var s = NoPoliceScene(seed);
                s.Civilian(10, 0, witness: true, seesPlayer: true);
                var l = new List<float>();
                for (int i = 0; i < 4; i++) { s.Gunshot(new Vec3(0, 0, 5 + i)); s.Step(police: false); }
                foreach (var c in s.Peds.Calls) l.Add(c.CallSeconds);
                return l.ToArray();
            }
            Assert.Equal(Lengths(11u), Lengths(11u));
            Assert.NotEqual(Lengths(11u), Lengths(12u));
        }
    }
}
