﻿using Game;

using Microsoft.Extensions.Logging;

using SharpGen.Runtime;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

using WinAPI;

using static WinAPI.NativeMethods;

namespace Core;

public sealed class WowScreenDXGI : IWowScreen, IAddonDataProvider
{
    private readonly ILogger<WowScreenDXGI> logger;
    private readonly WowProcess process;
    private readonly int Bgra32Size;

    public event Action? OnChanged;

    public bool Enabled { get; set; }
    public bool EnablePostProcess { get; set; }

    public bool MinimapEnabled { get; set; }

    public Rectangle ScreenRect => screenRect;
    private Rectangle screenRect;

    public Image<Bgra32> ScreenImage { get; init; }

    private readonly SixLabors.ImageSharp.Configuration ContiguousJpegConfiguration
        = new(new JpegConfigurationModule()) { PreferContiguousImageBuffers = true };

    // TODO: make it work for higher resolution ex. 4k
    public const int MiniMapSize = 200;
    public Rectangle MiniMapRect { get; private set; }
    public Image<Bgra32> MiniMapImage { get; init; }

    private static readonly FeatureLevel[] s_featureLevels =
    {
        FeatureLevel.Level_12_1,
        FeatureLevel.Level_12_0,
        FeatureLevel.Level_11_0,
    };

    private readonly IDXGIAdapter adapter;
    private readonly IDXGIOutput output;
    private readonly IDXGIOutput1 output1;

    private readonly ID3D11Texture2D minimapTexture;
    private readonly ID3D11Texture2D screenTexture;
    private ID3D11Texture2D addonTexture = null!;

    private readonly ID3D11Device device;
    private readonly IDXGIOutputDuplication duplication;

    private readonly bool windowedMode;

    // IAddonDataProvider

    private Size addonSize;
    private DataFrame[] frames = null!;
    private Image<Bgra32> addonImage = null!;

    public int[] Data { get; private set; } = Array.Empty<int>();
    public StringBuilder TextBuilder { get; } = new(3);

    public WowScreenDXGI(ILogger<WowScreenDXGI> logger,
        WowProcess process, DataFrame[] frames)
    {
        this.logger = logger;
        this.process = process;

        Bgra32Size = Unsafe.SizeOf<Bgra32>();

        GetRectangle(out screenRect);
        windowedMode = IsWindowedMode(screenRect.Location);

        ScreenImage = new(ContiguousJpegConfiguration, screenRect.Width, screenRect.Height);

        MiniMapRect = new(0, 0, MiniMapSize, MiniMapSize);
        MiniMapImage = new(ContiguousJpegConfiguration, MiniMapSize, MiniMapSize);

        IntPtr hMonitor =
            MonitorFromWindow(process.MainWindowHandle, MONITOR_DEFAULT_TO_NULL);

        Result result;

        IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        result = factory.EnumAdapters(0, out adapter);
        if (result == Result.Fail)
            throw new Exception($"Unable to enumerate adapter! {result.Description}");

        int srcIdx = 0;
        do
        {
            result = adapter.EnumOutputs(srcIdx, out output);
            if (result == Result.Ok &&
                output.Description.Monitor == hMonitor)
            {
                break;
            }
        } while (result != Result.Fail);

        output1 = output.QueryInterface<IDXGIOutput1>();
        result = D3D11.D3D11CreateDevice(adapter, DriverType.Unknown,
            DeviceCreationFlags.Singlethreaded, s_featureLevels, out device!);

        if (result == Result.Fail)
            throw new Exception($"device is null {result.Description}");

        duplication = output1.DuplicateOutput(device);

        Texture2DDescription screenTextureDesc = new()
        {
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            Width = screenRect.Right,
            Height = screenRect.Bottom,
            MiscFlags = ResourceOptionFlags.None,
            MipLevels = 1,
            ArraySize = 1,
            SampleDescription = { Count = 1, Quality = 0 },
            Usage = ResourceUsage.Staging
        };
        screenTexture = device.CreateTexture2D(screenTextureDesc);

        InitFrames(frames);

        Texture2DDescription miniMapTextureDesc = new()
        {
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            Width = MiniMapRect.Right,
            Height = MiniMapRect.Bottom,
            MiscFlags = ResourceOptionFlags.None,
            MipLevels = 1,
            ArraySize = 1,
            SampleDescription = { Count = 1, Quality = 0 },
            Usage = ResourceUsage.Staging
        };
        minimapTexture = device.CreateTexture2D(miniMapTextureDesc);

        this.logger.LogInformation($"{screenRect} - " +
            $"Windowed Mode: {windowedMode} - " +
            $"Scale: {DPI2PPI(GetDpi()):F2}");
    }

