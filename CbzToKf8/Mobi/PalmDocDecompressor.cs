// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System.IO;

namespace CbzToKf8.Mobi
{
    internal static class PalmDocDecompressor
    {
        /// <exception cref="InvalidDataException"/>
        public static int Decompress(byte[] source, int length, byte[] destination)
        {
            int sourceOffset = 0;
            int destinationOffset = 0;

            while (length > 0)
            {
                byte b = source[sourceOffset];
                sourceOffset += 1;
                length -= 1;

                if (b < 0x80)
                {
                    if (b == 0 || b > 8)
                    {
                        destination[destinationOffset] = b;
                        destinationOffset += 1;
                        continue;
                    }

                    int literalLength = b;

                    if (length < literalLength)
                        throw new InvalidDataException("Compressed data is truncated.");
                    length -= literalLength;

                    do
                    {
                        destination[destinationOffset] = source[sourceOffset];
                        destinationOffset += 1;
                        sourceOffset += 1;
                        literalLength -= 1;
                    }
                    while (literalLength > 0);

                }
                else if (b < 0xC0)
                {
                    byte lowerB = source[sourceOffset];
                    sourceOffset += 1;
                    length -= 1;

                    int copyDistance = ((b << 5) | (lowerB >> 3)) & 0x7FF;
                    int copyLength = (lowerB & 0x7) + 3;
                    if (copyDistance == 0)
                        throw new InvalidDataException("Compressed data is invalid.");
                    int copyOffset = destinationOffset - copyDistance;
                    do
                    {
                        destination[destinationOffset] = destination[copyOffset];
                        copyOffset += 1;
                        destinationOffset += 1;
                        copyLength -= 1;
                    }
                    while (copyLength > 0);
                }
                else
                {
                    destination[destinationOffset] = 0x20;
                    destinationOffset += 1;
                    destination[destinationOffset] = (byte)(b ^ 0x80);
                    destinationOffset += 1;
                }
            }

            return destinationOffset;
        }
    }
}
