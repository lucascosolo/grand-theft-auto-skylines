namespace Skylines.Core.Saves
{
    /// <summary>CRC-32 (IEEE 802.3, reflected, as zlib's crc32).</summary>
    public static class Crc32
    {
        private static readonly uint[] Table = MakeTable();

        /// <summary>CRC-32 of a byte range.</summary>
        public static uint Compute(byte[] data, int offset, int count)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = offset; i < offset + count; i++) c = Table[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        private static uint[] MakeTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }
    }
}
