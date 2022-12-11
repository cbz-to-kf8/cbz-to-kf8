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
    internal static partial class MobiRecord
    {
        public const uint MobiMagicValue = 0x4D4F4249;  // "MOBI"

        /// <exception cref="InvalidDataException"/>
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        /// <exception cref="System.NotSupportedException">The stream does not support reading and
        ///     seeking.</exception>
        public static uint ReadVersion(Stream stream)
        {
            using (var reader = new BigEndianBinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();

                uint mobiMagic = reader.ReadUInt32();
                uint mobiLength = reader.ReadUInt32();

                if (mobiMagic != MobiMagicValue)
                    throw new InvalidDataException("Mismatched MOBI magic number.");
                if (mobiLength < 8)
                    throw new InvalidDataException("Invalid MOBI length.");
                if (stream.Length - (stream.Position - 8) < mobiLength)
                    throw new InvalidDataException("Insufficient data for MOBI length.");

                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                return reader.ReadUInt32();
            }
        }
    }
}
