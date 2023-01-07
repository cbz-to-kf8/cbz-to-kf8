// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using System.IO;

namespace CbzToKf8.Mobi
{
    internal static class PalmDocDecompressor
    {
        /// <exception cref="InvalidDataException"/>
        public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int sourceOffset = 0;
            int destinationOffset = 0;

            while (source.Length - sourceOffset > 0)
            {
                byte s0 = source[sourceOffset];
                sourceOffset += 1;

                if (s0 < 0x80)
                {
                    if (s0 == 0 || s0 > 8)
                    {
                        if (destination.Length - destinationOffset < 1)
                            throw new InvalidDataException("Decompressed data is too long.");

                        destination[destinationOffset] = s0;
                        destinationOffset += 1;
                        continue;
                    }

                    int literalLength = s0;

                    if (source.Length - sourceOffset < literalLength)
                        throw new InvalidDataException("Compressed data is truncated.");

                    if (destination.Length - destinationOffset < literalLength)
                        throw new InvalidDataException("Decompressed data is too long.");

                    do
                    {
                        destination[destinationOffset] = source[sourceOffset];
                        destinationOffset += 1;

                        sourceOffset += 1;

                        literalLength -= 1;
                    }
                    while (literalLength > 0);
                }
                else if (s0 < 0xC0)
                {
                    if (source.Length - sourceOffset < 1)
                        throw new InvalidDataException("Compressed data is truncated.");
                    byte s1 = source[sourceOffset];
                    sourceOffset += 1;

                    int distance = ((s0 << 5) | (s1 >> 3)) & 0x7FF;
                    if (distance == 0 || distance > destinationOffset)
                        throw new InvalidDataException("Compressed data is invalid.");
                    int length = (s1 & 0x7) + 3;

                    if (destination.Length - destinationOffset < length)
                        throw new InvalidDataException("Decompressed data is too long.");

                    int offset = destinationOffset - distance;
                    do
                    {
                        destination[destinationOffset] = destination[offset];
                        destinationOffset += 1;

                        offset += 1;
                        length -= 1;
                    }
                    while (length > 0);
                }
                else
                {
                    if (destination.Length - destinationOffset < 2)
                        throw new InvalidDataException("Decompressed data is too long.");

                    destination[destinationOffset] = 0x20;
                    destinationOffset += 1;
                    destination[destinationOffset] = (byte)(s0 ^ 0x80);
                    destinationOffset += 1;
                }
            }

            return destinationOffset;
        }
    }
}
