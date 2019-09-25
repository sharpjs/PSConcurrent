namespace PSConcurrent
{
    internal static class ArrayExtensions
    {
        internal static T[] CompactOrEmpty<T>(this T?[]? array)
            where T : class
        {
            if (array == null)
                return new T[0];

            for (var i = 0; i < array.Length; i++)
                if (array[i] is null)
                    return array.CompactToNew();

            return array!;
        }

        private static T[] CompactToNew<T>(this T?[] array)
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
