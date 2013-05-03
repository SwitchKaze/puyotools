﻿using System;
using System.IO;
using System.Drawing;

namespace VrSharp.GvrTexture
{
    public class GvrTexture : VrTexture
    {
        #region Texture Properties
        /// <summary>
        /// The texture's pixel format. This only applies to palettized textures.
        /// </summary>
        public GvrPixelFormat PixelFormat { get; private set; }

        /// <summary>
        /// The texture's data flags. Can contain one or more of the following:
        /// <para>- GvrDataFlags.Mipmaps</para>
        /// <para>- GvrDataFlags.ExternalClut</para>
        /// <para>- GvrDataFlags.InternalClut</para>
        /// </summary>
        public GvrDataFlags DataFlags { get; private set; }

        /// <summary>
        /// The texture's data format.
        /// </summary>
        public GvrDataFormat DataFormat { get; private set; }
        #endregion

        #region Constructors & Initalizers
        /// <summary>
        /// Open a GVR texture from a file.
        /// </summary>
        /// <param name="file">Filename of the file that contains the texture data.</param>
        public GvrTexture(string file) : base(file) { }

        /// <summary>
        /// Open a GVR texture from a byte array.
        /// </summary>
        /// <param name="source">Byte array that contains the texture data.</param>
        public GvrTexture(byte[] source) : base(source) { }

        /// <summary>
        /// Open a GVR texture from a byte array.
        /// </summary>
        /// <param name="source">Byte array that contains the texture data.</param>
        /// <param name="offset">Offset of the texture in the array.</param>
        /// <param name="length">Number of bytes to read.</param>
        public GvrTexture(byte[] source, long offset, int length) : base(source, (int)offset, length) { }

        /// <summary>
        /// Open a GVR texture from a stream.
        /// </summary>
        /// <param name="source">Stream that contains the texture data.</param>
        public GvrTexture(Stream source) : base(source) { }

        /// <summary>
        /// Open a GVR texture from a stream.
        /// </summary>
        /// <param name="source">Stream that contains the texture data.</param>
        /// <param name="length">Number of bytes to read.</param>
        public GvrTexture(Stream source, int length) : base(source, length) { }

        protected override bool Initalize()
        {
            // Check to see if what we are dealing with is a GVR texture
            if (!Is(TextureData))
                return false;

            // Determine the offsets of the GBIX/GCIX (if present) and GCIX header chunks.
            if (Compare(TextureData, "GBIX", 0x00) ||
                Compare(TextureData, "GCIX", 0x00))
            {
                GbixOffset = 0x00;
                PvrtOffset = 0x10;
            }
            else
            {
                GbixOffset = -1;
                PvrtOffset = 0x00;
            }

            // Read the global index (if it is present). If it is not present, just set it to 0.
            if (GbixOffset != -1)
            {
                GlobalIndex = (uint)(TextureData[GbixOffset + 0x08] << 24 | TextureData[GbixOffset + 0x09] << 16 | TextureData[GbixOffset + 0x0A] << 8 | TextureData[GbixOffset + 0x0B]);
            }
            else
            {
                GlobalIndex = 0;
            }

            // Read information about the texture
            TextureWidth  = (ushort)((TextureData[PvrtOffset + 0x0C] << 8) | TextureData[PvrtOffset + 0x0D]);
            TextureHeight = (ushort)((TextureData[PvrtOffset + 0x0E] << 8) | TextureData[PvrtOffset + 0x0F]);

            PixelFormat = (GvrPixelFormat)(TextureData[PvrtOffset + 0x0A] >> 4); // Only the first 4 bits matter
            DataFlags   = (GvrDataFlags)(TextureData[PvrtOffset + 0x0A] & 0x0F); // Only the last 4 bits matter
            DataFormat  = (GvrDataFormat)TextureData[PvrtOffset + 0x0B];

            // Get the codecs and make sure we can decode using them
            PixelCodec = GvrCodecList.GetPixelCodec(PixelFormat);
            if ((DataFlags & GvrDataFlags.Clut) != 0 && (PixelCodec == null || !PixelCodec.CanDecode())) return false;

            DataCodec  = GvrCodecList.GetDataCodec(DataFormat);
            if (DataCodec == null || !DataCodec.CanDecode()) return false;

            // Set the clut and data offsets
            if ((DataFlags & GvrDataFlags.InternalClut) == 0 || DataCodec.GetNumClutEntries() == 0 || NeedsExternalClut())
            {
                ClutOffset = -1;
                DataOffset = PvrtOffset + 0x10;
            }
            else
            {
                ClutOffset = PvrtOffset + 0x10;
                DataOffset = ClutOffset + (DataCodec.GetNumClutEntries() * (PixelCodec.GetBpp() / 8));
            }

            RawImageData = new byte[TextureWidth * TextureHeight * 4];
            return true;
        }
        #endregion