    public void Dispose()
    {
        duplication?.ReleaseFrame();
        duplication?.Dispose();

        minimapTexture.Dispose();
        addonTexture.Dispose();
        screenTexture.Dispose();

        device.Dispose();
        adapter.Dispose();
        output1.Dispose();
        output.Dispose();
    }

    public void InitFrames(DataFrame[] frames)
    {
        this.frames = frames;
        Data = new int[frames.Length];

        addonSize = new();
        for (int i = 0; i < frames.Length; i++)
        {
            addonSize.Width = Math.Max(addonSize.Width, frames[i].X);
            addonSize.Height = Math.Max(addonSize.Height, frames[i].Y);
        }
        addonSize.Width++;
        addonSize.Height++;

        addonImage = new(ContiguousJpegConfiguration, addonSize.Width, addonSize.Height);

        Texture2DDescription addonTextureDesc = new()
        {
            CPUAccessFlags = CpuAccessFlags.Read,
            BindFlags = BindFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            Width = addonSize.Width,
            Height = addonSize.Height,
            MiscFlags = ResourceOptionFlags.None,
            MipLevels = 1,
            ArraySize = 1,
            SampleDescription = { Count = 1, Quality = 0 },
            Usage = ResourceUsage.Staging
        };

        addonTexture?.Dispose();
        addonTexture = device.CreateTexture2D(addonTextureDesc);

        logger.LogDebug($"DataFrames {frames.Length} - Texture: {addonSize}");
    }

    [SkipLocalsInit]
    public void Update()
    {
        if (windowedMode)
        {
            GetRectangle(out screenRect);

            // TODO: bounds check
            if (screenRect.X < 0 ||
                screenRect.Y < 0 ||
                screenRect.Right > output.Description.DesktopCoordinates.Right ||
                screenRect.Bottom > output.Description.DesktopCoordinates.Bottom)
                return;
        }

        duplication.ReleaseFrame();

        Result result = duplication.AcquireNextFrame(0,
            out OutduplFrameInfo frame,
        out IDXGIResource idxgiResource);

        // If only the pointer was updated(that is, the desktop image was not updated),
        // the AccumulatedFrames, TotalMetadataBufferSize, LastPresentTime members are set to zero.
        if (!result.Success ||
            frame.AccumulatedFrames == 0 ||
            frame.TotalMetadataBufferSize == 0 ||
            frame.LastPresentTime == 0)
        {
            return;
        }

        ID3D11Texture2D texture
            = idxgiResource.QueryInterface<ID3D11Texture2D>();

        if (frames.Length > 2)
            UpdateAddonImage(texture);

        if (Enabled)
            UpdateScreenImage(texture);

        if (MinimapEnabled)
            UpdateMinimapImage(texture);

        texture.Dispose();
    }

    [SkipLocalsInit]
    private void UpdateAddonImage(ID3D11Texture2D texture)
    {
        if (!addonImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> memory))
            return;

        Box areaOnScreen = new(
            screenRect.X, screenRect.Y, 0,
            screenRect.X + addonSize.Width,
            screenRect.Y + addonSize.Height, 1);

        device.ImmediateContext
            .CopySubresourceRegion(addonTexture, 0, 0, 0, 0, texture, 0, areaOnScreen);

