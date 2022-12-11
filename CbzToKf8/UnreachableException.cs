// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;

namespace CbzToKf8
{
    internal sealed class UnreachableException : Exception
    {
        public UnreachableException()
            : base("The program executed an instruction that was thought to be unreachable.")
        {
        }
    }
}
