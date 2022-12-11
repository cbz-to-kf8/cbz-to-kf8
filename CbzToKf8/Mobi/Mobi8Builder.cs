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
using System.Security.Cryptography;
using System.Text;

namespace CbzToKf8.Mobi
{
    internal sealed class Mobi8Builder : IDisposable
    {
        private const uint _pdocHeaderLength = 16;

        private const uint _mobiBlockHeaderLength = 264;

        private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly MemoryStream _fullName = new MemoryStream();

        private readonly ExthBuilder _exthBuilder = new ExthBuilder();

        private PdocHeader _pdoc;

        private Mobi8BlockHeader _mobi;

        private bool _built;

        public Mobi8Builder()
        {
            _pdoc = new PdocHeader
            {
                CompressionMethod = MobiCompressionMethod.Uncompressed,
                Unknown0002 = 0,
                TextLength = 0,
                TextRecordsCount = 0,
                TextRecordTextLength = 0,
                EncryptionMethod = MobiEncryptionMethod.Unencrypted,
                Unknown000E = 0,
            };

            _mobi = new Mobi8BlockHeader()
            {
                MobiMagic = MobiRecord.MobiMagicValue,
                HeaderLength = _mobiBlockHeaderLength,
                BookType = MobiBookType.Default,
                TextEncoding = MobiTextEncoding.Utf8,
                RandomId = 0,
                FormatVersion = 8,
                IndexRecordsIndex0028 = uint.MaxValue,
                IndexRecordsIndex002C = uint.MaxValue,
                IndexRecordsIndex0030 = uint.MaxValue,
                IndexRecordsIndex0034 = uint.MaxValue,
                IndexRecordsIndex0038 = uint.MaxValue,
                IndexRecordsIndex003C = uint.MaxValue,
                IndexRecordsIndex0040 = uint.MaxValue,
                IndexRecordsIndex0044 = uint.MaxValue,
                IndexRecordsIndex0048 = uint.MaxValue,
                IndexRecordsIndex004C = uint.MaxValue,
                IndexRecordsIndex0050 = uint.MaxValue,
                FullNameOffset = 0,
                FullNameLength = 0,
                Language = 0,
                InputLanguage = 0,
                OutputLanguage = 0,
                MinVersion = 8,
                EmbeddedRecordsIndex = uint.MaxValue,
                HuffDicRecordsIndex = 0,
                HuffDicRecordsCount = 0,
                HuffDicDirectAccessRecordsIndex = 0,
                HuffDicDirectAccessRecordsCount = 0,
                Flags = MobiFlags.HasExtendedHeader | MobiFlags.HasEndOfRecords,
                Unknown0084 = 0,
                Unknown0088 = 0,
                Unknown008C = 0,
                Unknown0090 = 0,
                Unknown0094 = 0,
                Unknown0098 = 0,
                Unknown009C = 0,
                Unknown00A0 = 0,
                IndexRecordsIndex00A4 = uint.MaxValue,
                Drm00A8 = uint.MaxValue,
                Drm00AC = 0u,
                Drm00B0 = 0u,
                Drm00B4 = 0u,
                Unknown00B8 = 0,
                Unknown00BC = 0,
                FdstRecordsIndex = uint.MaxValue,
                FdstFlowCount = 0,
                FcisRecordsIndex = uint.MaxValue,
                FcisRecordsCount = 0,
                FlisRecordsIndex = uint.MaxValue,
                FlisRecordsCount = 0,
                Unknown00D8 = 0,
                Unknown00DC = 0,
                SrcsRecordsIndex = uint.MaxValue,
                SrcsRecordsCount = 0,
                IndexRecordsIndex00E8 = uint.MaxValue,
                Unknown00EC = uint.MaxValue,
                TextTrailers = MobiTextTrailers.Multibyte,
                NcxRecordsIndex = uint.MaxValue,
                FragmentRecordsIndex = uint.MaxValue,
                SkeletonRecordsIndex = uint.MaxValue,
                LocationMapRecordIndex = uint.MaxValue,
                GuideRecordIndex = uint.MaxValue,
                WordMapRecordIndex = uint.MaxValue,
                Unknown010C = 0,
                HDResourceContainerRecordsIndex = uint.MaxValue,
                Unknown0114 = 0,
            };
        }

