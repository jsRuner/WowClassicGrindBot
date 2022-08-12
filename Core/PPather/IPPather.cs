﻿using PPather.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;

namespace Core
{
    public interface IPPather
    {
        ValueTask<Vector3[]> FindRoute(int map, Vector3 fromPoint, Vector3 toPoint);
        ValueTask DrawLines(List<LineArgs> lineArgs);
        ValueTask DrawSphere(SphereArgs args);
    }
}