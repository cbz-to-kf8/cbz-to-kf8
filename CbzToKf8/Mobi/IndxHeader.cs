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
    internal struct IndxHeader
    {
        public const uint IndxMagicValue = 0x494E4458;  // "INDX"

        public uint IndxMagic;
        public uint HeaderLength;
        public uint EntryDescriptor;  // If 3 then IDXT entry length is 16 bits?
        public uint Kind;
        public uint Unknown0010;
        public uint IdxtOffset;
        public uint EntriesCount;  // Also records count when Kind is 0.
        public uint TextEncoding;
        public uint Unknown0020;
        public uint TotalEntriesCount;
        public uint OrdtOffset;
        public uint LigtOffset;
        public uint LigtEntriesCount;
        public uint StringRecordsCount;
        public uint Unknown0038;
        public uint Unknown003C;
        public uint Unknown0040;
        public uint Unknown0044;
        public uint Unknown0048;
        public uint Unknown004C;
        public uint Unknown0050;
        public uint Unknown0054;
        public uint Unknown0058;
        public uint Unknown005C;
        public uint Unknown0060;
        public uint Unknown0064;
        public uint Unknown0068;
        public uint Unknown006C;
        public uint Unknown0070;
        public uint Unknown0074;
        public uint Unknown0078;
        public uint Unknown007C;
        public uint Unknown0080;
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
    }
}
