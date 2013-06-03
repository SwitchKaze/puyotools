﻿using System;
using System.IO;
using System.Text;
using System.Drawing;

namespace VrSharp.SvrTexture
{
    public class SvrTextureEncoder : VrTextureEncoder
    {
        #region Texture Properties
        /// <summary>
        /// The texture's pixel format.
        /// </summary>
        public SvrPixelFormat PixelFormat
        {
            get
            {
                if (!initalized)
                {
                    throw new TextureNotInitalizedException("Cannot access this property as the texture is not initalized.");
                }

                return pixelFormat;
            }
        }
        private SvrPixelFormat pixelFormat;

        /// <summary>
        /// The texture's data format.
        /// </summary>
        public SvrDataFormat DataFormat
        {
            get
            {
                if (!initalized)
                {
                    throw new TextureNotInitalizedException("Cannot access this property as the texture is not initalized.");
                }

                return dataFormat;
            }
        }
        private SvrDataFormat dataFormat;
        #endregion

        #region Constructors & Initalizers
        /// <summary>
        /// Opens a texture to encode from a file.
        /// </summary>
        /// <param name="file">Filename of the file that contains the texture data.</param>
        /// <param name="pixelFormat">Pixel format to encode the texture to.</param>
        /// <param name="dataFormat">Data format to encode the texture to.</param>
        public SvrTextureEncoder(string file, SvrPixelFormat pixelFormat, SvrDataFormat dataFormat) : base(file)
        {
            if (decodedBitmap != null)
            {
                initalized = Initalize(pixelFormat, dataFormat);
            }
        }

        /// <summary>
        /// Opens a texture to encode from a byte array.
        /// </summary>
        /// <param name="source">Byte array that contains the texture data.</param>
        /// <param name="pixelFormat">Pixel format to encode the texture to.</param>
        /// <param name="dataFormat">Data format to encode the texture to.</param>
        public SvrTextureEncoder(byte[] source, SvrPixelFormat pixelFormat, SvrDataFormat dataFormat)
            : base(source)
        {
            if (decodedBitmap != null)
            {
                initalized = Initalize(pixelFormat, dataFormat);
            }
        }

        /// <summary>
        /// Opens a texture to encode from a byte array.
        /// </summary>
        /// <param name="source">Byte array that contains the texture data.</param>
        /// <param name="offset">Offset of the texture in the array.</param>
        /// <param name="length">Number of bytes to read.</param>
        /// <param name="pixelFormat">Pixel format to encode the texture to.</param>
        /// <param name="dataFormat">Data format to encode the texture to.</param>
        public SvrTextureEncoder(byte[] source, int offset, int length, SvrPixelFormat pixelFormat, SvrDataFormat dataFormat)
            : base(source, offset, length)
        {
            if (decodedBitmap != null)
            {
                initalized = Initalize(pixelFormat, dataFormat);
            }
        }

        /// <summary>
        /// Opens a texture to encode from a stream.
        /// </summary>
        /// <param name="source">Stream that contains the texture data.</param>
        /// <param name="pixelFormat">Pixel format to encode the texture to.</param>
        /// <param name="dataFormat">Data format to encode the texture to.</param>
        public SvrTextureEncoder(Stream source, SvrPixelFormat pixelFormat, SvrDataFormat dataFormat)
            : base(source)
        {
            if (decodedBitmap != null)
            {
                initalized = Initalize(pixelFormat, dataFormat);
            }
        }

        /// <summary>
        /// Opens a texture to encode from a stream.
        /// </summary>
        /// <param name="source">Stream that contains the texture data.</param>
        /// <param name="length">Number of bytes to read.</param>
        /// <param name="pixelFormat">Pixel format to encode the texture to.</param>
        /// <param name="dataFormat">Data format to encode the texture to.</param>
        public SvrTextureEncoder(Stream source, int length, SvrPixelFormat pixelFormat, SvrDataFormat dataFormat)
            : base(source, length)
        {
            if (decodedBitmap != null)
            {
                initalized = Initalize(pixelFormat, dataFormat);
            }
        }

        /// <summary>
        /// Opens a texture to encode from a bitmap.
        /// </summary>
        /// <param name="source">Bitmap to encode.</param>
        /// <param name="pixelFormat">Pixel format to encode the texture to.</param>
        /// <param name="dataFormat">Data format to encode the texture to.</param>
        public SvrTextureEncoder(Bitmap source, SvrPixelFormat pixelFormat, SvrDataFormat dataFormat)
            : base(source)
        {
            if (decodedBitmap != null)
            {
                initalized = Initalize(pixelFormat, dataFormat);
            }
        }

