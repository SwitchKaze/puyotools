﻿using System;
using System.IO;
using System.Drawing;
using VrSharp.GvrTexture;

namespace PuyoTools.Modules.Texture
{
    public class GvrTexture : TextureBase
    {
        public override string Name
        {
            get { return "GVR"; }
        }

        public override string FileExtension
        {
            get { return ".gvr"; }
        }

        public override string PaletteFileExtension
        {
            get { return ".gvp"; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Read(byte[] source, long offset, out Bitmap destination, int length)
        {
            // Some GVR textures require an external clut, so we'll just pass this off to ReadWithCLUT
            ReadWithPalette(source, offset, null, 0, out destination, length, 0);
        }

        public override void ReadWithPalette(byte[] source, long offset, byte[] palette, long paletteOffset, out Bitmap destination, int length, int paletteLength)
        {
            // Reading GVR textures is done through VrSharp, so just pass it to that
            VrSharp.GvrTexture.GvrTexture texture = new VrSharp.GvrTexture.GvrTexture(source, offset, length);
            
            // Check to see if this texture requires an external palette.
            // If it does and none was set, throw an exception.
            if (texture.NeedsExternalClut())
            {
                if (palette != null && paletteLength > 0)
                    texture.SetClut(new GvpClut(palette, paletteOffset, paletteLength));
                else
                    throw new TextureNeedsPaletteException();
            }

            destination = texture.GetTextureAsBitmap();
        }

        public override void Write(byte[] source, long offset, Stream destination, int length, string fname)
        {
            throw new NotImplementedException();
        }

        public override bool Is(Stream source, int length, string fname)
        {
            return (length > 16 && VrSharp.GvrTexture.GvrTexture.Is(source, length));
        }
    }
}