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
    internal sealed class RescBuilder : IDisposable
    {
        private const uint _headerLength = 16;

        private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly MemoryStream _metadata = new MemoryStream();

        private RescHeader _header;

        private bool _built;

        public RescBuilder()
        {
            _header = new RescHeader
            {
                RescMagic = RescHeader.RescMagicValue,
                HeaderLength = _headerLength,
                Unknown0008 = 1,
                QueryStringLength = 0,
            };
        }

        public void Dispose()
        {
            _metadata.Dispose();
        }

        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.NotSupportedException
        public void SetMetadata(string xml)
        {
            ThrowIfBuilt();

            using (var writer = new StreamWriter(_metadata, _encoding, 4096, leaveOpen: true))
                writer.Write(xml);

            _metadata.Position = 0;
        }

        /// <exception cref="InvalidOperationException"/>
        // ExceptionAdjustment: M:CbzToKf8.BigEndianBinaryWriter.Write(System.UInt32) -T:System.IO.IOException
        // ExceptionAdjustment: M:CbzToKf8.StreamExtensions.ZeroPad4(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:CbzToKf8.StreamExtensions.ZeroPad4(System.IO.Stream) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.Stream.CopyTo(System.IO.Stream) -T:System.NotSupportedException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.IO.IOException
        // ExceptionAdjustment: M:System.IO.StreamWriter.Write(System.String) -T:System.NotSupportedException
        public MemoryStream Build()
        {
            ThrowIfBuilt();

            var record = new MemoryStream();

            using (var writer = new BigEndianBinaryWriter(record, Encoding.Default, leaveOpen: true))
            {
                writer.Write(_header.RescMagic);
                writer.Write(_header.HeaderLength);
                writer.Write(_header.Unknown0008);
                writer.Write(_header.QueryStringLength);
                writer.Flush();

                Debug.Assert(record.Position == _headerLength);

                using (var stringWriter = new StreamWriter(record, _encoding, 128, leaveOpen: true))
                {
                    string sizeBase32 = Base32.Encode((uint)_metadata.Length);
                    stringWriter.Write($"size={sizeBase32}&version=1&type=1");
                }

                _header.QueryStringLength = (uint)record.Position - _headerLength;

                record.Position = 12;
                writer.Write(_header.QueryStringLength);
            }

            record.Position = record.Length;
            _metadata.CopyTo(record);

            record.ZeroPad4();

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
