// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;

namespace CbzToKf8.Mobi
{
    [Flags]
    internal enum MobiFlags : uint
    {
        None = 0,
        HasEndOfRecords = 0x10,
        HasExtendedHeader = 0x40,
        Unknown11 = 0x800,
        HasHDResourceContainers = 0x40000000,
    }
}
