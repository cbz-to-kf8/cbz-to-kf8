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
using System.Text;

namespace CbzToKf8.Mobi
{
    internal sealed class HuffRecord
    {
        public const uint Huff4CC = 0x48554646;  // "HUFF"

        public uint HuffMagic;
        public uint HeaderLength;
        public uint LutBigEndianOffset;
        public uint CodeRangesBigEndianOffset;
        public uint LutLittleEndianOffset;
        public uint CodeRangesLittleEndianOffset;
        public LutEntry[] LutBigEndian;
        public CodeRangeEntry[] CodeRangesBigEndian;
        public LutEntry[] LutLittleEndian;
        public CodeRangeEntry[] CodeRangesLittleEndian;
        public byte[] TrailingData;

        /// <exception cref="InvalidDataException"/>
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        /// <exception cref="NotSupportedException">The stream does not support reading and seeking.
        ///     </exception>
        public void ReadFrom(Stream stream)
        {
            using (var reader = new BigEndianBinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                HuffMagic = reader.ReadUInt32();
                HeaderLength = reader.ReadUInt32();

                if (HuffMagic != Huff4CC)
                    throw new InvalidDataException("Mismatched HUFF magic number.");
                if (HeaderLength < 8)
                    throw new InvalidDataException("Invalid HUFF header length.");
                if (HeaderLength != 24)
                    throw new InvalidDataException("Unexpected HUFF header length.");

                LutBigEndianOffset = reader.ReadUInt32();
                CodeRangesBigEndianOffset = reader.ReadUInt32();
                LutLittleEndianOffset = reader.ReadUInt32();
                CodeRangesLittleEndianOffset = reader.ReadUInt32();

                if (LutBigEndianOffset != 0x18)
                    throw new InvalidDataException("Unexpected offset.");
                if (CodeRangesBigEndianOffset != 0x418)
                    throw new InvalidDataException("Unexpected offset.");
                if (LutLittleEndianOffset != 0x518)
                    throw new InvalidDataException("Unexpected offset.");
                if (CodeRangesLittleEndianOffset != 0x918)
                    throw new InvalidDataException("Unexpected offset.");

                LutBigEndian = new LutEntry[256];
                for (int i = 0; i < LutBigEndian.Length; ++i)
                    LutBigEndian[i].PackedFields = reader.ReadUInt32();

                CodeRangesBigEndian = new CodeRangeEntry[32];
                for (int i = 0; i < CodeRangesBigEndian.Length; ++i)
                {
                    ref var entry = ref CodeRangesBigEndian[i];
                    entry.MinCode = reader.ReadUInt32();
                    entry.MaxCode = reader.ReadUInt32();
                }
            }

            using (var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                LutLittleEndian = new LutEntry[256];
                for (int i = 0; i < LutLittleEndian.Length; ++i)
                    LutLittleEndian[i].PackedFields = reader.ReadUInt32();

                CodeRangesLittleEndian = new CodeRangeEntry[32];
                for (int i = 0; i < CodeRangesLittleEndian.Length; ++i)
                {
                    ref var entry = ref CodeRangesLittleEndian[i];
                    entry.MinCode = reader.ReadUInt32();
                    entry.MaxCode = reader.ReadUInt32();
                }

                TrailingData = reader.ReadBytes((int)(stream.Length - stream.Position));
            }
        }

        public struct LutEntry
        {
            internal uint PackedFields;

            public uint MaxCode
            {
                get => PackedFields >> 8;
                set => throw new NotImplementedException();  // TODO
            }

            public bool Terminal
            {
                get => (PackedFields & 0x80) != 0;
                set => throw new NotImplementedException();  // TODO
            }

            public byte Unused => (byte)((PackedFields & 0x60) >> 5);

            public byte CodeBitLength
            {
                get => (byte)(PackedFields & 0x1F);
                set => throw new NotImplementedException();  // TODO
            }
        }

        public struct CodeRangeEntry
        {
            public uint MinCode;
            public uint MaxCode;
        }
    }
}
