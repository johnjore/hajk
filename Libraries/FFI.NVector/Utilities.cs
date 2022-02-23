using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFI.NVector
{
    /// <summary>
    ///  Basic matrix library for NVMath. The library serves as a minimum matrix library for 
    ///  NVMath, so that the NVMath can be distributed without any depedencies of external 
    ///  matrix libraries, and it is not intended to be used for user code.
    /// </summary>
    public class Utilities
    {
        public static double Dot(double[] a, double[] b)
        {
            var sum = 0.0;
            for (int i=0; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }
            return sum;
        }

        public static double[] Cross(double[] a, double[] b)
        {
            return new double[3] {
                a[1] * b[2] - a[2] * b[1], 
                -a[0] * b[2] + a[2] * b[0], 
                a[0] * b[1] - a[1] * b[0]
            };
        }

        public static double[,] Transpose(double[,] A)
        {
            var m = A.GetLength(0);
            var n = A.GetLength(1);
            var B = new double[n, m];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    B[i, j] = A[j, i];
                }
            }
            return B;
        }

        public static double[] VecAdd(double[] a, double[] b)
        {
            int n = a.Length;
            var c = new double[n];
            for (int i = 0; i < n; i++)
            {
                c[i] = a[i] + b[i];
            }
            return c;
        }

        public static double[] VecSubtract(double[] a, double[] b)
        {
            int n = a.Length;
            var c = new double[n];
            for (int i = 0; i < n; i++)
            {
                c[i] = a[i] - b[i];
            }
            return c;
        }

        public static double[] VecMul(double a, double[] v)
        {
            var m = v.Length;
            var v2 = new double[m];
            for (int i = 0; i < m; i++)
            {
                v2[i] = a * v[i];
            }
            return v2;
        }

        public static double[] MatMul(double[,] M, double[] v)
        {
            var m = M.GetLength(0);
            var n = M.GetLength(1);
            var v2 = new double[m];
            for (int i = 0; i < m; i++)
            {
                double sum = 0;
                for (int j = 0; j < n; j++)
                {
                    sum += M[i, j] * v[j];
                }
                v2[i] = sum;
            }
            return v2;
        }

        public static double[,] MatMul(double[,] A, double[,] B)
        {
            var m = A.GetLength(0);
            var n = A.GetLength(1);
            var p = B.GetLength(0);
            var C = new double[m, p];
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < p; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += A[i, k] * B[k, j];
                    }
                    C[i, j] = sum;
                }
            }
            return C;
        }

        public static double Norm(double[] v)
        {
            double sum = 0.0;
            int num = v.Length;
            for (int i = 0; i < num; i++)
            {
                sum += Math.Pow(v[i],2);
            }
            return Math.Sqrt(sum);
        }

    }
}