        private bool Initalize(SvrPixelFormat pixelFormat, SvrDataFormat dataFormat)
        {
            // Set the default values
            includeGbixHeader = true;
            globalIndex = 0;

            // Set the data format and pixel format and load the appropiate codecs
            this.pixelFormat = pixelFormat;
            pixelCodec = SvrPixelCodec.GetPixelCodec(pixelFormat);

            this.dataFormat = dataFormat;
            dataCodec = SvrDataCodec.GetDataCodec(dataFormat);

            // Make sure the pixel and data codecs exists and we can encode to it
            if (pixelCodec == null || !pixelCodec.CanEncode) return false;
            if (dataCodec == null || !dataCodec.CanEncode) return false;

            // Set the correct data format (it's ok to do it after getting the codecs).
            if (dataFormat == SvrDataFormat.Index4RectRgb5a3 || dataFormat == SvrDataFormat.Index4SqrRgb5a3 ||
                dataFormat == SvrDataFormat.Index4RectArgb8 || dataFormat == SvrDataFormat.Index4SqrArgb8)
            {
                if (textureWidth == textureHeight) // Square texture
                {
                    if (pixelFormat == SvrPixelFormat.Rgb5a3)
                    {
                        dataFormat = SvrDataFormat.Index4SqrRgb5a3;
                    }
                    else
                    {
                        dataFormat = SvrDataFormat.Index4SqrArgb8;
                    }
                }
                else // Rectangular texture
                {
                    if (pixelFormat == SvrPixelFormat.Rgb5a3)
                    {
                        dataFormat = SvrDataFormat.Index4RectRgb5a3;
                    }
                    else
                    {
                        dataFormat = SvrDataFormat.Index4RectArgb8;
                    }
                }
            }

            else if (dataFormat == SvrDataFormat.Index8RectRgb5a3 || dataFormat == SvrDataFormat.Index8SqrRgb5a3 ||
                dataFormat == SvrDataFormat.Index8RectArgb8 || dataFormat == SvrDataFormat.Index8SqrArgb8)
            {
                if (textureWidth == textureHeight) // Square texture
                {
                    if (pixelFormat == SvrPixelFormat.Rgb5a3)
                    {
                        dataFormat = SvrDataFormat.Index8SqrRgb5a3;
                    }
                    else
                    {
                        dataFormat = SvrDataFormat.Index8SqrArgb8;
                    }
                }
                else // Rectangular texture
                {
                    if (pixelFormat == SvrPixelFormat.Rgb5a3)
                    {
                        dataFormat = SvrDataFormat.Index8RectRgb5a3;
                    }
                    else
                    {
                        dataFormat = SvrDataFormat.Index8RectArgb8;
                    }
                }
            }

            if (dataCodec.PaletteEntries != 0)
            {
                // Convert the bitmap to an array containing indicies.
                decodedData = BitmapToRawIndexed(decodedBitmap, dataCodec.PaletteEntries, out texturePalette);
                
                // If this texture has an external palette file, set up the palette encoder
                if (dataCodec.NeedsExternalPalette)
                {
                    paletteEncoder = new SvpPaletteEncoder(texturePalette, (ushort)dataCodec.PaletteEntries, pixelFormat, pixelCodec);
                }
            }
            else
            {
                // Convert the bitmap to an array
                decodedData = BitmapToRaw(decodedBitmap);
            }

            return true;
        }
        #endregion

        #region Encode Texture
        protected override MemoryStream EncodeTexture()
        {
            // Calculate what the length of the texture will be
            int textureLength = 16 + (textureWidth * textureHeight * dataCodec.Bpp / 8);
            if (includeGbixHeader)
            {
                textureLength += 16;
            }
            if (dataCodec.PaletteEntries != 0 && !dataCodec.NeedsExternalPalette)
            {
                textureLength += (dataCodec.PaletteEntries * pixelCodec.Bpp / 8);
            }

            MemoryStream destination = new MemoryStream(textureLength);

            // Write out the GBIX header (if we are including one)
            if (includeGbixHeader)
            {
                destination.WriteByte((byte)'G');
                destination.WriteByte((byte)'B');
                destination.WriteByte((byte)'I');
                destination.WriteByte((byte)'X');

                PTStream.WriteUInt32(destination, 8);
                PTStream.WriteUInt32(destination, globalIndex);
                PTStream.WriteUInt32(destination, 0);
            }

            // Write out the PVRT header
            destination.WriteByte((byte)'P');
            destination.WriteByte((byte)'V');
            destination.WriteByte((byte)'R');
            destination.WriteByte((byte)'T');

            PTStream.WriteInt32(destination, textureLength - 24);

            destination.WriteByte((byte)pixelFormat);
            destination.WriteByte((byte)dataFormat);
            PTStream.WriteUInt16(destination, 0);

            PTStream.WriteUInt16(destination, textureWidth);
            PTStream.WriteUInt16(destination, textureHeight);

            // If we have an internal palette, write it
            if (dataCodec.PaletteEntries != 0 && !dataCodec.NeedsExternalPalette)
            {
                byte[] palette = pixelCodec.EncodePalette(texturePalette, dataCodec.PaletteEntries);
                destination.Write(palette, 0, palette.Length);
            }

            // Write the texture data
            byte[] textureData = dataCodec.Encode(decodedData, textureWidth, textureHeight, null);
            destination.Write(textureData, 0, textureData.Length);

            return destination;
        }
        #endregion
    }
}