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
    internal sealed class TagxBlock
    {
        public TagxBlockHeader Header;

        public Entry[] Entries;
        public byte[] TrailingData;

        /// <exception cref="InvalidDataException"/>
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        /// <exception cref="System.NotSupportedException">The stream does not support reading
        ///     and seeking.</exception>
        public void ReadFrom(Stream stream)
        {
            using (var reader = new BigEndianBinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                long tagxPosition = stream.Position;

                Header.TagxMagic = reader.ReadUInt32();
                Header.TagxLength = reader.ReadUInt32();

                if (Header.TagxMagic != TagxBlockHeader.TagxMagicValue)
                    throw new InvalidDataException("Mismatched TAGX magic number.");
                if (Header.TagxLength < 8)
                    throw new InvalidDataException("Invalid TAGX length.");
                if (Header.TagxLength < 12)
                    throw new InvalidDataException("Unexpected TAGX length.");

                Header.ValuesDescriptorLength = reader.ReadUInt32();
                uint descriptorLength = 0;
                uint tagEntriesCount = (Header.TagxLength - 12) / 4;
                Entries = new Entry[tagEntriesCount];
                for (int i = 0; i < Entries.Length; ++i)
                {
                    ref var entry = ref Entries[i];

                    entry.TagId = reader.ReadByte();
                    entry.ValuesPerElement = reader.ReadByte();
                    entry.ValuesDescriptorByteMask = reader.ReadByte();
                    entry.EndOfValuesDescriptorByte = reader.ReadByte();

                    if (entry.EndOfValuesDescriptorByte != 0)
                        descriptorLength += 1;
                }

                if (descriptorLength != Header.ValuesDescriptorLength)
                    throw new InvalidDataException("Descriptor length does not match tag specification in TAGX.");

                TrailingData = reader.ReadBytes((int)(tagxPosition + Header.TagxLength - stream.Position));
            }
        }

        public struct Entry
        {
            public byte TagId;
            public byte ValuesPerElement;
            public byte ValuesDescriptorByteMask;
            public byte EndOfValuesDescriptorByte;
        }
    }
}
