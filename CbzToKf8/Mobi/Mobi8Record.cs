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
    internal sealed partial class Mobi8Record
    {
        public PdocHeader Pdoc;

        public Mobi8BlockHeader Mobi;

        public ExthBlock Exth;

        public byte[] FullName;

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
                Pdoc.CompressionMethod = (MobiCompressionMethod)reader.ReadUInt16();
                Pdoc.Unknown0002 = reader.ReadUInt16();
                Pdoc.TextLength = reader.ReadUInt32();
                Pdoc.TextRecordsCount = reader.ReadUInt16();
                Pdoc.TextRecordTextLength = reader.ReadUInt16();
                Pdoc.EncryptionMethod = (MobiEncryptionMethod)reader.ReadUInt16();
                Pdoc.Unknown000E = reader.ReadUInt16();

                Mobi.MobiMagic = reader.ReadUInt32();  // 0010
                Mobi.HeaderLength = reader.ReadUInt32();

                if (Mobi.MobiMagic != Mobi8BlockHeader.MobiMagicValue)
                    throw new InvalidDataException("Mismatched MOBI magic number.");
                if (Mobi.HeaderLength < 8)
                    throw new InvalidDataException("Invalid MOBI length.");
                if (Mobi.HeaderLength != 264)
                    throw new InvalidDataException("Unexpected MOBI length.");

                Mobi.BookType = (MobiBookType)reader.ReadUInt32();
                Mobi.TextEncoding = (MobiTextEncoding)reader.ReadUInt32();  // 0020
                Mobi.RandomId = reader.ReadUInt32();
                Mobi.FormatVersion = reader.ReadUInt32();

                if (Mobi.FormatVersion != 8)
                    throw new InvalidDataException("Unexpected MOBI format version.");

                Mobi.IndexRecordsIndex0028 = reader.ReadUInt32();
                Mobi.IndexRecordsIndex002C = reader.ReadUInt32();
                Mobi.IndexRecordsIndex0030 = reader.ReadUInt32();  // 0030
                Mobi.IndexRecordsIndex0034 = reader.ReadUInt32();
                Mobi.IndexRecordsIndex0038 = reader.ReadUInt32();
                Mobi.IndexRecordsIndex003C = reader.ReadUInt32();
                Mobi.IndexRecordsIndex0040 = reader.ReadUInt32();  // 0040
                Mobi.IndexRecordsIndex0044 = reader.ReadUInt32();
                Mobi.IndexRecordsIndex0048 = reader.ReadUInt32();
                Mobi.IndexRecordsIndex004C = reader.ReadUInt32();
                Mobi.IndexRecordsIndex0050 = reader.ReadUInt32();  // 0050
                Mobi.FullNameOffset = reader.ReadUInt32();
                Mobi.FullNameLength = reader.ReadUInt32();
                Mobi.Language = reader.ReadUInt32();
                Mobi.InputLanguage = reader.ReadUInt32();  // 0060
                Mobi.OutputLanguage = reader.ReadUInt32();
                Mobi.MinVersion = reader.ReadUInt32();
                Mobi.EmbeddedRecordsIndex = reader.ReadUInt32();
                Mobi.HuffDicRecordsIndex = reader.ReadUInt32();  // 0070
                Mobi.HuffDicRecordsCount = reader.ReadUInt32();
                Mobi.HuffDicDirectAccessRecordsIndex = reader.ReadUInt32();
                Mobi.HuffDicDirectAccessRecordsCount = reader.ReadUInt32();
                Mobi.Flags = (MobiFlags)reader.ReadUInt32();  // 0080
                Mobi.Unknown0084 = reader.ReadUInt32();
                Mobi.Unknown0088 = reader.ReadUInt32();
                Mobi.Unknown008C = reader.ReadUInt32();
                Mobi.Unknown0090 = reader.ReadUInt32();  //0090
                Mobi.Unknown0094 = reader.ReadUInt32();
                Mobi.Unknown0098 = reader.ReadUInt32();
                Mobi.Unknown009C = reader.ReadUInt32();
                Mobi.Unknown00A0 = reader.ReadUInt32();  // 00A0
                Mobi.IndexRecordsIndex00A4 = reader.ReadUInt32();
                Mobi.Drm00A8 = reader.ReadUInt32();
                Mobi.Drm00AC = reader.ReadUInt32();
                Mobi.Drm00B0 = reader.ReadUInt32();  // 00B0
                Mobi.Drm00B4 = reader.ReadUInt32();
                Mobi.Unknown00B8 = reader.ReadUInt32();
                Mobi.Unknown00BC = reader.ReadUInt32();
                Mobi.FdstRecordsIndex = reader.ReadUInt32();  // 00C0
                Mobi.FdstFlowCount = reader.ReadUInt32();
                Mobi.FcisRecordsIndex = reader.ReadUInt32();
                Mobi.FcisRecordsCount = reader.ReadUInt32();
                Mobi.FlisRecordsIndex = reader.ReadUInt32();  // 00D0
                Mobi.FlisRecordsCount = reader.ReadUInt32();
                Mobi.Unknown00D8 = reader.ReadUInt32();
                Mobi.Unknown00DC = reader.ReadUInt32();
                Mobi.SrcsRecordsIndex = reader.ReadUInt32();  // 00E0
                Mobi.SrcsRecordsCount = reader.ReadUInt32();
                Mobi.IndexRecordsIndex00E8 = reader.ReadUInt32();
                Mobi.Unknown00EC = reader.ReadUInt32();
                Mobi.TextTrailers = (MobiTextTrailers)reader.ReadUInt32();  // 00F0
                Mobi.NcxRecordsIndex = reader.ReadUInt32();
                Mobi.FragmentRecordsIndex = reader.ReadUInt32();
                Mobi.SkeletonRecordsIndex = reader.ReadUInt32();
                Mobi.LocationMapRecordIndex = reader.ReadUInt32();  // 0100
                Mobi.GuideRecordIndex = reader.ReadUInt32();
                Mobi.WordMapRecordIndex = reader.ReadUInt32();
                Mobi.Unknown010C = reader.ReadUInt32();
                Mobi.HDResourceContainerRecordsIndex = reader.ReadUInt32();  // 0110
                Mobi.Unknown0114 = reader.ReadUInt32();
            }

            if ((Mobi.Flags & MobiFlags.HasExtendedHeader) != 0)
            {
                Exth = new ExthBlock();
                Exth.ReadFrom(stream);
            }

            // TODO: Handle excess data between MOBI/EXTH and full name?

            if (stream.Length < Mobi.FullNameOffset)
                throw new InvalidDataException("Invalid MOBI full name offset.");
            if (stream.Length - Mobi.FullNameOffset < Mobi.FullNameLength)
                throw new InvalidDataException("Insufficient data for MOBI full name length.");

            FullName = new byte[Mobi.FullNameLength];
            stream.Position = Mobi.FullNameOffset;
            stream.ReadExactly(FullName, 0, FullName.Length);

            // TODO: Handle excess data after full name?
        }
    }
}
