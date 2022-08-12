﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

using Core.Database;

using WowheadDB;

namespace Core
{
    public class RouteInfoPoi
    {
        public Vector3 Location { get; }
        public string Name { get; }
        public string Color { get; }

        public double Radius { get; }

        public RouteInfoPoi(NPC npc, string color)
        {
            Location = npc.points.First();
            Name = npc.name;
            Color = color;
            Radius = 1;
        }

        public RouteInfoPoi(Vector3 wowPoint, string name, string color, double radius)
        {
            Location = wowPoint;
            Name = name;
            Color = color;
            Radius = radius;
        }
    }

    public class RouteInfo : IDisposable
    {
        public Vector3[] Route { get; private set; }

        public List<Vector3>? RouteToWaypoint
        {
            get
            {
                if (pathedRoutes.Any())
                {
                    return pathedRoutes.OrderByDescending(x => x.LastActive).First().PathingRoute();
                }
                return default;
            }
        }

        private readonly IEnumerable<IRouteProvider> pathedRoutes;
        private readonly AreaDB areaDB;
        private readonly PlayerReader playerReader;

        public List<RouteInfoPoi> PoiList { get; } = new();

        private double min;
        private double diff;

        private double addY;
        private double addX;

        private int margin;
        private int canvasSize;

        private double pointToGrid;

        private const int dSize = 2;

        public RouteInfo(Vector3[] route, IEnumerable<IRouteProvider> pathedRoutes, PlayerReader playerReader, AreaDB areaDB)
        {
            this.Route = route;
            this.pathedRoutes = pathedRoutes;

            this.playerReader = playerReader;
            this.areaDB = areaDB;

            this.areaDB.Changed += OnZoneChanged;
            OnZoneChanged();

            CalculateDiffs();
        }

        public void Dispose()
        {
            areaDB.Changed -= OnZoneChanged;
        }

        public void UpdateRoute(IEnumerable<Vector3> newRoute)
        {
            Route = newRoute.ToArray();

            IEnumerable<IEditedRouteReceiver> routeReceivers = pathedRoutes.Where(a => a is IEditedRouteReceiver).Cast<IEditedRouteReceiver>();
            foreach (IEditedRouteReceiver receiver in routeReceivers)
            {
                receiver.ReceivePath(Route);
            }
        }

        public void SetMargin(int margin)
        {
            this.margin = margin;
            CalculatePointToGrid();
        }

        public void SetCanvasSize(int size)
        {
            this.canvasSize = size;
            CalculatePointToGrid();
        }

        public void CalculatePointToGrid()
        {
            pointToGrid = ((double)canvasSize - (margin * 2)) / diff;
            CalculateDiffs();
        }

        public int ToCanvasPointX(double value)
        {
            return (int)(margin + ((value + addX - min) * pointToGrid));
        }

        public int ToCanvasPointY(double value)
        {
            return (int)(margin + ((value + addY - min) * pointToGrid));
        }

        public double DistanceToGrid(int value)
        {
            return value / 100f * pointToGrid;
        }

        private void OnZoneChanged()
        {
            if (areaDB.CurrentArea == null)
                return;

            PoiList.Clear();
            /*
            // Visualize the zone pois
            for (int i = 0; i < areaDB.CurrentArea.vendor?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.vendor[i];
                PoiList.Add(new RouteInfoPoi(npc, "green"));
            }

            for (int i = 0; i < areaDB.CurrentArea.repair?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.repair[i];
                PoiList.Add(new RouteInfoPoi(npc, "purple"));
            }

            for (int i = 0; i < areaDB.CurrentArea.innkeeper?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.innkeeper[i];
                PoiList.Add(new RouteInfoPoi(npc, "blue"));
            }

            for (int i = 0; i < areaDB.CurrentArea.flightmaster?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.flightmaster[i];
                PoiList.Add(new RouteInfoPoi(npc, "orange"));
            }
            */
        }

        private void CalculateDiffs()
        {
            var allPoints = Route.ToList();

            var wayPoints = RouteToWaypoint;
            if (wayPoints != null)
                allPoints.AddRange(wayPoints);

            var pois = PoiList.Select(p => p.Location);
            allPoints.AddRange(pois);

            allPoints.Add(playerReader.PlayerLocation);

            var maxX = allPoints.Max(s => s.X);
            var minX = allPoints.Min(s => s.X);
            var diffX = maxX - minX;

            var maxY = allPoints.Max(s => s.Y);
            var minY = allPoints.Min(s => s.Y);
            var diffY = maxY - minY;

            this.addY = 0;
            this.addX = 0;

            if (diffX > diffY)
            {
                this.addY = minX - minY;
                this.min = minX;
                this.diff = diffX;
            }
            else
            {
                this.addX = minY - minX;
                this.min = minY;
                this.diff = diffY;
            }
        }

        public string RenderPathLines(Vector3[] path)
        {
            StringBuilder sb = new();
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 p1 = path[i];
                Vector3 p2 = path[i + 1];
                sb.AppendLine($"<line x1 = '{ToCanvasPointX(p1.X)}' y1 = '{ToCanvasPointY(p1.Y)}' x2 = '{ToCanvasPointX(p2.X)}' y2 = '{ToCanvasPointY(p2.Y)}' />");
            }
            return sb.ToString();
        }

        private readonly string first = "<br><b>First</b>";
        private readonly string last = "<br><b>Last</b>";

        public string RenderPathPoints(Vector3[] path)
        {
            StringBuilder sb = new();
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 p = path[i];
                float x = p.X;
                float y = p.Y;
                sb.AppendLine($"<circle onmousedown=\"pointClick(evt,{x},{y},{i});\"  onmousemove=\"showTooltip(evt,'{x},{y}{(i == 0 ? first : i == path.Length - 1 ? last : string.Empty)}');\" onmouseout=\"hideTooltip();\"  cx = '{ToCanvasPointX(p.X)}' cy = '{ToCanvasPointY(p.Y)}' r = '{dSize}' />");
            }
            return sb.ToString();
        }

        public Vector3 NextPoint()
        {
            var route = pathedRoutes.OrderByDescending(s => s.LastActive).FirstOrDefault();
            if (route == null || !route.HasNext())
                return Vector3.Zero;

            return route.NextPoint();
        }

        public string RenderNextPoint()
        {
            Vector3 pt = NextPoint();
            if (pt == Vector3.Zero)
                return string.Empty;

            return $"<circle cx = '{ToCanvasPointX(pt.X)}' cy = '{ToCanvasPointY(pt.Y)}'r = '{dSize + 1}' />";
        }

        public string DeathImage(Vector3 pt)
        {
            var size = this.canvasSize / 25;
            return pt == Vector3.Zero ? string.Empty : $"<image href = '_content/Frontend/death.svg' x = '{ToCanvasPointX(pt.X) - size / 2}' y = '{ToCanvasPointY(pt.Y) - size / 2}' height='{size}' width='{size}' />";
        }

        public string DrawPoi(RouteInfoPoi poi)
        {
            return $"<circle onmousemove=\"showTooltip(evt, '{poi.Name}<br/>{poi.Location.X},{poi.Location.Y}');\" onmouseout=\"hideTooltip();\" cx='{ToCanvasPointX(poi.Location.X)}' cy='{ToCanvasPointY(poi.Location.Y)}' r='{(poi.Radius == 1 ? dSize : DistanceToGrid((int)poi.Radius))}' " + (poi.Radius == 1 ? $"fill='{poi.Color}'" : $"stroke='{poi.Color}' stroke-width='1' fill='none'") + " />";
        }
    }
}