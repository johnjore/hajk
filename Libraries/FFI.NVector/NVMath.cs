//   This file is available from www.navlab.net/nvector
//
//   The content of this file is based on the following publication:
//
//   Gade, K. (2010). A Nonsingular Horizontal Position Representation, The Journal 
//   of Navigation, Volume 63, Issue 03, pp 395-417, July 2010. 
//   (www.navlab.net/Publications/A_Nonsingular_Horizontal_Position_Representation.pdf)
//
//   This paper should be cited in publications using this file.
//
//   Copyright (c) 2016, Norwegian Defence Research Establishment (FFI)
//   All rights reserved.
//
//   Redistribution and use in source and binary forms, with or without 
//   modification, are permitted provided that the following conditions are met:
//
//   1. Redistributions of source code must retain the above publication 
//   information, copyright notice, this list of conditions and the following disclaimer.
//
//   2. Redistributions in binary form must reproduce the above publication 
//   information, copyright notice, this list of conditions and the following disclaimer 
//   in the documentation and/or other materials provided with the distribution.
//
//   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS 
//   "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED 
//   TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
//   PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS 
//   BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
//   CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
//   SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
//   INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
//   CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
//   ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
//   THE POSSIBILITY OF SUCH DAMAGE.
//   
//   Originated: 2016 Jørn Inge Vestgården, FFI
//   Based on original Matlab code by Kenneth Gade and Brita Hafskjold, FFI


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFI.NVector
{
    /// <summary>
    /// NVector for geographical positioning. The C# code is translated more or less directly from Matlab and    
    /// all funcionality is in one class. The implementation uses standard .NET arrays, i.e., 
    /// double[3] is a 3-vector and double[3,3] is a 3 x 3 matrix. For more documentation, 
    /// consult the n-vector webpage: www.navlab.net/nvector. 
    /// </summary>
    public class NVMath 
    {
        private const double piover180 = Math.PI / 180.0;

        public NVMath()
        {
            R_Ee = R_Ee_Northpole_Z; // Default choice of E-axis
        }

        /// <summary>
        ///   FUNCTION 1: lat_long2n_E 
        ///   
        ///   Converts latitude and longitude to n-vector.
        ///   n_E = lat_long2n_E(latitude,longitude) 
        ///   n-vector (n_E) is calculated from (geodetic) latitude and longitude.
        ///
        ///   IN:
        ///   latitude:  [rad]     Geodetic latitude
        ///   longitude: [rad]
        ///
        ///   OUT:
        ///   n_E:       [no unit] n-vector decomposed in E (3x1 vector)
        ///
        ///   The function also accepts vectors (1xm) with lat and long, then a 3xm 
        ///   matrix of n-vectors is returned.
        ///
        ///   See also n_E2lat_long.
        /// </summary>
        public double[] lat_long2n_E(double lat, double lon)
        {
            var n_E = Utilities.MatMul(
                Utilities.Transpose(R_Ee), 
                new double[3] {Math.Sin(lat), Math.Sin(lon)*Math.Cos(lat), -Math.Cos(lon)*Math.Cos(lat)});
            return n_E;
        }

        public double[] lat_long2n_E(double[] latlong)
        { 
            return lat_long2n_E(latlong[0], latlong[1]);
        }

        /// <summary>
        ///  FUNCTION 2: n_E2lat_long 
        ///   
        ///   Converts n-vector to latitude and longitude.
        ///   
        ///   [latitude,longitude] = n_E2lat_long(n_E) 
        ///   Geodetic latitude and longitude are calculated from n-vector (n_E).
        ///
        ///   IN:
        ///   n_E:       [no unit] n-vector decomposed in E (3x1 vector)
        ///
        ///   OUT:
        ///   latitude:  [rad]     Geodetic latitude
        ///   longitude: [rad]
        ///
        ///   The function also accepts vectorized form (i.e. a 3xm matrix of n-vectors 
        ///   is input, returning 1xm vectors of latitude and longitude)
        ///
        ///   See also lat_long2n_E.
        /// </summary>
        public double[] n_E2lat_long(double[] n_E1)
        {
            var n_E = Utilities.MatMul(R_Ee, n_E1); // R_Ee selects correct E-axes, see R_Ee.m for details
            // Equation (5) in Gade (2010):
            var longitude = Math.Atan2(n_E[1], -n_E[2]);
            // Equation (6) in Gade (2010) (Robust numerical solution)
            var equatorialEomponent = Math.Sqrt(Math.Pow(n_E[1], 2) + Math.Pow(n_E[2], 2));  // vector component in the equatorial plane
            var latitude = Math.Atan2(n_E[0], equatorialEomponent); // atan() could also be used since latitude is within [-pi/2,pi/2]
            return new double[2] { latitude, longitude };
            // latitude=asin(n_E(1)) is a theoretical solution, but close to the Poles it
            // is ill-conditioned which may lead to numerical inaccuracies (and it will give imaginary results for norm(n_E)>1)
        }

        public double n_E2lat(double[] n_E1)
        {
            var n_E = Utilities.MatMul(R_Ee, n_E1);            
            var equatorialEomponent = Math.Sqrt(Math.Pow(n_E[1], 2) + Math.Pow(n_E[2], 2));  // vector component in the equatorial plane
            var latitude = Math.Atan2(n_E[0], equatorialEomponent); // atan() could also be used since latitude is within [-pi/2,pi/2]
            return latitude;
        }

        public double n_E2long(double[] n_E1)
        {
            var n_E = Utilities.MatMul(R_Ee, n_E1); // R_Ee selects correct E-axes, see R_Ee.m for details
            // Equation (5) in Gade (2010):
            var longitude = Math.Atan2(n_E[1], -n_E[2]);
            return longitude;
        }

        /// <summary>
        ///   FUNCTION 3: n_EA_E_and_n_EB_E2p_AB_E 
        ///   From two positions A and B, finds the delta position.
        ///   
        ///   p_AB_E = n_EA_E_and_n_EB_E2p_AB_E(n_EA_E,n_EB_E)
        ///   The n-vectors for positions A (n_EA_E) and B (n_EB_E) are given. The
        ///   output is the delta vector from A to B (p_AB_E).
        ///   The calculation is exact, taking the ellipsity of the Earth into account.
        ///   It is also nonsingular as both n-vector and p-vector are nonsingular
        ///   (except for the center of the Earth).
        ///   The default ellipsoid model used is WGS-84, but other ellipsoids (or spheres)
        ///   might be specified.
        ///
        ///   p_AB_E = n_EA_E_and_n_EB_E2p_AB_E(n_EA_E,n_EB_E,z_EA)
        ///   p_AB_E = n_EA_E_and_n_EB_E2p_AB_E(n_EA_E,n_EB_E,z_EA,z_EB)
        ///   Depth(s) of A, z_EA (and of B, z_EB) are also specified, z_EA = 0 (and z_EB = 0)
        ///   is used when not specified.
        ///
        ///   p_AB_E = n_EA_E_and_n_EB_E2p_AB_E(n_EA_E,n_EB_E,z_EA,z_EB,a)
        ///   Spherical Earth with radius a is used instead of WGS-84.
        ///
        ///   p_AB_E = n_EA_E_and_n_EB_E2p_AB_E(n_EA_E,n_EB_E,z_EA,z_EB,a,f)
        ///   Ellipsoidal Earth model with semi-major axis a and flattening f is used 
        ///   instead of WGS-84.
        ///
        ///   IN: 
        ///   n_EA_E:  [no unit] n-vector of position A, decomposed in E (3x1 vector).
        ///   n_EB_E:  [no unit] n-vector of position B, decomposed in E (3x1 vector).
        ///   z_EA:    [m]       (Optional, assumed to be zero if not given) Depth of system A, 
        ///                      relative to the ellipsoid (z_EA = -height).                      
        ///   z_EB:    [m]       (Optional, assumed to be zero if not given) Depth of system B, 
        ///                      relative to the ellipsoid (z_EB = -height).
        ///   a:       [m]       (Optional) Semi-major axis of the Earth ellipsoid
        ///   f:       [no unit] (Optional) Flattening of the Earth ellipsoid
        ///
        ///   OUT: 
        ///   p_AB_E:  [m]       Position vector from A to B, decomposed in E (3x1 vector).
        ///
        ///   The function also accepts vectorized form, i.e. n_EA_E and n_EB_E are 3xn matrices, 
        ///   z_EA and z_EB are 1xn vectors and p_AB_E is a 3xn matrix.
        /// 
        ///   See also n_EA_E_and_p_AB_E2n_EB_E, p_EB_E2n_EB_E, n_EB_E2p_EB_E.
        /// </summary>
        public double[] n_EA_E_and_n_EB_E2p_AB_E(double[] n_EA_E, double[] n_EB_E, double z_EA, double z_EB, double a, double f)
        { 
            // Function 1. in Section 5.4 in Gade (2010):
            var p_EA_E=  n_EB_E2p_EB_E(n_EA_E, z_EA, a, f);
            var p_EB_E = n_EB_E2p_EB_E(n_EB_E, z_EB, a, f);
            var p_AB_E = new double[3] {
                -p_EA_E[0] + p_EB_E[0],
                -p_EA_E[1] + p_EB_E[1],
                -p_EA_E[2] + p_EB_E[2] };
            return p_AB_E;
        }

        /// <summary>
        /// FUNCTION 3: n_EA_E_and_n_EB_E2p_AB_E 
        /// Custom sphere is specificed
        /// </summary>
        public double[] n_EA_E_and_n_EB_E2p_AB_E(double[] n_EA_E, double[] n_EB_E, double z_EA, double z_EB, double a)
        {
            return n_EA_E_and_n_EB_E2p_AB_E(n_EA_E, n_EB_E, z_EA, z_EB, a, 0.0);
        }
        /// <summary>
        /// FUNCTION 3: n_EA_E_and_n_EB_E2p_AB_E 
        /// WGS-84 ellipsoid is used
        /// </summary>
        public double[] n_EA_E_and_n_EB_E2p_AB_E(double[] n_EA_E, double[] n_EB_E, double z_EA = 0.0, double z_EB = 0.0)
        {
            var a = 6378137.0; // the equatorial radius of the Earth-ellipsoid
            var f = 1 / 298.257223563; // the flattening of the Earth-ellipsoid
            return n_EA_E_and_n_EB_E2p_AB_E(n_EA_E, n_EB_E, z_EA, z_EB, a, f);
        }

        /// <summary>
        ///   FUNCTION 4: n_EA_E_and_p_AB_E2n_EB_E 
        ///   From position A and delta, finds position B.
        ///    n_EB_E      = n_EA_E_and_p_AB_E2n_EB_E(n_EA_E,p_AB_E)
        ///   [n_EB_E,z_EB] = n_EA_E_and_p_AB_E2n_EB_E(n_EA_E,p_AB_E)
        ///   The n-vector for position A (n_EA_E) and the position-vector from position 
        ///   A to position B (p_AB_E) are given. The output is the n-vector of position 
        ///   B (n_EB_E) and depth of B (z_EB).
        ///   The calculation is exact, taking the ellipsity of the Earth into account.
        ///   It is also nonsingular as both n-vector and p-vector are nonsingular
        ///   (except for the center of the Earth).
        ///   The default ellipsoid model used is WGS-84, but other ellipsoids (or spheres)
        ///   might be specified.
        ///
        ///   [n_EB_E,z_EB] = n_EA_E_and_p_AB_E2n_EB_E(n_EA_E,p_AB_E,z_EA) Depth of A, z_EA, 
        ///   is also specified, z_EA = 0 is used when not spefified.
        ///
        ///   [n_EB_E,z_EB] = n_EA_E_and_p_AB_E2n_EB_E(n_EA_E,p_AB_E,z_EA,a)
        ///   Spherical Earth with radius a is used instead of WGS-84.
        ///
        ///   [n_EB_E,z_EB] = n_EA_E_and_p_AB_E2n_EB_E(n_EA_E,p_AB_E,z_EA,a,f)
        ///   Ellipsoidal Earth model with semi-major axis a and flattening f is used 
        ///   instead of WGS-84.
        ///
        ///   IN: 
        ///   n_EA_E:  [no unit] n-vector of position A, decomposed in E (3x1 vector).
        ///   p_AB_E:  [m]       Position vector from A to B, decomposed in E (3x1 vector).
        ///   z_EA:    [m]       (Optional, assumed to be zero if not given) Depth of system A, 
        ///                      relative to the ellipsoid (z_EA = -height).
        ///   a:       [m]       (Optional) Semi-major axis of the Earth ellipsoid
        ///   f:       [no unit] (Optional) Flattening of the Earth ellipsoid
        ///
        ///   OUT:
        ///   n_EB_E:  [no unit] n-vector of position B, decomposed in E (3x1 vector).
        ///   z_EB:    [m]       Depth of system B, relative to the ellipsoid (z_EB = -height).
        ///
        ///   The function also accepts vectorized form, i.e. n_EA_E and p_AB_E are 3xn matrixes, 
        ///   z_EA and z_EB are 1xn vectors and n_EB_E is a 3xn matrix.
        ///
        ///   See also n_EA_E_and_n_EB_E2p_AB_E, p_EB_E2n_EB_E, n_EB_E2p_EB_E.        
        /// </summary>
        public Tuple<double[], double> n_EA_E_and_p_AB_E2n_EB_E(double[] n_EA_E,double[] p_AB_E,double z_EA, double a, double f)
        {
            // Function 2. in Section 5.4 in Gade (2010):
            var p_EA_E = n_EB_E2p_EB_E(n_EA_E,z_EA, a, f);
            var p_EB_E= Utilities.VecAdd(p_EA_E, p_AB_E);
            return p_EB_E2n_EB_E(p_EB_E,a, f);
        }

        /// <summary>
        /// FUNCTION 4: n_EA_E_and_p_AB_E2n_EB_E 
        /// Custom sphere is specificed
        /// </summary>
        public Tuple<double[], double> n_EA_E_and_p_AB_E2n_EB_E(double[] n_EA_E, double[] p_AB_E, double z_EA, double a)
        {
            return  n_EA_E_and_p_AB_E2n_EB_E(n_EA_E, p_AB_E, z_EA, a, 0.0);
        }

        /// <summary>
        /// FUNCTION 4: n_EA_E_and_p_AB_E2n_EB_E 
        /// WGS-84 ellipsoid is used
        /// </summary>
        public Tuple<double[], double> n_EA_E_and_p_AB_E2n_EB_E(double[] n_EA_E, double[] p_AB_E, double z_EA = 0.0)
        {
            var a = 6378137.0; // the equatorial radius of the Earth-ellipsoid  
            var f = 1.0 / 298.257223563; // the flattening of the Earth-ellipsoid
            return n_EA_E_and_p_AB_E2n_EB_E(n_EA_E, p_AB_E, z_EA, a, f);
        }

        /// <summary>
        ///   FUNCTION 5: n_EB_E2p_EB_E
        ///   Converts n-vector to Cartesian position vector in meters
        ///   p_EB_E = n_EB_E2p_EB_E(n_EB_E)
        ///   The position of B (typically body) relative to E (typically Earth) is
        ///   given into this function as n-vector, n_EB_E. The function converts
        ///   to cartesian position vector ("ECEF-vector"), p_EB_E, in meters.
        ///   The calculation is exact, taking the ellipsity of the Earth into account.
        ///   It is also nonsingular as both n-vector and p-vector are nonsingular
        ///   (except for the center of the Earth).
        ///   The default ellipsoid model used is WGS-84, but other ellipsoids (or spheres)
        ///   might be specified.
        ///
        ///   p_EB_E = n_EB_E2p_EB_E(n_EB_E,z_EB) Depth of B, z_EB, is also specified,
        ///   z_EB = 0 is used when not specified.
        ///
        ///   p_EB_E = n_EB_E2p_EB_E(n_EB_E,z_EB,a) Spherical Earth with radius a is
        ///   used instead of WGS-84.
        ///
        ///   p_EB_E = n_EB_E2p_EB_E(n_EB_E,z_EB,a,f) Ellipsoidal Earth model with
        ///   semi-major axis a and flattening f is used instead of WGS-84.
        ///
        ///
        ///   IN:
        ///   n_EB_E:  [no unit] n-vector of position B, decomposed in E (3x1 vector).
        ///   z_EB:    [m]       (Optional, assumed to be zero if not given) Depth of system B,
        ///                      relative to the ellipsoid (z_EB = -height)
        ///   a:       [m]       (Optional) Semi-major axis of the Earth ellipsoid
        ///   f:       [no unit] (Optional) Flattening of the Earth ellipsoid
        ///
        ///   OUT:
        ///   p_EB_E:  [m]       Cartesian position vector from E to B, decomposed in E (3x1 vector).
        ///
        ///   The function also accepts vectorized form, i.e. n_EB_E is a 3xn matrix, z_EB is
        ///   a 1xn vector and p_EB_E is a 3xn matrix.
        ///
        ///   See also p_EB_E2n_EB_E, n_EA_E_and_p_AB_E2n_EB_E, n_EA_E_and_n_EB_E2p_AB_E.
        /// </summary>
        public double[] n_EB_E2p_EB_E(double[] n_EB_E1, double z_EB, double a, double f)
        {
            var n_EB_E = unit(Utilities.MatMul(R_Ee, n_EB_E1));  // Ensures unit length. R_Ee selects correct E-axes, see R_Ee.m for details.
            // Note: In code where the norm of the input n_EB_E is guaranteed to be 1,
            // the use of the unit-function can be removed, to gain some speed.

            // semi-minor axis:
            var b = a * (1.0 - f);
            var tmp = Math.Pow(1.0 - f, 2);
            // The following code implements equation (22) in Gade (2010):
            var denominator = Math.Sqrt(Math.Pow(n_EB_E[0], 2) + Math.Pow(n_EB_E[1], 2) / tmp + Math.Pow(n_EB_E[2], 2) / tmp);
            // We first calculate the position at the origin of coordinate system L,
            // which has the same n-vector as B (n_EL_E = n_EB_E),
            // but lies at the surface of the Earth (z_EL = 0).
            var p_EL_E = new double[3] { 
                b / denominator * n_EB_E[0], 
                b / denominator * n_EB_E[1] / tmp, 
                b / denominator * n_EB_E[2] / tmp };
            // (The factor ellipsoid_semiaxis_Ex./denominator.* is put inside to make it work on vectorized form)
            var p_EB_E = Utilities.MatMul(
                Utilities.Transpose(R_Ee), 
                new double[3] { 
                    p_EL_E[0] - n_EB_E[0] * z_EB, 
                    p_EL_E[1] - n_EB_E[1] * z_EB, 
                    p_EL_E[2] - n_EB_E[2] * z_EB });
            return p_EB_E;
        }

        /// <summary>
        /// FUNCTION 5: n_EB_E2p_EB_E
        // WGS-84 ellipsoid is used
        /// </summary>
        public double[] n_EB_E2p_EB_E(double[] n_EB_E, double z_EB = 0.0)
        {
            var a = 6378137.0; // the equatorial radius of the Earth-ellipsoid
            var f = 1.0 / 298.257223563; // the flattening of the Earth-ellipsoid
            return n_EB_E2p_EB_E(n_EB_E, z_EB, a, f);
        }

        /// <summary>
        /// FUNCTION 5: n_EB_E2p_EB_E
        /// Custom sphere is specified: 
        /// f == 0
        /// </summary>
        public double[] n_EB_E2p_EB_E(double[] n_EB_E, double z_EB, double a)
        {
            return n_EB_E2p_EB_E(n_EB_E, z_EB, a, 0.0);
        }
      
        /// <summary>
        ///   FUNCTION 6: p_EB_E2n_EB_E
        ///   Converts Cartesian position vector in meters to n-vector.
        ///   [n_EB_E,z_EB] = p_EB_E2n_EB_E(p_EB_E)
        ///   The position of B (typically body) relative to E (typically Earth) is
        ///   given into this function as cartesian position vector p_EB_E, in meters
        ///   ("ECEF-vector"). The function converts to n-vector, n_EB_E and its
        ///   depth, z_EB.
        ///   The calculation is exact, taking the ellipsity of the Earth into account.
        ///   It is also nonsingular as both n-vector and p-vector are nonsingular
        ///   (except for the center of the Earth).
        ///   The default ellipsoid model used is WGS-84, but other ellipsoids (or spheres)
        ///   might be specified.
        ///
        ///   [n_EB_E,z_EB] = p_EB_E2n_EB_E(p_EB_E,a) Spherical Earth with radius a is
        ///   used instead of WGS-84.
        ///
        ///   [n_EB_E,z_EB] = p_EB_E2n_EB_E(p_EB_E,a,f) Ellipsoidal Earth model with
        ///   semi-major axis a and flattening f is used instead of WGS-84.
        ///
        ///   IN:
        ///   p_EB_E: [m]       Cartesian position vector from E to B, decomposed in E (3x1 vector).
        ///   a:      [m]       (Optional) Semi-major axis of the Earth ellipsoid
        ///   f:      [no unit] (Optional) Flattening of the Earth ellipsoid
        ///
        ///   OUT:
        ///   n_EB_E: [no unit] n-vector  representation of position B, decomposed in E (3x1 vector).
        ///   z_EB:   [m]       Depth of system B relative to the ellipsoid (z_EB = -height).
        ///
        ///
        ///   The function also accepts vectorized form, i.e. p_EB_E is a 3xn matrix,
        ///   n_EB_E is a 3xn matrix and z_EB is a 1xn vector.
        ///
        ///   See also n_EB_E2p_EB_E, n_EA_E_and_p_AB_E2n_EB_E, n_EA_E_and_n_EB_E2p_AB_E.
        /// </summary>
        public Tuple<double[], double> p_EB_E2n_EB_E(double[] p_EB_E1, double a, double f)
        {
            var p_EB_E = Utilities.MatMul(R_Ee, p_EB_E1); //  Selects correct E-axes
            // e_2 = eccentricity^2
            var e_2 = 2 * f - Math.Pow(f, 2); // = 1-b^2/a^2;
            
            // The following code implements equation (23) from Gade (2010):
            var R_2 = Math.Pow(p_EB_E[1], 2) + Math.Pow(p_EB_E[2], 2);
            var R = Math.Sqrt(R_2); // R = component of p_EB_E in the equatorial plane

            var p = R_2 / Math.Pow(a, 2);
            var q = (1 - e_2) / Math.Pow(a, 2) * Math.Pow(p_EB_E[0], 2);
            var r = (p + q - Math.Pow(e_2, 2)) / 6.0;

            var s = Math.Pow(e_2, 2) * p * q / (4.0 * Math.Pow(r, 3));
            var t = Math.Pow((1 + s + Math.Sqrt(s * (2 + s))), 1.0 / 3.0);
            var u = r * (1 + t + 1 / t);
            var v = Math.Sqrt(Math.Pow(u, 2) + Math.Pow(e_2, 2) * q);

            var w = e_2 * (u + v - q) /(2.0 * v);
            var k = Math.Sqrt(u + v + Math.Pow(w, 2)) - w;
            var d = k * R / (k + e_2);

            // Calculate height:
            var height = (k + e_2 - 1) / k * Math.Sqrt(Math.Pow(d, 2) + Math.Pow(p_EB_E[0], 2));
            var temp = 1 / Math.Sqrt(Math.Pow(d, 2) + Math.Pow(p_EB_E[0], 2));

            var n_EB_E_x = temp * p_EB_E[0];
            var n_EB_E_y = temp * k / (k + e_2) * p_EB_E[1];
            var n_EB_E_z = temp * k / (k + e_2) * p_EB_E[2];

            var n_EB_E = Utilities.MatMul(Utilities.Transpose(R_Ee), new double[3] { n_EB_E_x, n_EB_E_y, n_EB_E_z });             
            return new Tuple<double[], double>(unit(n_EB_E),  -height);
        }

        /// <summary>
        /// FUNCTION 6: p_EB_E2n_EB_E
        /// WGS-84 ellipsoid is used.
        /// </summary>
        public Tuple<double[], double> p_EB_E2n_EB_E(double[] p_EB_E1)
        {
            var a = 6378137.0; // the equatorial radius of the Earth-ellipsoid
            var f = 1 / 298.257223563; // the flattening of the Earth-ellipsoid
            return p_EB_E2n_EB_E(p_EB_E1, a, f);
        }

        /// <summary>
        /// FUNCTION 6: p_EB_E2n_EB_E
        /// Custom sphere is specified, i.e., f == 0.
        /// </summary>
        public Tuple<double[], double> p_EB_E2n_EB_E(double[] p_EB_E1, double a)
        {
            return p_EB_E2n_EB_E(p_EB_E1, a, 0.0);
        }

        /// <summary>
        /// FYNCTION 7: R_EN2n_E 
        /// Finds n-vector from R_EN.
        ///   n_E = R_EN2n_E(R_EN) 
        ///   n-vector is found from the rotation matrix (direction cosine matrix)
        ///   R_EN.
        /// 
        ///   IN:
        ///   R_EN:  [no unit] Rotation matrix (direction cosine matrix)
        ///
        ///   OUT:
        ///   n_E:   [no unit] n-vector decomposed in E (3x1 vector)
        ///
        ///   See also n_E2R_EN, R_EL2n_E, n_E_and_wa2R_EL.
        /// </summary>
        public double[] R_EN2n_E(double[,] R_EN)
        {
            // n-vector equals minus the last column of R_EL and R_EN, see Section 5.5
            // in Gade (2010)
            return Utilities.MatMul(R_EN, new double[3] { 0, 0, -1 });
        }

        /// <summary>
        /// FUNCTION 8: n_E2R_EN 
        ///   Finds the rotation matrix R_EN from n-vector.
        ///   R_EN = n_E2R_EN(n_E) 
        ///   The rotation matrix (direction cosine matrix) R_EN is calculated based
        ///   on n-vector (n_E).
        ///
        ///   IN:
        ///   n_E:   [no unit] n-vector decomposed in E (3x1 vector)
        ///
        ///   OUT:
        ///   R_EN:  [no unit] The resulting rotation matrix (direction cosine matrix)
        ///
        ///   See also R_EN2n_E, n_E_and_wa2R_EL, R_EL2n_E.
        /// </summary>
        public double[,] n_E2R_EN(double[] n_E1)
        {
            var n_E = unit(Utilities.MatMul(R_Ee, n_E1)); // Ensures unit length. R_Ee selects correct E-axes, see R_Ee.m for details.
            // Note: In code where the norm of the input n_EB_E is guaranteed to be 1,
            // the use of the unit-function can be removed, to gain some speed.

            // CALCULATIONS:

            // N coordinate frame (North-East-Down) is defined in Table 2 in Gade (2010)

            // R_EN is constructed by the following three column vectors: The x, y and z
            // basis vectors (axes) of N, each decomposed in E.

            // Find z-axis of N (Nz):
            var Nz_E = Utilities.VecMul(-1.0, n_E); // z-axis of N (down) points opposite to n-vector

            // Find y-axis of N (East)(remember that N is singular at Poles)
            // Equation (9) in Gade (2010):
            var Ny_E_direction = Utilities.Cross(new double[3] { 1, 0, 0 }, n_E); // Ny points perpendicular to the plane
            // formed by n-vector and Earth's spin axis 
            var Ny_E = (Utilities.Norm(Ny_E_direction) != 0.0) ? 
                unit(Ny_E_direction) : new double[3] {0,1,0};

            // Find x-axis of N (North):
            var Nx_E = Utilities.Cross(Ny_E, Nz_E); // Final axis found by right hand rule

            // Form R_EN from the unit vectors:
            // JIV TODO check orientation
            var R_EN = Utilities.MatMul(
                Utilities.Transpose(R_Ee), 
                new double[3,3] { 
                    {Nx_E[0], Ny_E[0],Nz_E[0]},
                    {Nx_E[1], Ny_E[1],Nz_E[1]},
                    {Nx_E[2], Ny_E[2],Nz_E[2]}}); // R_Ee selects correct E-axes, see R_Ee.m for details
            return R_EN;
        }

        /// <summary>
        /// FUNCTION 9: R_EL2n_E
        ///   R_EL2n_E Finds n-vector from R_EL.
        ///   n_E = R_EL2n_E(R_EL) 
        ///   n-vector is found from the rotation matrix (direction cosine matrix)
        ///   R_EL.
        /// 
        ///   IN:
        ///   R_EL:  [no unit] Rotation matrix (direction cosine matrix)
        ///
        ///   OUT:
        ///   n_E:   [no unit] n-vector decomposed in E (3x1 vector)
        ///
        ///   See also R_EN2n_E, n_E_and_wa2R_EL, n_E2R_EN
        /// </summary>
        public double[] R_EL2n_E(double[,] R_EL)
        { 
            // n-vector equals minus the last column of R_EL and R_EN, see Section 5.5
            // in Gade (2010)
            return Utilities.MatMul(R_EL, new double[3] { 0, 0, -1 });        
        }

        /// <summary>
        ///   FUNCTION 10: n_E_and_wa2R_EL 
        ///   Finds R_EL from n-vector and wander azimuth angle.
        ///   R_EL = n_E_and_wa2R_EL(n_E,wander_azimuth) 
        ///   Calculates the rotation matrix (direction cosine matrix) R_EL using
        ///   n-vector (n_E) and the wander azimuth angle.
        ///   When wander_azimuth=0, we have that N=L (See Table 2 in Gade (2010) for
        ///   details)
        ///
        ///   IN: 
        ///   n_E:        [no unit] n-vector decomposed in E (3x1 vector)
        ///   wander_azimuth: [rad] The angle between L's x-axis and north, pos about L's z-axis
        ///
        ///   OUT:
        ///   R_EL:       [no unit] The resulting rotation matrix (3x3)
        ///
        ///   See also R_EL2n_E, R_EN2n_E, n_E2R_EN.
        /// </summary>
        public double[,] n_E_and_wa2R_EL(double[] n_E, double wander_azimuth)
        {
            var latlon = n_E2lat_long(n_E);
            var lat = latlon[0];
            var lon = latlon[1];
            // Longitude, -latitude, and wander azimuth are the x-y-z Euler angles (about
            // new axes) for R_EL. See also the second paragraph of Section 5.2 in Gade (2010):
            var R_EL = Utilities.MatMul(Utilities.Transpose(R_Ee), xyz2R(lon, -lat, wander_azimuth)); // R_Ee selects correct E-axes, see R_Ee.m for details
            return R_EL;
        }

        /// <summary>
        ///   FUNCTION 11: xyz2R 
        ///   Creates a rotation matrix from 3 angles about new axes in the xyz order. 
        ///   R_AB = xyz2R(x,y,z) 
        ///   The rotation matrix R_AB is created based on 3 angles x,y,z about new
        ///   axes (intrinsic) in the order x-y-z. The angles (called Euler angles or
        ///   Tait–Bryan angles) are defined by the following procedure of successive
        ///   rotations:
        ///   Given two arbitrary coordinate frames A and B. Consider a temporary frame 
        ///   T that initially coincides with A. In order to make T align with B, we 
        ///   first rotate T an angle x about its x-axis (common axis for both A and T). 
        ///   Secondly, T is rotated an angle y about the NEW y-axis of T. Finally, T 
        ///   is rotated an angle z about its NEWEST z-axis. The final orientation of 
        ///   T now coincides with the orientation of B.
        ///
        ///   The signs of the angles are given by the directions of the axes and the 
        ///   right hand rule.
        ///
        ///   IN: 
        ///   x,y,z [rad]	    Angles of rotation about new axes.
        ///
        ///   OUT:
        ///   R_AB  [no unit]	3x3 rotation matrix (direction cosine matrix) such that the 
        ///                   relation between a vector v decomposed in A and B is 
        ///                   given by: v_A = R_AB * v_B
        /// 
        ///   See also R2xyz, zyx2R, R2zyx.
        /// </summary>
        public double[,] xyz2R(double x, double y, double z)
        {
            var sx = Math.Sin(x);
            var cx = Math.Cos(x);
            var sy = Math.Sin(y);
            var cy = Math.Cos(y);
            var sz = Math.Sin(z);
            var cz = Math.Cos(z);
            return new double[3, 3] {
                    { cy*cz, -cy*sz, sy }, 
                    { sy*sx*cz + cx*sz, -sy*sx*sz+cx*cz, -cy*sx }, 
                    { -sy*cx*cz+sx*sz, sy*cx*sz+sx*cz, cy*cx } 
                };
        }

        /// <summary>
        ///   FUNCTION 12: R2xyz 
        ///   Three angles about new axes in the xyz order are found from a rotation matrix. 
        ///   [x,y,z] = R2xyz(R_AB) 
        ///   3 angles x,y,z about new axes (intrinsic) in the order x-y-z are found
        ///   from the rotation matrix R_AB. The angles (called Euler angles or
        ///   Tait–Bryan angles) are defined by the following procedure of successive
        ///   rotations:
        ///   Given two arbitrary coordinate frames A and B. Consider a temporary frame 
        ///   T that initially coincides with A. In order to make T align with B, we 
        ///   first rotate T an angle x about its x-axis (common axis for both A and T). 
        ///   Secondly, T is rotated an angle y about the NEW y-axis of T. Finally, T 
        ///   is rotated an angle z about its NEWEST z-axis. The final orientation of 
        ///   T now coincides with the orientation of B.
        ///
        ///   The signs of the angles are given by the directions of the axes and the 
        ///   right hand rule.
        ///
        ///   IN: 
        ///   R_AB  [no unit]	3x3 rotation matrix (direction cosine matrix) such that the 
        ///                   relation between a vector v decomposed in A and B is 
        ///                   given by: v_A = R_AB * v_B
        ///
        ///   OUT: 
        ///   x,y,z [rad]	    Angles of rotation about new axes.
        ///
        ///   See also xyz2R, R2zyx, zyx2R.
        /// </summary>
        /// <param name="R_AB"></param>
        public double[] R2xyz(double[,] R_AB)
        {
            // atan2: [-pi pi]
            var z = Math.Atan2(-R_AB[0, 1], R_AB[0, 0]);
            var x = Math.Atan2(-R_AB[1, 2], R_AB[2, 2]);

            var sin_y=R_AB[0,2];

            // cos_y is based on as many elements as possible, to average out
            // numerical errors. It is selected as the positive square root since
            // y: [-pi/2 pi/2]
            var cos_y = Math.Sqrt((Math.Pow(R_AB[0, 0], 2) + Math.Pow(R_AB[0, 1], 2) + Math.Pow(R_AB[1, 2], 2) + Math.Pow(R_AB[2, 2], 2)) / 2.0);

            var y = Math.Atan2(sin_y, cos_y);
            return new double[3] { x, y, z };
        }

        /// <summary>
        ///   FUNCTION 13: zyx2R 
        ///   Creates a rotation matrix from 3 angles about new axes in the zyx order. 
        ///   R_AB = zyx2R(z,y,x) 
        ///   The rotation matrix R_AB is created based on 3 angles z,y,x about new
        ///   axes (intrinsic) in the order z-y-x. The angles (called Euler angles or
        ///   Tait–Bryan angles) are defined by the following procedure of successive
        ///   rotations:
        ///   Given two arbitrary coordinate frames A and B. Consider a temporary frame 
        ///   T that initially coincides with A. In order to make T align with B, we 
        ///   first rotate T an angle z about its z-axis (common axis for both A and T). 
        ///   Secondly, T is rotated an angle y about the NEW y-axis of T. Finally, T 
        ///   is rotated an angle x about its NEWEST x-axis. The final orientation of 
        ///   T now coincides with the orientation of B.
        ///
        ///   The signs of the angles are given by the directions of the axes and the 
        ///   right hand rule.
        ///
        ///   Note that if A is a north-east-down frame and B is a body frame, we 
        ///   have that z=yaw, y=pitch and x=roll. 
        ///
        ///   IN: 
        ///   z,y,x [rad]	    Angles of rotation about new axes.
        ///
        ///   OUT:
        ///   R_AB  [no unit]	3x3 rotation matrix (direction cosine matrix) such that the 
        ///                   relation between a vector v decomposed in A and B is 
        ///                   given by: v_A = R_AB * v_B
        /// 
        ///   See also R2zyx, xyz2R, R2xyz.
        /// </summary>
        public double[,] zyx2R(double z, double y, double x)
        {
            var cz = Math.Cos(z); var sz = Math.Sin(z);
            var cy = Math.Cos(y); var sy = Math.Sin(y);
            var cx = Math.Cos(x); var sx = Math.Sin(x);
            var R_AB = new double[3, 3] 
                {   {cz*cy, -sz*cx+cz*sy*sx,  sz*sx+cz*sy*cx }, 
                    {sz*cy,  cz*cx+sz*sy*sx, -cz*sx+sz*sy*cx},  
                    {-sy,       cy*sx,            cy*cx}};
            return R_AB;
        }

        /// <summary>
        ///   FUNCTION 14: R2zyx 
        ///   Three angles about new axes in the zyx order are found from a rotation matrix. 
        ///   [z,y,x] = R2zyx(R_AB) 
        ///   3 angles z,y,x about new axes (intrinsic) in the order z-y-x are found
        ///   from the rotation matrix R_AB. The angles (called Euler angles or
        ///   Tait–Bryan angles) are defined by the following procedure of successive
        ///   rotations:
        ///   Given two arbitrary coordinate frames A and B. Consider a temporary frame 
        ///   T that initially coincides with A. In order to make T align with B, we 
        ///   first rotate T an angle z about its z-axis (common axis for both A and T). 
        ///   Secondly, T is rotated an angle y about the NEW y-axis of T. Finally, T 
        ///   is rotated an angle x about its NEWEST x-axis. The final orientation of 
        ///   T now coincides with the orientation of B.
        ///
        ///   The signs of the angles are given by the directions of the axes and the 
        ///   right hand rule.
        ///
        ///   Note that if A is a north-east-down frame and B is a body frame, we 
        ///   have that z=yaw, y=pitch and x=roll. 
        ///
        ///   IN: 
        ///   R_AB  [no unit]	3x3 rotation matrix (direction cosine matrix) such that the 
        ///                   relation between a vector v decomposed in A and B is 
        ///                   given by: v_A = R_AB * v_B
        ///
        ///   OUT: 
        ///   z,y,x [rad]	    Angles of rotation about new axes.
        ///
        ///   See also zyx2R, xyz2R, R2xyz.
        /// </summary>
        public double[] R2zyx(double[,] R_AB)
        {
            // atan2: [-pi pi]
            var z = Math.Atan2(R_AB[1,0],R_AB[0,0]);
            var x = Math.Atan2(R_AB[2,1],R_AB[2,2]);

            var sin_y = -R_AB[2,0];

            // cos_y is based on as many elements as possible, to average out
            // numerical errors. It is selected as the positive square root since
            // y: [-pi/2 pi/2]
            var cos_y = Math.Sqrt((Math.Pow(R_AB[0,0],2)+Math.Pow(R_AB[1,0],2)+Math.Pow(R_AB[2,1],2)+Math.Pow(R_AB[2,2],2))/2.0);

            var y = Math.Atan2(sin_y,cos_y);
            return new double[3] { z, y, x };
        }

        /// <summary>
        ///   FUNCTION 15: unit 
        ///   Makes input vector unit length, i.e. norm==1.
        ///   unit_vector = unit(vector) 
        ///   makes the general 1xm vector a unit vector (norm==1). 
        /// </summary>
        public double[] unit(double[] v)
        {
            // Find vector norm
            var current_norm = Utilities.Norm(v);
            return new double[3] { v[0] / current_norm, v[1] / current_norm, v[2] / current_norm };
        }

        /// <summary>
        ///   FUNCTION 16: rad 
        ///   Converts angle in degrees to radians.
        ///   rad_angle = rad(deg_angle)
        /// 
        ///   IN:
        ///   angle in degrees
        ///
        ///   OUT:
        ///   angle in radians
        ///
        ///   See also deg.
        /// </summary>
        public double rad(double deg_angle)
        { 
            return deg_angle *  piover180;
        }

        /// <summary>
        ///   FUNCTION 17: deg 
        ///   Converts angle in radians to degrees.
        ///   deg_angle = deg(rad_angle)
        /// 
        ///   IN:
        ///   angle in radians
        ///
        ///   OUT:
        ///   angle in degrees
        ///
        ///       See also rad.
        /// </summary>
        public double deg(double rad_angle)
        {
            return rad_angle / piover180;
        }

        /// <summary>
        /// R_Ee 
        /// Selects axes of the coordinate frame E.
        ///   This file controls the axes of the coordinate frame E (Earth-Centred, 
        ///   Earth-Fixed, ECEF) used by the other files in this library
        ///
        ///   There are two choices of E-axes that are described in Table 2 in Gade
        ///   (2010):
        ///
        ///  * e: z-axis points to the North Pole and x-axis points to the point where
        ///       latitude = longitude = 0. This choice is very common in many fields.
        ///
        ///  * E: x-axis points to the North Pole, y-axis points towards longitude +90deg 
        ///       (east) and latitude = 0. This choice of axis directions ensures
        ///       that at zero latitude and longitude, N (North-East-Down) has the
        ///       same orientation as E. If roll/pitch/yaw are zero, also B (Body,
        ///       forward, starboard, down) has this orientation. In this manner, the
        ///       axes of E is chosen to correspond with the axes of N and B.
        ///
        ///   Based on this we get:
        ///   R_Ee=[0 0 1
        ///         0 1 0
        ///        -1 0 0]
        ///
        ///   The above R_Ee should be returned from this file when using z-axis to the 
        ///   North pole (which is most common). When using x-axis to the North 
        ///   pole, R_Ee should be set to I (identity matrix) (since the files in 
        ///   this library are originally written for this option).
        ///
        ///   Reference:
        ///   K Gade (2010): A Nonsingular Horizontal Position Representation, 
        ///   The Journal of Navigation, Volume 63, Issue 03, pp 395-417, July 2010. 
        ///   (www.navlab.net/Publications/A_Nonsingular_Horizontal_Position_Representation.pdf)
        /// </summary>
        public double[,] R_Ee { set; get; }

        public static double[,] R_Ee_Northpole_X
        {
            get
            {
                return new double[3, 3] 
                { { 1, 0, 0 }, 
                  { 0, 1, 0 }, 
                  { 0, 0, 1 } };
            }
        }

        public static double[,] R_Ee_Northpole_Z
        {
            get 
            {
                return new double[3, 3] 
                { { 0, 0, 1 }, 
                  { 0, 1, 0 }, 
                  { -1, 0, 0 } };
            }
        }

    }
}
