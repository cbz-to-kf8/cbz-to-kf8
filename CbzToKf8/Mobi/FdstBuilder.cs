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
    internal sealed class FdstBuilder : IDisposable
    {
        private const uint _headerLength = 12;

        private readonly uint _entriesPerRecord;

        private readonly List<MemoryStream> _records = new List<MemoryStream>();

        private MemoryStream? _stream;

        private BigEndianBinaryWriter? _writer;

        private FdstHeader _header;

        private bool _built;

        /// <exception cref="ArgumentOutOfRangeException"/>
        public FdstBuilder(uint entriesPerRecord)
        {
            if (entriesPerRecord == 0)
                throw new ArgumentOutOfRangeException(nameof(entriesPerRecord));

            _entriesPerRecord = entriesPerRecord;
        }

        public void Dispose()
        {
            if (!_built)
            {
                foreach (var record in _records)
                    record.Dispose();
                _stream?.Dispose();
            }

            _writer?.Dispose();
        }

        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        public void AddFlow(uint startOffset, uint endOffset)
        {
            ThrowIfBuilt();

            EnsureCapacity();

            _writer!.Write(startOffset);
            _writer.Write(endOffset);

            _header.EntriesCount += 1;
        }

        /// <exception cref="InvalidOperationException"/>
        public MemoryStream[] Build()
        {
            ThrowIfBuilt();

            FinishRecord();

            _built = true;

            return _records.ToArray();
        }

        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        [MemberNotNull(nameof(_stream))]
        private void EnsureCapacity()
        {
            if (_header.EntriesCount == _entriesPerRecord)
                FinishRecord();

            if (_stream == null)
            {
                _stream = new MemoryStream();
                _writer = new BigEndianBinaryWriter(_stream, Encoding.Default, leaveOpen: true);

                _header.FdstMagic = FdstHeader.FdstMagicValue;
                _header.HeaderLength = _headerLength;
                _header.EntriesCount = 0;

                _writer.Write(_header.FdstMagic);
                _writer.Write(_header.HeaderLength);
                _writer.Write(_header.EntriesCount);

                _writer.Flush();
                Debug.Assert(_stream.Position == _headerLength);
            }
        }

        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        private void FinishRecord()
        {
            if (_stream == null)
                return;

            _writer!.Flush();

            _stream.Position = 8;
            _writer.Write(_header.EntriesCount);
            _writer.Flush();

            _stream.Position = 0;

            _records.Add(_stream);

            _stream = null;
            _writer = null;
        }

        /// <exception cref="InvalidOperationException"/>
        private void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("Records were already built.");
        }
    }
}
