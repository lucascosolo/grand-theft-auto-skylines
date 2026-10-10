using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GtaSkylines.Game.Profile;
using Skylines.Core.Saves;
using Xunit;

namespace GtaSkylines.Game.Tests.Profile
{
    public class ProfileRecordTests
    {
        private static PlayerProfile Full()
        {
            var p = new PlayerProfile();
            p.AddCash(PlayerProfile.MaxCash);
            p.Health = 37;
            p.Armour = 64;
            p.OutfitId = 5;
            p.CampaignStep = 12;
            p.Heat = 55.5f;
            p.PlayTimeSeconds = 12345.678;
            p.Properties.Add(4000000000u);
            p.Properties.Add(7u);
            p.LastSafehouse = 7u;
            p.CompletedMissions.Add("m01");
            p.CompletedMissions.Add("café ☃");
            p.SideJobLevels["taxi"] = 3;
            p.SideJobLevels["courier"] = 1;
            p.Stats["distance_m"] = 123456789012L;
            p.Stats["crimes"] = long.MaxValue;
            p.DistrictHeat[3] = 12.5f;
            p.DistrictHeat[-1] = 100f;
            p.GiveWeapon(WeaponId.Bat);
            p.GiveWeapon(WeaponId.Pistol);
            p.SetAmmo(WeaponId.Pistol, 250);
            p.GiveWeapon(WeaponId.Carbine);
            p.SetAmmo(WeaponId.Carbine, 17);
            return p;
        }

        private static SortedDictionary<K, V> Sorted<K, V>(IDictionary<K, V> d, IComparer<K> c)
        {
            return new SortedDictionary<K, V>(new Dictionary<K, V>(d), c);
        }

        private static void AssertEqual(PlayerProfile a, PlayerProfile b)
        {
            Assert.Equal(a.Cash, b.Cash);
            Assert.Equal(a.Health, b.Health);
            Assert.Equal(a.Armour, b.Armour);
            Assert.Equal(a.OutfitId, b.OutfitId);
            Assert.Equal(a.LastSafehouse, b.LastSafehouse);
            Assert.Equal(a.CampaignStep, b.CampaignStep);
            Assert.Equal(a.Heat, b.Heat);
            Assert.Equal(a.PlayTimeSeconds, b.PlayTimeSeconds);
            Assert.Equal(a.Properties, b.Properties);
            Assert.Equal(a.CompletedMissions, b.CompletedMissions);
            Assert.Equal(Sorted(a.SideJobLevels, StringComparer.Ordinal), Sorted(b.SideJobLevels, StringComparer.Ordinal));
            Assert.Equal(Sorted(a.Stats, StringComparer.Ordinal), Sorted(b.Stats, StringComparer.Ordinal));
            Assert.Equal(Sorted(a.DistrictHeat, Comparer<int>.Default), Sorted(b.DistrictHeat, Comparer<int>.Default));
            Assert.Equal(a.OwnedWeapons, b.OwnedWeapons);
            Assert.Equal(a.AmmoByWeapon, b.AmmoByWeapon);
        }

        // Builds a version-1 payload by hand from the documented layout.
        private sealed class PayloadBuilder
        {
            public byte Version = 1;
            public List<byte[]> Weapons = new List<byte[]>();
            public List<byte[]> Missions = new List<byte[]>();
            public byte[] Trailing = new byte[0];
            public bool CutInsideCash;

            public static byte[] Str(byte[] utf8)
            {
                var m = new MemoryStream();
                var w = new BinaryWriter(m);
                w.Write((ushort)utf8.Length);
                w.Write(utf8);
                return m.ToArray();
            }

            public static byte[] Weapon(byte id, bool owned, int ammo)
            {
                var m = new MemoryStream();
                var w = new BinaryWriter(m);
                w.Write(id);
                w.Write((byte)(owned ? 1 : 0));
                w.Write(ammo);
                return m.ToArray();
            }

