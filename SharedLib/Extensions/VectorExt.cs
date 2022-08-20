﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SharedLib.Extensions
{
    public static class VectorExt
    {
        public static Vector3[] FromList(List<List<float>> points)
        {
            Vector3[] output = new Vector3[points.Count];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = new(points[i][0], points[i][1], 0);
            }
            return output;
        }

        public static float MapDistanceXYTo(this Vector3 l1, in Vector3 l2)
        {
            return MapDistanceXY(l1, l2);
        }

        public static float MapDistanceXY(Vector3 l1, Vector3 l2)
        {
            return Vector2.Distance(l1.AsVector2() * 100, l2.AsVector2() * 100); // would be nice to remove that 100 multiplier :sweat:
        }

        public static float WorldDistanceXYTo(this Vector3 l1, in Vector3 l2)
        {
            return WorldDistanceXY(l1, l2);
        }

        public static float WorldDistanceXY(Vector3 l1, Vector3 l2)
        {
            return Vector2.Distance(l1.AsVector2(), l2.AsVector2());
        }

        public static List<Vector3> ShortenRouteFromLocation(Vector3 location, List<Vector3> pointsList)
        {
            var result = new List<Vector3>();

            var closestDistance = pointsList.Select(p => (point: p, distance: MapDistanceXYTo(location, p)))
                .OrderBy(s => s.distance);

            var closestPoint = closestDistance.First();

            var startPoint = 0;
            for (int i = 0; i < pointsList.Count; i++)
            {
                if (pointsList[i] == closestPoint.point)
                {
                    startPoint = i;
                    break;
                }
            }

            for (int i = startPoint; i < pointsList.Count; i++)
            {
                result.Add(pointsList[i]);
            }

            return result;
        }

        public static Vector2 GetClosestPointOnLineSegment(in Vector2 A, in Vector2 B, in Vector2 P)
        {
            Vector2 AP = P - A;       //Vector from A to P
            Vector2 AB = B - A;       //Vector from A to B

            float magnitudeAB = AB.LengthSquared();     //Magnitude of AB vector (it's length squared)
            float ABAPproduct = Vector2.Dot(AP, AB);    //The DOT product of a_to_p and a_to_b
            float distance = ABAPproduct / magnitudeAB; //The normalized "distance" from a to your closest point

            if (distance < 0)     //Check if P projection is over vectorAB
            {
                return A;
            }
            else if (distance > 1)
            {
                return B;
            }
            else
            {
                return A + (AB * distance);
            }
        }


        public static Vector2 AsVector2(this Vector3 v3)
        {
            return new Vector2(v3.X, v3.Y);
        }

        public static Vector3 AsVector3(this Vector4 v4)
        {
            return new Vector3(v4.X, v4.Y, v4.Z);
        }
    }
}
