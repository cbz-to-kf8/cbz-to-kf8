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
    internal static class Base32
    {
        public static string Encode(uint value)
        {
            char[] chars = new char[7];
            for (int i = chars.Length; ;)
            {
                i -= 1;
                uint bits = value & 0x1F;
                value >>= 5;
                chars[i] = (char)((bits < 10 ? '0' : ('A' - 10)) + bits);
                if (value == 0)
                    return new string(chars, i, chars.Length - i);
            }
        }
    }
}