        #region Clut
        /// <summary>
        /// Set the clut data from an external clut file.
        /// </summary>
        /// <param name="clut">A GvpClut object</param>
        public override void SetClut(VpClut clut)
        {
            if (!(clut is GvpClut)) // Make sure this is a GvpClut object
            {
                throw new ArgumentException(String.Format(
                    "VpClut type is {0} when it needs to be GvpClut.",
                    clut.GetType()));
            }

            base.SetClut(clut);
        }

        /// <summary>
        /// Returns if the texture needs an external clut file.
        /// </summary>
        /// <returns></returns>
        public override bool NeedsExternalClut()
        {
            if (!InitSuccess) return false;

            return ((DataFlags & GvrDataFlags.ExternalClut) != 0);
        }
        #endregion

        #region Texture Check
        /// <summary>
        /// Determines if this is a GVR texture.
        /// </summary>
        /// <param name="source">Byte array containing the data.</param>
        /// <param name="offset">The offset in the byte array to start at.</param>
        /// <param name="length">Length of the data (in bytes).</param>
        /// <returns>True if this is a GVR texture, false otherwise.</returns>
        public static bool Is(byte[] source, int offset, int length)
        {
            // GBIX and GVRT
            if (length >= 32 &&
                Compare(source, "GBIX", offset + 0x00) &&
                Compare(source, "GVRT", offset + 0x10) &&
                BitConverter.ToUInt32(source, offset + 0x14) == length - 24)
                return true;

            // GCIX and GVRT
            else if (length >= 32 &&
                Compare(source, "GCIX", offset + 0x00) &&
                Compare(source, "GVRT", offset + 0x10) &&
                BitConverter.ToUInt32(source, offset + 0x14) == length - 24)
                return true;

            // GVRT (and no GBIX or GCIX chunk)
            else if (length > 16 &&
                Compare(source, "GVRT", offset + 0x00) &&
                BitConverter.ToUInt32(source, offset + 0x04) == length - 8)
                return true;

            return false;
        }

        /// <summary>
        /// Determines if this is a GVR texture.
        /// </summary>
        /// <param name="source">Byte array containing the data.</param>
        /// <returns>True if this is a GVR texture, false otherwise.</returns>
        public static bool Is(byte[] source)
        {
            return Is(source, 0, source.Length);
        }

        /// <summary>
        /// Determines if this is a GVR texture.
        /// </summary>
        /// <param name="source">The stream to read from. The stream position is not changed.</param>
        /// <param name="length">Number of bytes to read.</param>
        /// <returns>True if this is a GVR texture, false otherwise.</returns>
        public static bool Is(Stream source, int length)
        {
            // If the length is < 16, then there is no way this is a valid texture.
            if (length < 16)
            {
                return false;
            }

            // Let's see if we should check 16 bytes or 32 bytes
            int amountToRead = 0;
            if (length < 32)
            {
                amountToRead = 16;
            }
            else
            {
                amountToRead = 32;
            }

            byte[] buffer = new byte[amountToRead];
            source.Read(buffer, 0, amountToRead);
            source.Position -= amountToRead;

            return Is(buffer, 0, length);
        }

        /// <summary>
        /// Determines if this is a GVR texture.
        /// </summary>
        /// <param name="source">The stream to read from. The stream position is not changed.</param>
        /// <returns>True if this is a GVR texture, false otherwise.</returns>
        public static bool Is(Stream source)
        {
            return Is(source, (int)(source.Length - source.Position));
        }
        #endregion
    }
}