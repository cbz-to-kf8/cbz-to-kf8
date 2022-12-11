// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CbzToKf8.Mobi
{
    internal sealed class CdicRecord
    {
        public const uint Cdic4CC = 0x43444943;  // "CDIC"

        public uint CdicMagic;
        public uint HeaderLength;
        public uint TotalEntriesCount;
        public uint IndexShift;
        public ushort[] EntryOffsets;
        public Entry[] Entries;

        /// <exception cref="InvalidDataException"/>
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        /// <exception cref="NotSupportedException">The stream does not support reading and seeking.
        ///     </exception>
        public void ReadFrom(Stream stream, bool final)
        {
            Debug.Assert(stream.Position == 0);

            using (var reader = new BigEndianBinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                CdicMagic = reader.ReadUInt32();
                HeaderLength = reader.ReadUInt32();

                if (CdicMagic != Cdic4CC)
                    throw new InvalidDataException("Mismatched CDIC magic number.");
                if (HeaderLength < 8)
                    throw new InvalidDataException("Invalid CDIC header length.");
                if (HeaderLength != 16)
                    throw new InvalidDataException("Unexpected CDIC header length.");

                TotalEntriesCount = reader.ReadUInt32();
                IndexShift = reader.ReadUInt32();

                if (IndexShift >= 32)
                    throw new InvalidDataException("Invalid CDIC entries index shift.");

                uint entriesCount = 1u << (int)IndexShift;
                if (final)
                    entriesCount = TotalEntriesCount & (entriesCount - 1);

                EntryOffsets = new ushort[entriesCount];
                for (int i = 0; i < EntryOffsets.Length; ++i)
                    EntryOffsets[i] = reader.ReadUInt16();

                Entries = new Entry[entriesCount];
                for (int i = 0; i < EntryOffsets.Length; ++i)
                {
                    stream.Position = HeaderLength + EntryOffsets[i];

                    ref var entry = ref Entries[i];
                    entry.PackedFields = reader.ReadUInt16();

                    byte[] data = new byte[entry.Length];
                    reader.ReadExactly(data, 0, data.Length);
                    entry.Data = data;
                }
            }
        }

        public struct Entry
        {
            internal ushort PackedFields;

            public byte[] Data;

            public bool Literal
            {
                get => (PackedFields & 0x8000) != 0;
                set => throw new NotImplementedException();  // TODO
            }

            public ushort Length
            {
                get => (ushort)(PackedFields & 0x7FFF);
                set => throw new NotImplementedException();  // TODO
            }
        }
    }
}
