/*
  This file is part of ppather.

    PPather is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PPather is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with ppather.  If not, see <http://www.gnu.org/licenses/>.

*/

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace PPather.Graph
{
    public sealed class GraphChunk
    {
        public const int CHUNK_SIZE = 512;
        public const int SIZE = CHUNK_SIZE * CHUNK_SIZE;
        private const bool saveEnabled = true;

        private const uint FILE_MAGIC = 0x12341234;
        private const uint FILE_ENDMAGIC = 0x43214321;
        private const uint SPOT_MAGIC = 0x53504f54;

        private readonly ILogger logger;
        private readonly float base_x, base_y;
        private readonly string filePath;
        private readonly Spot[] spots;

        public readonly int ix, iy;
        public bool modified;
        public long LRU;

        // Per spot:
        // uint32 magic
        // uint32 reserved;
        // uint32 flags;
        // float x;
        // float y;
        // float z;
        // uint32 no_paths
        //   for each path
        //     float x;
        //     float y;
        //     float z;

        public GraphChunk(float base_x, float base_y, int ix, int iy, ILogger logger, string baseDir)
        {
            this.logger = logger;
            this.base_x = base_x;
            this.base_y = base_y;

            this.ix = ix;
            this.iy = iy;

            spots = new Spot[SIZE];

            filePath = System.IO.Path.Join(baseDir, string.Format("c_{0,3:000}_{1,3:000}.bin", ix, iy));
        }

        public void Clear()
        {
            for (int i = 0; i < SIZE; i++)
            {
                if (spots[i] != null)
                {
                    spots[i].traceBack = null;
                }
            }

            //spots = null;
        }

        private void LocalCoords(float x, float y, out int ix, out int iy)
        {
            ix = (int)(x - base_x);
            iy = (int)(y - base_y);
        }

        public Spot GetSpot2D(float x, float y)
        {
            LocalCoords(x, y, out int ix, out int iy);
            return spots[Index(ix, iy)];
        }

        public Spot GetSpot(float x, float y, float z)
        {
            Spot s = GetSpot2D(x, y);

            while (s != null && !s.IsCloseZ(z))
            {
                s = s.next;
            }

            return s;
        }

        // return old spot at conflicting poision
        // or the same as passed the function if all was ok
        public Spot AddSpot(Spot s)
        {
            Spot old = GetSpot(s.Loc.X, s.Loc.Y, s.Loc.Z);
            if (old != null)
                return old;

            s.chunk = this;

            LocalCoords(s.Loc.X, s.Loc.Y, out int x, out int y);

            int i = Index(x, y);
            s.next = spots[i];
            spots[i] = s;
            modified = true;
            return s;
        }

        public List<Spot> GetAllSpots()
        {
            List<Spot> l = new();
            for (int i = 0; i < SIZE; i++)
            {
                Spot s = spots[i];
                while (s != null)
                {
                    l.Add(s);
                    s = s.next;
                }
            }

            return l;
        }

        private static int Index(int x, int y)
        {
            return (y * CHUNK_SIZE) + x;
        }

        public bool Load()
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                Stopwatch sw = Stopwatch.StartNew();

                using Stream stream = File.OpenRead(filePath);
                using BinaryReader br = new(stream);

                if (br.ReadUInt32() != FILE_MAGIC)
                {
                    return false;
                }

                int n_spots = 0;
                int n_steps = 0;
                while (br.ReadUInt32() != FILE_ENDMAGIC)
                {
                    n_spots++;
                    uint reserved = br.ReadUInt32();
                    uint flags = br.ReadUInt32();
                    float x = br.ReadSingle();
                    float y = br.ReadSingle();
                    float z = br.ReadSingle();
                    uint n_paths = br.ReadUInt32();

                    if (x == 0 || y == 0)
                    {
                        continue;
                    }

                    Spot s = new(x, y, z)
                    {
                        flags = flags
                    };

                    for (uint i = 0; i < n_paths; i++)
                    {
                        n_steps++;
                        s.AddPathTo(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                    }
                    _ = AddSpot(s);

                    // After loading a Chunk mark it unmodified
                    modified = false;
                }

                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace($"Loaded {filePath} {n_spots} spots {n_steps} steps {sw.ElapsedMilliseconds} ms");

                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }

            modified = false;
            return false;
        }

        public void Save()
        {
            if (!saveEnabled || !modified)
                return;

            try
            {
                using Stream stream = File.Create(filePath);
                using BinaryWriter bw = new(stream);
                bw.Write(FILE_MAGIC);

                int n_spots = 0;
                int n_steps = 0;
                foreach (Spot s in GetAllSpots())
                {
                    bw.Write(SPOT_MAGIC);
                    bw.Write((uint)0); // reserved
                    bw.Write(s.flags);
                    bw.Write(s.Loc.X);
                    bw.Write(s.Loc.Y);
                    bw.Write(s.Loc.Z);
                    uint n_paths = (uint)s.n_paths;
                    bw.Write(n_paths);
                    for (uint i = 0; i < n_paths; i++)
                    {
                        uint off = i * 3;
                        bw.Write(s.paths[off]);
                        bw.Write(s.paths[off + 1]);
                        bw.Write(s.paths[off + 2]);
                        n_steps++;
                    }
                    n_spots++;
                }
                bw.Write(FILE_ENDMAGIC);

                bw.Close();
                stream.Close();
                modified = false;

                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace($"Saved {filePath} {n_spots} spots {n_steps} steps");
            }
            catch (Exception e)
            {
                logger.LogError("Save failed " + e);
            }
        }
    }
}