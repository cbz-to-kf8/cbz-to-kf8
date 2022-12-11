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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace CbzToKf8.Mobi
{
    internal sealed class IndxBuilder : IDisposable
    {
        private const uint _headerLength = 192;

        private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly MemoryStream _metaentryRecord;

        private readonly List<MemoryStream> _entryRecords;

        private readonly List<MemoryStream> _stringRecords;

        private readonly TagxBlock.Entry[] _tagxEntries;

        private readonly uint _valuesDescriptorLength;

        private uint _totalEntriesCount;

        private readonly List<ushort> _metaentryOffsets;

        private readonly MemoryStream _metaentryData;

        private readonly BigEndianBinaryWriter _metaentryDataWriter;

        private readonly MemoryStream _metaentryLastKeyStream;

        private readonly StreamWriter _metaentryLastKeyStreamWriter;

        private readonly List<ushort> _entryOffsets;

        private readonly MemoryStream _entryData;

        private readonly MemoryStream _buffer;

        private readonly StreamWriter _bufferWriter;

        private MemoryStream? _stringRecord;

        private bool _built;

        /// <exception cref="ArgumentNullException"/>
        public IndxBuilder(TagxBlock.Entry[] tagxEntries)
        {
            if (tagxEntries is null)
                throw new ArgumentNullException(nameof(tagxEntries));

            _metaentryRecord = new MemoryStream();

            _entryRecords = new List<MemoryStream>();

            _stringRecords = new List<MemoryStream>();

            _tagxEntries = tagxEntries;
            foreach (var tagxEntry in _tagxEntries)
                if (tagxEntry.EndOfValuesDescriptorByte != 0)
                    _valuesDescriptorLength += 1;

            _metaentryOffsets = new List<ushort>();

            _metaentryData = new MemoryStream();
            _metaentryDataWriter = new BigEndianBinaryWriter(_metaentryData);

            _metaentryLastKeyStream = new MemoryStream();
            _metaentryLastKeyStreamWriter = new StreamWriter(_metaentryLastKeyStream, _encoding);

            _entryOffsets = new List<ushort>();

            _entryData = new MemoryStream();

            _buffer = new MemoryStream();
            _bufferWriter = new StreamWriter(_buffer, _encoding);
        }

        public void Dispose()
        {
            if (!_built)
            {
                _metaentryRecord.Dispose();
                foreach (var entryRecord in _entryRecords)
                    entryRecord.Dispose();
                foreach (var stringRecord in _stringRecords)
                    stringRecord.Dispose();
            }

            _metaentryData.Dispose();
            _metaentryDataWriter.Dispose();
            _metaentryLastKeyStream.Dispose();
            _metaentryLastKeyStreamWriter.Dispose();
            _entryData.Dispose();
            _buffer.Dispose();
            _bufferWriter.Dispose();
            _stringRecord?.Dispose();
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:System.IO.MemoryStream.SetLength(System.Int64) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.MemoryStream.WriteByte(System.Byte) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Flush -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.NotSupportedException
        public void AddEntry(string key, Tag[] tags)
        {
            if (key is null)
                throw new ArgumentNullException(nameof(key));
            if (tags is null)
                throw new ArgumentNullException(nameof(tags));

            ThrowIfBuilt();

            _metaentryLastKeyStream.SetLength(0);
            _metaentryLastKeyStreamWriter.Write(key);
            _metaentryLastKeyStreamWriter.Flush();

            if (_metaentryLastKeyStream.Length > 255)
                throw new ArgumentException(null, nameof(key));

            uint valueDescriptorByteIndex = 0;
            byte valueDescriptorByte = 0;
            _buffer.Position = _valuesDescriptorLength;
            foreach (var tagxEntry in _tagxEntries)
            {
                if (tagxEntry.EndOfValuesDescriptorByte != 0)
                {
                    _buffer.Position = valueDescriptorByteIndex;
                    _buffer.WriteByte(valueDescriptorByte);
                    _buffer.Position = _buffer.Length;
                    valueDescriptorByteIndex += 1;
                    valueDescriptorByte = 0;
                    continue;
                }

                foreach (var tag in tags)
                {
                    if (tag.TagId != tagxEntry.TagId)
                        continue;

                    if (!IndxRecord.HasMultipleBitsSet(tagxEntry.ValuesDescriptorByteMask))
                    {
                        if (tag.Values.Length != tagxEntry.ValuesPerElement)
                            throw new InvalidOperationException();  // TODO

                        valueDescriptorByte |= tagxEntry.ValuesDescriptorByteMask;

                        foreach (ulong value in tag.Values)
                            WriteVarUInt64(_buffer, value);
                    }
                    else
                    {
                        int shift = IndxRecord.GetShift(tagxEntry.ValuesDescriptorByteMask);
                        int maxElementsCount = (tagxEntry.ValuesDescriptorByteMask >> shift) - 1;
                        int elementsCount = tag.Values.Length / tagxEntry.ValuesPerElement;
                        int orphanedValuesCount = tag.Values.Length % tagxEntry.ValuesPerElement;

                        if (elementsCount == 0 || elementsCount > maxElementsCount || orphanedValuesCount != 0)
                            throw new NotImplementedException();  // TODO

                        valueDescriptorByte |= (byte)(elementsCount << shift);

                        foreach (ulong value in tag.Values)
                            WriteVarUInt64(_buffer, value);
                    }

                    break;
                }
            }

            EnsureEntryRecordCapacity(1 + (uint)_metaentryLastKeyStream.Length + (uint)_buffer.Length);

            _entryOffsets.Add((ushort)_entryData.Position);

            _entryData.WriteByte((byte)_metaentryLastKeyStream.Length);

            _metaentryLastKeyStream.Position = 0;
            _metaentryLastKeyStream.CopyTo(_entryData);

            _buffer.Position = 0;
            _buffer.CopyTo(_entryData);
            _buffer.SetLength(0);
        }

        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:System.IO.MemoryStream.SetLength(System.Int64) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Flush -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.NotSupportedException
        public uint AddString(string value)
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value));

            ThrowIfBuilt();

            EnsureStringRecordCapacity();

            ushort position = (ushort)_stringRecord.Position;

            _bufferWriter.Write(value);
            _bufferWriter.Flush();

            WriteVarUInt32(_stringRecord, (uint)_buffer.Length);

            _buffer.Position = 0;
            _buffer.CopyTo(_stringRecord);
            _buffer.SetLength(0);

            return (uint)((_stringRecords.Count << 16) | position);
        }

        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt16) -T:System.IO.IOException
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.BinaryWriter.Write(System.Byte) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.MemoryStream.SetLength(System.Int64) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.MemoryStream.WriteByte(System.Byte) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.NotSupportedException
        public MemoryStream Build(out MemoryStream[] entryRecords, out MemoryStream[] stringRecords)
        {
            ThrowIfBuilt();

            FinishEntryRecord();

            FinishStringRecord();

            Debug.Assert(_entryRecords.Count == _metaentryOffsets.Count);

            uint metaentriesCount = (uint)_metaentryOffsets.Count;

            uint tagxBlockOffset = _headerLength;
            uint tagxBlockLength = 12 + (uint)_tagxEntries.Length * 4;

            uint metaentryDataOffset = tagxBlockOffset + tagxBlockLength;
            uint metaentryDataLength = (uint)_metaentryData.Length;
            uint metaentryDataPadLength = (~metaentryDataLength + 1) % 4;

            uint idxtBlockOffset = metaentryDataOffset + metaentryDataLength + metaentryDataPadLength;
            uint idxtBlockLength = 4 + metaentriesCount * 2;
            uint idxtBlockPadLength = (~idxtBlockLength + 1) % 4;

            uint recordLength = idxtBlockOffset + idxtBlockLength + idxtBlockPadLength;

            using (var writer = new BigEndianBinaryWriter(_metaentryRecord, Encoding.Default, leaveOpen: true))
            {
                var header = new IndxHeader()
                {
                    IndxMagic = IndxHeader.IndxMagicValue,
                    HeaderLength = _headerLength,
                    EntryDescriptor = 0,
                    Kind = 0,
                    Unknown0010 = 2,
                    IdxtOffset = idxtBlockOffset,
                    EntriesCount = metaentriesCount,
                    TextEncoding = 65001,
                    Unknown0020 = uint.MaxValue,
                    TotalEntriesCount = _totalEntriesCount,
                    OrdtOffset = 0u,
                    LigtOffset = 0u,
                    LigtEntriesCount = 0u,
                    StringRecordsCount = (uint)_stringRecords.Count,
                    Unknown0038 = 0,
                    Unknown003C = 0,
                    Unknown0040 = 0,
                    Unknown0044 = 0,
                    Unknown0048 = 0,
                    Unknown004C = 0,
                    Unknown0050 = 0,
                    Unknown0054 = 0,
                    Unknown0058 = 0,
                    Unknown005C = 0,
                    Unknown0060 = 0,
                    Unknown0064 = 0,
                    Unknown0068 = 0,
                    Unknown006C = 0,
                    Unknown0070 = 0,
                    Unknown0074 = 0,
                    Unknown0078 = 0,
                    Unknown007C = 0,
                    Unknown0080 = 0,
                    Unknown0084 = 0,
                    Unknown0088 = 0,
                    Unknown008C = 0,
                    Unknown0090 = 0,
                    Unknown0094 = 0,
                    Unknown0098 = 0,
                    Unknown009C = 0,
                    Unknown00A0 = 0,
                    Unknown00A4 = 0,
                    Unknown00A8 = 0,
                    Unknown00AC = 0,
                    Unknown00B0 = 0,
                    Unknown00B4 = _headerLength,
                    Unknown00B8 = 0,
                    Unknown00BC = 0,
                };

                WriteHeader(writer, ref header);

                writer.Flush();
                Debug.Assert(_metaentryRecord.Position == tagxBlockOffset);

                var tagxBlockHeader = new TagxBlockHeader()
                {
                    TagxMagic = TagxBlockHeader.TagxMagicValue,
                    TagxLength = tagxBlockLength,
                    ValuesDescriptorLength = _valuesDescriptorLength,
                };

                writer.Write(tagxBlockHeader.TagxMagic);
                writer.Write(tagxBlockHeader.TagxLength);
                writer.Write(tagxBlockHeader.ValuesDescriptorLength);

                foreach (var tagxEntry in _tagxEntries)
                {
                    writer.Write(tagxEntry.TagId);
                    writer.Write(tagxEntry.ValuesPerElement);
                    writer.Write(tagxEntry.ValuesDescriptorByteMask);
                    writer.Write(tagxEntry.EndOfValuesDescriptorByte);
                }

                writer.Flush();
                Debug.Assert(_metaentryRecord.Position == metaentryDataOffset);

                _metaentryData.Position = 0;
                _metaentryData.CopyTo(_metaentryRecord);
                _metaentryData.SetLength(0);

                for (int i = 0; i < metaentryDataPadLength; ++i)
                    _metaentryRecord.WriteByte(0);

                Debug.Assert(_metaentryRecord.Position == idxtBlockOffset);

                var idxtBlockHeader = new IdxtBlockHeader()
                {
                    IdxtMagic = IdxtBlockHeader.IdxtMagicValue,
                };

                WriteHeader(writer, ref idxtBlockHeader);

                foreach (ushort metaentryOffset in _metaentryOffsets)
                    writer.Write((ushort)(metaentryDataOffset + metaentryOffset));

                _metaentryOffsets.Clear();
            }

            for (int i = 0; i < idxtBlockPadLength; ++i)
                _metaentryRecord.WriteByte(0);

            Debug.Assert(_metaentryRecord.Position == recordLength);

            _metaentryRecord.Position = 0;

            _built = true;

            entryRecords = _entryRecords.ToArray();
            stringRecords = _stringRecords.ToArray();
            return _metaentryRecord;
        }

        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        private static void WriteHeader(BigEndianBinaryWriter writer, ref IndxHeader header)
        {
            writer.Write(header.IndxMagic);  // 0000
            writer.Write(header.HeaderLength);
            writer.Write(header.EntryDescriptor);
            writer.Write(header.Kind);
            writer.Write(header.Unknown0010);  // 0010
            writer.Write(header.IdxtOffset);
            writer.Write(header.EntriesCount);
            writer.Write(header.TextEncoding);
            writer.Write(header.Unknown0020);  // 0020
            writer.Write(header.TotalEntriesCount);
            writer.Write(header.OrdtOffset);
            writer.Write(header.LigtOffset);
            writer.Write(header.LigtEntriesCount);  // 0030
            writer.Write(header.StringRecordsCount);
            writer.Write(header.Unknown0038);
            writer.Write(header.Unknown003C);
            writer.Write(header.Unknown0040);  // 0040
            writer.Write(header.Unknown0044);
            writer.Write(header.Unknown0048);
            writer.Write(header.Unknown004C);
            writer.Write(header.Unknown0050);  // 0050
            writer.Write(header.Unknown0054);
            writer.Write(header.Unknown0058);
            writer.Write(header.Unknown005C);
            writer.Write(header.Unknown0060);  // 0060
            writer.Write(header.Unknown0064);
            writer.Write(header.Unknown0068);
            writer.Write(header.Unknown006C);
            writer.Write(header.Unknown0070);  // 0070
            writer.Write(header.Unknown0074);
            writer.Write(header.Unknown0078);
            writer.Write(header.Unknown007C);
            writer.Write(header.Unknown0080);  // 0080
            writer.Write(header.Unknown0084);
            writer.Write(header.Unknown0088);
            writer.Write(header.Unknown008C);
            writer.Write(header.Unknown0090);  // 0090
            writer.Write(header.Unknown0094);
            writer.Write(header.Unknown0098);
            writer.Write(header.Unknown009C);
            writer.Write(header.Unknown00A0);  // 00A0
            writer.Write(header.Unknown00A4);
            writer.Write(header.Unknown00A8);
            writer.Write(header.Unknown00AC);
            writer.Write(header.Unknown00B0);  // 00B0
            writer.Write(header.Unknown00B4);
            writer.Write(header.Unknown00B8);
            writer.Write(header.Unknown00BC);
        }

        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        private static void WriteHeader(BigEndianBinaryWriter writer, ref IdxtBlockHeader header)
        {
            writer.Write(header.IdxtMagic);
        }

        // ExceptionAdjustment: M:System.IO.MemoryStream.WriteByte(System.Byte) -T:System.NotSupportedException
        private static void WriteVarUInt32(MemoryStream stream, uint value)
        {
            int i = 28;
            for (; i > 0; i -= 7)
                if ((value >> i) != 0)
                    break;
            for (; i > 0; i -= 7)
                stream.WriteByte((byte)((value >> i) & 0x7F));
            stream.WriteByte((byte)(value | 0x80));
        }

        // ExceptionAdjustment: M:System.IO.MemoryStream.WriteByte(System.Byte) -T:System.NotSupportedException
        private static void WriteVarUInt64(MemoryStream stream, ulong value)
        {
            int i = 63;
            for (; i > 0; i -= 7)
                if ((value >> i) != 0)
                    break;
            for (; i > 0; i -= 7)
                stream.WriteByte((byte)((value >> i) & 0x7F));
            stream.WriteByte((byte)(value | 0x80));
        }

        /// <exception cref="InvalidOperationException"/>
        private void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("Records were already built.");
        }

        private void EnsureEntryRecordCapacity(uint length)
        {
            if (_headerLength + _entryData.Length + length > ushort.MaxValue)
                FinishEntryRecord();
        }

        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt16) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.MemoryStream.SetLength(System.Int64) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.MemoryStream.WriteByte(System.Byte) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.NotSupportedException
        private void FinishEntryRecord()
        {
            uint entriesCount = (uint)_entryOffsets.Count;

            if (entriesCount == 0)
                return;

            _totalEntriesCount += entriesCount;

            _metaentryOffsets.Add((ushort)_metaentryData.Position);

            _metaentryData.WriteByte((byte)_metaentryLastKeyStream.Length);

            _metaentryLastKeyStream.Position = 0;
            _metaentryLastKeyStream.CopyTo(_metaentryData);
            _metaentryLastKeyStream.SetLength(0);

            _metaentryDataWriter.Write((ushort)entriesCount);
            _metaentryDataWriter.Flush();

            var entryRecord = new MemoryStream();

            uint entryDataOffset = _headerLength;
            uint entryDataLength = (uint)_entryData.Length;
            uint entryDataPadLength = (~entryDataLength + 1) % 4;

            uint idxtBlockOffset = entryDataOffset + entryDataLength + entryDataPadLength;
            uint idxtBlockLength = 4 + entriesCount * 2;
            uint idxtBlockPadLength = (~idxtBlockLength + 1) % 4;

            uint recordLength = idxtBlockOffset + idxtBlockLength + idxtBlockPadLength;

            using (var writer = new BigEndianBinaryWriter(entryRecord, Encoding.Default, leaveOpen: true))
            {
                var header = new IndxHeader()
                {
                    IndxMagic = IndxHeader.IndxMagicValue,
                    HeaderLength = _headerLength,
                    EntryDescriptor = 0,
                    Kind = 1,
                    Unknown0010 = 0,
                    IdxtOffset = idxtBlockOffset,
                    EntriesCount = entriesCount,
                    TextEncoding = uint.MaxValue,
                    Unknown0020 = uint.MaxValue,
                    TotalEntriesCount = 0u,
                    OrdtOffset = 0u,
                    LigtOffset = 0u,
                    LigtEntriesCount = 0u,
                    StringRecordsCount = 0u,
                    Unknown0038 = 0,
                    Unknown003C = 0,
                    Unknown0040 = 0,
                    Unknown0044 = 0,
                    Unknown0048 = 0,
                    Unknown004C = 0,
                    Unknown0050 = 0,
                    Unknown0054 = 0,
                    Unknown0058 = 0,
                    Unknown005C = 0,
                    Unknown0060 = 0,
                    Unknown0064 = 0,
                    Unknown0068 = 0,
                    Unknown006C = 0,
                    Unknown0070 = 0,
                    Unknown0074 = 0,
                    Unknown0078 = 0,
                    Unknown007C = 0,
                    Unknown0080 = 0,
                    Unknown0084 = 0,
                    Unknown0088 = 0,
                    Unknown008C = 0,
                    Unknown0090 = 0,
                    Unknown0094 = 0,
                    Unknown0098 = 0,
                    Unknown009C = 0,
                    Unknown00A0 = 0,
                    Unknown00A4 = 0,
                    Unknown00A8 = 0,
                    Unknown00AC = 0,
                    Unknown00B0 = 0,
                    Unknown00B4 = 0,
                    Unknown00B8 = 0,
                    Unknown00BC = 0,
                };

                WriteHeader(writer, ref header);

                writer.Flush();
                Debug.Assert(entryRecord.Position == entryDataOffset);

                _entryData.Position = 0;
                _entryData.CopyTo(entryRecord);
                _entryData.SetLength(0);

                for (int i = 0; i < entryDataPadLength; ++i)
                    entryRecord.WriteByte(0);

                Debug.Assert(entryRecord.Position == idxtBlockOffset);

                var idxtBlockHeader = new IdxtBlockHeader()
                {
                    IdxtMagic = IdxtBlockHeader.IdxtMagicValue,
                };

                WriteHeader(writer, ref idxtBlockHeader);

                foreach (ushort entryOffset in _entryOffsets)
                    writer.Write((ushort)(entryDataOffset + entryOffset));

                _entryOffsets.Clear();
            }

            for (int i = 0; i < idxtBlockPadLength; ++i)
                entryRecord.WriteByte(0);

            Debug.Assert(entryRecord.Position == recordLength);

            entryRecord.Position = 0;

            _entryRecords.Add(entryRecord);
        }

        [MemberNotNull(nameof(_stringRecord))]
        private void EnsureStringRecordCapacity()
        {
            if (_stringRecord != null && _stringRecord.Length > ushort.MaxValue)
                FinishStringRecord();

            if (_stringRecord == null)
                _stringRecord = new MemoryStream();
        }

        // ExceptionAdjustment: M:CbzToKf8.StreamExtensions.ZeroPad4(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:CbzToKf8.StreamExtensions.ZeroPad4(System.IO.Stream) -T:System.NotSupportedException
        private void FinishStringRecord()
        {
            if (_stringRecord == null)
                return;

            _stringRecord.ZeroPad4();

            _stringRecord.Position = 0;

            _stringRecords.Add(_stringRecord);

            _stringRecord = null;
        }

        public struct Tag
        {
            public byte TagId;
            public ulong[] Values;

            /// <exception cref="ArgumentNullException"/>
            public Tag(byte tagId, ulong[] values)
            {
                if (values is null)
                    throw new ArgumentNullException(nameof(values));

                TagId = tagId;
                Values = values;
            }
        }
    }
}
