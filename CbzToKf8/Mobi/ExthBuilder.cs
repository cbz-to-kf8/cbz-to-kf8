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
    internal sealed class ExthBuilder : IDisposable
    {
        private const uint _headerLength = 12;

        private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly MemoryStream _stream;

        private readonly BigEndianBinaryWriter _writer;

        private readonly StreamWriter _streamWriter;

        private ExthBlockHeader _header;

        private bool _built;

        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        public ExthBuilder()
        {
            _stream = new MemoryStream();
            _writer = new BigEndianBinaryWriter(_stream, Encoding.Default, leaveOpen: true);
            _streamWriter = new StreamWriter(_stream, _encoding, leaveOpen: true);

            _header.ExthMagic = ExthBlockHeader.ExthMagicValue;
            _header.ExthLength = _headerLength;
            _header.EntriesCount = 0;

            _writer.Write(_header.ExthMagic);
            _writer.Write(_header.ExthLength);
            _writer.Write(_header.EntriesCount);
        }

        public void Dispose()
        {
            if (!_built)
                _stream.Dispose();

            _writer.Dispose();
            _streamWriter.Dispose();
        }

        /// <exception cref="InvalidOperationException">Block was already built.</exception>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        public void Add(ExthTag key, uint value)
        {
            ThrowIfBuilt();

            _writer.Write((uint)key);
            _writer.Write(12u);
            _writer.Write(value);

            _header.EntriesCount += 1;
        }

        /// <exception cref="InvalidOperationException">Block was already built.</exception>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Flush -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.NotSupportedException
        public void Add(ExthTag key, string value)
        {
            ThrowIfBuilt();

            _writer.Write((uint)key);
            _writer.Write(0u);
            _writer.Flush();

            long startPosition = _stream.Position;

            _streamWriter.Write(value);
            _streamWriter.Flush();

            long endPosition = _stream.Position;

            _stream.Position = startPosition - 4;
            _writer.Write(8u + (uint)(endPosition - startPosition));
            _writer.Flush();

            _stream.Position = endPosition;

            _header.EntriesCount += 1;
        }

        /// <exception cref="InvalidOperationException">Block was already built.</exception>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.BinaryWriter.Write(System.Byte[]) -T:System.IO.IOException
        public void Add(ExthTag key, byte[] value)
        {
            ThrowIfBuilt();

            _writer.Write((uint)key);
            _writer.Write(8u + (uint)value.Length);
            _writer.Write(value);

            _header.EntriesCount += 1;
        }

        /// <exception cref="InvalidOperationException">Block was already built.</exception>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        // ExceptionAdjustment: M:CbzToKf8.StreamExtensions.ZeroPad4(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:CbzToKf8.StreamExtensions.ZeroPad4(System.IO.Stream) -T:System.NotSupportedException
        public MemoryStream Build()
        {
            ThrowIfBuilt();

            _writer.Flush();

            _stream.ZeroPad4();

            _header.ExthLength = (uint)_stream.Length;

            _stream.Position = 4;
            _writer.Write(_header.ExthLength);
            _writer.Write(_header.EntriesCount);
            _writer.Flush();

            _stream.Position = 0;

            _built = true;

            return _stream;
        }

        /// <exception cref="InvalidOperationException"/>
        private void ThrowIfBuilt()
        {
            if (_built)
                throw new InvalidOperationException("Block was already built.");
        }
    }
}
