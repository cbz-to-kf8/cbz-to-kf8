// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using Xunit;

namespace CbzToKf8.Mobi.Tests
{
    public static class PalmDocCompressorTests
    {
        public static TheoryData<byte[], byte[]> Compress_Literal_Data()
        {
            var result = new TheoryData<byte[], byte[]>();

            result.Add(new byte[] { 0x00 }, new byte[] { 0x00 });
            for (int b = 0x09; b <= 0x7F; ++b)
                result.Add(new byte[] { (byte)b }, new byte[] { (byte)b });

            result.Add(new byte[] { 0x00, 0x00, 0x00 }, new byte[] { 0x00, 0x00, 0x00 });
            for (int b = 0x09; b <= 0x7F; ++b)
                result.Add(new byte[] { (byte)b, (byte)b, (byte)b }, new byte[] { (byte)b, (byte)b, (byte)b });

            return result;
        }

        [Theory]
        [MemberData(nameof(Compress_Literal_Data))]
        public static void Compress_Literal_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }

        public static TheoryData<byte[], byte[]> Compress_EscapedLiterals_Data()
        {
            var result = new TheoryData<byte[], byte[]>();

            for (int b = 0x01; b <= 0x08; ++b)
                result.Add(new byte[] { (byte)b }, new byte[] { 0x01, (byte)b });
            for (int b = 0x80; b <= 0xFF; ++b)
                result.Add(new byte[] { (byte)b }, new byte[] { 0x01, (byte)b });

            for (int b = 0x01; b <= 0x08; ++b)
                result.Add(new byte[] { (byte)b, (byte)b, (byte)b }, new byte[] { 0x03, (byte)b, (byte)b, (byte)b });
            for (int b = 0x80; b <= 0xFF; ++b)
                result.Add(new byte[] { (byte)b, (byte)b, (byte)b }, new byte[] { 0x03, (byte)b, (byte)b, (byte)b });

            return result;
        }