            public byte[] Build()
            {
                var m = new MemoryStream();
                var w = new BinaryWriter(m);
                w.Write(Version);
                if (CutInsideCash) { w.Write((byte)1); w.Write((byte)2); return m.ToArray(); }
                w.Write(1000L);
                w.Write((byte)80);
                w.Write((byte)20);
                w.Write(2);
                w.Write(0u);
                w.Write(9);
                w.Write(40f);
                w.Write(60.0);
                w.Write((byte)Weapons.Count);
                foreach (var x in Weapons) w.Write(x);
                w.Write((ushort)0);
                w.Write((ushort)Missions.Count);
                foreach (var x in Missions) w.Write(x);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write((ushort)0);
                w.Write(Trailing);
                return m.ToArray();
            }
        }

        [Fact]
        public void Default_profile_round_trips()
        {
            var p = new PlayerProfile();
            var r = ProfileRecord.Decode(ProfileRecord.Encode(p));
            Assert.True(r.Ok, r.Error);
            Assert.Equal("", r.Error);
            AssertEqual(p, r.Profile);
            Assert.Equal(PlayerProfile.StartCash, r.Profile.Cash);
        }

        [Fact]
        public void Fully_populated_profile_round_trips_exactly()
        {
            var p = Full();
            var r = ProfileRecord.Decode(ProfileRecord.Encode(p));
            Assert.True(r.Ok, r.Error);
            AssertEqual(p, r.Profile);
            Assert.Equal(PlayerProfile.MaxCash, r.Profile.Cash);
            Assert.Equal(250, r.Profile.GetAmmo(WeaponId.Pistol));
        }

        [Fact]
        public void Large_collections_round_trip()
        {
            var p = new PlayerProfile();
            for (uint i = 1; i <= 3000; i++) p.Properties.Add(i);
            for (int i = 0; i < 3000; i++) p.CompletedMissions.Add("mission_" + i);
            for (int i = 0; i < 3000; i++) { p.Stats["stat" + i] = i; p.SideJobLevels["job" + i] = i + 1; p.DistrictHeat[i] = i % 101; }
            var r = ProfileRecord.Decode(ProfileRecord.Encode(p));
            Assert.True(r.Ok, r.Error);
            AssertEqual(p, r.Profile);
        }

        [Fact]
        public void Encoding_is_deterministic_regardless_of_insertion_order()
        {
            var a = new PlayerProfile();
            a.Stats["b"] = 1; a.Stats["a"] = 2; a.DistrictHeat[9] = 1f; a.DistrictHeat[2] = 3f;
            var b = new PlayerProfile();
            b.Stats["a"] = 2; b.Stats["b"] = 1; b.DistrictHeat[2] = 3f; b.DistrictHeat[9] = 1f;
            Assert.Equal(ProfileRecord.Encode(a), ProfileRecord.Encode(b));
        }

        [Fact]
        public void Encoded_bytes_are_a_blob_record_with_profile_version_1()
        {
            var payload = BlobRecord.Decode(ProfileRecord.Encode(new PlayerProfile()));
            Assert.Equal(1, payload[0]);
            Assert.Equal(1, ProfileRecord.ProfileVersion);
        }

