using System;

namespace SceneNavigation
{
    public readonly struct ByteSize
    {
        private readonly long _bytes;

        public ByteSize(long bytes)
        {
            if (bytes < 0)
            {
                throw new ArgumentOutOfRangeException("bytes", "Byte size cannot be negative.");
            }

            _bytes = bytes;
        }

        public override string ToString()
        {
            if (_bytes == 0)
            {
                return "0 B";
            }

            int suffixIndex = 0;
            double size = _bytes;

            while (size >= 1024 && suffixIndex < SizeSuffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return $"{size:0.##} {SizeSuffixes[suffixIndex]}";
        }

        public static implicit operator ByteSize(long bytes) => new(bytes);
        public static implicit operator long(ByteSize size) => size._bytes;

        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
    }
}