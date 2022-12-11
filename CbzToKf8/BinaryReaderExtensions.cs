// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System.IO;

namespace CbzToKf8
{
    internal static class BinaryReaderExtensions
    {
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        public static void ReadExactly(this BinaryReader binaryReader, byte[] bytes, int offset, int length)
        {
            while (length > 0)
            {
                int amount = binaryReader.Read(bytes, offset, length);
                if (amount == 0)
                    throw new EndOfStreamException();

                offset += amount;
                length -= amount;
            }
        }
    }
}