        [Theory]
        [MemberData(nameof(Compress_EscapedLiterals_Data))]
        [InlineData(new byte[] { 0x01, 0x03, 0x05, 0x07, 0x02, 0x04, 0x06, 0x08 },
                    new byte[] { 0x08, 0x01, 0x03, 0x05, 0x07, 0x02, 0x04, 0x06, 0x08 })]
        [InlineData(new byte[] { 0x01, 0x03, 0x05, 0x07, 0x02, 0x04, 0x06, 0x08,
                                 0x07 },
                    new byte[] { 0x08, 0x01, 0x03, 0x05, 0x07, 0x02, 0x04, 0x06, 0x08,
                                 0x01, 0x07 })]
        [InlineData(new byte[] { 0x01, 0x03, 0x05, 0x07, 0x02, 0x04, 0x06, 0x08,
                                 0x07, 0x05 },
                    new byte[] { 0x08, 0x01, 0x03, 0x05, 0x07, 0x02, 0x04, 0x06, 0x08,
                                 0x02, 0x07, 0x05 })]
        public static void Compress_EscapedLiterals_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }

        [Theory]
        [InlineData(new byte[] { 0x01, 0x41 },
                    new byte[] { 0x02, 0x01, 0x41 })]
        [InlineData(new byte[] { 0x01, 0x41, 0x00 },
                    new byte[] { 0x03, 0x01, 0x41, 0x00 })]
        public static void Compress_EscapedLiteralsFollowedByLiteral_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }

        [Theory]
        [InlineData(new byte[] { 0x01, 0x20, 0x41 },
                    new byte[] { 0x01, 0x01, 0xC1 })]
        [InlineData(new byte[] { 0x01, 0x20, 0x41, 0x00 },
                    new byte[] { 0x01, 0x01, 0xC1, 0x00 })]
        public static void Compress_EscapedLiteralsFolloweByBytePair_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }

        public static TheoryData<byte[], byte[]> Compress_Match_Data()
        {
            var result = new TheoryData<byte[], byte[]>();

            // Match at max distance.
            {
                byte[] source = new byte[0x802];
                source[0x000] = source[0x7FF] = 0x41;
                source[0x001] = source[0x800] = 0x42;
                source[0x002] = source[0x801] = 0x43;

                byte[] expectedResult = new byte[0x1A0];
                expectedResult[0x000] = 0x41;
                expectedResult[0x001] = 0x42;
                expectedResult[0x002] = 0x43;
                expectedResult[0x003] = 0x00;
                for (int i = 0x004; i < 0x19C; i += 2)
                {
                    expectedResult[i] = 0x80;
                    expectedResult[i + 1] = 0x0F;
                }
                expectedResult[0x19C] = 0x80;
                expectedResult[0x19D] = 0x08;
                expectedResult[0x19E] = 0xBF;
                expectedResult[0x19F] = 0xF8;

                result.Add(source, expectedResult);
            }

            // Match beyond max distance.
            {
                byte[] source = new byte[0x803];
                source[0x000] = source[0x800] = 0x41;
                source[0x001] = source[0x801] = 0x42;
                source[0x002] = source[0x802] = 0x43;

                byte[] expectedResult = new byte[0x1A1];
                expectedResult[0x000] = 0x41;
                expectedResult[0x001] = 0x42;
                expectedResult[0x002] = 0x43;
                expectedResult[0x003] = 0x00;
                for (int i = 0x004; i < 0x19C; i += 2)
                {
                    expectedResult[i] = 0x80;
                    expectedResult[i + 1] = 0x0F;
                }
                expectedResult[0x19C] = 0x80;
                expectedResult[0x19D] = 0x09;
                expectedResult[0x19E] = 0x41;
                expectedResult[0x19F] = 0x42;
                expectedResult[0x1A0] = 0x43;

                result.Add(source, expectedResult);
            }

            return result;
        }

        [Theory]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00 },
                    new byte[] { 0x00, 0x80, 0x08 })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                    new byte[] { 0x00, 0x80, 0x0F })]
        [InlineData(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
                    new byte[] { 0x00, 0x80, 0x0F, 0x00 })]
        [InlineData(new byte[] { 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x40, 0x41, 0x42, 0x43 },
                    new byte[] { 0x40, 0x41, 0x42, 0x43, 0x44, 0x45, 0x46, 0x47, 0x80, 0x41 })]
        [InlineData(new byte[] { 0x40, 0x41, 0x42, 0x43, 0x40, 0x41, 0x42, 0x44, 0x40, 0x41, 0x42, 0x43 },
                    new byte[] { 0x40, 0x41, 0x42, 0x43, 0x80, 0x20, 0x44, 0x80, 0x41 })]
        [InlineData(new byte[] { 0x40, 0x41, 0x42, 0x44, 0x40, 0x41, 0x42, 0x43, 0x40, 0x41, 0x42, 0x43 },
                    new byte[] { 0x40, 0x41, 0x42, 0x44, 0x80, 0x20, 0x43, 0x80, 0x21 })]
        [InlineData(new byte[] { 0x40, 0x41, 0x42, 0xFF, 0x40, 0x41, 0x42 },
                    new byte[] { 0x40, 0x41, 0x42, 0x01, 0xFF, 0x80, 0x20 })]
        [MemberData(nameof(Compress_Match_Data))]
        public static void Compress_Match_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }

        public static TheoryData<byte[], byte[]> Compress_BytePair_Data()
        {
            var result = new TheoryData<byte[], byte[]>();

            for (int b = 0x40; b <= 0x7F; ++b)
                result.Add(new byte[] { 0x20, (byte)b }, new byte[] { (byte)(b ^ 0x80) });

            for (int b = 0x40; b <= 0x7F; ++b)
                result.Add(new byte[] { 0x20, (byte)b, 0x20, (byte)b }, new byte[] { (byte)(b ^ 0x80), (byte)(b ^ 0x80) });

            return result;
        }

        [Theory]
        [MemberData(nameof(Compress_BytePair_Data))]
        public static void Compress_BytePair_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }

        public static TheoryData<byte[], byte[]> Compress_NonBytePair_Data()
        {
            var result = new TheoryData<byte[], byte[]>();

            result.Add(new byte[] { 0x20, 0x00 }, new byte[] { 0x20, 0x00 });
            for (int b = 0x01; b <= 0x08; ++b)
                result.Add(new byte[] { 0x20, (byte)b }, new byte[] { 0x20, 0x01, (byte)b });
            for (int b = 0x09; b <= 0x3F; ++b)
                result.Add(new byte[] { 0x20, (byte)b }, new byte[] { 0x20, (byte)b });
            for (int b = 0x80; b <= 0xFF; ++b)
                result.Add(new byte[] { 0x20, (byte)b }, new byte[] { 0x20, 0x01, (byte)b });

            result.Add(new byte[] { 0x20, 0x00, 0x00 }, new byte[] { 0x20, 0x00, 0x00 });
            for (int b = 0x01; b <= 0x08; ++b)
                result.Add(new byte[] { 0x20, (byte)b, 0x00 }, new byte[] { 0x20, 0x02, (byte)b, 0x00 });
            for (int b = 0x09; b <= 0x3F; ++b)
                result.Add(new byte[] { 0x20, (byte)b, 0x00 }, new byte[] { 0x20, (byte)b, 0x00 });
            for (int b = 0x80; b <= 0xFF; ++b)
                result.Add(new byte[] { 0x20, (byte)b, 0x00 }, new byte[] { 0x20, 0x02, (byte)b, 0x00 });

            return result;
        }

        [Theory]
        [MemberData(nameof(Compress_NonBytePair_Data))]
        public static void Compress_NonBytePair_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }

        [Theory]
        [InlineData(new byte[] { 0x20, 0x41, 0x42, 0x20, 0x41, 0x42 },
                    new byte[] { 0xC1, 0x42, 0xC1, 0x42 })]
        public static void Compress_BytePairPreferredOverThreeByteMatch_ProducesExpectedResult(byte[] source, byte[] expectedResult)
        {
            byte[] compressed = new byte[PalmDocCompressor.GetMaxCompressedSize(source.Length)];

            int compressedLength = PalmDocCompressor.Compress(source, compressed);

            byte[] decompressed = new byte[source.Length];
            int decompressedLength = PalmDocDecompressor.Decompress(compressed.AsSpan(0, compressedLength), decompressed);
            Assert.Equal(source, decompressed.AsSpan(0, decompressedLength).ToArray());

            Assert.Equal(expectedResult, compressed.AsSpan(0, compressedLength).ToArray());
        }
    }
}
