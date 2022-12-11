// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

namespace CbzToKf8.Mobi
{
    internal struct Mobi6BlockHeader
    {
        public const uint MobiMagicValue = 0x4D4F4249;  // "MOBI"

        public uint MobiMagic;
        public uint HeaderLength;
        public MobiBookType BookType;
        public MobiTextEncoding TextEncoding;
        public uint RandomId;
        public uint FormatVersion;

        // Version 3+ required fields:
        public uint Unknown0028;
        public uint Unknown002C;
        public uint Unknown0030;
        public uint Unknown0034;
        public uint Unknown0038;
        public uint Unknown003C;
        public uint Unknown0040;
        public uint Unknown0044;
        public uint Unknown0048;
        public uint Unknown004C;
        public uint Unknown0050;
        public uint FullNameOffset;
        public uint FullNameLength;
        public uint Language;
        public uint InputLanguage;
        public uint OutputLanguage;
        public uint MinVersion;
        public uint Unknown006C;
        public uint HuffDicRecordsIndex;
        public uint HuffDicRecordsCount;
        public uint HuffDicDirectAccessRecordsIndex;
        public uint HuffDicDirectAccessRecordsCount;
        public MobiFlags Flags;

        // Version 4+ required fields:
        public uint Unknown0084;
        public uint Unknown0088;
        public uint Unknown008C;
        public uint Unknown0090;
        public uint Unknown0094;
        public uint Unknown0098;
        public uint Unknown009C;
        public uint Unknown00A0;
        public uint Unknown00A4;
        public uint Unknown00A8;
        public uint Unknown00AC;
        public uint Unknown00B0;
        public uint Unknown00B4;
        public uint Unknown00B8;
        public uint Unknown00BC;
        public ushort Unknown00C0;
        public ushort Unknown00C2;
        public uint Unknown00C4;
        public uint FcisRecordsIndex;
        public uint FcisRecordsCount;
        public uint FlisRecordsIndex;
        public uint FlisRecordsCount;
        public uint Unknown00D8;
        public uint Unknown00DC;

        // Version 6+ required fields:
        public uint Unknown00E0;
        public uint Unknown00E4;
        public uint Unknown00E8;
        public uint Unknown00EC;
        public uint Unknown00F0;

        // Version 6+ fields:
        public uint NcxRecordsIndex;

        public byte[] TrailingData;
    }
}
