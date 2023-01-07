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
    internal enum MobiCompressionMethod : ushort
    {
        /// <summary>
        /// No compression.
        /// </summary>
        Uncompressed = 1,

        /// <summary>
        /// PalmDOC LZ77 and byte-pair compression.
        /// </summary>
        // https://en.wikibooks.org/wiki/Data_Compression/Dictionary_compression#PalmDoc
        PalmDoc = 2,

        /// <summary>
        /// Huffman with dictionary.
        /// </summary>
        HuffDic = 17480,
    }
}