        public uint TextLength { set => _pdoc.TextLength = value; }

        public ushort TextRecordsCount { set => _pdoc.TextRecordsCount = value; }

        public ushort TextRecordTextLength { set => _pdoc.TextRecordTextLength = value; }

        public MobiCompressionMethod CompressionMethod { set => _pdoc.CompressionMethod = value; }

        public uint FlowsRecordsIndex { set => _mobi.FdstRecordsIndex = value; }

        public uint FlowsCount { set => _mobi.FdstFlowCount = value; }

        public uint SkeletonRecordsIndex { set => _mobi.SkeletonRecordsIndex = value; }

        public uint FragmentRecordsIndex { set => _mobi.FragmentRecordsIndex = value; }

        public uint EmbeddedRecordsIndex
        {
            get => _mobi.EmbeddedRecordsIndex;
            set => _mobi.EmbeddedRecordsIndex = value;
        }

        public uint IndexRecordsIndex0028 { set => _mobi.IndexRecordsIndex0028 = value; }

        public uint IndexRecordsIndex0050 { set => _mobi.IndexRecordsIndex0050 = value; }

        public void Dispose()
        {
            _fullName.Dispose();
            _exthBuilder.Dispose();
        }

        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.NotSupportedException
        public void SetFullName(string value)
        {
            ThrowIfBuilt();

            using (var writer = new StreamWriter(_fullName, _encoding, 128, leaveOpen: true))
                writer.Write(value);

            _fullName.Position = 0;
        }

        /// <exception cref="InvalidOperationException"/>
        public void AddHeader(ExthTag key, uint value)
        {
            ThrowIfBuilt();

            _exthBuilder.Add(key, value);
        }

        /// <exception cref="InvalidOperationException"/>
        public void AddHeader(ExthTag key, string value)
        {
            ThrowIfBuilt();

            _exthBuilder.Add(key, value);
        }

