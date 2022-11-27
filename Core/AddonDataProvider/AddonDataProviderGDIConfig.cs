﻿using Game;

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;

namespace Core
{
    public sealed class AddonDataProviderGDIConfig : IAddonDataProvider, IDisposable
    {
        private readonly CancellationToken ct;
        private readonly ManualResetEvent manualReset = new(true);
        private readonly WowScreen wowScreen;

        private readonly StringBuilder sb = new(3);

        private int[] data = Array.Empty<int>();
        private DataFrame[] frames = Array.Empty<DataFrame>();

        private Rectangle rect;
        private Bitmap? bitmap;
        private Graphics? graphics;

        private bool disposing;

        public AddonDataProviderGDIConfig(CancellationTokenSource cts, WowScreen wowScreen, DataFrame[] frames)
        {
            ct = cts.Token;
            this.wowScreen = wowScreen;
            InitFrames(frames);
        }

        public void Dispose()
        {
            if (disposing)
                return;

            disposing = true;

            graphics?.Dispose();
            bitmap?.Dispose();
        }

        public void Update()
        {
            manualReset.WaitOne();
            ct.WaitHandle.WaitOne(25);

            if (ct.IsCancellationRequested ||
                disposing ||
                data.Length == 0 ||
                frames.Length == 0 ||
                bitmap == null ||
                graphics == null)
                return;

            Point p = new();
            wowScreen.GetPosition(ref p);
            graphics.CopyFromScreen(p, Point.Empty, rect.Size);

            unsafe
            {
                BitmapData bd = bitmap.LockBits(rect, ImageLockMode.ReadOnly, AddonDataProviderConfig.PIXEL_FORMAT);

                byte* fLine = (byte*)bd.Scan0 + (frames[0].Y * bd.Stride);
                int fx = frames[0].X * AddonDataProviderConfig.BYTES_PER_PIXEL;

                byte* lLine = (byte*)bd.Scan0 + (frames[^1].Y * bd.Stride);
                int lx = frames[^1].X * AddonDataProviderConfig.BYTES_PER_PIXEL;

                for (int i = 0; i < 3; i++)
                {
                    if (fLine[fx + i] != AddonDataProviderConfig.fColor[i] ||
                        lLine[lx + i] != AddonDataProviderConfig.lColor[i])
                        goto Exit;
                }

                for (int i = 0; i < frames.Length; i++)
                {
                    fLine = (byte*)bd.Scan0 + (frames[i].Y * bd.Stride);
                    fx = frames[i].X * AddonDataProviderConfig.BYTES_PER_PIXEL;

                    data[frames[i].Index] = (fLine[fx + 2] * 65536) + (fLine[fx + 1] * 256) + fLine[fx];
                }

            Exit:
                bitmap.UnlockBits(bd);
            }
        }

        public void InitFrames(DataFrame[] frames)
        {
            manualReset.Reset();

            this.frames = frames;
            data = new int[this.frames.Length];

            for (int i = 0; i < this.frames.Length; i++)
            {
                rect.Width = Math.Max(rect.Width, frames[i].X);
                rect.Height = Math.Max(rect.Height, frames[i].Y);
            }
            rect.Width++;
            rect.Height++;

            bitmap = new(rect.Width, rect.Height, AddonDataProviderConfig.PIXEL_FORMAT);
            graphics = Graphics.FromImage(bitmap);

            manualReset.Set();
        }

        public int GetInt(int index)
        {
            return index > data.Length ? 0 : data[index];
        }

        public float GetFixed(int index)
        {
            return GetInt(index) / 100000f;
        }

        public string GetString(int index)
        {
            int color = GetInt(index);
            if (color == 0 || color > 999999)
                return string.Empty;

            sb.Clear();

            int n = color / 10000;
            if (n > 0) sb.Append((char)n);

            n = color / 100 % 100;
            if (n > 0) sb.Append((char)n);

            n = color % 100;
            if (n > 0) sb.Append((char)n);

            return sb.ToString().Trim();
        }
    }
}

