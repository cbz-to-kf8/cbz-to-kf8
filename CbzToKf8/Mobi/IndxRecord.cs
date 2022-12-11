// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CbzToKf8.Mobi
{
    internal sealed partial class IndxRecord
    {
        public IndxHeader Header;

        public TagxBlock Tagx;

        public IdxtBlockHeader IdxtHeader;
        public ushort[] IdxtOffsets;
        public IdxtEntry[] IdxtEntries;

        /// <exception cref="InvalidDataException"/>
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        /// <exception cref="NotSupportedException">The stream does not support reading and seeking.
        ///     </exception>
        public void ReadFrom(Stream stream, TagxBlock? tagx)
        {
            Debug.Assert(stream.Position == 0);

            using (var reader = new BigEndianBinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                Header.IndxMagic = reader.ReadUInt32();
                Header.HeaderLength = reader.ReadUInt32();

                if (Header.IndxMagic != IndxHeader.IndxMagicValue)
                    throw new InvalidDataException("Mismatched INDX magic number.");
                if (Header.HeaderLength < 8)
                    throw new InvalidDataException("Invalid INDX header length.");
                if (Header.HeaderLength != 192)
                    throw new InvalidDataException("Unexpected INDX header length.");

                Header.EntryDescriptor = reader.ReadUInt32();
                Header.Kind = reader.ReadUInt32();
                Header.Unknown0010 = reader.ReadUInt32();  // 0010
                Header.IdxtOffset = reader.ReadUInt32();
                Header.EntriesCount = reader.ReadUInt32();
                Header.TextEncoding = reader.ReadUInt32();
                Header.Unknown0020 = reader.ReadUInt32();  // 0020
                Header.TotalEntriesCount = reader.ReadUInt32();
                Header.OrdtOffset = reader.ReadUInt32();
                Header.LigtOffset = reader.ReadUInt32();
                Header.LigtEntriesCount = reader.ReadUInt32();  // 0030
                Header.StringRecordsCount = reader.ReadUInt32();
                Header.Unknown0038 = reader.ReadUInt32();
                Header.Unknown003C = reader.ReadUInt32();
                Header.Unknown0040 = reader.ReadUInt32();  // 0040
                Header.Unknown0044 = reader.ReadUInt32();
                Header.Unknown0048 = reader.ReadUInt32();
                Header.Unknown004C = reader.ReadUInt32();
                Header.Unknown0050 = reader.ReadUInt32();  // 0050
                Header.Unknown0054 = reader.ReadUInt32();
                Header.Unknown0058 = reader.ReadUInt32();
                Header.Unknown005C = reader.ReadUInt32();
                Header.Unknown0060 = reader.ReadUInt32();  // 0060
                Header.Unknown0064 = reader.ReadUInt32();
                Header.Unknown0068 = reader.ReadUInt32();
                Header.Unknown006C = reader.ReadUInt32();
                Header.Unknown0070 = reader.ReadUInt32();  // 0070
                Header.Unknown0074 = reader.ReadUInt32();
                Header.Unknown0078 = reader.ReadUInt32();
                Header.Unknown007C = reader.ReadUInt32();
                Header.Unknown0080 = reader.ReadUInt32();  // 0080
                Header.Unknown0084 = reader.ReadUInt32();
                Header.Unknown0088 = reader.ReadUInt32();
                Header.Unknown008C = reader.ReadUInt32();
                Header.Unknown0090 = reader.ReadUInt32();  // 0090
                Header.Unknown0094 = reader.ReadUInt32();
                Header.Unknown0098 = reader.ReadUInt32();
                Header.Unknown009C = reader.ReadUInt32();
                Header.Unknown00A0 = reader.ReadUInt32();  // 00A0
                Header.Unknown00A4 = reader.ReadUInt32();
                Header.Unknown00A8 = reader.ReadUInt32();
                Header.Unknown00AC = reader.ReadUInt32();
                Header.Unknown00B0 = reader.ReadUInt32();  // 00B0
                Header.Unknown00B4 = reader.ReadUInt32();
                Header.Unknown00B8 = reader.ReadUInt32();
                Header.Unknown00BC = reader.ReadUInt32();

                if (Header.Kind == 0)
                {
                    if (tagx != null)
                        throw new InvalidDataException("Unexpected INDX kind.");

                    Tagx = new TagxBlock();
                    Tagx.ReadFrom(stream);
                }

                stream.Position = Header.IdxtOffset;

                IdxtHeader.IdxtMagic = reader.ReadUInt32();

                if (IdxtHeader.IdxtMagic != IdxtBlockHeader.IdxtMagicValue)
                    throw new InvalidDataException("Mismatched IDXT magic number.");

                IdxtOffsets = new ushort[Header.EntriesCount];
                for (int i = 0; i < IdxtOffsets.Length; ++i)
                    IdxtOffsets[i] = reader.ReadUInt16();

                if (Header.Kind == 0)
                {
                    IdxtEntries = new IdxtEntry[Header.EntriesCount];
                    for (int i = 0; i < IdxtEntries.Length; ++i)
                    {
                        stream.Position = IdxtOffsets[i];

                        ref var entry = ref IdxtEntries[i];

                        entry.Length = reader.ReadByte();
                        entry.Data = reader.ReadBytes(entry.Length);
                        entry.EntriesCount = reader.ReadUInt16();
                    }
                }
                else
                {
                    if (tagx == null)
                        throw new InvalidDataException("Unexpected INDX kind.");

                    byte[] valuesDescriptor = new byte[tagx.Header.ValuesDescriptorLength];

                    var tagxEntries = tagx.Entries;
                    uint[] valuesLengths = new uint[tagxEntries.Length];

                    var valuesBuilder = new List<ulong>();
                    var tagsBuilder = new List<IdxtTag>();

                    IdxtEntries = new IdxtEntry[Header.EntriesCount];
                    for (int i = 0; i < IdxtEntries.Length; ++i)
                    {
                        stream.Position = IdxtOffsets[i];

                        ref var entry = ref IdxtEntries[i];

                        entry.Length = reader.ReadByte();
                        entry.Data = reader.ReadBytes(entry.Length);

                        reader.ReadExactly(valuesDescriptor, 0, valuesDescriptor.Length);

                        int controlBytesOffset = 0;
                        for (int j = 0; j < tagxEntries.Length; ++j)
                        {
                            var tagEntry = tagxEntries[j];

                            if (tagEntry.EndOfValuesDescriptorByte != 0)
                            {
                                controlBytesOffset += 1;
                                continue;
                            }

                            byte controlByte = valuesDescriptor[controlBytesOffset];
                            byte controlMask = tagEntry.ValuesDescriptorByteMask;
                            if (HasMultipleBitsSet(controlMask) && HasAllBitsSet(controlByte, controlMask))
                                valuesLengths[j] = ReadVarUInt32(reader, 7, out _);
                        }

                        controlBytesOffset = 0;
                        for (int j = 0; j < tagxEntries.Length; ++j)
                        {
                            var tagEntry = tagxEntries[j];

                            if (tagEntry.EndOfValuesDescriptorByte != 0)
                            {
                                controlBytesOffset += 1;
                                continue;
                            }

                            byte controlByte = valuesDescriptor[controlBytesOffset];
                            byte controlMask = tagEntry.ValuesDescriptorByteMask;
                            if (!HasMultipleBitsSet(controlMask))
                            {
                                if (HasAllBitsUnset(controlByte, controlMask))
                                    continue;

                                for (int n = 0; n < tagEntry.ValuesPerElement; ++n)
                                {
                                    ulong value = ReadVarUInt64(reader, int.MaxValue, out _);

                                    valuesBuilder.Add(value);
                                }
                            }
                            else if (!HasAllBitsSet(controlByte, controlMask))
                            {
                                if (HasAllBitsUnset(controlByte, controlMask))
                                    continue;

                                int shift = GetShift(controlMask);
                                int repeatCount = (controlByte & controlMask) >> shift;

                                for (int k = 0; k < repeatCount; ++k)
                                {
                                    for (int n = 0; n < tagEntry.ValuesPerElement; ++n)
                                    {
                                        ulong value = ReadVarUInt64(reader, int.MaxValue, out _);

                                        valuesBuilder.Add(value);
                                    }
                                }
                            }
                            else
                            {
                                uint valuesLength = valuesLengths[j];

                                while (valuesLength > 0)
                                {
                                    ulong value = ReadVarUInt64(reader, valuesLength, out uint valueLength);
                                    valuesLength -= valueLength;

                                    valuesBuilder.Add(value);
                                }
                            }

                            tagsBuilder.Add(new IdxtTag
                            {
                                TagEntry = tagEntry,
                                Values = valuesBuilder.ToArray()
                            });

                            valuesBuilder.Clear();
                        }

                        entry.Tags = tagsBuilder.ToArray();

                        tagsBuilder.Clear();
                    }
                }
            }
        }

        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        private static uint ReadVarUInt32(BigEndianBinaryReader reader, uint maxLength, out uint length)
        {
            uint value = 0;
            uint i;
            for (i = 0; i < maxLength; ++i)
            {
                byte b = reader.ReadByte();
                value = (value << 7) | ((uint)b & 0x7F);
                if ((b & 0x80) != 0)
                    break;
            }
            length = i;
            return value;
        }

        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        private static ulong ReadVarUInt64(BigEndianBinaryReader reader, uint maxLength, out uint length)
        {
            ulong value = 0;
            uint i;
            for (i = 0; i < maxLength; ++i)
            {
                byte b = reader.ReadByte();
                value = (value << 7) | ((uint)b & 0x7F);
                if ((b & 0x80) != 0)
                    break;
            }
            length = i;
            return value;
        }

        internal static bool HasAllBitsUnset(uint value, uint mask) => (value & mask) == 0;

        internal static bool HasAllBitsSet(uint value, uint mask) => (~value & mask) == 0;

        internal static bool HasMultipleBitsSet(uint value) => (value & (value - 1)) != 0;

        internal static int GetShift(byte value)
        {
            int count = 0;
            while ((value & 1) == 0 && value != 0)
            {
                value >>= 1;
                count += 1;
            }
            return count;
        }

        public struct IdxtEntry
        {
            public byte Length;
            public byte[] Data;
            public ushort EntriesCount;
            public IdxtTag[] Tags;
        }

        public struct IdxtTag
        {
            public TagxBlock.Entry TagEntry;
            public ulong[] Values;
        }
    }
}
