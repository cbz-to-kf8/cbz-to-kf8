// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using System;
using System.Buffers.Binary;
using System.Collections;
using System.IO;
using Tetractic.CommandLine;
using Tetractic.Formats.PalmPdb;

namespace CbzToKf8.Mobi.Dump
{
    internal sealed class Program
    {
        private const uint _book4CC = 0x424F4F4B;  // "BOOK"
        private const uint _mobi4CC = 0x4D4F4249;  // "MOBI"

        private const ulong _boundary8CC = 0x424F554E44415259;

        private const uint _endOfRecords4CC = 0xE98E0D0A;

        /// <exception cref="IOException"/>
        internal static int Main(string[] args)
        {
            var rootCommand = new RootCommand(GetExecutableName("MobiDump"));

            var pathParameter = rootCommand.AddParameter("<mobi>", "The path of a MOBI ebook file.");

            rootCommand.HelpOption = rootCommand.AddOption('h', "help", "Shows a usage summary.");

            var verboseOption = rootCommand.AddOption('v', null, "Enable additional output.");
            rootCommand.VerboseOption = verboseOption;

            rootCommand.SetInvokeHandler(() =>
            {
                string path = pathParameter.Value;

                return Dump(path, verboseOption.Count > 0);
            });

            try
            {
                return rootCommand.Execute(args);
            }
            catch (InvalidCommandLineException ex)
            {
                Console.Error.WriteLine(ex.Message);
                CommandHelp.WriteHelpHint(ex.Command, Console.Error);
                return -1;
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return -1;
            }
        }

        private static string GetExecutableName(string defaultName)
        {
            try
            {
                string[] commandLineArgs = Environment.GetCommandLineArgs();
                if (commandLineArgs.Length > 0)
                    return Path.GetFileNameWithoutExtension(commandLineArgs[0]);
            }
            catch
            {
                // Fall through.
            }

            return defaultName;
        }

