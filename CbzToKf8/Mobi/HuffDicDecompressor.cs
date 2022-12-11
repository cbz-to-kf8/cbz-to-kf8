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
    internal sealed class HuffDicDecompressor
    {
        private readonly HuffRecord.LutEntry[] _lut;
        private readonly HuffRecord.CodeRangeEntry[] _codeRanges;
        private readonly uint _indexShift;
        private readonly uint _indexMask;
        private readonly CdicRecord.Entry[][] _entries;

        public HuffDicDecompressor(HuffRecord huffRecord, CdicRecord[] cdicRecords)
        {
            _lut = huffRecord.LutBigEndian;
            _codeRanges = huffRecord.CodeRangesBigEndian;
            _indexShift = cdicRecords[0].IndexShift;
            _indexMask = (1u << (int)_indexShift) - 1;
            _entries = new CdicRecord.Entry[cdicRecords.Length][];

            for (int i = 0; i < cdicRecords.Length; ++i)
                _entries[i] = cdicRecords[i].Entries;
        }

        /// <exception cref="InvalidDataException"/>
        public int Decompress(byte[] source, int length, byte[] destination)
        {
            int sourceOffset = 0;
            int destinationOffset = 0;

            return Decompress(source, sourceOffset, length, destination, destinationOffset, depth: 0);
        }

        /// <exception cref="InvalidDataException"/>
        private int Decompress(byte[] source, int sourceOffset, int sourceLength, byte[] destination, int destinationOffset, int depth)
        {
            if (depth == 32)
                throw new InvalidDataException("Dictionary entry requires excessive recursion.");

            uint bits = 0;
            int bitsCount = 0;

            while (true)
            {
                while (bitsCount <= 24 && sourceLength > 0)
                {
                    bits |= (uint)source[sourceOffset] << (24 - bitsCount);
                    bitsCount += 8;
                    sourceOffset += 1;
                    sourceLength -= 1;
                }

                var lutEntry = _lut[bits >> 24];
                byte codeBitLength = lutEntry.CodeBitLength;
                if (codeBitLength < 1)
                    throw new InvalidDataException("Invalid HUFF record.");
                uint code = bits >> (32 - codeBitLength);
                uint maxCode;
                if (lutEntry.Terminal)
                {
                    maxCode = lutEntry.MaxCode;
                }
                else
                {
                    while (code < _codeRanges[codeBitLength - 1].MinCode)
                    {
                        codeBitLength += 1;
                        if (codeBitLength > 32)
                            throw new InvalidDataException("Invalid HUFF record.");
                        code = bits >> (32 - codeBitLength);
                    }
                    maxCode = _codeRanges[codeBitLength - 1].MaxCode;
                }
                uint index = maxCode - code;

                if (bitsCount < codeBitLength)
                    return destinationOffset;

                bitsCount -= codeBitLength;
                bits <<= codeBitLength;

                var entries = _entries[index >> (int)_indexShift];
                var entry = entries[index & _indexMask];

                byte[] entryData = entry.Data;

                if (entry.Literal)
                {
                    Array.Copy(entryData, 0, destination, destinationOffset, entryData.Length);
                    destinationOffset += entryData.Length;
                }
                else
                {
                    destinationOffset = Decompress(entryData, 0, entryData.Length, destination, destinationOffset, depth + 1);
                }
            }
        }
    }
}
