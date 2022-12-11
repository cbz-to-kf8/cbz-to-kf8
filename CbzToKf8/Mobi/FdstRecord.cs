// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System.IO;
using System.Text;

namespace CbzToKf8.Mobi
{
    internal sealed class FdstRecord
    {
        public FdstHeader Header;

        public Entry[] Entries;
        public byte[] TrailingData;

        /// <exception cref="InvalidDataException"/>
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        /// <exception cref="System.NotSupportedException">The stream does not support reading and
        ///     seeking.</exception>
        public void ReadFrom(Stream stream)
        {
            using (var reader = new BigEndianBinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                Header.FdstMagic = reader.ReadUInt32();
                Header.HeaderLength = reader.ReadUInt32();

                if (Header.FdstMagic != FdstHeader.FdstMagicValue)
                    throw new InvalidDataException("Mismatched FDST magic number.");
                if (Header.HeaderLength < 8)
                    throw new InvalidDataException("Invalid FDST length.");
                if (Header.HeaderLength != 12)
                    throw new InvalidDataException("Unexpected FDST length.");

                Header.EntriesCount = reader.ReadUInt32();

                Entries = new Entry[Header.EntriesCount];
                for (int i = 0; i < Entries.Length; ++i)
                {
                    ref var entry = ref Entries[i];
                    entry.StartOffset = reader.ReadUInt32();
                    entry.EndOffset = reader.ReadUInt32();
                }

                TrailingData = reader.ReadBytes((int)(stream.Length - stream.Position));
            }
        }

        public struct Entry
        {
            public uint StartOffset;
            public uint EndOffset;
        }
    }
}