        /// <exception cref="Exception"/>
        private static int Dump(string path, bool verbose)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0x10000))
            using (var pdb = new PdbFile(stream))
            {
                if (pdb.TypeId != _book4CC)
                    throw new InvalidDataException("Invalid PDB type.");
                if (pdb.CreatorId != _mobi4CC)
                    throw new InvalidDataException("Invalid PDB creator.");

                var records = pdb.Records;

                if (records.Count == 0)
                    throw new InvalidDataException();

                var recordUsed = new BitArray(records.Count);

                Mobi7Record? mobi7 = null;
                uint mobi7RecordIndex = uint.MaxValue;
                Mobi8Record? mobi8 = null;
                uint mobi8RecordIndex = uint.MaxValue;

                uint mobiVersion;
                using (var recordStream = records[0].OpenData(FileAccess.Read))
                    mobiVersion = MobiRecord.ReadVersion(recordStream);

                if (mobiVersion < 8)
                {
                    mobi7RecordIndex = 0;
                    SetRecordUsed(recordUsed, mobi7RecordIndex);

                    mobi7 = new Mobi7Record();
                    using (var recordStream = records[(int)mobi7RecordIndex].OpenData(FileAccess.Read))
                        mobi7.ReadFrom(recordStream);

                    var entry = Array.Find(mobi7.Exth.Entries, x => x.Tag == ExthTag.Mobi8RecordIndex);
                    if (entry.Data != null && entry.Data.Length == 4)
                    {
                        uint index = BinaryPrimitives.ReadUInt32BigEndian(entry.Data);
                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing MOBI8 record {index}.");

                        if (index > 0)
                        {
                            uint boundaryIndex = index - 1;

                            var record = records[(int)boundaryIndex];
                            if (record.DataLength == 8)
                            {
                                using (var recordStream = record.OpenData(FileAccess.Read))
                                using (var recordReader = new BigEndianBinaryReader(recordStream))
                                    if (recordReader.ReadUInt64() == _boundary8CC)
                                        SetRecordUsed(recordUsed, boundaryIndex);
                            }
                        }

                        mobi8RecordIndex = index;
                        SetRecordUsed(recordUsed, index);

                        mobi8 = new Mobi8Record();
                        using (var recordStream = records[(int)mobi8RecordIndex].OpenData(FileAccess.Read))
                            mobi8.ReadFrom(recordStream);
                    }
                }
                else
                {
                    mobi8RecordIndex = 0;
                    SetRecordUsed(recordUsed, mobi8RecordIndex);

                    mobi8 = new Mobi8Record();
                    using (var recordStream = records[(int)mobi8RecordIndex].OpenData(FileAccess.Read))
                        mobi8.ReadFrom(recordStream);
                }

                var flags = mobi8 != null
                    ? mobi8.Mobi.Flags
                    : mobi7!.Mobi.Flags;
                if ((flags & MobiFlags.HasEndOfRecords) != 0)
                {
                    int index = records.Count - 1;

                    var record = records[index];

                    bool truncated = true;

                    if (record.DataLength == 4)
                    {
                        using (var recordStream = record.OpenData(FileAccess.Read))
                        using (var recordReader = new BigEndianBinaryReader(recordStream))
                            truncated = recordReader.ReadUInt32() != _endOfRecords4CC;
                    }

                    if (truncated)
                        throw new InvalidDataException($"File is truncated.");

                    SetRecordUsed(recordUsed, (uint)index);
                }

                if (mobi7 != null)
                {
                    Console.WriteLine($"MOBI7:");
                    DumpMobi7(mobi7);
                }

                if (mobi8 != null)
                {
                    Console.WriteLine($"MOBI8:");
                    DumpMobi8(mobi8);

                    HuffRecord? huff = null;
                    CdicRecord[]? cdics = null;
                    DatpRecord[]? datps = null;
                    FdstRecord? fdst = null;
                    IndxRecord? ncxIndx = null;
                    IndxRecord? fragmentIndx = null;
                    IndxRecord? skeletonIndx = null;
                    IndxRecord? guideIndx = null;
                    DatpRecord? datp2 = null;

                    if (mobi8.Mobi.HuffDicRecordsCount != 0)
                    {
                        cdics = new CdicRecord[mobi8.Mobi.HuffDicRecordsCount - 1];

                        for (uint i = 0; i < mobi8.Mobi.HuffDicRecordsCount; ++i)
                        {
                            uint index = mobi8RecordIndex + mobi8.Mobi.HuffDicRecordsIndex + i;

                            if (index >= records.Count)
                                throw new InvalidDataException($"Missing HUFF/CDIC record {index}.");

                            SetRecordUsed(recordUsed, index);

                            if (i == 0)
                            {
                                huff = new HuffRecord();
                                using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                                    huff.ReadFrom(recordStream);

                                if (verbose)
                                    DumpHuff(huff);
                            }
                            else
                            {
                                var cdic = new CdicRecord();
                                using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                                    cdic.ReadFrom(recordStream, i == mobi8.Mobi.HuffDicRecordsCount - 1);
                                cdics[i - 1] = cdic;

                                if (verbose)
                                    DumpCdic(cdic, index);
                            }
                        }
                    }

                    HuffDicDecompressor? huffDicDecompressor = null;
                    if (mobi8.Pdoc.CompressionMethod == MobiCompressionMethod.HuffDic)
                    {
                        if (huff == null)
                            throw new InvalidDataException("Missing HUFF record.");
                        if (cdics == null || cdics.Length == 0)
                            throw new InvalidDataException("Missing CDIC records.");

                        huffDicDecompressor = new HuffDicDecompressor(huff, cdics);
                    }

                    for (uint i = 0; i < mobi8.Pdoc.TextRecordsCount; ++i)
                    {
                        uint index = mobi8RecordIndex + 1 + i;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing text record {index}.");

                        SetRecordUsed(recordUsed, index);

                        if (mobi8.Pdoc.EncryptionMethod != MobiEncryptionMethod.Unencrypted)
                            continue;

                        uint recordDataLength = records[(int)index].DataLength;
                        byte[] recordData = new byte[recordDataLength];
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            recordStream.ReadExactly(recordData, 0, recordData.Length);

                        int textMultibyteLength;
                        int textLength = TextRecord.GetTextLength(recordData, mobi8.Mobi.TextTrailers, out textMultibyteLength);

                        byte[] recordText = new byte[mobi8.Pdoc.TextRecordTextLength];

                        switch (mobi8.Pdoc.CompressionMethod)
                        {
                            case MobiCompressionMethod.Uncompressed:
                            {
                                Array.Copy(recordData, recordText, textLength);
                                Array.Resize(ref recordText, textLength);
                                break;
                            }
                            case MobiCompressionMethod.PalmDoc:
                            {
                                int decompressedLength = PalmDocDecompressor.Decompress(recordData, textLength, recordText);
                                Array.Resize(ref recordText, decompressedLength);
                                break;
                            }
                            case MobiCompressionMethod.HuffDic:
                            {
                                int decompressedLength = huffDicDecompressor!.Decompress(recordData, textLength, recordText);
                                Array.Resize(ref recordText, decompressedLength);
                                break;
                            }
                            default:
                                throw new InvalidDataException("Unexpected text compression method.");
                        }

                        Console.WriteLine($"Text {i}:");
                        Hex.Dump(recordText, "  ", writeOffset: true);
                        // TODO: Dump trailers
                        Console.WriteLine();
                    }

                    if (mobi8.Mobi.HuffDicDirectAccessRecordsCount > 0)
                    {
                        datps = new DatpRecord[mobi8.Mobi.HuffDicDirectAccessRecordsCount];

                        for (uint i = 0; i < mobi8.Mobi.HuffDicDirectAccessRecordsCount; ++i)
                        {
                            uint index = mobi8RecordIndex + mobi8.Mobi.HuffDicDirectAccessRecordsIndex + i;

                            if (index >= records.Count)
                                throw new InvalidDataException($"Missing DATP record {index}.");

                            SetRecordUsed(recordUsed, index);

                            var datp = new DatpRecord();
                            using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                                datp.ReadFrom(recordStream);
                            datps[i] = datp;

                            if (verbose)
                                DumpDatp(datp, index);
                        }
                    }

                    if (mobi8.Mobi.FdstRecordsIndex < records.Count)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.FdstRecordsIndex;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing FDST record {index}.");

                        SetRecordUsed(recordUsed, index);

                        fdst = new FdstRecord();
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            fdst.ReadFrom(recordStream);

                        DumpFdst(fdst);
                    }

                    for (uint i = 0; i < mobi8.Mobi.FcisRecordsCount; ++i)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.FcisRecordsIndex + i;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing FCIS record {index}.");

                        SetRecordUsed(recordUsed, index);

                        Console.WriteLine($"TODO: FCIS {i}");
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            Hex.Dump(recordStream, "  ", writeOffset: true);
                        Console.WriteLine();
                    }

                    for (uint i = 0; i < mobi8.Mobi.FlisRecordsCount; ++i)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.FlisRecordsIndex + i;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing FLIS record {index}.");

                        SetRecordUsed(recordUsed, index);

                        Console.WriteLine($"TODO: FLIS {i}");
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            Hex.Dump(recordStream, "  ", writeOffset: true);
                        Console.WriteLine();
                    }

                    if (mobi8.Mobi.NcxRecordsIndex != ~0u)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.NcxRecordsIndex;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing INDX record {index}.");

                        SetRecordUsed(recordUsed, index);

                        ncxIndx = new IndxRecord();
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            ncxIndx.ReadFrom(recordStream, null);

                        DumpIndx(ncxIndx, index, "NCX");

                        var ncxIndxs = new IndxRecord[ncxIndx.Header.EntriesCount];
                        for (uint i = 0; i < ncxIndxs.Length; ++i)
                        {
                            uint index2 = index + 1 + i;

                            var ncxIndx2 = new IndxRecord();
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                ncxIndx2.ReadFrom(recordStream, ncxIndx.Tagx);
                            ncxIndxs[i] = ncxIndx2;

                            SetRecordUsed(recordUsed, index2);

                            DumpIndx(ncxIndx2, index2, $"NCX {i}");
                        }

                        for (uint i = 0; i < ncxIndx.Header.StringRecordsCount; ++i)
                        {
                            uint index2 = index + 1 + ncxIndx.Header.EntriesCount + i;

                            SetRecordUsed(recordUsed, index2);

                            Console.WriteLine($"Record {index2} (NCX strings {i}):");
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                Hex.Dump(recordStream, "  ", writeOffset: true);
                            Console.WriteLine();
                        }
                    }

                    if (mobi8.Mobi.FragmentRecordsIndex != ~0u)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.FragmentRecordsIndex;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing INDX record {index}.");

                        SetRecordUsed(recordUsed, index);

                        fragmentIndx = new IndxRecord();
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            fragmentIndx.ReadFrom(recordStream, null);

                        DumpIndx(fragmentIndx, index, "fragment");

                        var fragmentIndxs = new IndxRecord[fragmentIndx.Header.EntriesCount];
                        for (uint i = 0; i < fragmentIndxs.Length; ++i)
                        {
                            uint index2 = index + 1 + i;

                            var fragmentIndx2 = new IndxRecord();
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                fragmentIndx2.ReadFrom(recordStream, fragmentIndx.Tagx);
                            fragmentIndxs[i] = fragmentIndx2;

                            SetRecordUsed(recordUsed, index2);

                            DumpIndx(fragmentIndx2, index2, $"fragment {i}");
                        }

                        for (uint i = 0; i < fragmentIndx.Header.StringRecordsCount; ++i)
                        {
                            uint index2 = index + 1 + fragmentIndx.Header.EntriesCount + i;

                            SetRecordUsed(recordUsed, index2);

                            Console.WriteLine($"Record {index2} (fragment strings {i}):");
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                Hex.Dump(recordStream, "  ", writeOffset: true);
                            Console.WriteLine();
                        }
                    }

                    if (mobi8.Mobi.SkeletonRecordsIndex != ~0u)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.SkeletonRecordsIndex;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing INDX record {index}.");

                        SetRecordUsed(recordUsed, index);

                        skeletonIndx = new IndxRecord();
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            skeletonIndx.ReadFrom(recordStream, null);

                        DumpIndx(skeletonIndx, index, "skeleton");

                        var skeletonIndxs = new IndxRecord[skeletonIndx.Header.EntriesCount];
                        for (uint i = 0; i < skeletonIndxs.Length; ++i)
                        {
                            uint index2 = index + 1 + i;

                            var skeletonIndx2 = new IndxRecord();
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                skeletonIndx2.ReadFrom(recordStream, skeletonIndx.Tagx);
                            skeletonIndxs[i] = skeletonIndx2;

                            SetRecordUsed(recordUsed, index2);

                            DumpIndx(skeletonIndx2, index2, $"skeleton {i}");
                        }

                        for (uint i = 0; i < skeletonIndx.Header.StringRecordsCount; ++i)
                        {
                            uint index2 = index + 1 + skeletonIndx.Header.EntriesCount + i;

                            SetRecordUsed(recordUsed, index2);

                            Console.WriteLine($"Record {index2} (skeleton strings {i}):");
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                Hex.Dump(recordStream, "  ", writeOffset: true);
                            Console.WriteLine();
                        }
                    }

                    if (mobi8.Mobi.LocationMapRecordIndex != ~0u)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.LocationMapRecordIndex;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing DATP record {index}.");

                        SetRecordUsed(recordUsed, index);

                        datp2 = new DatpRecord();
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            datp2.ReadFrom(recordStream);

                        if (verbose)
                            DumpDatp(datp2, index);
                    }

                    if (mobi8.Mobi.GuideRecordIndex != ~0u)
                    {
                        uint index = mobi8RecordIndex + mobi8.Mobi.GuideRecordIndex;

                        if (index >= records.Count)
                            throw new InvalidDataException($"Missing INDX record {index}.");

                        SetRecordUsed(recordUsed, index);

                        guideIndx = new IndxRecord();
                        using (var recordStream = records[(int)index].OpenData(FileAccess.Read))
                            guideIndx.ReadFrom(recordStream, null);

                        DumpIndx(guideIndx, index, "guide");

                        var guideIndxs = new IndxRecord[guideIndx.Header.EntriesCount];
                        for (uint i = 0; i < guideIndxs.Length; ++i)
                        {
                            uint index2 = index + 1 + i;

                            var guideIndx2 = new IndxRecord();
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                guideIndx2.ReadFrom(recordStream, guideIndx.Tagx);
                            guideIndxs[i] = guideIndx2;

                            SetRecordUsed(recordUsed, index2);

                            DumpIndx(guideIndx2, index2, $"guide {i}");
                        }

                        for (uint i = 0; i < guideIndx.Header.StringRecordsCount; ++i)
                        {
                            uint index2 = index + 1 + guideIndx.Header.EntriesCount + i;

                            SetRecordUsed(recordUsed, index2);

                            Console.WriteLine($"Record {index2} (guide strings {i}):");
                            using (var recordStream = records[(int)index2].OpenData(FileAccess.Read))
                                Hex.Dump(recordStream, "  ", writeOffset: true);
                            Console.WriteLine();
                        }
                    }
                }

                for (int i = 0; i < recordUsed.Length; ++i)
                {
                    if (!recordUsed[i])
                    {
                        Console.WriteLine($"Unused record {i} ({records[i].DataLength} bytes):");

                        using (var dataStream = records[i].OpenData(FileAccess.Read))
                        {
                            byte[] bytes = new byte[16];
                            int length = dataStream.Read(bytes, 0, bytes.Length);
                            Array.Resize(ref bytes, length);
                            Hex.Dump(bytes, "  ", writeOffset: false);
                        }
                    }
                }
            }

            return 0;
        }

        /// <exception cref="InvalidDataException"/>
        private static void SetRecordUsed(BitArray recordUsed, uint index)
        {
            if (recordUsed[(int)index])
                throw new InvalidDataException($"Record {index} used more than once.");

            recordUsed[(int)index] = true;
        }

        /// <exception cref="IOException"/>
        private static void DumpMobi7(Mobi7Record mobi)
        {
            Console.WriteLine($"{nameof(mobi.Pdoc.CompressionMethod)}:               0x{(ushort)mobi.Pdoc.CompressionMethod:X4} ({mobi.Pdoc.CompressionMethod})");
            Console.WriteLine($"{nameof(mobi.Pdoc.Unknown0002)}:                     0x{mobi.Pdoc.Unknown0002:X4}");
            Console.WriteLine($"{nameof(mobi.Pdoc.TextLength)}:                      {mobi.Pdoc.TextLength}");
            Console.WriteLine($"{nameof(mobi.Pdoc.TextRecordsCount)}:                {mobi.Pdoc.TextRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Pdoc.TextRecordTextLength)}:            {mobi.Pdoc.TextRecordTextLength}");
            Console.WriteLine($"{nameof(mobi.Pdoc.EncryptionMethod)}:                0x{(ushort)mobi.Pdoc.EncryptionMethod:X4} ({mobi.Pdoc.EncryptionMethod})");
            Console.WriteLine($"{nameof(mobi.Pdoc.Unknown000E)}:                     0x{mobi.Pdoc.Unknown000E:X4}");
            Console.WriteLine($"{nameof(mobi.Mobi.MobiMagic)}:                       0x{mobi.Mobi.MobiMagic:X8} ({GetPrintable(mobi.Mobi.MobiMagic)})");
            Console.WriteLine($"{nameof(mobi.Mobi.HeaderLength)}:                    {mobi.Mobi.HeaderLength}");
            Console.WriteLine($"{nameof(mobi.Mobi.BookType)}:                        0x{(uint)mobi.Mobi.BookType:X8} ({mobi.Mobi.BookType})");
            Console.WriteLine($"{nameof(mobi.Mobi.TextEncoding)}:                    0x{(uint)mobi.Mobi.TextEncoding:X8} ({mobi.Mobi.TextEncoding})");
            Console.WriteLine($"{nameof(mobi.Mobi.RandomId)}:                        0x{mobi.Mobi.RandomId:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FormatVersion)}:                   {mobi.Mobi.FormatVersion}");
            if (mobi.Mobi.FormatVersion < 3)
                goto end;
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0028)}:                     0x{mobi.Mobi.Unknown0028:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown002C)}:                     0x{mobi.Mobi.Unknown002C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0030)}:                     0x{mobi.Mobi.Unknown0030:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0034)}:                     0x{mobi.Mobi.Unknown0034:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0038)}:                     0x{mobi.Mobi.Unknown0038:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown003C)}:                     0x{mobi.Mobi.Unknown003C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0040)}:                     0x{mobi.Mobi.Unknown0040:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0044)}:                     0x{mobi.Mobi.Unknown0044:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0048)}:                     0x{mobi.Mobi.Unknown0048:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown004C)}:                     0x{mobi.Mobi.Unknown004C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0050)}:                     0x{mobi.Mobi.Unknown0050:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FullNameOffset)}:                  0x{mobi.Mobi.FullNameOffset:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FullNameLength)}:                  {mobi.Mobi.FullNameLength}");
            Console.WriteLine($"{nameof(mobi.Mobi.Language)}:                        {mobi.Mobi.Language}");
            Console.WriteLine($"{nameof(mobi.Mobi.InputLanguage)}:                   {mobi.Mobi.InputLanguage}");
            Console.WriteLine($"{nameof(mobi.Mobi.OutputLanguage)}:                  {mobi.Mobi.OutputLanguage}");
            Console.WriteLine($"{nameof(mobi.Mobi.MinVersion)}:                      {mobi.Mobi.MinVersion}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown006C)}:                     0x{mobi.Mobi.Unknown006C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicRecordsIndex)}:             {mobi.Mobi.HuffDicRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicRecordsCount)}:             {mobi.Mobi.HuffDicRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicDirectAccessRecordsIndex)}: {mobi.Mobi.HuffDicDirectAccessRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicDirectAccessRecordsCount)}: {mobi.Mobi.HuffDicDirectAccessRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.Flags)}:                           0x{(uint)mobi.Mobi.Flags:X8} ({mobi.Mobi.Flags})");
            if (mobi.Mobi.FormatVersion < 4)
                goto end;
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0084)}:                     0x{mobi.Mobi.Unknown0084:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0088)}:                     0x{mobi.Mobi.Unknown0088:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown008C)}:                     0x{mobi.Mobi.Unknown008C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0090)}:                     0x{mobi.Mobi.Unknown0090:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0094)}:                     0x{mobi.Mobi.Unknown0094:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0098)}:                     0x{mobi.Mobi.Unknown0098:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown009C)}:                     0x{mobi.Mobi.Unknown009C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00A0)}:                     0x{mobi.Mobi.Unknown00A0:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00A4)}:                     0x{mobi.Mobi.Unknown00A4:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00A8)}:                     0x{mobi.Mobi.Unknown00A8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00AC)}:                     0x{mobi.Mobi.Unknown00AC:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00B0)}:                     0x{mobi.Mobi.Unknown00B0:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00B4)}:                     0x{mobi.Mobi.Unknown00B4:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00B8)}:                     0x{mobi.Mobi.Unknown00B8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00BC)}:                     0x{mobi.Mobi.Unknown00BC:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00C0)}:                     0x{mobi.Mobi.Unknown00C0:X4}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00C2)}:                     0x{mobi.Mobi.Unknown00C2:X4}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00C4)}:                     0x{mobi.Mobi.Unknown00C4:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FcisRecordsIndex)}:                {mobi.Mobi.FcisRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.FcisRecordsCount)}:                {mobi.Mobi.FcisRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.FlisRecordsIndex)}:                {mobi.Mobi.FlisRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.FlisRecordsCount)}:                {mobi.Mobi.FlisRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00D8)}:                     0x{mobi.Mobi.Unknown00D8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00DC)}:                     0x{mobi.Mobi.Unknown00DC:X8}");
            if (mobi.Mobi.FormatVersion < 6)
                goto end;
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00E0)}:                     0x{mobi.Mobi.Unknown00E0:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00E4)}:                     0x{mobi.Mobi.Unknown00E4:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00E8)}:                     0x{mobi.Mobi.Unknown00E8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00EC)}:                     0x{mobi.Mobi.Unknown00EC:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00F0)}:                     0x{mobi.Mobi.Unknown00F0:X8}");
            if (mobi.Mobi.HeaderLength < 232)
                goto end;
            Console.WriteLine($"{nameof(mobi.Mobi.NcxRecordsIndex)}:                 {mobi.Mobi.NcxRecordsIndex}");
        end:
            if (mobi.Mobi.TrailingData.Length > 0)
            {
                Console.WriteLine($"{nameof(mobi.Mobi.TrailingData)}:");
                Hex.Dump(mobi.Mobi.TrailingData, "  ");
            }
            Console.WriteLine($"{nameof(mobi.FullName)}:");
            Hex.Dump(mobi.FullName, "  ");
            Console.WriteLine();

            if (mobi.Exth != null)
                DumpExth(mobi.Exth);
        }

        /// <exception cref="IOException"/>
        private static void DumpMobi8(Mobi8Record mobi)
        {
            Console.WriteLine($"{nameof(mobi.Pdoc.CompressionMethod)}:               0x{(ushort)mobi.Pdoc.CompressionMethod:X4} ({mobi.Pdoc.CompressionMethod})");
            Console.WriteLine($"{nameof(mobi.Pdoc.Unknown0002)}:                     0x{mobi.Pdoc.Unknown0002:X4}");
            Console.WriteLine($"{nameof(mobi.Pdoc.TextLength)}:                      {mobi.Pdoc.TextLength}");
            Console.WriteLine($"{nameof(mobi.Pdoc.TextRecordsCount)}:                {mobi.Pdoc.TextRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Pdoc.TextRecordTextLength)}:            {mobi.Pdoc.TextRecordTextLength}");
            Console.WriteLine($"{nameof(mobi.Pdoc.EncryptionMethod)}:                0x{(ushort)mobi.Pdoc.EncryptionMethod:X4} ({mobi.Pdoc.EncryptionMethod})");
            Console.WriteLine($"{nameof(mobi.Pdoc.Unknown000E)}:                     0x{mobi.Pdoc.Unknown000E:X4}");
            Console.WriteLine($"{nameof(mobi.Mobi.MobiMagic)}:                       0x{mobi.Mobi.MobiMagic:X8} ({GetPrintable(mobi.Mobi.MobiMagic)})");
            Console.WriteLine($"{nameof(mobi.Mobi.HeaderLength)}:                    {mobi.Mobi.HeaderLength}");
            Console.WriteLine($"{nameof(mobi.Mobi.BookType)}:                        0x{(uint)mobi.Mobi.BookType:X8} ({mobi.Mobi.BookType})");
            Console.WriteLine($"{nameof(mobi.Mobi.TextEncoding)}:                    0x{(uint)mobi.Mobi.TextEncoding:X8} ({mobi.Mobi.TextEncoding})");
            Console.WriteLine($"{nameof(mobi.Mobi.RandomId)}:                        0x{mobi.Mobi.RandomId:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FormatVersion)}:                   {mobi.Mobi.FormatVersion}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0028)}:           0x{mobi.Mobi.IndexRecordsIndex0028:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex002C)}:           0x{mobi.Mobi.IndexRecordsIndex002C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0030)}:           0x{mobi.Mobi.IndexRecordsIndex0030:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0034)}:           0x{mobi.Mobi.IndexRecordsIndex0034:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0038)}:           0x{mobi.Mobi.IndexRecordsIndex0038:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex003C)}:           0x{mobi.Mobi.IndexRecordsIndex003C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0040)}:           0x{mobi.Mobi.IndexRecordsIndex0040:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0044)}:           0x{mobi.Mobi.IndexRecordsIndex0044:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0048)}:           0x{mobi.Mobi.IndexRecordsIndex0048:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex004C)}:           0x{mobi.Mobi.IndexRecordsIndex004C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex0050)}:           0x{mobi.Mobi.IndexRecordsIndex0050:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FullNameOffset)}:                  0x{mobi.Mobi.FullNameOffset:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FullNameLength)}:                  {mobi.Mobi.FullNameLength}");
            Console.WriteLine($"{nameof(mobi.Mobi.Language)}:                        {mobi.Mobi.Language}");
            Console.WriteLine($"{nameof(mobi.Mobi.InputLanguage)}:                   {mobi.Mobi.InputLanguage}");
            Console.WriteLine($"{nameof(mobi.Mobi.OutputLanguage)}:                  {mobi.Mobi.OutputLanguage}");
            Console.WriteLine($"{nameof(mobi.Mobi.MinVersion)}:                      {mobi.Mobi.MinVersion}");
            Console.WriteLine($"{nameof(mobi.Mobi.EmbeddedRecordsIndex)}:            {mobi.Mobi.EmbeddedRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicRecordsIndex)}:             {mobi.Mobi.HuffDicRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicRecordsCount)}:             {mobi.Mobi.HuffDicRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicDirectAccessRecordsIndex)}: {mobi.Mobi.HuffDicDirectAccessRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.HuffDicDirectAccessRecordsCount)}: {mobi.Mobi.HuffDicDirectAccessRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.Flags)}:                           0x{(uint)mobi.Mobi.Flags:X8} ({mobi.Mobi.Flags})");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0084)}:                     0x{mobi.Mobi.Unknown0084:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0088)}:                     0x{mobi.Mobi.Unknown0088:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown008C)}:                     0x{mobi.Mobi.Unknown008C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0090)}:                     0x{mobi.Mobi.Unknown0090:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0094)}:                     0x{mobi.Mobi.Unknown0094:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0098)}:                     0x{mobi.Mobi.Unknown0098:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown009C)}:                     0x{mobi.Mobi.Unknown009C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00A0)}:                     0x{mobi.Mobi.Unknown00A0:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex00A4)}:           0x{mobi.Mobi.IndexRecordsIndex00A4:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Drm00A8)}:                         0x{mobi.Mobi.Drm00A8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Drm00AC)}:                         0x{mobi.Mobi.Drm00AC:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Drm00B0)}:                         0x{mobi.Mobi.Drm00B0:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Drm00B4)}:                         0x{mobi.Mobi.Drm00B4:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00B8)}:                     0x{mobi.Mobi.Unknown00B8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00BC)}:                     0x{mobi.Mobi.Unknown00BC:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.FdstRecordsIndex)}:                {mobi.Mobi.FdstRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.FdstFlowCount)}:                   {mobi.Mobi.FdstFlowCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.FcisRecordsIndex)}:                {mobi.Mobi.FcisRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.FcisRecordsCount)}:                {mobi.Mobi.FcisRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.FlisRecordsIndex)}:                {mobi.Mobi.FlisRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.FlisRecordsCount)}:                {mobi.Mobi.FlisRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00D8)}:                     0x{mobi.Mobi.Unknown00D8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00DC)}:                     0x{mobi.Mobi.Unknown00DC:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.SrcsRecordsIndex)}:                {mobi.Mobi.SrcsRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.SrcsRecordsCount)}:                {mobi.Mobi.SrcsRecordsCount}");
            Console.WriteLine($"{nameof(mobi.Mobi.IndexRecordsIndex00E8)}:           0x{mobi.Mobi.IndexRecordsIndex00E8:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown00EC)}:                     0x{mobi.Mobi.Unknown00EC:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.TextTrailers)}:                    0x{(uint)mobi.Mobi.TextTrailers:X8} ({mobi.Mobi.TextTrailers})");
            Console.WriteLine($"{nameof(mobi.Mobi.NcxRecordsIndex)}:                 {mobi.Mobi.NcxRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.FragmentRecordsIndex)}:            {mobi.Mobi.FragmentRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.SkeletonRecordsIndex)}:            {mobi.Mobi.SkeletonRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.LocationMapRecordIndex)}:          {mobi.Mobi.LocationMapRecordIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.GuideRecordIndex)}:                {mobi.Mobi.GuideRecordIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.WordMapRecordIndex)}:              {mobi.Mobi.WordMapRecordIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown010C)}:                     0x{mobi.Mobi.Unknown010C:X8}");
            Console.WriteLine($"{nameof(mobi.Mobi.HDResourceContainerRecordsIndex)}: {mobi.Mobi.HDResourceContainerRecordsIndex}");
            Console.WriteLine($"{nameof(mobi.Mobi.Unknown0114)}:                     0x{mobi.Mobi.Unknown0114:X8}");
            Console.WriteLine($"{nameof(mobi.FullName)}:");
            Hex.Dump(mobi.FullName, "  ");
            Console.WriteLine();

            if (mobi.Exth != null)
                DumpExth(mobi.Exth);
        }

        /// <exception cref="IOException"/>
        private static void DumpExth(ExthBlock exth)
        {
            Console.WriteLine($"{nameof(exth.Header.ExthMagic)}:    0x{exth.Header.ExthMagic:X8} ({GetPrintable(exth.Header.ExthMagic)})");
            Console.WriteLine($"{nameof(exth.Header.ExthLength)}:   {exth.Header.ExthLength}");
            Console.WriteLine($"{nameof(exth.Header.EntriesCount)}: {exth.Header.EntriesCount}");
            Console.WriteLine($"{nameof(exth.Entries)}:");
            for (int i = 0; i < exth.Entries.Length; ++i)
            {
                ref var entry = ref exth.Entries[i];

                Console.WriteLine($"  Tag: 0x{(uint)entry.Tag:X8} ({entry.Tag}); Length: {entry.Length}");
                Hex.Dump(entry.Data, "    ");
            }
            if (exth.TrailingData.Length > 0)
            {
                Console.WriteLine($"{nameof(exth.TrailingData)}:");
                Hex.Dump(exth.TrailingData, "  ");
            }
            Console.WriteLine();
        }

        /// <exception cref="IOException"/>
        private static void DumpCdic(CdicRecord cdic, uint recordIndex)
        {
            Console.WriteLine($"Record {recordIndex}:");
            Console.WriteLine($"{nameof(cdic.CdicMagic)}:         0x{cdic.CdicMagic:X8} ({GetPrintable(cdic.CdicMagic)})");
            Console.WriteLine($"{nameof(cdic.HeaderLength)}:      {cdic.HeaderLength}");
            Console.WriteLine($"{nameof(cdic.TotalEntriesCount)}: {cdic.TotalEntriesCount}");
            Console.WriteLine($"{nameof(cdic.IndexShift)}:        {cdic.IndexShift}");
            Console.WriteLine($"{nameof(cdic.EntryOffsets)}:");
            for (int i = 0; i < cdic.EntryOffsets.Length; ++i)
                Console.WriteLine($"  0x{cdic.EntryOffsets[i]:X8}");
            Console.WriteLine($"{nameof(cdic.Entries)}:");
            for (int i = 0; i < cdic.Entries.Length; ++i)
            {
                ref var entry = ref cdic.Entries[i];

                Console.WriteLine($"  {nameof(entry.Literal)}: {(entry.Literal ? 1 : 0)}; {nameof(entry.Length)}: {entry.Length}");
                Hex.Dump(entry.Data, "    ");
            }
            Console.WriteLine();
        }

        /// <exception cref="IOException"/>
        private static void DumpDatp(DatpRecord datp, uint recordIndex)
        {
            Console.WriteLine($"Record {recordIndex}:");
            Console.WriteLine($"{nameof(datp.DatpMagic)}:      0x{datp.DatpMagic:X8} ({GetPrintable(datp.DatpMagic)})");
            Console.WriteLine($"{nameof(datp.HeaderLength)}:   {datp.HeaderLength}");
            Console.WriteLine($"{nameof(datp.Unknowns1Count)}: {datp.Unknowns1Count}");
            Console.WriteLine($"{nameof(datp.Unknowns2Shift)}: {datp.Unknowns2Shift}");
            Console.WriteLine($"{nameof(datp.Unknowns3Count)}: {datp.Unknowns3Count}");
            if (datp.HeaderTrailingData.Length > 0)
            {
                Console.WriteLine($"{nameof(datp.HeaderTrailingData)}:");
                Hex.Dump(datp.HeaderTrailingData, "  ");
            }
            Console.WriteLine($"{nameof(datp.Unknowns1)}:");
            for (int i = 0; i < datp.Unknowns1.Length; ++i)
                Console.WriteLine($"  0x{datp.Unknowns1[i]:X8}");
            Console.WriteLine($"{nameof(datp.Unknowns2)}:");
            for (int i = 0; i < datp.Unknowns2.Length; ++i)
                Console.WriteLine($"  0x{datp.Unknowns2[i]:X8}");
            Console.WriteLine($"{nameof(datp.Unknowns3):X8}:");
            for (int i = 0; i < datp.Unknowns3.Length; ++i)
                Console.WriteLine($"  0x{datp.Unknowns3[i]:X4}");
            if (datp.TrailingData.Length > 0)
            {
                Console.WriteLine($"{nameof(datp.TrailingData)}:");
                Hex.Dump(datp.TrailingData, "  ");
            }
            Console.WriteLine();
        }

        /// <exception cref="IOException"/>
        private static void DumpFdst(FdstRecord fdst)
        {
            Console.WriteLine($"{nameof(fdst.Header.FdstMagic)}:    0x{fdst.Header.FdstMagic:X8} ({GetPrintable(fdst.Header.FdstMagic)})");
            Console.WriteLine($"{nameof(fdst.Header.HeaderLength)}: {fdst.Header.HeaderLength}");
            Console.WriteLine($"{nameof(fdst.Header.EntriesCount)}: {fdst.Header.EntriesCount}");
            Console.WriteLine($"{nameof(fdst.Entries)}:");
            for (int i = 0; i < fdst.Entries.Length; ++i)
            {
                ref var entry = ref fdst.Entries[i];

                Console.WriteLine($"  [0x{entry.StartOffset:X8}, 0x{entry.EndOffset:X8})");
            }
            if (fdst.TrailingData.Length > 0)
            {
                Console.WriteLine($"{nameof(fdst.TrailingData)}:");
                Hex.Dump(fdst.TrailingData, "  ");
            }
            Console.WriteLine();
        }

        /// <exception cref="IOException"/>
        private static void DumpHuff(HuffRecord huff)
        {
            Console.WriteLine($"{nameof(huff.HuffMagic)}:                    0x{huff.HuffMagic:X8} ({GetPrintable(huff.HuffMagic)})");
            Console.WriteLine($"{nameof(huff.HeaderLength)}:                 {huff.HeaderLength}");
            Console.WriteLine($"{nameof(huff.LutBigEndianOffset)}:           0x{huff.LutBigEndianOffset:X8}");
            Console.WriteLine($"{nameof(huff.CodeRangesBigEndianOffset)}:    0x{huff.CodeRangesBigEndianOffset:X8}");
            Console.WriteLine($"{nameof(huff.LutLittleEndianOffset)}:        0x{huff.LutLittleEndianOffset:X8}");
            Console.WriteLine($"{nameof(huff.CodeRangesLittleEndianOffset)}: 0x{huff.CodeRangesLittleEndianOffset:X8}");
            Console.WriteLine($"{nameof(huff.LutBigEndian)}:");
            DumpLut(huff.LutBigEndian);
            Console.WriteLine($"{nameof(huff.CodeRangesBigEndian)}:");
            DumpCodeRanges(huff.CodeRangesBigEndian);
            Console.WriteLine($"{nameof(huff.LutLittleEndian)}:");
            DumpLut(huff.LutLittleEndian);
            Console.WriteLine($"{nameof(huff.CodeRangesLittleEndian)}:");
            DumpCodeRanges(huff.CodeRangesLittleEndian);
            if (huff.TrailingData.Length > 0)
            {
                Console.WriteLine($"{nameof(huff.TrailingData)}:");
                Hex.Dump(huff.TrailingData, "  ");
            }
            Console.WriteLine();

            void DumpLut(HuffRecord.LutEntry[] lut)
            {
                for (int i = 0; i < lut.Length; ++i)
                {
                    ref var entry = ref lut[i];

                    uint maxCode = entry.MaxCode;
                    uint maxCodeExtended = (maxCode + 1 << (32 - entry.CodeBitLength)) - 1;

                    Console.WriteLine($"  0x{i:X2}: " +
                        $"{nameof(entry.MaxCode)}: 0x{entry.MaxCode:X8} (0x{maxCodeExtended:X8}; " +
                        $"{nameof(entry.Terminal)}: {(entry.Terminal ? 1 : 0)}; " +
                        $"{nameof(entry.Unused)}: {entry.Unused}; " +
                        $"{nameof(entry.CodeBitLength)}: {entry.CodeBitLength,2})");
                }
            }

            void DumpCodeRanges(HuffRecord.CodeRangeEntry[] codeRanges)
            {
                for (int i = 0; i < codeRanges.Length; ++i)
                {
                    ref var entry = ref codeRanges[i];

                    uint minCode = entry.MinCode;
                    uint maxCode = entry.MaxCode;
                    int codeBitLength = i + 1;
                    uint minCodeExtended = minCode << (32 - codeBitLength);
                    uint maxCodeExtended = (maxCode + 1 << (32 - codeBitLength)) - 1;

                    Console.WriteLine($"  {codeBitLength,2}: [0x{minCode:X8}, 0x{maxCode:X8}] ([0x{minCodeExtended:X8}, 0x{maxCodeExtended:X8}])");
                }
            }
        }

        /// <exception cref="IOException"/>
        private static void DumpIndx(IndxRecord indx, uint recordIndex, string hint)
        {
            Console.WriteLine($"Record {recordIndex} ({hint}):");
            Console.WriteLine($"{nameof(indx.Header.IndxMagic)}:          0x{indx.Header.IndxMagic:X8} ({GetPrintable(indx.Header.IndxMagic)})");
            Console.WriteLine($"{nameof(indx.Header.HeaderLength)}:       {indx.Header.HeaderLength}");
            Console.WriteLine($"{nameof(indx.Header.EntryDescriptor)}:    0x{indx.Header.EntryDescriptor:X8}");
            Console.WriteLine($"{nameof(indx.Header.Kind)}:               0x{indx.Header.Kind:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0010)}:        0x{indx.Header.Unknown0010:X8}");
            Console.WriteLine($"{nameof(indx.Header.IdxtOffset)}:         0x{indx.Header.IdxtOffset:X8}");
            Console.WriteLine($"{nameof(indx.Header.EntriesCount)}:       {indx.Header.EntriesCount}");
            Console.WriteLine($"{nameof(indx.Header.TextEncoding)}:       0x{indx.Header.TextEncoding:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0020)}:        0x{indx.Header.Unknown0020:X8}");
            Console.WriteLine($"{nameof(indx.Header.TotalEntriesCount)}:  {indx.Header.TotalEntriesCount}");
            Console.WriteLine($"{nameof(indx.Header.OrdtOffset)}:         0x{indx.Header.OrdtOffset:X8}");
            Console.WriteLine($"{nameof(indx.Header.LigtOffset)}:         0x{indx.Header.LigtOffset:X8}");
            Console.WriteLine($"{nameof(indx.Header.LigtEntriesCount)}:   {indx.Header.LigtEntriesCount}");
            Console.WriteLine($"{nameof(indx.Header.StringRecordsCount)}: {indx.Header.StringRecordsCount}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0038)}:        0x{indx.Header.Unknown0038:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown003C)}:        0x{indx.Header.Unknown003C:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0040)}:        0x{indx.Header.Unknown0040:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0044)}:        0x{indx.Header.Unknown0044:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0048)}:        0x{indx.Header.Unknown0048:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown004C)}:        0x{indx.Header.Unknown004C:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0050)}:        0x{indx.Header.Unknown0050:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0054)}:        0x{indx.Header.Unknown0054:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0058)}:        0x{indx.Header.Unknown0058:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown005C)}:        0x{indx.Header.Unknown005C:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0060)}:        0x{indx.Header.Unknown0060:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0064)}:        0x{indx.Header.Unknown0064:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0068)}:        0x{indx.Header.Unknown0068:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown006C)}:        0x{indx.Header.Unknown006C:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0070)}:        0x{indx.Header.Unknown0070:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0074)}:        0x{indx.Header.Unknown0074:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0078)}:        0x{indx.Header.Unknown0078:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown007C)}:        0x{indx.Header.Unknown007C:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0080)}:        0x{indx.Header.Unknown0080:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0084)}:        0x{indx.Header.Unknown0084:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0088)}:        0x{indx.Header.Unknown0088:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown008C)}:        0x{indx.Header.Unknown008C:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0090)}:        0x{indx.Header.Unknown0090:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0094)}:        0x{indx.Header.Unknown0094:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown0098)}:        0x{indx.Header.Unknown0098:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown009C)}:        0x{indx.Header.Unknown009C:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00A0)}:        0x{indx.Header.Unknown00A0:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00A4)}:        0x{indx.Header.Unknown00A4:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00A8)}:        0x{indx.Header.Unknown00A8:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00AC)}:        0x{indx.Header.Unknown00AC:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00B0)}:        0x{indx.Header.Unknown00B0:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00B4)}:        0x{indx.Header.Unknown00B4:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00B8)}:        0x{indx.Header.Unknown00B8:X8}");
            Console.WriteLine($"{nameof(indx.Header.Unknown00BC)}:        0x{indx.Header.Unknown00BC:X8}");
            if (indx.Tagx != null)
                DumpTagx(indx.Tagx);
            Console.WriteLine($"{nameof(indx.IdxtHeader.IdxtMagic)}: 0x{indx.IdxtHeader.IdxtMagic:X8} ({GetPrintable(indx.IdxtHeader.IdxtMagic)})");
            Console.WriteLine($"{nameof(indx.IdxtOffsets)}:");
            for (int i = 0; i < indx.IdxtOffsets.Length; ++i)
                Console.WriteLine($"  0x{indx.IdxtOffsets[i]:X4}");
            Console.WriteLine($"{nameof(indx.IdxtEntries)}:");
            for (int i = 0; i < indx.IdxtEntries.Length; ++i)
            {
                ref var entry = ref indx.IdxtEntries[i];

                if (entry.Tags == null)
                {
                    Console.WriteLine($"  {nameof(entry.Length)}: {entry.Length}; {nameof(entry.EntriesCount)}: {entry.EntriesCount}");
                    Hex.Dump(entry.Data, "    ");
                }
                else
                {
                    Console.WriteLine($"  {nameof(entry.Length)}: {entry.Length}");
                    Hex.Dump(entry.Data, "    ");
                    foreach (var tag in entry.Tags)
                    {
                        Console.WriteLine($"    Tag {tag.TagEntry.TagId}{(tag.Values.Length > 0 ? ":" : "")}");
                        foreach (ulong value in tag.Values)
                            Console.WriteLine($"      0x{value:X16}");
                    }
                }
            }
            Console.WriteLine();
        }

        /// <exception cref="IOException"/>
        private static void DumpTagx(TagxBlock tagx)
        {
            Console.WriteLine($"{nameof(tagx.Header.TagxMagic)}:              0x{tagx.Header.TagxMagic:X8} ({GetPrintable(tagx.Header.TagxMagic)})");
            Console.WriteLine($"{nameof(tagx.Header.TagxLength)}:             {tagx.Header.TagxLength}");
            Console.WriteLine($"{nameof(tagx.Header.ValuesDescriptorLength)}: {tagx.Header.ValuesDescriptorLength}");
            Console.WriteLine($"{nameof(tagx.Entries)}:");
            for (int i = 0; i < tagx.Entries.Length; ++i)
            {
                ref var entry = ref tagx.Entries[i];

                Console.WriteLine($"  {nameof(entry.TagId)}: {entry.TagId,3}; {nameof(entry.ValuesPerElement)}: {entry.ValuesPerElement,3}; {nameof(entry.ValuesDescriptorByteMask)}: 0x{entry.ValuesDescriptorByteMask:X2}; {nameof(entry.EndOfValuesDescriptorByte)}: 0x{entry.EndOfValuesDescriptorByte:X2}");
            }
            if (tagx.TrailingData.Length > 0)
            {
                Console.WriteLine($"{nameof(tagx.TrailingData)}:");
                Hex.Dump(tagx.TrailingData, "  ");
            }
        }

        private static string GetPrintable(uint id)
        {
            Span<byte> bytes = stackalloc byte[4];
            Span<char> chars = stackalloc char[4];

            BinaryPrimitives.WriteUInt32BigEndian(bytes, id);

            for (int i = 0; i < 4; ++i)
            {
                byte b = bytes[i];
                chars[i] = b < ' ' ? '.' : (char)b;
            }

            return new string(chars);
        }
    }
}
