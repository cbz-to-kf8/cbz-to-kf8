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
    internal struct Mobi8BlockHeader
    {
        public const uint MobiMagicValue = 0x4D4F4249;  // "MOBI"

        public uint MobiMagic;
        public uint HeaderLength;
        public MobiBookType BookType;
        public MobiTextEncoding TextEncoding;
        public uint RandomId;
        public uint FormatVersion;
        public uint IndexRecordsIndex0028;              // 0xFFFFFFFF
        public uint IndexRecordsIndex002C;              // 0xFFFFFFFF
        public uint IndexRecordsIndex0030;              // 0xFFFFFFFF
        public uint IndexRecordsIndex0034;              // 0xFFFFFFFF
        public uint IndexRecordsIndex0038;              // 0xFFFFFFFF
        public uint IndexRecordsIndex003C;              // 0xFFFFFFFF
        public uint IndexRecordsIndex0040;              // 0xFFFFFFFF
        public uint IndexRecordsIndex0044;              // 0xFFFFFFFF
        public uint IndexRecordsIndex0048;              // 0xFFFFFFFF
        public uint IndexRecordsIndex004C;              // 0xFFFFFFFF
        public uint IndexRecordsIndex0050;              // 0xFFFFFFFF
        public uint FullNameOffset;
        public uint FullNameLength;
        public uint Language;
        public uint InputLanguage;
        public uint OutputLanguage;
        public uint MinVersion;
        public uint EmbeddedRecordsIndex;
        public uint HuffDicRecordsIndex;
        public uint HuffDicRecordsCount;
        public uint HuffDicDirectAccessRecordsIndex;
        public uint HuffDicDirectAccessRecordsCount;
        public MobiFlags Flags;
        public uint Unknown0084;                        // 0x00000000
        public uint Unknown0088;                        // 0x00000000
        public uint Unknown008C;                        // 0x00000000
        public uint Unknown0090;                        // 0x00000000
        public uint Unknown0094;                        // 0x00000000
        public uint Unknown0098;                        // 0x00000000
        public uint Unknown009C;                        // 0x00000000
        public uint Unknown00A0;                        // 0x00000000
        public uint IndexRecordsIndex00A4;              // 0xFFFFFFFF
        public uint Drm00A8;                            // 0xFFFFFFFF
        public uint Drm00AC;                            // 0x00000000
        public uint Drm00B0;                            // 0x00000000
        public uint Drm00B4;                            // 0x00000000
        public uint Unknown00B8;                        // 0x00000000
        public uint Unknown00BC;                        // 0x00000000
        public uint FdstRecordsIndex;
        public uint FdstFlowCount;
        public uint FcisRecordsIndex;
        public uint FcisRecordsCount;
        public uint FlisRecordsIndex;
        public uint FlisRecordsCount;
        public uint Unknown00D8;                        // 0x00000000
        public uint Unknown00DC;                        // 0x00000000
        public uint SrcsRecordsIndex;
        public uint SrcsRecordsCount;
        public uint IndexRecordsIndex00E8;              // 0xFFFFFFFF
        public uint Unknown00EC;                        // 0xFFFFFFFF
        public MobiTextTrailers TextTrailers;
        public uint NcxRecordsIndex;
        public uint FragmentRecordsIndex;
        public uint SkeletonRecordsIndex;
        public uint LocationMapRecordIndex;
        public uint GuideRecordIndex;
        public uint WordMapRecordIndex;
        public uint Unknown010C;                        // 0x00000000
        public uint HDResourceContainerRecordsIndex;
        public uint Unknown0114;                        // 0x00000000
    }
}
