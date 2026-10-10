using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Skylines.Core.Saves;

namespace GtaSkylines.Game.Profile
{
    /// <summary>Outcome of <see cref="ProfileRecord.Decode"/>.</summary>
    public sealed class ProfileDecodeResult
    {
        /// <summary>True if <see cref="Profile"/> is valid.</summary>
        public bool Ok { get; private set; }
        /// <summary>The decoded, <see cref="PlayerProfile.Normalise"/>d profile when <see cref="Ok"/>; null otherwise.</summary>
        public PlayerProfile Profile { get; private set; }
        /// <summary>Human-readable reason when not <see cref="Ok"/>; empty when Ok.</summary>
        public string Error { get; private set; }

        /// <summary>Creates a result.</summary>
        public ProfileDecodeResult(bool ok, PlayerProfile profile, string error)
        {
            Ok = ok;
            Profile = profile;
            Error = error ?? "";
        }
    }

    /// <summary>
    /// Binary codec for <see cref="PlayerProfile"/>. The bytes are a <c>Skylines.Core.Saves.BlobRecord</c> (u8 version 1,
    /// u32 length, payload, u32 CRC-32) whose payload is little-endian:
    /// u8 profileVersion (1); i64 cash; u8 health; u8 armour; i32 outfit; u32 lastSafehouse; i32 campaignStep;
    /// f32 heat; f64 playTimeSeconds; u8 weaponCount then per weapon (u8 weaponId, u8 owned, i32 ammo);
    /// u16 count + u32 each (properties); u16 count + strings (completed missions); u16 count + (string, i32) (side-job levels);
    /// u16 count + (string, i64) (stats); u16 count + (i32 district, f32 heat) (district heat).
    /// A string is u16 byte length + UTF-8. Bytes after the last known field are ignored (forward compatibility); a
    /// profileVersion above 1 is still read with the fields known here. Unknown weapon ids are skipped.
    /// Collections are written up to 65535 entries; strings longer than 65535 UTF-8 bytes are not written.
    /// </summary>
    public static class ProfileRecord
    {
        /// <summary>Payload version written (1).</summary>
        public const byte ProfileVersion = 1;

        private const int MaxCount = 65535;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>Serialises the profile. Deterministic: maps are written sorted by key (ordinal). Throws ArgumentNullException for null.</summary>
        public static byte[] Encode(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            var m = new MemoryStream();
            var w = new BinaryWriter(m);
            w.Write(ProfileVersion);
            w.Write(profile.Cash);
            w.Write((byte)profile.Health);
            w.Write((byte)profile.Armour);
            w.Write(profile.OutfitId);
            w.Write(profile.LastSafehouse);
            w.Write(profile.CampaignStep);
            w.Write(profile.Heat);
            w.Write(profile.PlayTimeSeconds);

            w.Write((byte)WeaponInfo.Count);
            for (int i = 0; i < WeaponInfo.Count; i++)
            {
                w.Write((byte)i);
                w.Write((byte)(profile.OwnedWeapons[i] ? 1 : 0));
                w.Write(profile.AmmoByWeapon[i]);
            }

            int n = Math.Min(profile.Properties.Count, MaxCount);
            w.Write((ushort)n);
            for (int i = 0; i < n; i++) w.Write(profile.Properties[i]);

            var missions = new List<string>();
            foreach (string s in profile.CompletedMissions) if (Fits(s)) missions.Add(s);
            n = Math.Min(missions.Count, MaxCount);
            w.Write((ushort)n);
            for (int i = 0; i < n; i++) WriteString(w, missions[i]);

            var jobs = SortedKeys(profile.SideJobLevels.Keys);
            n = Math.Min(jobs.Count, MaxCount);
            w.Write((ushort)n);
            for (int i = 0; i < n; i++) { WriteString(w, jobs[i]); w.Write(profile.SideJobLevels[jobs[i]]); }

            var stats = SortedKeys(profile.Stats.Keys);
            n = Math.Min(stats.Count, MaxCount);
            w.Write((ushort)n);
            for (int i = 0; i < n; i++) { WriteString(w, stats[i]); w.Write(profile.Stats[stats[i]]); }

            var districts = new List<int>(profile.DistrictHeat.Keys);
            districts.Sort();
            n = Math.Min(districts.Count, MaxCount);
            w.Write((ushort)n);
            for (int i = 0; i < n; i++) { w.Write(districts[i]); w.Write(profile.DistrictHeat[districts[i]]); }

            w.Flush();
            return BlobRecord.Encode(m.ToArray());
        }

