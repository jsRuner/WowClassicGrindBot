﻿using System;
using SixLabors.ImageSharp;
using System.Threading;

using Game;

namespace CoreTests;

internal sealed class MockWowProcessInput : IMouseInput
{
    public void InteractMouseOver(CancellationToken token)
    {
        throw new NotImplementedException();
    }

    public void LeftClick(Point p)
    {
        throw new NotImplementedException();
    }

    public void RightClick(Point p)
    {
        throw new NotImplementedException();
    }

    public void SetCursorPos(Point p)
    {
        throw new NotImplementedException();
    }
}