        MappedSubresource resource = device.ImmediateContext
            .Map(addonTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

        int rowPitch = resource.RowPitch;
        ReadOnlySpan<byte> src = resource.AsSpan(addonSize.Height * rowPitch);
        Span<byte> dest = MemoryMarshal.Cast<Bgra32, byte>(memory.Span);

        int bytesToCopy = addonSize.Width * Bgra32Size;
        for (int y = 0; y < addonSize.Height; y++)
        {
            ReadOnlySpan<byte> srcRow = src.Slice(y * rowPitch, bytesToCopy);
            Span<byte> destRow = dest.Slice(y * bytesToCopy, bytesToCopy);
            srcRow.TryCopyTo(destRow);
        }

        device.ImmediateContext.Unmap(addonTexture, 0);
    }

    [SkipLocalsInit]
    private void UpdateScreenImage(ID3D11Texture2D texture)
    {
        if (!ScreenImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> memory))
            return;

        Box areaOnScreen = new(
            screenRect.X, screenRect.Y, 0,
            screenRect.Right, screenRect.Bottom, 1);

        device.ImmediateContext
            .CopySubresourceRegion(screenTexture, 0, 0, 0, 0, texture, 0, areaOnScreen);

        MappedSubresource resource = device.ImmediateContext
            .Map(screenTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

        int rowPitch = resource.RowPitch;
        ReadOnlySpan<byte> src = resource.AsSpan(screenRect.Height * rowPitch);
        Span<byte> dest = MemoryMarshal.Cast<Bgra32, byte>(memory.Span);

        if (!windowedMode)
        {
            src.TryCopyTo(dest);
        }
        else
        {
            int bytesToCopy = screenRect.Width * Bgra32Size;
            for (int y = 0; y < screenRect.Height; y++)
            {
                ReadOnlySpan<byte> srcRow = src.Slice(y * rowPitch, bytesToCopy);
                Span<byte> destRow = dest.Slice(y * bytesToCopy, bytesToCopy);
                srcRow.TryCopyTo(destRow);
            }
        }

        device.ImmediateContext.Unmap(screenTexture, 0);
    }

    [SkipLocalsInit]
    private void UpdateMinimapImage(ID3D11Texture2D texture)
    {
        if (!MiniMapImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> memory))
            return;

        Box areaOnScreen = new(
            screenRect.Right - MiniMapSize, screenRect.Y, 0,
            screenRect.Right, screenRect.Top + MiniMapRect.Bottom, 1);

        device.ImmediateContext
            .CopySubresourceRegion(minimapTexture, 0, 0, 0, 0, texture, 0, areaOnScreen);

        MappedSubresource resource = device.ImmediateContext
            .Map(minimapTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

        int rowPitch = resource.RowPitch;
        ReadOnlySpan<byte> src = resource.AsSpan(MiniMapRect.Height * rowPitch);
        Span<byte> dest = MemoryMarshal.Cast<Bgra32, byte>(memory.Span);

        int bytesToCopy = MiniMapRect.Width * Bgra32Size;
        for (int y = 0; y < MiniMapRect.Height; y++)
        {
            ReadOnlySpan<byte> srcRow = src.Slice(y * rowPitch, bytesToCopy);
            Span<byte> destRow = dest.Slice(y * bytesToCopy, bytesToCopy);
            srcRow.TryCopyTo(destRow);
        }

        device.ImmediateContext.Unmap(minimapTexture, 0);
    }

    public void UpdateData()
    {
        if (frames.Length <= 2)
            return;

        IAddonDataProvider.InternalUpdate(addonImage, frames, Data);
    }

    public void PostProcess()
    {
        OnChanged?.Invoke();
    }

    public void GetPosition(ref Point point)
    {
        NativeMethods.GetPosition(process.MainWindowHandle, ref point);
    }

    public void GetRectangle(out Rectangle rect)
    {
        NativeMethods.GetWindowRect(process.MainWindowHandle, out rect);
    }
}