        [Fact]
        public void Encode_null_throws_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => ProfileRecord.Encode(null));
        }

        [Fact]
        public void Decode_null_and_tiny_inputs_give_error_without_throwing()
        {
            foreach (var bytes in new byte[][] { null, new byte[0], new byte[3] })
            {
                var r = ProfileRecord.Decode(bytes);
                Assert.False(r.Ok);
                Assert.Null(r.Profile);
                Assert.NotEqual("", r.Error);
            }
        }

        [Fact]
        public void Every_truncation_gives_error_without_throwing()
        {
            var bytes = ProfileRecord.Encode(Full());
            for (int len = 0; len < bytes.Length; len++)
            {
                var cut = new byte[len];
                Array.Copy(bytes, cut, len);
                var r = ProfileRecord.Decode(cut);
                Assert.False(r.Ok);
                Assert.Null(r.Profile);
                Assert.NotEqual("", r.Error);
            }
        }

        [Fact]
        public void Every_single_byte_corruption_gives_error()
        {
            var bytes = ProfileRecord.Encode(Full());
            for (int i = 0; i < bytes.Length; i++)
            {
                var bad = (byte[])bytes.Clone();
                bad[i] ^= 0x40;
                var r = ProfileRecord.Decode(bad);
                Assert.False(r.Ok);
                Assert.Null(r.Profile);
            }
        }

        [Fact]
        public void Wrong_blob_record_version_gives_error()
        {
            var bytes = ProfileRecord.Encode(new PlayerProfile());
            bytes[0] = 2;
            Assert.False(ProfileRecord.Decode(bytes).Ok);
        }

        [Fact]
        public void Profile_version_0_gives_error()
        {
            var b = new PayloadBuilder { Version = 0 };
            Assert.False(ProfileRecord.Decode(BlobRecord.Encode(b.Build())).Ok);
        }

        [Fact]
        public void Payload_ending_inside_a_known_field_gives_error()
        {
            var b = new PayloadBuilder { CutInsideCash = true };
            var r = ProfileRecord.Decode(BlobRecord.Encode(b.Build()));
            Assert.False(r.Ok);
            Assert.Null(r.Profile);
        }

        [Fact]
        public void Valid_hand_built_payload_decodes_documented_layout()
        {
            var b = new PayloadBuilder();
            b.Weapons.Add(PayloadBuilder.Weapon(2, true, 30));
            b.Missions.Add(PayloadBuilder.Str(Encoding.UTF8.GetBytes("m1")));
            var r = ProfileRecord.Decode(BlobRecord.Encode(b.Build()));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(1000L, r.Profile.Cash);
            Assert.Equal(80, r.Profile.Health);
            Assert.Equal(20, r.Profile.Armour);
            Assert.Equal(2, r.Profile.OutfitId);
            Assert.Equal(9, r.Profile.CampaignStep);
            Assert.Equal(40f, r.Profile.Heat);
            Assert.Equal(60.0, r.Profile.PlayTimeSeconds);
            Assert.True(r.Profile.OwnsWeapon(WeaponId.Pistol));
            Assert.Equal(30, r.Profile.GetAmmo(WeaponId.Pistol));
            Assert.True(r.Profile.IsMissionComplete("m1"));
        }

        [Fact]
        public void Trailing_bytes_after_known_fields_are_ignored()
        {
            var b = new PayloadBuilder { Trailing = new byte[] { 1, 2, 3, 4, 5, 250 } };
            var r = ProfileRecord.Decode(BlobRecord.Encode(b.Build()));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(1000L, r.Profile.Cash);
        }

        [Fact]
        public void Newer_profile_version_is_still_read()
        {
            var b = new PayloadBuilder { Version = 7, Trailing = new byte[] { 9, 9, 9 } };
            var r = ProfileRecord.Decode(BlobRecord.Encode(b.Build()));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(1000L, r.Profile.Cash);
        }

        [Fact]
        public void Unknown_weapon_ids_are_skipped()
        {
            var b = new PayloadBuilder();
            b.Weapons.Add(PayloadBuilder.Weapon(200, true, 5));
            b.Weapons.Add(PayloadBuilder.Weapon(3, true, 44));
            var r = ProfileRecord.Decode(BlobRecord.Encode(b.Build()));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(44, r.Profile.GetAmmo(WeaponId.Smg));
        }

        [Fact]
        public void Invalid_utf8_string_gives_error()
        {
            var b = new PayloadBuilder();
            b.Missions.Add(PayloadBuilder.Str(new byte[] { 0xFF, 0xFE, 0xC0 }));
            var r = ProfileRecord.Decode(BlobRecord.Encode(b.Build()));
            Assert.False(r.Ok);
            Assert.Null(r.Profile);
        }

        [Fact]
        public void Decoded_profile_is_normalised()
        {
            var b = new PayloadBuilder();
            b.Weapons.Add(PayloadBuilder.Weapon(2, true, 99999)); // above max 250
            b.Weapons.Add(PayloadBuilder.Weapon(4, false, 10));   // ammo for an unowned weapon
            b.Missions.Add(PayloadBuilder.Str(Encoding.UTF8.GetBytes("dup")));
            b.Missions.Add(PayloadBuilder.Str(Encoding.UTF8.GetBytes("dup")));
            var r = ProfileRecord.Decode(BlobRecord.Encode(b.Build()));
            Assert.True(r.Ok, r.Error);
            Assert.Single(r.Profile.CompletedMissions);
            Assert.Equal(250, r.Profile.GetAmmo(WeaponId.Pistol));
            Assert.Equal(0, r.Profile.AmmoByWeapon[(int)WeaponId.Shotgun]);
            Assert.True(r.Profile.OwnedWeapons[0]);
        }
    }
}
