﻿using Microsoft.Extensions.Logging;
using PPather.Graph;
using System;
using WowTriangles;
using System.Numerics;
using SharedLib.Extensions;

namespace PPather
{
    public class Search
    {
        public PathGraph PathGraph { get; set; }
        public float MapId { get; set; }

        private readonly DataConfig dataConfig;
        private readonly ILogger logger;

        public Vector4 locationFrom { get; set; }
        public Vector4 locationTo { get; set; }

        private const float toonHeight = 2.0f;
        private const float toonSize = 0.5f;

        public Search(float mapId, ILogger logger, DataConfig dataConfig)
        {
            this.logger = logger;
            this.MapId = mapId;
            this.dataConfig = dataConfig;

            CreatePathGraph(mapId);
        }

        public Vector4 CreateWorldLocation(float x, float y, float z, int mapId)
        {
            float zTerrain = GetZValueAt(x, y,
                new int[] { ChunkedTriangleCollection.TriangleTerrain });

            float zWater = GetZValueAt(x, y,
                new int[] { ChunkedTriangleCollection.TriangleFlagDeepWater });

            if (zWater > zTerrain)
            {
                return new Vector4(x, y, zWater - toonHeight, mapId);
            }

            float zModel = GetZValueAt(x, y,
            new int[] { ChunkedTriangleCollection.TriangleFlagModel, ChunkedTriangleCollection.TriangleFlagObject });

            if (zModel != float.MinValue)
            {
                if (MathF.Abs(zModel - zTerrain) > toonHeight)
                {
                    return new Vector4(x, y, zTerrain - toonHeight, mapId);
                }
                else
                {
                    return new Vector4(x, y, zModel - toonHeight, mapId);
                }
            }

            return new Vector4(x, y, zTerrain - toonHeight, mapId);
        }

        private float GetZValueAt(float x, float y, int[] allowedModels)
        {
            if (PathGraph.triangleWorld.FindStandableAt1(x, y, -1000, 2000, out float z1, out int flags1, toonHeight, toonSize, true, allowedModels))
            {
                return z1;
            }

            return float.MinValue;
        }

        public void CreatePathGraph(float mapId)
        {
            this.MapId = mapId;

            MPQTriangleSupplier mpq = new(logger, dataConfig, mapId);
            ChunkedTriangleCollection triangleWorld = new(logger, 64, mpq);
            PathGraph = new(mapId, triangleWorld, logger, dataConfig);
        }

        public Path DoSearch(PathGraph.eSearchScoreSpot searchType)
        {
            PathGraph.SearchEnabled = true;

            // tell the pathgraph which type of search to do
            PathGraph.searchScoreSpot = searchType;

            //slow down the search if required.
            //PathGraph.sleepMSBetweenSpots = 0;

            try
            {
                return PathGraph.CreatePath(locationFrom.AsVector3(), locationTo.AsVector3(), 5, null);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return null;
            }
        }
    }
}