﻿using System;
using System.IO;

namespace PuyoTools.Modules.Archive
{
    public class AfsArchive : ArchiveBase
    {
        public override string Name
        {
            get { return "AFS"; }
        }

        public override string FileExtension
        {
            get { return ".afs"; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override ArchiveReader Open(Stream source)
        {
            return new Reader(source);
        }

        public override ArchiveWriter Create(Stream destination)
        {
            return new Writer(destination);
        }

        public override ModuleSettingsControl GetModuleSettingsControl()
        {
            return new AfsWriterSettings();
        }

        public override bool Is(Stream source, int length, string fname)
        {
            return (length > 8 && PTStream.Contains(source, 0, new byte[] { (byte)'A', (byte)'F', (byte)'S', 0 }));
        }

        public class Reader : ArchiveReader
        {
            public Reader(Stream source) : base(source)
            {
                // Get the number of entries in the archive
                source.Position += 4;
                int numEntries = PTStream.ReadInt32(source);
                entries = new ArchiveEntryCollection(this, numEntries);

                // Get the offset of the metadata
                source.Position += (numEntries * 8);
                int metadataOffset = PTStream.ReadInt32(source);

                // If the offset isn't stored there, then it is stored right before the offset of the first entry
                if (metadataOffset == 0)
                {
                    source.Position = archiveOffset + 8;
                    source.Position = PTStream.ReadInt32(source) - 8;
                    metadataOffset = PTStream.ReadInt32(source);
                }

                // Read in all the entries
                for (int i = 0; i < numEntries; i++)
                {
                    // Read in the entry offset and length
                    source.Position = archiveOffset + 8 + (i * 8);
                    int entryOffset = PTStream.ReadInt32(source);
                    int entryLength = PTStream.ReadInt32(source);

                    // Read in the entry file name
                    source.Position = metadataOffset + (i * 48);
                    string entryFname = PTStream.ReadCString(source, 32);

                    // Add this entry to the collection
                    entries.Add(archiveOffset + entryOffset, entryLength, entryFname);
                }

                // Set the position of the stream to the end of the file
                source.Seek(0, SeekOrigin.End);
            }
        }

        public class Writer : ArchiveWriter
        {
            #region Settings
            /// <summary>
            /// The block size for this archive. The default value is 2048.
            /// </summary>
            public int BlockSize
            {
                get { return blockSize; }
                set
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException("BlockSize");
                    }

                    blockSize = value;
                }
            }
            private int blockSize;

            /// <summary>
            /// The version of this archive to use. Use Version 1 for Dreamcast games and Version 2 for PS2, GCN, Xbox, and beyond.
            /// The default value is WriterSettings.AfsVersion.Version1.
            /// </summary>
            public AfsVersion Version
            {
                get { return version; }
                set
                {
                    if (value != AfsVersion.Version1 && value != AfsVersion.Version2)
                    {
                        throw new ArgumentOutOfRangeException("Version");
                    }

                    version = value;
                }
            }
            private AfsVersion version;

            public enum AfsVersion
            {
                Version1, // Dreamcast
                Version2, // Post Dreamcast (PS2, GC, Xbox and after)
            }

            /// <summary>
            /// Sets if each file should include a timestamp. The default value is true.
            /// </summary>
            public bool HasTimestamps { get; set; }
            #endregion

            public Writer(Stream destination) : base(destination)
            {
                // Set default settings
                blockSize = 2048;
                version = AfsVersion.Version1;
                HasTimestamps = true;
            }

            public override void Flush()
            {
                // The start of the archive
                long offset = destination.Position;

                // Magic code "AFS\0"
                destination.WriteByte((byte)'A');
                destination.WriteByte((byte)'F');
                destination.WriteByte((byte)'S');
                destination.WriteByte(0);

                // Number of entries in the archive
                PTStream.WriteInt32(destination, entries.Count);

                // Write out the header for the archive
                int entryOffset = PTMethods.RoundUp(12 + (entries.Count * 8), blockSize);
                int firstEntryOffset = entryOffset;

                for (int i = 0; i < entries.Count; i++)
                {
                    PTStream.WriteInt32(destination, entryOffset);
                    PTStream.WriteInt32(destination, entries[i].Length);

                    entryOffset += PTMethods.RoundUp(entries[i].Length, blockSize);
                }

                // If this is AFS v1, then the metadata offset is stored at 8 bytes before
                // the first entry offset.
                if (version == AfsVersion.Version1)
                {
                    destination.Position = offset + firstEntryOffset - 8;
                }

                // Write out the metadata offset and length
                PTStream.WriteInt32(destination, entryOffset);
                PTStream.WriteInt32(destination, entries.Count * 48);

                destination.Position = offset + firstEntryOffset;

                // Write out the file data for each entry
                for (int i = 0; i < entries.Count; i++)
                {
                    PTStream.CopyToPadded(entries[i].Open(), destination, blockSize, 0);

                    // Call the file added event
                    OnFileAdded(EventArgs.Empty);
                }

                // Write out the footer for the archive
                for (int i = 0; i < entries.Count; i++)
                {
                    PTStream.WriteCString(destination, entries[i].Name, 32);

                    // File timestamp
                    if (HasTimestamps && !String.IsNullOrEmpty(entries[i].Path) && File.Exists(entries[i].Path))
                    {
                        // File exists, let's read in the file timestamp
                        FileInfo fileInfo = new FileInfo(entries[i].Path);

                        PTStream.WriteInt16(destination, (short)fileInfo.LastWriteTime.Year);
                        PTStream.WriteInt16(destination, (short)fileInfo.LastWriteTime.Month);
                        PTStream.WriteInt16(destination, (short)fileInfo.LastWriteTime.Day);
                        PTStream.WriteInt16(destination, (short)fileInfo.LastWriteTime.Hour);
                        PTStream.WriteInt16(destination, (short)fileInfo.LastWriteTime.Minute);
                        PTStream.WriteInt16(destination, (short)fileInfo.LastWriteTime.Second);
                    }
                    else
                    {
                        // File does not exist, just store all 0s
                        PTStream.WriteInt16(destination, 0);
                        PTStream.WriteInt16(destination, 0);
                        PTStream.WriteInt16(destination, 0);
                        PTStream.WriteInt16(destination, 0);
                        PTStream.WriteInt16(destination, 0);
                        PTStream.WriteInt16(destination, 0);
                    }

                    // Write out this data that I have no idea what its purpose is
                    long oldPosition = destination.Position;
                    byte[] buffer = new byte[4];

                    if (version == AfsVersion.Version1)
                        destination.Position = offset + 8 + (i * 8);
                    else
                        destination.Position = offset + 4 + (i * 4);

                    destination.Read(buffer, 0, 4);
                    destination.Position = oldPosition;
                    destination.Write(buffer, 0, 4);
                }

                // Finish padding out the archive
                while ((destination.Position - offset) % blockSize != 0)
                    destination.WriteByte(0);
            }
        }
    }
}