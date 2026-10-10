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
    /// </summary>
    public static class ProfileRecord
    {
        /// <summary>Payload version written (1).</summary>
        public const byte ProfileVersion = 1;

        /// <summary>Serialises the profile. Deterministic: maps are written sorted by key (ordinal). Throws ArgumentNullException for null.</summary>
        public static byte[] Encode(PlayerProfile profile) { throw new System.NotImplementedException(); }

        /// <summary>
        /// Decodes a record. Never throws: null, truncated, wrong BlobRecord version, bad length, CRC mismatch, a payload that
        /// ends inside a known field, profileVersion 0, or a string that is not valid UTF-8 yields <c>Ok == false</c> with a message
        /// and a null profile. Valid data decodes to a profile equal in every field to the encoded one (after Normalise).
        /// </summary>
        public static ProfileDecodeResult Decode(byte[] record) { throw new System.NotImplementedException(); }
    }
}
