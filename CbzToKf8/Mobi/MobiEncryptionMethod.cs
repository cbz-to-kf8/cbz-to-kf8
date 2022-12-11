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
    public enum MobiEncryptionMethod : ushort
    {
        /// <summary>
        /// No encryption.
        /// </summary>
        Unencrypted = 0,

        /// <summary>
        /// Mobipocket method 1.
        /// </summary>
        Mobipocket1 = 1,

        /// <summary>
        /// Mobipocket method 2.
        /// </summary>
        Mobipocket2 = 2,
    }
}