        /// <summary>
        /// Decodes a record. Never throws: null, truncated, wrong BlobRecord version, bad length, CRC mismatch, a payload that
        /// ends inside a known field, profileVersion 0, or a string that is not valid UTF-8 yields <c>Ok == false</c> with a message
        /// and a null profile. Valid data decodes to a profile equal in every field to the encoded one (after Normalise).
        /// </summary>
        public static ProfileDecodeResult Decode(byte[] record)
        {
            byte[] payload;
            try { payload = BlobRecord.Decode(record); }
            catch (Exception e) { return new ProfileDecodeResult(false, null, e.Message); }

            try
            {
                var r = new BinaryReader(new MemoryStream(payload));
                byte version = r.ReadByte();
                if (version == 0) return new ProfileDecodeResult(false, null, "unknown profile version 0");
                var p = new PlayerProfile();
                p.SetCash(r.ReadInt64());
                p.Health = r.ReadByte();
                p.Armour = r.ReadByte();
                p.OutfitId = r.ReadInt32();
                p.LastSafehouse = r.ReadUInt32();
                p.CampaignStep = r.ReadInt32();
                p.Heat = r.ReadSingle();
                p.PlayTimeSeconds = r.ReadDouble();

                int weapons = r.ReadByte();
                for (int i = 0; i < weapons; i++)
                {
                    byte id = r.ReadByte();
                    bool owned = r.ReadByte() != 0;
                    int ammo = r.ReadInt32();
                    if (id >= WeaponInfo.Count) continue;
                    p.OwnedWeapons[id] = owned;
                    p.AmmoByWeapon[id] = ammo;
                }

                int count = r.ReadUInt16();
                for (int i = 0; i < count; i++) p.Properties.Add(r.ReadUInt32());
                count = r.ReadUInt16();
                for (int i = 0; i < count; i++) p.CompletedMissions.Add(ReadString(r));
                count = r.ReadUInt16();
                for (int i = 0; i < count; i++) { string k = ReadString(r); p.SideJobLevels[k] = r.ReadInt32(); }
                count = r.ReadUInt16();
                for (int i = 0; i < count; i++) { string k = ReadString(r); p.Stats[k] = r.ReadInt64(); }
                count = r.ReadUInt16();
                for (int i = 0; i < count; i++) { int d = r.ReadInt32(); p.DistrictHeat[d] = r.ReadSingle(); }

                p.Normalise();
                return new ProfileDecodeResult(true, p, "");
            }
            catch (Exception e)
            {
                return new ProfileDecodeResult(false, null, "corrupt profile payload: " + e.Message);
            }
        }

        private static bool Fits(string s) { return s != null && StrictUtf8.GetByteCount(s) <= MaxCount; }

        private static List<string> SortedKeys(ICollection<string> keys)
        {
            var l = new List<string>();
            foreach (string k in keys) if (Fits(k)) l.Add(k);
            l.Sort(StringComparer.Ordinal);
            return l;
        }

        private static void WriteString(BinaryWriter w, string s)
        {
            byte[] b = StrictUtf8.GetBytes(s);
            w.Write((ushort)b.Length);
            w.Write(b);
        }

        private static string ReadString(BinaryReader r)
        {
            int len = r.ReadUInt16();
            byte[] b = r.ReadBytes(len);
            if (b.Length != len) throw new EndOfStreamException();
            return StrictUtf8.GetString(b);
        }
    }
}
