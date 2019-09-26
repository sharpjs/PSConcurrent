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

using System;

namespace PSConcurrent
{
    /// <summary>
    ///   Extension methods for arrays.
    /// </summary>
    internal static class ArrayExtensions
    {
        /// <summary>
        ///   Returns the specified array if it is not <c>null</c>;
        ///   otherwise, returns an empty array.
        /// </summary>
        /// <typeparam name="T">
        ///   The type of elements in <paramref name="array"/>.
        /// </typeparam>
        /// <param name="array">
        ///   The array.
        /// </param>
        /// <returns>
        ///   <paramref name="array"/>, if it is not <c>null</c>;
        ///   otherwise, an empty array of <typeparamref name="T"/> elements.
        /// </returns>
        internal static T[] OrEmpty<T>(this T[]? array)
        {
            return array ?? new T[0];
        }

        /// <summary>
        ///   Returns an array consisting of the non-<c>null</c> elements of
        ///   the specified array.
        /// </summary>
        /// <typeparam name="T">
        ///   The type of elements in <paramref name="array"/>.
        /// </typeparam>
        /// <param name="array">
        ///   The array.
        /// </param>
        /// <returns>
        ///   <paramref name="array"/>, if it contains no <c>null</c> element;
        ///   otherwise, a new array containing the non-<c>null</c> elements of
        ///   <paramref name="array"/> in the same order.
        /// </returns>
        internal static T[] Compact<T>(this T?[] array)
            where T : class
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            for (var i = 0; i < array.Length; i++)
                if (array[i] is null)
                    return array.CompactCopy();

            return array!;
        }

        private static T[] CompactCopy<T>(this T?[] array)
            where T : class
        {
            var count = 0;

            for (var i = 0; i < array.Length; i++)
                if (null != array[i])
                    count++;

            var result = new T[count];
            count = 0;
            T? item;

            for (var i = 0; i < array.Length; i++)
                if (null != (item = array[i]))
                    result[count++] = item;

            return result;
        }
    }
}
