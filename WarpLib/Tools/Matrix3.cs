using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warp.Tools
{
    public class Matrix3
    {
        public float M11, M21, M31;
        public float M12, M22, M32;
        public float M13, M23, M33;

        public Matrix3()
        {
            M11 = 1;
            M21 = 0;
            M31 = 0;

            M12 = 0;
            M22 = 1;
            M32 = 0;

            M13 = 0;
            M23 = 0;
            M33 = 1;
        }

        public Matrix3(float m11, float m21, float m31,
                       float m12, float m22, float m32,
                       float m13, float m23, float m33)
        {
            M11 = m11;
            M21 = m21;
            M31 = m31;

            M12 = m12;
            M22 = m22;
            M32 = m32;

            M13 = m13;
            M23 = m23;
            M33 = m33;
        }

        public Matrix3 Transposed()
        {
            return new Matrix3(M11, M12, M13,
                               M21, M22, M23,
                               M31, M32, M33);
        }

        public static Matrix3 RotateX(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);

            return new Matrix3(1, 0, 0, 0, c, s, 0, -s, c);
        }

        public static Matrix3 RotateY(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);

            return new Matrix3(c, 0, -s, 0, 1, 0, s, 0, c);
        }

        public static Matrix3 RotateZ(float angle)
        {
            float c = (float)Math.Cos(angle);
            float s = (float)Math.Sin(angle);

            return new Matrix3(c, s, 0, -s, c, 0, 0, 0, 1);
        }

        public static Matrix3 Euler(float rot, float tilt, float psi)
        {
            float ca, sa, cb, sb, cg, sg;
            float cc, cs, sc, ss;

            ca = (float)Math.Cos(rot);
            cb = (float)Math.Cos(tilt);
            cg = (float)Math.Cos(psi);
            sa = (float)Math.Sin(rot);
            sb = (float)Math.Sin(tilt);
            sg = (float)Math.Sin(psi);
            cc = cb * ca;
            cs = cb * sa;
            sc = sb * ca;
            ss = sb * sa;

            Matrix3 A = new Matrix3();
            A.M11 = cg * cc - sg * sa;
            A.M12 = cg * cs + sg * ca;
            A.M13 = -cg * sb;
            A.M21 = -sg * cc - cg * sa;
            A.M22 = -sg * cs + cg * ca;
            A.M23 = sg * sb;
            A.M31 = sc;
            A.M32 = ss;
            A.M33 = cb;

            return A;
        }

        public static float3 EulerFromMatrix(Matrix3 a)
        {
            float alpha, beta, gamma;
            float abs_sb, sign_sb;

            abs_sb = (float)Math.Sqrt(a.M13 * a.M13 + a.M23 * a.M23);
            if (abs_sb > 16 * 1.192092896e-07f)
            {
                gamma = (float)Math.Atan2(a.M23, -a.M13);
                alpha = (float)Math.Atan2(a.M32, a.M31);
                if (Math.Abs((float)Math.Sin(gamma)) < 1.192092896e-07f)
                    sign_sb = Math.Sign(-a.M13 / Math.Cos(gamma));
                else
                    sign_sb = (Math.Sin(gamma) > 0) ? Math.Sign(a.M23) : -Math.Sign(a.M23);
                beta = (float)Math.Atan2(sign_sb * abs_sb, a.M33);
            }
            else
            {
                if (Math.Sign(a.M33) > 0)
                {
                    // Let's consider the matrix as a rotation around Z
                    alpha = 0;
                    beta = 0;
                    gamma = (float)Math.Atan2(-a.M21, a.M11);
                }
                else
                {
                    alpha = 0;
                    beta = (float)Math.PI;
                    gamma = (float)Math.Atan2(a.M21, -a.M11);
                }
            }

            return new float3(alpha, beta, gamma);
        }

        public static Matrix3 operator +(Matrix3 o1, Matrix3 o2)
        {
            return new Matrix3(o1.M11 + o2.M11, o1.M21 + o2.M21, o1.M31 + o2.M31,
                               o1.M12 + o2.M12, o1.M22 + o2.M22, o1.M32 + o2.M32,
                               o1.M13 + o2.M13, o1.M23 + o2.M23, o1.M33 + o2.M33);
        }

        public static Matrix3 operator -(Matrix3 o1, Matrix3 o2)
        {
            return new Matrix3(o1.M11 - o2.M11, o1.M21 - o2.M21, o1.M31 - o2.M31,
                               o1.M12 - o2.M12, o1.M22 - o2.M22, o1.M32 - o2.M32,
                               o1.M13 - o2.M13, o1.M23 - o2.M23, o1.M33 - o2.M33);
        }

        public static Matrix3 operator *(Matrix3 o1, Matrix3 o2)
        {
            return new Matrix3(o1.M11 * o2.M11 + o1.M12 * o2.M21 + o1.M13 * o2.M31, o1.M21 * o2.M11 + o1.M22 * o2.M21 + o1.M23 * o2.M31, o1.M31 * o2.M11 + o1.M32 * o2.M21 + o1.M33 * o2.M31,
                               o1.M11 * o2.M12 + o1.M12 * o2.M22 + o1.M13 * o2.M32, o1.M21 * o2.M12 + o1.M22 * o2.M22 + o1.M23 * o2.M32, o1.M31 * o2.M12 + o1.M32 * o2.M22 + o1.M33 * o2.M32,
                               o1.M11 * o2.M13 + o1.M12 * o2.M23 + o1.M13 * o2.M33, o1.M21 * o2.M13 + o1.M22 * o2.M23 + o1.M23 * o2.M33, o1.M31 * o2.M13 + o1.M32 * o2.M23 + o1.M33 * o2.M33);
        }

        public static float3 operator *(Matrix3 o1, float3 o2)
        {
            return new float3(o1.M11 * o2.X + o1.M12 * o2.Y + o1.M13 * o2.Z,
                              o1.M21 * o2.X + o1.M22 * o2.Y + o1.M23 * o2.Z,
                              o1.M31 * o2.X + o1.M32 * o2.Y + o1.M33 * o2.Z);
        }
    }
}
