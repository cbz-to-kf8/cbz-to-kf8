// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using System.Text;

namespace CbzToKf8.Mobi.Dump
{
    internal sealed class ConsoleEncoding : Encoding
    {
        private readonly Encoding _encoding;

        /// <exception cref="ArgumentNullException"/>
        public ConsoleEncoding(Encoding encoding)
        {
            if (encoding is null)
                throw new ArgumentNullException(nameof(encoding));

            _encoding = encoding;
        }

        public override string BodyName => _encoding.BodyName;

        public override int CodePage => _encoding.CodePage;

        public override string EncodingName => _encoding.EncodingName;

        public override string HeaderName => _encoding.HeaderName;

        public override bool IsBrowserDisplay => _encoding.IsBrowserDisplay;

        public override bool IsBrowserSave => _encoding.IsBrowserSave;

        public override bool IsMailNewsDisplay => _encoding.IsMailNewsDisplay;

        public override bool IsMailNewsSave => _encoding.IsMailNewsSave;

        public override bool IsSingleByte => _encoding.IsSingleByte;

        public override ReadOnlySpan<byte> Preamble => ReadOnlySpan<byte>.Empty;

        public override string WebName => _encoding.WebName;

        public override int WindowsCodePage => _encoding.WindowsCodePage;

        public override object Clone() => new ConsoleEncoding(_encoding);

        public override int GetByteCount(char[] chars, int index, int count) => _encoding.GetByteCount(chars, index, count);

        public override int GetByteCount(char[] chars) => _encoding.GetByteCount(chars);

        public override int GetByteCount(ReadOnlySpan<char> chars) => _encoding.GetByteCount(chars);

        public override int GetByteCount(string s) => _encoding.GetByteCount(s);

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) => _encoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex);

        public override byte[] GetBytes(char[] chars) => _encoding.GetBytes(chars);

        public override byte[] GetBytes(char[] chars, int index, int count) => _encoding.GetBytes(chars, index, count);

        public override int GetBytes(ReadOnlySpan<char> chars, Span<byte> bytes) => _encoding.GetBytes(chars, bytes);

        public override byte[] GetBytes(string s) => _encoding.GetBytes(s);

        public override int GetBytes(string s, int charIndex, int charCount, byte[] bytes, int byteIndex) => _encoding.GetBytes(s, charIndex, charCount, bytes, byteIndex);

        public override int GetCharCount(byte[] bytes, int index, int count) => _encoding.GetCharCount(bytes, index, count);

        public override int GetCharCount(byte[] bytes) => _encoding.GetCharCount(bytes);

        public override int GetCharCount(ReadOnlySpan<byte> bytes) => _encoding.GetCharCount(bytes);

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) => _encoding.GetChars(bytes, byteIndex, byteCount, chars, charIndex);

        public override char[] GetChars(byte[] bytes) => _encoding.GetChars(bytes);

        public override char[] GetChars(byte[] bytes, int index, int count) => _encoding.GetChars(bytes, index, count);

        public override int GetChars(ReadOnlySpan<byte> bytes, Span<char> chars) => _encoding.GetChars(bytes, chars);

        public override Decoder GetDecoder() => _encoding.GetDecoder();

        public override Encoder GetEncoder() => _encoding.GetEncoder();

        public override byte[] GetPreamble() => Array.Empty<byte>();

        public override int GetMaxByteCount(int charCount) => _encoding.GetMaxByteCount(charCount);

        public override int GetMaxCharCount(int byteCount) => _encoding.GetMaxCharCount(byteCount);

        public override string GetString(byte[] bytes) => _encoding.GetString(bytes);

        public override string GetString(byte[] bytes, int index, int count) => _encoding.GetString(bytes, index, count);

        public override bool IsAlwaysNormalized(NormalizationForm form) => _encoding.IsAlwaysNormalized(form);
    }
}
