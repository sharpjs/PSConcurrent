/*
    Copyright (C) 2019 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

#nullable enable

using System;
using FluentAssertions;
using NUnit.Framework;

namespace PSConcurrent
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class ArrayExtensionsTests
    {
        [Test]
        public void OrEmpty_NullArray()
        {
            (null as string?[]).OrEmpty().Should().BeEmpty();
        }

        [Test]
        public void OrEmpty_NonNullArray()
        {
            var array = new string?[] { "a", null, "b" };

            array.OrEmpty().Should().BeSameAs(array);
        }

        [Test]
        public void Compact_NullArray()
        {
            (null as string?[])
                .Invoking(a => a!.Compact())
                .Should().Throw<ArgumentNullException>()
                .Where(e => e.ParamName == "array");
        }

        [Test]
        public void Compact_NonNullArray_WithNullElement()
        {
            var array = new string?[] { "a", null, "b" };

            array.Compact().Should().Equal("a", "b");
        }

        [Test]
        public void Compact_NonNullArray_NoNullElement()
        {
            var array = new string?[] { "a", "b" };

            array.Compact().Should().BeSameAs(array);
        }
    }
}
