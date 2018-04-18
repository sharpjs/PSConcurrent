/*
    Copyright (C) 2018 Jeffrey Sharp

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

using System;
using System.Management.Automation;
using FluentAssertions;
using NUnit.Framework;

namespace PSConcurrent.Tests
{
    [TestFixture]
    public class InvokeConcurrentCmdletTests : CmdletTests
    {
        [Test]
        public void One()
        {
            var output = Invoke(
                @"Invoke-Concurrent {'a'}"
            );

            output.Should().HaveCount(1);

            output.OfTask(1).Should().Contain("a");
        }

        [Test]
        public void Multiple()
        {
            var output = Invoke(
                @"Invoke-Concurrent {'a'}, {'b'}, {'c'}"
            );

            output.Should().HaveCount(3);

            output.OfTask(1).Should().Contain("a");
            output.OfTask(2).Should().Contain("b");
            output.OfTask(3).Should().Contain("c");
        }

        [Test]
        public void Throws()
        {
            @"
                Invoke-Concurrent {'a'}, {throw 'b'}, {'c'} -MaxConcurrency 1
            "
            .Invoking(s => Invoke(s))
            .Should().Throw<CmdletInvocationException>();

            // TODO: Somehow test this too
            //output.Should().HaveCount(1);
            //output.OfTask(1).Should().Contain("a");
        }
    }
}