        /// <exception cref="InvalidOperationException"/>
        public void AddHeader(ExthTag key, byte[] value)
        {
            ThrowIfBuilt();

            _exthBuilder.Add(key, value);
        }

        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt16) -T:System.IO.IOException
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.MemoryStream.WriteByte(System.Byte) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.NotSupportedException
        public MemoryStream Build()
        {
            ThrowIfBuilt();

            var exthBlock = _exthBuilder.Build();

            uint exthBlockOffset = _pdocHeaderLength + _mobiBlockHeaderLength;
            uint exthBlockLength = (uint)exthBlock.Length;

            uint fullNameOffset = exthBlockOffset + exthBlockLength;
            uint fullNameLength = (uint)_fullName.Length;

            uint padOffset = fullNameOffset + fullNameLength;
            uint padLength = (~padOffset + 1) % 4;

            uint recordLength = padOffset + padLength;

            _mobi.FullNameOffset = fullNameOffset;
            _mobi.FullNameLength = fullNameLength;

            byte[] randomBytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(randomBytes);

            _mobi.RandomId = BitConverter.ToUInt32(randomBytes, 0);

            var record = new MemoryStream();

            using (var writer = new BigEndianBinaryWriter(record, Encoding.Default, leaveOpen: true))
            {
                writer.Write((ushort)_pdoc.CompressionMethod);
                writer.Write(_pdoc.Unknown0002);
                writer.Write(_pdoc.TextLength);
                writer.Write(_pdoc.TextRecordsCount);
                writer.Write(_pdoc.TextRecordTextLength);
                writer.Write((ushort)_pdoc.EncryptionMethod);
                writer.Write(_pdoc.Unknown000E);

                writer.Write(_mobi.MobiMagic);  // 0010
                writer.Write(_mobi.HeaderLength);
                writer.Write((uint)_mobi.BookType);
                writer.Write((uint)_mobi.TextEncoding);  // 0020
                writer.Write(_mobi.RandomId);
                writer.Write(_mobi.FormatVersion);
                writer.Write(_mobi.IndexRecordsIndex0028);
                writer.Write(_mobi.IndexRecordsIndex002C);
                writer.Write(_mobi.IndexRecordsIndex0030);  // 0030
                writer.Write(_mobi.IndexRecordsIndex0034);
                writer.Write(_mobi.IndexRecordsIndex0038);
                writer.Write(_mobi.IndexRecordsIndex003C);
                writer.Write(_mobi.IndexRecordsIndex0040);  // 0040
                writer.Write(_mobi.IndexRecordsIndex0044);
                writer.Write(_mobi.IndexRecordsIndex0048);
                writer.Write(_mobi.IndexRecordsIndex004C);
                writer.Write(_mobi.IndexRecordsIndex0050);  // 0050
                writer.Write(_mobi.FullNameOffset);
                writer.Write(_mobi.FullNameLength);
                writer.Write(_mobi.Language);
                writer.Write(_mobi.InputLanguage);  // 0060
                writer.Write(_mobi.OutputLanguage);
                writer.Write(_mobi.MinVersion);
                writer.Write(_mobi.EmbeddedRecordsIndex);
                writer.Write(_mobi.HuffDicRecordsIndex);  // 0070
                writer.Write(_mobi.HuffDicRecordsCount);
                writer.Write(_mobi.HuffDicDirectAccessRecordsIndex);
                writer.Write(_mobi.HuffDicDirectAccessRecordsCount);
                writer.Write((uint)_mobi.Flags);  // 0080
                writer.Write(_mobi.Unknown0084);
                writer.Write(_mobi.Unknown0088);
                writer.Write(_mobi.Unknown008C);
                writer.Write(_mobi.Unknown0090);  //0090
                writer.Write(_mobi.Unknown0094);
                writer.Write(_mobi.Unknown0098);
                writer.Write(_mobi.Unknown009C);
                writer.Write(_mobi.Unknown00A0);  // 00A0
                writer.Write(_mobi.IndexRecordsIndex00A4);
                writer.Write(_mobi.Drm00A8);
                writer.Write(_mobi.Drm00AC);
                writer.Write(_mobi.Drm00B0);  // 00B0
                writer.Write(_mobi.Drm00B4);
                writer.Write(_mobi.Unknown00B8);
                writer.Write(_mobi.Unknown00BC);
                writer.Write(_mobi.FdstRecordsIndex);  // 00C0
                writer.Write(_mobi.FdstFlowCount);
                writer.Write(_mobi.FcisRecordsIndex);
                writer.Write(_mobi.FcisRecordsCount);
                writer.Write(_mobi.FlisRecordsIndex);  // 00D0
                writer.Write(_mobi.FlisRecordsCount);
                writer.Write(_mobi.Unknown00D8);
                writer.Write(_mobi.Unknown00DC);
                writer.Write(_mobi.SrcsRecordsIndex);  // 00E0
                writer.Write(_mobi.SrcsRecordsCount);
                writer.Write(_mobi.IndexRecordsIndex00E8);
                writer.Write(_mobi.Unknown00EC);
                writer.Write((uint)_mobi.TextTrailers);  // 00F0
                writer.Write(_mobi.NcxRecordsIndex);
                writer.Write(_mobi.FragmentRecordsIndex);
                writer.Write(_mobi.SkeletonRecordsIndex);
                writer.Write(_mobi.LocationMapRecordIndex);  // 0100
                writer.Write(_mobi.GuideRecordIndex);
                writer.Write(_mobi.WordMapRecordIndex);
                writer.Write(_mobi.Unknown010C);
                writer.Write(_mobi.HDResourceContainerRecordsIndex);  // 0110
                writer.Write(_mobi.Unknown0114);
            }

            Debug.Assert(record.Position == exthBlockOffset);

            exthBlock.CopyTo(record);

            Debug.Assert(record.Position == fullNameOffset);

            _fullName.CopyTo(record);

            Debug.Assert(record.Position == padOffset);

            for (int i = 0; i < padLength; ++i)
                record.WriteByte(0);

            Debug.Assert(record.Position == recordLength);

            record.Position = 0;

            _built = true;

            return record;
        }

        /// <exception cref="InvalidOperationException"/>
        private void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("Record was already built.");
        }
    }
}
