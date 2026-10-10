using System;
using System.Collections.Generic;

namespace Skylines.Host.Input
{
    /// <summary>Removes entries from the middle of a stack without disturbing the others (BCL only, unit-tested).</summary>
    internal static class ModalStackEdit
    {
        /// <summary>
        /// <paramref name="topFirst"/> is a stack's content as <c>Stack&lt;T&gt;.ToArray()</c> returns it. Returns the entries to
        /// push back, bottom first, onto the cleared stack so that it equals the original minus every entry
        /// <paramref name="isOurs"/> matches; null when nothing matched (leave the stack alone).
        /// </summary>
        public static object[] PushOrderWithout(object[] topFirst, Predicate<object> isOurs)
        {
            if (topFirst == null) return null;
            var keep = new List<object>(topFirst.Length);
            bool removed = false;
            for (int i = topFirst.Length - 1; i >= 0; i--)
            {
                if (isOurs(topFirst[i])) removed = true;
                else keep.Add(topFirst[i]);
            }
            return removed ? keep.ToArray() : null;
        }
    }
}
