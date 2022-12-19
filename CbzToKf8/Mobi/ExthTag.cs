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
    internal enum ExthTag : uint
    {
        // 3                                // string
        Author = 100,                       // string
        Publisher = 101,                    // string
        Description = 103,                  // string
        PublicationDate = 106,              // string (YYYY-MM-DD)
        Rights = 109,                       // string
        Source = 112,                       // string
        Asin = 113,                         // string (ASIN)
        Sample = 115,                       // (ex. 0x00000000)
        StartReadingLocation = 116,         // (ex. 0xFFFFFFFF)
        Mobi8RecordIndex = 121,             // (ex. 0x000000EB)
        FixedLayout = 122,                  // string (ex. "true")
        BookType = 123,                     // string (ex. "comic")
        OrientationLock = 124,              // string (ex. "none", "landscape", portrait")
        ResourcesCount = 125,               // (ex. 0x000000DC)
        OriginalResolution = 126,           // string (ex. "2000x3000")
        ZeroGutter = 127,                   // string (ex. "true")
        ZeroMargin = 128,                   // string (ex. "true")
        MetadataResourceUrl = 129,          // string (ex. "kindle:embed:0001")
        MetadataRecordOffset = 131,         // (ex. 0xFFFFFFFF)
        RegionMagnification = 132,          // string (ex. "false")
        VeryShortTitle = 200,               // string
        CoverImageIndex = 201,              // 4-byte integer
        ThumbImageIndex = 202,              // 4-byte integer
        HasFakeCover = 203,                 // (ex. 0x00000000)
        CreatorSoftwareId = 204,            // (ex. 0x000000C9)
        CreatorSoftwareMajorVersion = 205,  // (ex. 0x00000002)
        CreatorSoftwareMinorVersion = 206,  // (ex. 0x00000009)
        CreatorSoftwareBuildNumber = 207,   // (ex. 0x00000000)
        Watermark = 208,                    // string
        TamperproofKeys = 209,              // bytes
        FontSignature = 300,                // bytes
        ClippingLimit = 401,                // 1 byte (percentage)
        // 403                              // 1 byte (ex. 0x01)
        // 404                              // 1 byte (ex. 0x01)
        // 405                              // 1 byte (ex. 0x00)
        // 406                              // 8 bytes (ex. 0x0000000000000000)
        // 407                              // 8 bytes (ex. 0x0000000000000000)
        DocumentType = 501,                 // string (ex. "EBOK")
        UpdatedTitle = 503,                 // string
        DocumentId = 504,                   // string (ASIN)
        Language = 524,                     // string (ex. "en")
        PrimaryWritingMode = 525,           // string (ex. "horizontal-rl")
        PageProgressionDirection = 527,     // string (ex. "rtl")
        OverrideKindleFonts = 528,          // string (ex. "false")
        // 529                              // string (ex. "Source-Target:c1-c2 KT_Version:2.9 Build:0000-kdevbld"
        // 534                              // string (ex. "dtc")
        CreatorSoftwareBuild = 535,         // string (ex. "0721-dedaf5")
        HDResourceContainerInfo = 536,      // string (ex. "1920x1920:0-220|")
        // 542                              // 4 bytes
        // 547                              // string (ex. "OnDisk" or "InMemory")
        // 548                              // string (ex. "OnDisk")
    }
}
