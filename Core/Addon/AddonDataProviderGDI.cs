﻿using Game;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace Core
{
    public sealed class AddonDataProviderGDI : IAddonDataProvider, IDisposable
    {
        private readonly CancellationToken ct;

        private readonly int[] data;
        private readonly DataFrame[] frames;

        private readonly Rectangle rect;
        private readonly Bitmap bitmap;
        private readonly Graphics graphics;

        private readonly WowScreen wowScreen;
        private readonly int bytesPerPixel;

        //                                 B  G  R
        private readonly byte[] fColor = { 0, 0, 0 };
        private readonly byte[] lColor = { 129, 132, 30 };

        private const PixelFormat pixelFormat = PixelFormat.Format32bppPArgb;

        private bool disposing;

        public AddonDataProviderGDI(CancellationTokenSource cts, WowScreen wowScreen, DataFrame[] frames)
        {
            ct = cts.Token;
            this.wowScreen = wowScreen;
            this.frames = frames;

            data = new int[this.frames.Length];

            for (int i = 0; i < this.frames.Length; i++)
            {
                if (frames[i].X > rect.Width)
                    rect.Width = frames[i].X;

                if (frames[i].Y > rect.Height)
                    rect.Height = frames[i].Y;
            }
            rect.Width++;
            rect.Height++;

            bitmap = new(rect.Width, rect.Height, pixelFormat);
            graphics = Graphics.FromImage(bitmap);

            bytesPerPixel = Image.GetPixelFormatSize(pixelFormat) / 8;
        }

        public void Dispose()
        {
            if (disposing)
                return;

            disposing = true;

            graphics.Dispose();
            bitmap.Dispose();
        }

        public void Update()
        {
            ct.WaitHandle.WaitOne(1);

            if (ct.IsCancellationRequested || disposing) return;

            Point p = new();
            wowScreen.GetPosition(ref p);
            graphics.CopyFromScreen(p, Point.Empty, rect.Size);

            unsafe
            {
                BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadOnly, pixelFormat);

                byte* fLine = (byte*)bd.Scan0 + (frames[0].Y * bd.Stride);
                int fx = frames[0].X * bytesPerPixel;

                byte* lLine = (byte*)bd.Scan0 + (frames[^1].Y * bd.Stride);
                int lx = frames[^1].X * bytesPerPixel;

                for (int i = 0; i < 3; i++)
                {
                    if (fLine[fx + i] != fColor[i] || lLine[lx + i] != lColor[i])
                        goto Exit;
                }

                for (int i = 0; i < frames.Length; i++)
                {
                    fLine = (byte*)bd.Scan0 + (frames[i].Y * bd.Stride);
                    fx = frames[i].X * bytesPerPixel;

                    data[frames[i].Index] = (fLine[fx + 2] * 65536) + (fLine[fx + 1] * 256) + fLine[fx];
                }

            Exit:
                bitmap.UnlockBits(bd);
            }
        }

        public void InitFrames(DataFrame[] frames) { }

        public int GetInt(int index)
        {
            return data[index];
        }

        public float GetFixed(int index)
        {
            return GetInt(index) / 100000f;
        }

        public string GetString(int index)
        {
            int color = GetInt(index);
            if (color != 0)
            {
                string colorString = color.ToString();
                if (colorString.Length > 6) { return string.Empty; }
                string colorText = "000000"[..(6 - colorString.Length)] + colorString;
                return ToChar(colorText, 0) + ToChar(colorText, 2) + ToChar(colorText, 4);
            }
            else
            {
                return string.Empty;
            }
        }

        private static string ToChar(string colorText, int start)
        {
            return ((char)int.Parse(colorText.Substring(start, 2))).ToString();
        }
    }
}

