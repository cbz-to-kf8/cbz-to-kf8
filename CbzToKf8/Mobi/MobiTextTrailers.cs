﻿// Copyright 2022 Carl Reinke
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
    internal enum MobiTextTrailers : uint
    {
        None = 0,
        Multibyte = 0x1,
        Unknown1 = 0x2,
        UncrossableBreaks = 0x4,
    }
}
