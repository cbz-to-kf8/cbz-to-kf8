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
    public enum MobiTextEncoding : uint
    {
        /// <summary>
        /// Windows code page 1252.
        /// </summary>
        Windows1252 = 1252,

        /// <summary>
        /// UTF-8.
        /// </summary>
        Utf8 = 65001,
    }
}
