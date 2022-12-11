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
    internal static class MobiUrl
    {
        public static string GetEmbedUrl(ushort index)
        {
            string id = GetBase32Encoded((ushort)(index + 1));

            return $"kindle:embed:{id}";
        }

        private static string GetBase32Encoded(ushort index)
        {
            char[] idChars = new char[4];
            for (int i = idChars.Length; i > 0; )
            {
                i -= 1;
                int bits = index & 0x1F;
                index >>= 5;
                idChars[i] = (char)((bits < 10 ? '0' : ('A' - 10)) + bits);
            }
            return new string(idChars);
        }
    }
}
