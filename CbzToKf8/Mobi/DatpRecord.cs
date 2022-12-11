// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System.Diagnostics;
using System.IO;
using System.Text;

namespace CbzToKf8.Mobi
{
    internal sealed class DatpRecord
    {
        public const uint Datp4CC = 0x44415450;  // "DATP"

        public uint DatpMagic;
        public uint HeaderLength;
        public byte Unknowns1Count;
        public byte Unknowns2Shift;
        public ushort Unknowns3Count;
        public byte[] HeaderTrailingData;

        public uint[] Unknowns1;
        public uint[] Unknowns2;
        public ushort[] Unknowns3;
        public byte[] TrailingData;

        /// <exception cref="InvalidDataException"/>
        /// <exception cref="IOException"/>
        /// <exception cref="EndOfStreamException"/>
        /// <exception cref="System.NotSupportedException">The stream does not support reading and
        ///     seeking.</exception>
        public void ReadFrom(Stream stream)
        {
            Debug.Assert(stream.Position == 0);

            using (var reader = new BigEndianBinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                DatpMagic = reader.ReadUInt32();
                HeaderLength = reader.ReadUInt32();

                if (DatpMagic != Datp4CC)
                    throw new InvalidDataException("Mismatched DATP magic number.");
                if (HeaderLength < 8)
                    throw new InvalidDataException("Invalid DATP header length.");
                if (HeaderLength < 12)
                    throw new InvalidDataException("Unexpected DATP header length.");

                Unknowns1Count = reader.ReadByte();
                Unknowns2Shift = reader.ReadByte();
                Unknowns3Count = reader.ReadUInt16();

                HeaderTrailingData = reader.ReadBytes((int)HeaderLength - 12);

                stream.Position = HeaderLength;

                Unknowns1 = new uint[Unknowns1Count];
                for (int i = 0; i < Unknowns1.Length; ++i)
                    Unknowns1[i] = reader.ReadUInt32();

                // One entry in Unknowns2 for each (1 << Unknowns2Shift) entries in Unknowns3.
                uint unknowns2Count = (Unknowns3Count + (1u << Unknowns2Shift) - 1) >> Unknowns2Shift;
                Unknowns2 = new uint[unknowns2Count];
                for (int i = 0; i < Unknowns2.Length; ++i)
                    Unknowns2[i] = reader.ReadUInt32();

                Unknowns3 = new ushort[Unknowns3Count];
                for (int i = 0; i < Unknowns3.Length; ++i)
                    Unknowns3[i] = reader.ReadUInt16();

                TrailingData = reader.ReadBytes((int)(stream.Length - stream.Position));
            }
        }
    }
}
