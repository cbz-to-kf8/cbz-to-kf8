// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System.IO;

namespace CbzToKf8.Mobi
{
    internal static class TextRecord
    {
        /// <exception cref="InvalidDataException"/>
        public static int GetTextLength(byte[] recordData, MobiTextTrailers trailers, out int multibyteLength)
        {
            int textLength = recordData.Length;

            for (uint trailerBits = (uint)trailers >> 1; trailerBits != 0; trailerBits >>= 1)
            {
                if ((trailerBits & 1) != 0)
                {
                    if (textLength == 0)
                        throw new InvalidDataException("Invalid text trailer.");

                    uint index = (uint)textLength - 1;
                    uint trailerLength = 0;
                    for (int shift = 0; shift < 32; shift += 7)
                    {
                        byte b = recordData[index];
                        trailerLength |= ((uint)b & 0x7f) << shift;
                        if ((b & 0x80) != 0)
                            break;

                        if (index == 0)
                            throw new InvalidDataException("Invalid text trailer.");
                        index -= 1;
                    }

                    if (trailerLength > textLength)
                        throw new InvalidDataException("Invalid text trailer.");
                    textLength = (int)(textLength - trailerLength);
                }
            }

            if ((trailers & MobiTextTrailers.Multibyte) != 0)
            {
                if (textLength == 0)
                    throw new InvalidDataException("Invalid text trailer.");

                uint index = (uint)textLength - 1;
                byte trailerLength = (byte)(recordData[index] & 0xF);

                if (trailerLength > index)
                    throw new InvalidDataException("Invalid text trailer.");
                textLength = (int)(index - trailerLength);

                multibyteLength = trailerLength;
            }
            else
            {
                multibyteLength = 0;
            }

            return textLength;
        }
    }
}
