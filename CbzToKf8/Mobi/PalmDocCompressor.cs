// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;

namespace CbzToKf8.Mobi
{
    internal static class PalmDocCompressor
    {
        public static int GetMaxCompressedSize(int sourceLength)
        {
            return checked(sourceLength + sourceLength / 8 + 1);
        }

        /// <exception cref="ArgumentException"/>
        public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (destination.Length < GetMaxCompressedSize(source.Length))
                throw new ArgumentException("Insufficient destination length.", nameof(destination));

            if (source.Length == 0)
                return 0;

            int sourceOffset = 0;
            int destinationOffset = 0;

            byte s0;
            byte s1 = source[sourceOffset];
            byte s2 = source.Length - sourceOffset > 1
                ? source[sourceOffset + 1]
                : s1;

            int pendingLiteralCount = 0;

            // Maps hash of 3 bytes at offset to offset of first potential match.
            ushort[] offsets = new ushort[0x1000];

            // Maps offset of potential match to offset of next potential match (via distance).
            ushort[] nextDistances = new ushort[0x800];

            while (source.Length - sourceOffset > 2)
            {
                s0 = s1;
                s1 = s2;
                s2 = source[sourceOffset + 2];

                int bestLength = 0;
                int bestDistance = 0;

                ushort initialOffset = SwapOffset(offsets, s0, s1, s2, sourceOffset);

                ushort initialDistance = GetDistance(sourceOffset, initialOffset);
                for (int distance = initialDistance; distance <= 0x7FF;)
                {
                    int offset = sourceOffset - distance;

                    int maxLength = source.Length - sourceOffset;
                    if (maxLength > 10)
                        maxLength = 10;

                    int length;
                    for (length = 0; length < maxLength; ++length)
                        if (source[offset + length] != source[sourceOffset + length])
                            break;

                    if (bestLength < length)
                    {
                        bestLength = length;
                        bestDistance = distance;

                        if (bestLength == 10)
                            break;
                    }

                    distance += nextDistances[offset & 0x7FF];
                }
                nextDistances[sourceOffset & 0x7FF] = initialDistance;

                if (bestLength <= 3 && s0 == 0x20 && (uint)(s1 - 0x40) < 0x80 - 0x40)
                {
                    if (pendingLiteralCount > 0)
                        FlushPendingLiterals(source, destination);

                    // Encode space-letter pair.
                    destination[destinationOffset] = (byte)(s1 ^ 0x80);
                    destinationOffset += 1;

                    AdvanceSource(source);

                    sourceOffset += 1;
                }
                else if (bestLength >= 3)
                {
                    if (pendingLiteralCount > 0)
                        FlushPendingLiterals(source, destination);

                    // Encode distance-length pair.
                    destination[destinationOffset] = (byte)(0x80 | (bestDistance >> 5));
                    destinationOffset += 1;
                    destination[destinationOffset] = (byte)((bestDistance << 3) | (bestLength - 3));
                    destinationOffset += 1;

                    while (true)
                    {
                        bestLength -= 1;
                        if (bestLength == 0)
                            break;

                        AdvanceSource(source);
                    }

                    sourceOffset += 1;
                }
                else if (pendingLiteralCount == 0 && (s0 == 0x00 || (uint)(s0 - 0x09) < 0x80 - 0x09))
                {
                    // Encode unescaped literal.
                    destination[destinationOffset] = s0;
                    destinationOffset += 1;

                    sourceOffset += 1;
                }
                else
                {
                    // Add pending escaped literal.
                    pendingLiteralCount += 1;

                    sourceOffset += 1;

                    if (pendingLiteralCount == 8)
                        FlushPendingLiterals(source, destination);
                }
            }

            while (source.Length - sourceOffset > 0)
            {
                s0 = s1;
                s1 = s2;

                if (s0 == 0x20 && source.Length - sourceOffset == 2)
                {
                    if ((uint)(s1 - 0x40) < 0x80 - 0x40)
                    {
                        if (pendingLiteralCount > 0)
                            FlushPendingLiterals(source, destination);

                        // Encode space-letter pair.
                        destination[destinationOffset] = (byte)(s1 ^ 0x80);
                        destinationOffset += 1;

                        sourceOffset += 2;

                        break;
                    }
                }

                if (pendingLiteralCount == 0 && (s0 == 0x00 || (uint)(s0 - 0x09) < 0x80 - 0x09))
                {
                    // Encode unescaped literal.
                    destination[destinationOffset] = s0;
                    destinationOffset += 1;

                    sourceOffset += 1;
                }
                else
                {
                    // Add pending escaped literal.
                    pendingLiteralCount += 1;

                    sourceOffset += 1;

                    if (pendingLiteralCount == 8)
                        FlushPendingLiterals(source, destination);
                }
            }

            if (pendingLiteralCount > 0)
                FlushPendingLiterals(source, destination);

            return destinationOffset;

            static ushort SwapOffset(ushort[] offsets, byte s0, byte s1, byte s2, int sourceOffset)
            {
                uint hash = (uint)((s0 << 8) ^ (s1 << 4) ^ s2 ^ (s0 >> 4)) & 0xFFF;
                ushort offset = offsets[hash];
                offsets[hash] = (ushort)(sourceOffset | 0x8000);
                return offset;
            }

            static ushort GetDistance(int sourceOffset, ushort offset)
            {
                int distance = sourceOffset - offset;
                distance &= (short)offset >> 16;  // Invalidate distance if offset MSB is unset.
                distance = (distance - 1 & 0x7FF) + 1;
                return (ushort)distance;
            }

            void AdvanceSource(ReadOnlySpan<byte> source)
            {
                sourceOffset += 1;

                s0 = s1;
                s1 = s2;
                if (source.Length - sourceOffset > 2)
                {
                    s2 = source[sourceOffset + 2];

                    ushort offset = SwapOffset(offsets, s0, s1, s2, sourceOffset);

                    ushort distance = GetDistance(sourceOffset, offset);
                    nextDistances[sourceOffset & 0x7FF] = distance;
                }
            }

            void FlushPendingLiterals(ReadOnlySpan<byte> source, Span<byte> destination)
            {
                destination[destinationOffset] = (byte)pendingLiteralCount;
                destinationOffset += 1;
                do
                {
                    destination[destinationOffset] = source[sourceOffset - pendingLiteralCount];
                    destinationOffset += 1;

                    pendingLiteralCount -= 1;
                }
                while (pendingLiteralCount > 0);
            }
        }
    }
}
