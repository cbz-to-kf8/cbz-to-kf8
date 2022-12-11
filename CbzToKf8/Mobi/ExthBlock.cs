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
    internal sealed class ExthBlock
    {
        public ExthBlockHeader Header;

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
                Header.ExthMagic = reader.ReadUInt32();
                Header.ExthLength = reader.ReadUInt32();

                if (Header.ExthMagic != ExthBlockHeader.ExthMagicValue)
                    throw new InvalidDataException("Mismatched EXTH magic number.");
                if (Header.ExthLength < 12)
                    throw new InvalidDataException("Invalid EXTH length.");

                long endPosition = stream.Position - 8 + Header.ExthLength;

                Header.EntriesCount = reader.ReadUInt32();

                Entries = new Entry[Header.EntriesCount];

                for (int i = 0; i < Entries.Length; ++i)
                {
                    var entry = new Entry();

                    entry.Tag = (ExthTag)reader.ReadUInt32();
                    entry.Length = reader.ReadUInt32();

                    if (entry.Length < 8)
                        throw new InvalidDataException("Invalid EXTH entry length.");
                    if (endPosition - (stream.Position - 8) < entry.Length)
                        throw new InvalidDataException("Insufficient data for EXTH entry length.");

                    entry.Data = new byte[entry.Length - 8];
                    reader.ReadExactly(entry.Data, 0, entry.Data.Length);

                    Entries[i] = entry;
                }

                TrailingData = reader.ReadBytes((int)(endPosition - stream.Position));
            }
        }

        public struct Entry
        {
            public ExthTag Tag;
            public uint Length;
            public byte[] Data;
        }
    }
}
