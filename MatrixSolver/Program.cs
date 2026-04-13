using System;
using System.Collections.Generic;
using System.Linq;

namespace MatrixSolver
{
    class Program
    {
        static void Main()
        {
            // Current matrix in the app
            double m11_old = -0.0779, m12_old = 9.6543, m21_old = 10.1058, m22_old = 12.103, dx_old = 1488.5, dy_old = 1368.4;

            // User points (Current in-app coordinates)
            var wPoints = new[] {
                (76.0, -37.0),
                (-4.2, 56.1),
                (-108.0, 72.5),
                (-3.6, -19.7)
            };

            // Calculated target pixels (P)
            var pPoints = wPoints.Select(w => (
                x: w.Item1 * m11_old + w.Item2 * m12_old + dx_old,
                y: w.Item1 * m21_old + w.Item2 * m22_old + dy_old
            )).ToArray();

            // Real game points (Should be)
            var gPoints = new[] {
                (9.47, -36.25),
                (40.92, 55.43),
                (-45.25, 72.47),
                (-51.11, -18.63)
            };

            Console.WriteLine("Solving for T_new(gx, gz) = (px, py)...");
            
            // X components: m11, m12, dx
            Solve(gPoints, pPoints.Select(p => p.x).ToArray(), "X-Params");
            // Y components: m21, m22, dy
            Solve(gPoints, pPoints.Select(p => p.y).ToArray(), "Y-Params");
        }

        static void Solve(IEnumerable<(double gx, double gz)> g, double[] targets, string label)
        {
            // Solve A * x = B using Normal Equations: A^T * A * x = A^T * B
            double sX2 = 0, sZ2 = 0, sXZ = 0, sX = 0, sZ = 0, n = 0;
            double sXT = 0, sZT = 0, sT = 0;

            var gList = g.ToList();
            for (int i = 0; i < gList.Count; i++)
            {
                double x = gList[i].gx;
                double z = gList[i].gz;
                double t = targets[i];

                sX2 += x * x;
                sZ2 += z * z;
                sXZ += x * z;
                sX += x;
                sZ += z;
                sXT += x * t;
                sZT += z * t;
                sT += t;
                n++;
            }

            // Matrix M = [[sX2, sXZ, sX], [sXZ, sZ2, sZ], [sX, sZ, n]]
            // Vector V = [sXT, sZT, sT]
            // Solve M * X = V using Cramer's rule or similar
            double det = sX2 * (sZ2 * n - sZ * sZ) - sXZ * (sXZ * n - sZ * sX) + sX * (sXZ * sZ - sZ2 * sX);
            
            double mH = (sXT * (sZ2 * n - sZ * sZ) - sXZ * (sZT * n - sT * sZ) + sX * (sZT * sZ - sT * sZ2)) / det;
            double mV = (sX2 * (sZT * n - sT * sZ) - sXT * (sXZ * n - sZ * sX) + sX * (sXZ * sT - sZT * sX)) / det;
            double mD = (sX2 * (sZ2 * sT - sZ * sZT) - sXZ * (sXZ * sT - sZT * sX) + sXT * (sXZ * sZ - sZ2 * sX)) / det;

            Console.WriteLine($"{label}: m1={mH:F4}, m2={mV:F4}, d={mD:F4}");
        }
    }
}
