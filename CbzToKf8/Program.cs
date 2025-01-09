// Copyright 2022 Carl Reinke
//
// This file is part of a program that is licensed under the terms of the GNU
// Affero General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using CbzToKf8.Mobi;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Web;
using Tetractic.CommandLine;
using Tetractic.Formats.PalmPdb;
using static System.FormattableString;

namespace CbzToKf8
{
    internal sealed class Program
    {
        private const uint _bookMagicValue = 0x424F4F4B;  // "BOOK"
        private const uint _mobiMagicValue = 0x4D4F4249;  // "MOBI"

        private static readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly char[] _pathSeparators = ['/', '\\'];

        private static readonly SortedList<string, DeviceInfo> _deviceInfos = new()
        {
            { "pw4", new DeviceInfo {
                Name = "Kindle Paperwhite 4 (10th gen)",
                Resolution = (1072, 1448) } },
            { "pw5", new DeviceInfo {
                Name = "Kindle Paperwhite 5 (11th gen)",
                Resolution = (1236, 1648) } },
        };

        /// <exception cref="IOException"/>
        internal static int Main(string[] args)
        {
            var rootCommand = new RootCommand(GetExecutableName("cbz-to-kf8"));

            var inputParameter = rootCommand.AddParameter(
                name: "FILE",
                description: "A zip file containing images.");

            var outputOption = rootCommand.AddOption(
                shortName: 'o',
                longName: "output",
                parameterName: "FILE",
                description: "Write output to FILE.");
            var asinOption = rootCommand.AddOption(
                shortName: null,
                longName: "asin",
                parameterName: "TEXT",
                description: "Set the ASIN of the book.");
            var titleOption = rootCommand.AddOption(
                shortName: null,
                longName: "title",
                parameterName: "TEXT",
                description: "Set the title of the book.  Default is output filename.");
            var authorOption = rootCommand.AddOption(
                shortName: null,
                longName: "author",
                parameterName: "TEXT",
                description: "Set the author of the book.");
            var directionOption = rootCommand.AddOption<Direction>(
                shortName: null,
                longName: "direction",
                parameterName: "rtl|ltr",
                description: "Sets the reading direction to RTL (right-to-left) or LTR (left-to-right).  Default is RTL.",
                parse: TryParseReadingDirection);
            var noCoverOption = rootCommand.AddOption(
                shortName: null,
                longName: "no-cover",
                description: "Process the first image as a page rather than as the book cover.");
            var firstPageOption = rootCommand.AddOption<PageSide>(
                shortName: null,
                longName: "first-page",
                parameterName: "left|right",
                description: @"Designate the first page as a left or right page.  Default is left for RTL and right for LTR.",
                parse: TryParsePageSide);
            var deviceOption = rootCommand.AddOption<DeviceInfo>(
                shortName: null,
                longName: "device",
                parameterName: "ID",
                description: "Set default resolution setting by device." + GetDeviceList(),
                parse: TryParseDeviceId);
            var resolutionOption = rootCommand.AddOption<(int Width, int Height)>(
                shortName: null,
                longName: "resolution",
                parameterName: "WxH",
                description: "Resize images to fit inside the dimensions.",
                parse: TryParseDimensions);

            rootCommand.HelpOption = rootCommand.AddOption(
                shortName: 'h',
                longName: "help",
                description: "Shows a usage summary.");
            rootCommand.VerboseOption = rootCommand.AddOption(
                shortName: 'v',
                longName: "verbose",
                description: "Enables additional output.");

            rootCommand.SetInvokeHandler(() =>
            {
                string inPath = inputParameter.Value;
                string? outPath = outputOption.ValueOrDefault;
                string? asin = asinOption.ValueOrDefault;
                string? title = titleOption.ValueOrDefault;
                string? author = authorOption.ValueOrDefault;
                var direction = directionOption.GetValueOrDefault(Direction.Rtl);
                bool hasCover = noCoverOption.Count == 0;
                var firstPageSide = firstPageOption.GetValueOrDefault(PageSide.Unspecified);

                (int Width, int Height)? resolution = null;
                if (deviceOption.GetValueOrNull() is DeviceInfo deviceInfo)
                    resolution = deviceInfo.Resolution;
                if (resolutionOption.HasValue)
                    resolution = resolutionOption.Value;

                if (outPath == null)
                {
                    try
                    {
                        outPath = Path.ChangeExtension(inPath, ".azw3");
                    }
                    catch (ArgumentException)
                    {
                        outPath = inPath + ".azw3";
                    }
                }
                if (title == null)
                {
                    try
                    {
                        title = Path.GetFileNameWithoutExtension(outPath);
                    }
                    catch (ArgumentException)
                    {
                        title = "Untitled";
                    }
                }

                return Convert(inPath, outPath, asin, title, author, direction, hasCover, firstPageSide, resolution);
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

        private static string GetDeviceList()
        {
            var builder = new StringBuilder();
            foreach (var (key, deviceInfo) in _deviceInfos)
                _ = builder.AppendLine().Append("  ").Append(key).Append(": ").Append(deviceInfo.Name);
            return builder.ToString();
        }

        private static bool TryParseReadingDirection(string text, out Direction value)
        {
            switch (text)
            {
                case "ltr":
                    value = Direction.Ltr;
                    return true;
                case "rtl":
                    value = Direction.Rtl;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryParsePageSide(string text, out PageSide value)
        {
            switch (text)
            {
                case "left":
                    value = PageSide.Left;
                    return true;
                case "right":
                    value = PageSide.Right;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryParseDeviceId(string text, out DeviceInfo value)
        {
            return _deviceInfos.TryGetValue(text, out value);
        }

        private static bool TryParseDimensions(string text, out (int X, int Y) value)
        {
            string[] dimensions = text.Split(['x'], 2);
            if (dimensions.Length < 2 ||
                !int.TryParse(dimensions[0], NumberStyles.None, CultureInfo.InvariantCulture, out int x) ||
                x == 0 ||
                !int.TryParse(dimensions[1], NumberStyles.None, CultureInfo.InvariantCulture, out int y) ||
                y == 0)
            {
                value = default;
                return false;
            }

            value = (x, y);
            return true;
        }

        /// <exception cref="Exception"/>
        private static int Convert(string inPath, string outPath, string? asin, string title, string? author, Direction direction, bool hasCover, PageSide firstPageSide, (int Width, int Height)? resolution)
        {
            using var inFileStream = new FileStream(inPath, FileMode.Open, FileAccess.Read);
            using var zipArchive = new ZipArchive(inFileStream, ZipArchiveMode.Read);

            Console.Error.WriteLine("Analyzing images...");

            var pageInfos = new List<ImageSourceInfo>();

            var zipEntries = new ZipArchiveEntry[zipArchive.Entries.Count];
            zipArchive.Entries.CopyTo(zipEntries, 0);
            Array.Sort(zipEntries, ComparePaths);

            foreach (var zipEntry in zipEntries)
            {
                // Skip directories.
                if (zipEntry.Name.Length == 0)
                    continue;

                using (var fileStream = zipEntry.Open())
                {
                    IImageInfo imageInfo;
                    try
                    {
                        imageInfo = Image.Identify(fileStream);
                    }
                    catch (InvalidImageContentException)
                    {
                        continue;
                    }
                    if (imageInfo == null)
                        continue;

                    if (imageInfo.Width > imageInfo.Height)
                    {
                        var rightPageInfo = new ImageSourceInfo(zipEntry, imageInfo, PageSide.Right);
                        var leftPageInfo = new ImageSourceInfo(zipEntry, imageInfo, PageSide.Left);

                        if (direction == Direction.Ltr)
                        {
                            pageInfos.Add(leftPageInfo);
                            pageInfos.Add(rightPageInfo);
                        }
                        else
                        {
                            pageInfos.Add(rightPageInfo);
                            pageInfos.Add(leftPageInfo);
                        }

                        if (resolution is (int width, int height))
                        {
                            rightPageInfo.FitInside(width, height);
                            leftPageInfo.FitInside(width, height);
                        }
                    }
                    else
                    {
                        var pageInfo = new ImageSourceInfo(zipEntry, imageInfo, PageSide.Unspecified);
                        pageInfos.Add(pageInfo);

                        if (resolution is (int width, int height))
                            pageInfo.FitInside(width, height);
                    }
                }
            }

            ImageSourceInfo? coverInfo = null;

            if (hasCover && pageInfos.Count > 0)
            {
                coverInfo = pageInfos[0];
                pageInfos.RemoveAt(0);
            }

            if (pageInfos.Count == 0)
                throw new InvalidDataException("No pages found.");

            // Assign page sides.

            var pageSide = firstPageSide != PageSide.Unspecified
                ? firstPageSide
                : direction == Direction.Ltr
                    ? PageSide.Right
                    : PageSide.Left;

            foreach (var pageInfo in pageInfos)
            {
                if (pageInfo.ImageSplit != PageSide.Unspecified)
                    pageSide = pageInfo.ImageSplit;

                pageInfo.PageSide = pageSide;

                if (pageSide == PageSide.Left)
                    pageSide = PageSide.Right;
                else if (pageSide == PageSide.Right)
                    pageSide = PageSide.Left;
                else
                    throw new UnreachableException();
            }

            // Determine "original" resolution.

            int originalWidth;
            int originalHeight;

            {
                int[] temp = new int[pageInfos.Count];

                for (int i = 0; i < temp.Length; ++i)
                    temp[i] = pageInfos[i].PageWidth;
                Array.Sort(temp);
                originalWidth = temp[temp.Length / 2];

                for (int i = 0; i < temp.Length; ++i)
                    temp[i] = pageInfos[i].PageHeight;
                Array.Sort(temp);
                originalHeight = temp[temp.Length / 2];
            }

            // Build text.

            var records = new List<Record>();

            var mobi8Builder = new Mobi8Builder();

            uint mobi8RecordIndex = (uint)records.Count;
            records.Add(new Record());

            uint textLength;
            ushort textRecordsCount = 0;

            var flowEntries = new List<FlowEntry>();

            var skeletonEntries = new List<SkeletonEntry>();

            const ushort chunkTextLength = 4096;

            using (var text = new MemoryStream())
            {
                using (var writer = new StreamWriter(text, _encoding, 4096, leaveOpen: true))
                {
                    int skeletonsStartByteIndex = (int)text.Position;

                    for (int i = 0; i < pageInfos.Count; ++i)
                    {
                        uint aidBase = (uint)i * 1000000;

                        string skeletonTopText =
                            $@"<?xml version=""1.0""?><!DOCTYPE html><html xmlns=""http://www.w3.org/1999/xhtml"" xmlns:epub=""http://www.idpf.org/2007/ops"">" +
                            $@"<head>" +
                            $@"<title>{HttpUtility.HtmlEncode(title)}</title>" +
                            $@"<meta charset=""utf-8""/>" +
                            $@"</head>" +
                            $@"<body aid=""{Base32.Encode(aidBase)}"" style=""background-color:black;"">";

                        string skeletonBottomText =
                            $@"</body>" +
                            $@"</html>";

                        int skeletonBytesIndex = (int)text.Position;

                        writer.Write(skeletonTopText);
                        writer.Flush();

                        int fragmentOriginalByteIndex = (int)text.Position;

                        writer.Write(skeletonBottomText);
                        writer.Flush();

                        int skeletonBytesLength = (int)text.Position - skeletonBytesIndex;

                        var fragmentEntries = new List<FragmentEntry>();

                        int fragmentsByteIndex = (int)text.Position;

                        string fragmentText =
                            $@"<div aid=""{Base32.Encode(aidBase + 1)}"" style=""text-align:center;"">" +
                            $@"<img style=""margin:0;"" src=""{MobiUrl.GetEmbedUrl((ushort)i)}?mime=image/jpg"" width=""{pageInfos[i].PageWidth}"" height=""{pageInfos[i].PageHeight}"" />" +
                            $@"</div>";

                        int fragmentByteIndex = (int)text.Position - fragmentsByteIndex;

                        writer.Write(fragmentText);
                        writer.Flush();

                        int fragmentByteLength = (int)text.Position - fragmentsByteIndex - fragmentByteIndex;

                        fragmentEntries.Add(new FragmentEntry()
                        {
                            OriginalTextIndex = (uint)fragmentOriginalByteIndex,
                            ParentPath = $"P-//*[@aid='{Base32.Encode(aidBase)}']",
                            TextIndex = (uint)fragmentByteIndex,
                            TextLength = (uint)fragmentByteLength,
                        });

                        skeletonEntries.Add(new SkeletonEntry
                        {
                            Fragments = fragmentEntries,
                            TextIndex = (uint)skeletonBytesIndex,
                            TextLength = (uint)skeletonBytesLength,
                        });

                        fragmentOriginalByteIndex += fragmentByteLength;
                    }

                    int skeletonsEndByteIndex = (int)text.Position;

                    flowEntries.Add(new FlowEntry()
                    {
                        StartIndex = (uint)skeletonsStartByteIndex,
                        EndIndex = (uint)skeletonsEndByteIndex,
                    });
                }

                textLength = (uint)text.Length;

                uint textRecordsLength = 0;

                byte[] textBuffer = text.GetBuffer();

                byte[] compressedText = new byte[PalmDocCompressor.GetMaxCompressedSize(chunkTextLength)];

#if DEBUG
                byte[] decompressedText = new byte[chunkTextLength];
#endif

                for (uint offset = 0; offset < textLength; offset += chunkTextLength)
                {
                    ushort length = (ushort)Math.Min(textLength - offset, chunkTextLength);

                    var stream = new MemoryStream();

                    var chunkRecord = new Record(stream);
                    records.Add(chunkRecord);

                    int compressedTextLength = PalmDocCompressor.Compress(textBuffer.AsSpan((int)offset, length), compressedText);

#if DEBUG
                    int decompressedLength = PalmDocDecompressor.Decompress(compressedText.AsSpan(0, compressedTextLength), decompressedText);
                    Debug.Assert(textBuffer.AsSpan((int)offset, length).SequenceEqual(decompressedText.AsSpan(0, decompressedLength)));
#endif

                    stream.Write(compressedText, 0, compressedTextLength);

                    // Write multibyte trailer.
                    byte multibyteCount = 0;
                    for (; multibyteCount < 15; ++multibyteCount)
                    {
                        long index = offset + length + multibyteCount;
                        if (index > textBuffer.Length)
                            break;
                        byte b = textBuffer[index];
                        if ((b & 0xC0) != 0x80)
                            break;
                        stream.WriteByte(b);
                    }
                    stream.WriteByte(multibyteCount);

                    textRecordsLength += (uint)stream.Length;

                    textRecordsCount += 1;
                }

                uint padRecordLength = (~textRecordsLength + 1) % 4;

                if (padRecordLength != 0)
                {
                    var stream = new MemoryStream(new byte[padRecordLength]);

                    records.Add(new Record(stream));
                }
            }

            mobi8Builder.TextLength = textLength;
            mobi8Builder.TextRecordsCount = textRecordsCount;
            mobi8Builder.TextRecordTextLength = chunkTextLength;
            mobi8Builder.CompressionMethod = MobiCompressionMethod.PalmDoc;

            mobi8Builder.IndexRecordsIndex0028 = (uint)records.Count;
            mobi8Builder.IndexRecordsIndex0050 = (uint)records.Count;

            // Build fragments INDX.

            var fragmentTagxEntries = new TagxBlock.Entry[]
            {
                new() { TagId = 2, ValuesPerElement = 1, ValuesDescriptorByteMask = 0x01, EndOfValuesDescriptorByte = 0 },
                new() { TagId = 3, ValuesPerElement = 1, ValuesDescriptorByteMask = 0x02, EndOfValuesDescriptorByte = 0 },
                new() { TagId = 4, ValuesPerElement = 1, ValuesDescriptorByteMask = 0x04, EndOfValuesDescriptorByte = 0 },
                new() { TagId = 6, ValuesPerElement = 2, ValuesDescriptorByteMask = 0x08, EndOfValuesDescriptorByte = 0 },
                new() { TagId = 0, ValuesPerElement = 0, ValuesDescriptorByteMask = 0x00, EndOfValuesDescriptorByte = 1 },
            };

            var fragmentIndxBuilder = new IndxBuilder(fragmentTagxEntries);

            int fragmentIndex = 0;
            for (int skeletonIndex = 0; skeletonIndex < skeletonEntries.Count; ++skeletonIndex)
            {
                var skeletonEntry = skeletonEntries[skeletonIndex];

                foreach (var fragmentEntry in skeletonEntry.Fragments)
                {
                    fragmentIndxBuilder.AddEntry(fragmentEntry.OriginalTextIndex.ToStringInvariant("D10"), new IndxBuilder.Tag[]
                    {
                        new(2, [fragmentIndxBuilder.AddString(fragmentEntry.ParentPath)]),
                        new(3, [(ulong)skeletonIndex]),
                        new(4, [(ulong)fragmentIndex]),
                        new(6, [fragmentEntry.TextIndex, fragmentEntry.TextLength]),
                    });

                    fragmentIndex += 1;
                }
            }

            mobi8Builder.FragmentRecordsIndex = (uint)records.Count;

            CreateIndxRecords(records, fragmentIndxBuilder);

            // Build skeletons INDX.

            var skeletonTagxEntries = new TagxBlock.Entry[]
            {
                new() { TagId = 1, ValuesPerElement = 1, ValuesDescriptorByteMask = 0x03, EndOfValuesDescriptorByte = 0 },
                new() { TagId = 6, ValuesPerElement = 2, ValuesDescriptorByteMask = 0x0C, EndOfValuesDescriptorByte = 0 },
                new() { TagId = 0, ValuesPerElement = 0, ValuesDescriptorByteMask = 0x00, EndOfValuesDescriptorByte = 1 },
            };

            var skeletonIndxBuilder = new IndxBuilder(skeletonTagxEntries);

            for (int skeletonIndex = 0; skeletonIndex < skeletonEntries.Count; ++skeletonIndex)
            {
                var skeletonEntry = skeletonEntries[skeletonIndex];

                skeletonIndxBuilder.AddEntry("SKEL" + skeletonIndex.ToStringInvariant("D10"), new IndxBuilder.Tag[]
                {
                    new(1, [(ulong)skeletonEntry.Fragments.Count, (ulong)skeletonEntry.Fragments.Count]),
                    new(6, [skeletonEntry.TextIndex, skeletonEntry.TextLength, skeletonEntry.TextIndex, skeletonEntry.TextLength]),
                });
            }

            mobi8Builder.SkeletonRecordsIndex = (uint)records.Count;

            CreateIndxRecords(records, skeletonIndxBuilder);

            // Add embedded page images.

            mobi8Builder.EmbeddedRecordsIndex = (uint)records.Count;

            for (int i = 0; i < pageInfos.Count; ++i)
            {
                var pageInfo = pageInfos[i];

                string pageSideName = pageInfo.PageSide switch
                {
                    PageSide.Left => "left",
                    PageSide.Right => "right",
                    _ => throw new UnreachableException(),
                };

                pageInfo.Name = $"page {i + 1} ({pageSideName})";

                records.Add(new Record(pageInfo));
            }

            // Add embedded cover image.

            uint? coverImageIndex = null;

            if (coverInfo != null)
            {
                coverInfo.Name = "cover";

                coverImageIndex = (uint)records.Count - mobi8Builder.EmbeddedRecordsIndex;

                records.Add(new Record(coverInfo));
            }

            // Build metadata resource.

            var metadataXmlBuilder = new StringBuilder();
            _ = metadataXmlBuilder.Append(Invariant($@"<package version=""2.0"" xmlns=""http://www.idpf.org/2007/opf"" unique-identifier=""{Guid.NewGuid():B}"">"));
            _ = metadataXmlBuilder.Append(@"</package>");
            _ = metadataXmlBuilder.Append(@"<metadata xmlns=""http://www.idpf.org/2007/opf"" xmlns:dc=""http://purl.org/dc/elements/1.1/"">");
            _ = metadataXmlBuilder.Append(@"</metadata>");
            _ = metadataXmlBuilder.Append(@"<spine xmlns=""http://www.idpf.org/2007/opf"">");
            for (int i = 0; i < pageInfos.Count; ++i)
            {
                var pageInfo = pageInfos[i];
                _ = metadataXmlBuilder.Append(Invariant($@"<itemref idref=""{i}"""));
                if (pageInfo.PageSide == PageSide.Left)
                    _ = metadataXmlBuilder.Append(@" properties=""page-spread-left""");
                else if (pageInfo.PageSide == PageSide.Right)
                    _ = metadataXmlBuilder.Append(@" properties=""page-spread-right""");
                _ = metadataXmlBuilder.Append(Invariant($@" skelid=""{i}"" />"));
            }
            _ = metadataXmlBuilder.Append(@"</spine>");

            var rescBuilder = new RescBuilder();
            rescBuilder.SetMetadata(metadataXmlBuilder.ToString());

            uint metadataRecordIndex = (uint)records.Count;
            string metadataUrl = MobiUrl.GetEmbedUrl((ushort)(metadataRecordIndex - mobi8Builder.EmbeddedRecordsIndex));

            records.Add(new Record(rescBuilder.Build()));

            // Add embedded thumb image.

            uint? thumbImageIndex = null;

            if (coverInfo != null)
            {
                thumbImageIndex = (uint)records.Count - mobi8Builder.EmbeddedRecordsIndex;

                var thumbInfo = new ImageSourceInfo(coverInfo.ZipEntry, coverInfo.ImageInfo, coverInfo.ImageSplit);
                thumbInfo.FitInside(160, 256);
                thumbInfo.Name = "thumbnail";

                records.Add(new Record(thumbInfo));
            }

            uint resourcesCount = (uint)records.Count - mobi8Builder.EmbeddedRecordsIndex;

            // Build FDST.

            mobi8Builder.FlowsRecordsIndex = (uint)records.Count;
            mobi8Builder.FlowsCount = (uint)flowEntries.Count;

            var fdstBuilder = new FdstBuilder(0x1000);

            foreach (var flowEntry in flowEntries)
                fdstBuilder.AddFlow(flowEntry.StartIndex, flowEntry.EndIndex);

            CreateFdstRecords(records, fdstBuilder);

            // TODO: Build Locations Map?

            // Add end-of-records.

            records.Add(new Record(new MemoryStream([0xE9, 0x8E, 0x0D, 0x0A])));

            // Build MOBI.

            mobi8Builder.SetFullName(title);

            if (author != null)
                mobi8Builder.AddHeader(ExthTag.Author, author);
            if (asin != null)
                mobi8Builder.AddHeader(ExthTag.Asin, asin);
            mobi8Builder.AddHeader(ExthTag.FixedLayout, "true");
            mobi8Builder.AddHeader(ExthTag.BookType, "comic");
            mobi8Builder.AddHeader(ExthTag.OrientationLock, "none");
            mobi8Builder.AddHeader(ExthTag.ResourcesCount, resourcesCount);
            mobi8Builder.AddHeader(ExthTag.OriginalResolution, $"{originalWidth}x{originalHeight}");
            mobi8Builder.AddHeader(ExthTag.MetadataResourceUrl, metadataUrl);
            if (hasCover)
            {
                mobi8Builder.AddHeader(ExthTag.CoverImageIndex, coverImageIndex!.Value);
                mobi8Builder.AddHeader(ExthTag.HasFakeCover, 0);
                mobi8Builder.AddHeader(ExthTag.ThumbImageIndex, thumbImageIndex!.Value);
            }
            mobi8Builder.AddHeader(ExthTag.PrimaryWritingMode, direction == Direction.Ltr ? "horizontal-lr" : "horizontal-rl");

            records[(int)mobi8RecordIndex] = new Record(mobi8Builder.Build());

            Console.WriteLine("Creating ebook...");

            using (var outStream = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read, 0x10000))
            using (var writer = new PdbWriter(outStream))
            {
                uint[] recordDataLengths = new uint[records.Count];

                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                uint nowTime = (uint)((DateTime.UtcNow - epoch).Ticks / TimeSpan.TicksPerSecond);

                writer.WriteHeader(
                    name: new byte[32],
                    attributes: PdbAttributes.None,
                    version: 0,
                    creationTime: nowTime,
                    modificationTime: nowTime,
                    backupTime: 0,
                    modificationNumber: 0,
                    appInfoLength: null,
                    sortInfoLength: null,
                    typeId: _bookMagicValue,
                    creatorId: _mobiMagicValue,
                    uniqueIdSeed: (uint)records.Count * 2 + 1,
                    recordCount: (ushort)records.Count);

                int imageProcessingThreadsCount = Environment.ProcessorCount;

                using (var imageProcessingThrottle = new Semaphore(0, imageProcessingThreadsCount))
                {
                    int imageProcessingRecordIndex = 0;

                    for (int i = 0; i < imageProcessingThreadsCount; ++i)
                    {
                        var thread = new Thread(ProcessImages);
                        thread.Name = $"Image Processor {i}";
                        thread.IsBackground = true;
                        thread.Start();

                        void ProcessImages()
                        {
                            while (true)
                            {
                                // Ensure that image processing doesn't get too far ahead of file
                                // writing.
                                _ = imageProcessingThrottle.WaitOne();

                                int recordIndex;
                                ImageSourceInfo info;

                                // Take the next job.
                                lock (records)
                                {
                                    while (true)
                                    {
                                        if (imageProcessingRecordIndex == records.Count)
                                            return;

                                        recordIndex = imageProcessingRecordIndex;

                                        imageProcessingRecordIndex += 1;

                                        if (records[recordIndex].ImageSourceInfo is ImageSourceInfo temp)
                                        {
                                            info = temp;
                                            break;
                                        }
                                    }

                                    Console.Error.WriteLine($@"Processing ""{info.ZipEntry.FullName}"" into {info.Name}...");
                                }

                                var stream = new MemoryStream();

                                Image image;

                                lock (info.ZipEntry.Archive)
                                    using (var sourceStream = info.ZipEntry.Open())
                                        image = Image.Load(sourceStream);

                                using (image)
                                {
                                    image.Mutate(context =>
                                    {
                                        _ = context.Grayscale();
                                        if (info.ImageArea != image.Bounds())
                                            _ = context.Crop(info.ImageArea);
                                        if (info.PageWidth != image.Width || info.PageHeight != image.Height)
                                            _ = context.Resize(info.PageWidth, info.PageHeight, KnownResamplers.Lanczos3);
                                    });

                                    image.Metadata.ExifProfile = null;

                                    var encoder = new JpegEncoder()
                                    {
                                        ColorType = JpegColorType.Luminance,
                                        Quality = 85,
                                    };

                                    image.Save(stream, encoder);
                                }

                                stream.ZeroPad4();

                                // Publish the job result.
                                lock (records)
                                {
                                    records[recordIndex] = new Record(stream, info);

                                    Monitor.Pulse(records);
                                }
                            }
                        }
                    }

                    _ = imageProcessingThrottle.Release(imageProcessingThreadsCount);

                    for (int i = 0; i < records.Count; ++i)
                    {
                        MemoryStream stream;

                        lock (records)
                        {
                            while (true)
                            {
                                if (records[i].Stream is MemoryStream temp)
                                {
                                    stream = temp;
                                    break;
                                }

                                _ = Monitor.Wait(records);
                            }

                            // If the record data came from an image processing job, release another
                            // job, 
                            if (records[i].ImageSourceInfo != null)
                                _ = imageProcessingThrottle.Release();
                        }

                        using (stream)
                        {
                            stream.Position = 0;

                            recordDataLengths[i] = writer.WriteRecordData(stream);
                        }
                    }
                }

                for (int i = 0; i < records.Count; ++i)
                    writer.WriteRecordEntry(
                        dataLength: recordDataLengths[i],
                        attributes: PdbRecordAttributes.None,
                        category: 0,
                        uniqueId: (uint)i * 2);
            }

            return 0;

            void CreateFdstRecords(List<Record> records, FdstBuilder fdstBuilder)
            {
                var fdstRecordDatas = fdstBuilder.Build();

                for (int i = 0; i < fdstRecordDatas.Length; ++i)
                    records.Add(new Record(fdstRecordDatas[i]));
            }

            void CreateIndxRecords(List<Record> records, IndxBuilder indxBuilder)
            {
                var metaentryRecordData = indxBuilder.Build(out var entryRecordDatas, out var stringRecordDatas);

                records.Add(new Record(metaentryRecordData));

                for (int i = 0; i < entryRecordDatas.Length; ++i)
                    records.Add(new Record(entryRecordDatas[i]));

                for (int i = 0; i < stringRecordDatas.Length; ++i)
                    records.Add(new Record(stringRecordDatas[i]));
            }
        }

        private static int ComparePaths(ZipArchiveEntry entry1, ZipArchiveEntry entry2)
        {
            string path1 = entry1.FullName;
            string path2 = entry2.FullName;

            int offset1 = 0;
            int offset2 = 0;

            for (; ; )
            {
                bool ended1 = offset1 >= path1.Length;
                bool ended2 = offset2 >= path2.Length;
                if (ended1)
                    return ended2 ? 0 : -1;
                else if (ended2)
                    return 1;

                string segment1 = GetSegment(path1, offset1);
                string segment2 = GetSegment(path2, offset2);

                int result = string.Compare(segment1, segment2, StringComparison.InvariantCultureIgnoreCase);
                if (result != 0)
                    return result;

                offset1 += segment1.Length + 1;
                offset2 += segment2.Length + 1;
            }

            static string GetSegment(string path, int offset)
            {
                int endOffset = path.IndexOfAny(_pathSeparators, offset);
                return endOffset < 0
                    ? path.Substring(offset)
                    : path.Substring(offset, endOffset - offset);
            }
        }

        private readonly struct DeviceInfo
        {
            public string Name { get; init; }
            public (int X, int Y) Resolution { get; init; }
        }

        private enum Direction
        {
            Ltr,
            Rtl,
        }

        private enum PageSide
        {
            Unspecified,
            Left,
            Right,
        }

        private sealed class ImageSourceInfo
        {
            public readonly ZipArchiveEntry ZipEntry;
            public readonly IImageInfo ImageInfo;
            public readonly PageSide ImageSplit;

            public readonly Rectangle ImageArea;

            public ImageSourceInfo(ZipArchiveEntry entry, IImageInfo imageInfo, PageSide split)
            {
                ZipEntry = entry;
                ImageInfo = imageInfo;
                ImageSplit = split;

                ImageArea = GetCropArea(imageInfo.Width, imageInfo.Height, split);

                PageWidth = ImageArea.Width;
                PageHeight = ImageArea.Height;
            }

            public int PageWidth { get; private set; }
            public int PageHeight { get; private set; }

            public string Name { get; set; } = string.Empty;
            public PageSide PageSide { get; set; }

            public void FitInside(int maxWidth, int maxHeight)
            {
                int width = ImageArea.Width;
                int height = ImageArea.Height;

                double widthScale = (double)maxWidth / width;
                double heightScale = (double)maxHeight / height;

                if (widthScale < heightScale)
                {
                    PageWidth = maxWidth;
                    PageHeight = Math.Min((int)Math.Round(height * widthScale), maxHeight);
                }
                else
                {
                    PageWidth = Math.Min((int)Math.Round(width * heightScale), maxWidth);
                    PageHeight = maxHeight;
                }
            }

            private static Rectangle GetCropArea(int width, int height, PageSide split)
            {
                var area = new Rectangle(0, 0, width, height);

                switch (split)
                {
                    case PageSide.Left:
                    {
                        int halfWidth = area.Width / 2;
                        area.Width = halfWidth;
                        break;
                    }
                    case PageSide.Right:
                    {
                        int halfWidth = area.Width / 2;
                        area.X = area.Width - halfWidth;
                        area.Width = halfWidth;
                        break;
                    }
                }

                return area;
            }
        }

        private readonly struct Record
        {
            public Record(MemoryStream stream)
            {
                Stream = stream;
                ImageSourceInfo = null;
            }

            public Record(ImageSourceInfo imageSourceInfo)
            {
                Stream = null;
                ImageSourceInfo = imageSourceInfo;
            }

            public Record(MemoryStream stream, ImageSourceInfo imageSourceInfo)
            {
                Stream = stream;
                ImageSourceInfo = imageSourceInfo;
            }

            public MemoryStream? Stream { get; }

            public ImageSourceInfo? ImageSourceInfo { get; }
        }

        private struct FlowEntry
        {
            public uint StartIndex;
            public uint EndIndex;
        }

        private struct SkeletonEntry
        {
            public uint TextIndex;
            public uint TextLength;
            public List<FragmentEntry> Fragments;
        }

        private struct FragmentEntry
        {
            public uint OriginalTextIndex;
            public string ParentPath;
            public uint TextIndex;
            public uint TextLength;
        }
    }
}
