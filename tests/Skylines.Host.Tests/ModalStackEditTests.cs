using System.Collections.Generic;
using Skylines.Host.Input;
using Xunit;

namespace Skylines.Host.Tests
{
    public class ModalStackEditTests
    {
        private static bool IsOurs(object o) { return o is string && ((string)o).StartsWith("ours"); }

        [Fact]
        public void Ours_on_top_is_dropped_and_the_rest_is_returned_bottom_first()
        {
            // top first: ours, b, a
            object[] r = ModalStackEdit.PushOrderWithout(new object[] { "ours", "b", "a" }, IsOurs);
            Assert.Equal(new object[] { "a", "b" }, r);
        }

        [Fact]
        public void Ours_in_the_middle_is_dropped()
        {
            object[] r = ModalStackEdit.PushOrderWithout(new object[] { "c", "ours", "a" }, IsOurs);
            Assert.Equal(new object[] { "a", "c" }, r);
        }

        [Fact]
        public void Ours_at_the_bottom_is_dropped()
        {
            object[] r = ModalStackEdit.PushOrderWithout(new object[] { "c", "b", "ours" }, IsOurs);
            Assert.Equal(new object[] { "b", "c" }, r);
        }

        [Fact]
        public void Every_matching_entry_is_removed()
        {
            object[] r = ModalStackEdit.PushOrderWithout(new object[] { "ours1", "x", "ours2", "y", "ours3" }, IsOurs);
            Assert.Equal(new object[] { "y", "x" }, r);
        }

        [Fact]
        public void No_match_returns_null()
        {
            Assert.Null(ModalStackEdit.PushOrderWithout(new object[] { "c", "b", "a" }, IsOurs));
        }

        [Fact]
        public void Empty_input_returns_null()
        {
            Assert.Null(ModalStackEdit.PushOrderWithout(new object[0], IsOurs));
        }

        [Fact]
        public void Only_ours_returns_an_empty_array_not_null()
        {
            object[] r = ModalStackEdit.PushOrderWithout(new object[] { "ours1", "ours2" }, IsOurs);
            Assert.NotNull(r);
            Assert.Empty(r);
        }

        [Fact]
        public void Null_input_returns_null()
        {
            Assert.Null(ModalStackEdit.PushOrderWithout(null, IsOurs));
        }

        [Fact]
        public void Other_entries_keep_their_identity_and_relative_order()
        {
            object a = new object(), b = new object(), c = new object();
            object[] r = ModalStackEdit.PushOrderWithout(new object[] { c, "ours", b, a }, IsOurs);
            Assert.Equal(3, r.Length);
            Assert.Same(a, r[0]);
            Assert.Same(b, r[1]);
            Assert.Same(c, r[2]);
        }

        [Fact]
        public void Round_trip_through_a_real_stack_equals_the_original_without_ours()
        {
            var stack = new Stack<object>();
            foreach (object o in new object[] { "a", "ours", "b", "ours2", "c" }) stack.Push(o);
            object[] r = ModalStackEdit.PushOrderWithout(stack.ToArray(), IsOurs);
            Assert.NotNull(r);
            stack.Clear();
            foreach (object o in r) stack.Push(o);
            Assert.Equal(new object[] { "c", "b", "a" }, stack.ToArray());
        }
    }
}
