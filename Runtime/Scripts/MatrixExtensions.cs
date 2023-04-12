using System.Collections.Generic;
using UnityEngine;

namespace cosmicpotato.sgl
{
    public class Scope
    {
        public static Scope identity = 
            new Scope(Vector3.zero, Quaternion.identity, Vector3.one);

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public Scope(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public Scope(Transform transform)
        {
            this.position = transform.position;
            this.rotation = transform.rotation;
            this.scale = transform.localScale;
        }

        public Scope(Scope other)
        {
            this.position = other.position;
            this.rotation = other.rotation;
            this.scale = other.scale;
        }

        public Scope Copy()
        {
            return new Scope(this.position, this.rotation, this.scale);
        }

        public Matrix4x4 GetMatrix()
        {
            return Matrix4x4.TRS(position, rotation, scale);
        }

        public void Translate(Vector3 translation)
        {
            position += rotation * Vector3.Scale(translation, scale);
        }

        public void SetTranslation(Vector3 translation)
        {
            position = translation;
        }

        public void Rotate(Vector3 eulerRot)
        {
            Rotate(Quaternion.Euler(eulerRot));
        }

        public void Rotate(Quaternion rotation)
        {
            //var mat = Matrix4x4.TRS(position, rotation, scale) * Matrix4x4.TRS(Vector3.zero, rotation, Vector3.one);
            float angle;
            Vector3 axis;
            rotation.ToAngleAxis(out angle, out axis);
            // scale the rotation properly
            axis = Vector3.Normalize(new Vector3(
                axis.x * scale.y * scale.z,
                axis.y * scale.x * scale.z,
                axis.z * scale.x * scale.y
                ));

            this.rotation *= Quaternion.AngleAxis(angle, axis);
        }

        public void SetRotation(Vector3 rot)
        {
            this.rotation = Quaternion.Euler(rot);
        }

        public void SetRotation(Quaternion rot)
        {
            this.rotation = rot;
        }

        public void Scale(Vector3 scale)
        {
            this.scale.Scale(scale);
        }

        public void SetScale(Vector3 scale)
        {
            this.scale = scale;
        }

        public Scope[] Subdivide(int divisions, Axis axis = 0)
        {
            Scope[] newScopes = new Scope[divisions];

            Vector3 scale;
            Vector3 dir;
            switch (axis)
            {
                case Axis.x:
                    scale = new Vector3(1.0f / (float)divisions, 1, 1);
                    dir = new Vector3(1, 0, 0);
                    break;
                case Axis.y:
                    scale = new Vector3(1, 1.0f / (float)divisions, 1);
                    dir = new Vector3(0, 1, 0);
                    break;
                case Axis.z:
                    scale = new Vector3(1, 1, 1.0f / (float)divisions);
                    dir = new Vector3(0, 0, 1);
                    break;
                default:
                    scale = new Vector3(1.0f / (float)divisions, 1, 1);
                    dir = new Vector3(1, 0, 0);
                    break;
            }

            for (int i = 0; i < divisions; i++)
            {
                Scope m = this.Copy();
                float rel = 1.0f / (float)divisions;
                Vector3 t = (i * rel - .5f + rel / 2.0f) * dir;
                m.Translate(t);
                m.Scale(scale);
                newScopes[i] = m;
            }

            return newScopes;
        }
    }


    public struct ScopeBounds
    {
        public Vector3 x, y, z;
        public Vector3 offset;
    }

    public enum Axis
    {
        x, y, z
    }

    public static class MatrixExtensions
    {
        public static Quaternion GetRotation(this Matrix4x4 scope)
        {
            //Vector3 forward;
            //forward.x = scope.m02;
            //forward.y = scope.m12;
            //forward.z = scope.m22;

            //Vector3 upwards;
            //upwards.x = scope.m01;
            //upwards.y = scope.m11;
            //upwards.z = scope.m21;

            //return Quaternion.LookRotation(forward, upwards);

            return scope.rotation;
        }

        public static Vector3 GetPosition(this Matrix4x4 scope)
        {
            Vector3 position;
            position.x = scope.m03;
            position.y = scope.m13;
            position.z = scope.m23;
            return position;
        }

        public static Vector3 GetScale(this Matrix4x4 scope)
        {
            //Vector3 scale;
            //scale.x = new Vector4(scope.m00, scope.m10, scope.m20, scope.m30).magnitude;
            //scale.y = new Vector4(scope.m01, scope.m11, scope.m21, scope.m31).magnitude;
            //scale.z = new Vector4(scope.m02, scope.m12, scope.m22, scope.m32).magnitude;
            //return scale;

            return scope.lossyScale;
        }

        public static Matrix4x4 Translate(this Matrix4x4 scope, Vector3 translation)
        {
            return scope * Matrix4x4.TRS(translation, Quaternion.identity, Vector3.one);
        }

        public static Matrix4x4 Rotate(this Matrix4x4 scope, Vector3 eulerRot)
        {
            return scope * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(eulerRot), Vector3.one);
        }

        public static Matrix4x4 Rotate(this Matrix4x4 scope, Quaternion quat)
        {
            return scope * Matrix4x4.TRS(Vector3.zero, quat, Vector3.one);
        }

        public static Matrix4x4 Scale(this Matrix4x4 scope, Vector3 scale)
        {
            return scope * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);
        }

        public static Matrix4x4 SetScale(this Matrix4x4 scope, Vector3 scale)
        {
            return Matrix4x4.TRS(scope.GetPosition(), scope.GetRotation(), scale);
        }

        public static ScopeBounds GetBounds(this Matrix4x4 scope)
        {
            ScopeBounds sb;
            sb.offset = scope.GetPosition();
            var s = scope.Translate(-scope.GetPosition());
            sb.x = s.MultiplyPoint(new Vector3(1, 0, 0));
            sb.y = s.MultiplyPoint(new Vector3(0, 1, 0));
            sb.z = s.MultiplyPoint(new Vector3(0, 0, 1));
            return sb;
        }

        public static Matrix4x4 FromScopeBounds(ScopeBounds scopeBounds)
        {
            Vector3 to = scopeBounds.x + scopeBounds.offset;
            Matrix4x4 m = Matrix4x4.LookAt(scopeBounds.offset, to, scopeBounds.y);
            Vector3 scale = new Vector3(
                scopeBounds.x.magnitude, scopeBounds.y.magnitude, scopeBounds.z.magnitude);
            return m.SetScale(scale);
        }

        public static Matrix4x4[] SubdivideScope(this Matrix4x4 scope, int divisions, Axis axis = 0)
        {
            Matrix4x4[] newScopes = new Matrix4x4[divisions];
            ScopeBounds sb = scope.GetBounds();

            Vector3 scale;
            Vector3 dir;
            switch (axis)
            {
                case Axis.x:
                    scale = new Vector3(1.0f / (float)divisions, 1, 1);
                    dir = new Vector3(1, 0, 0);
                    break;
                case Axis.y:
                    scale = new Vector3(1, 1.0f / (float)divisions, 1);
                    dir = new Vector3(0, 1, 0);
                    break;
                case Axis.z:
                    scale = new Vector3(1, 1, 1.0f / (float)divisions);
                    dir = new Vector3(0, 0, 1);
                    break;
                default:
                    scale = new Vector3(1.0f / (float)divisions, 1, 1);
                    dir = new Vector3(1, 0, 0);
                    break;
            }

            for (int i = 0; i < divisions; i++)
            {
                Matrix4x4 m = scope;
                float rel = 1.0f / (float)divisions;
                Vector3 t = (i * rel - .5f + rel / 2.0f) * dir;
                m = m.Translate(t);
                newScopes[i] = m.Scale(scale);
            }

            return newScopes;
        }
    }

    public static class TransformExtensions
    {
        public static void FromMatrix(this Transform transform, Matrix4x4 scope)
        {
            transform.localScale = scope.GetScale();
            transform.localRotation = scope.GetRotation();
            transform.localPosition = scope.GetPosition();
        }

        public static void FromScope(this Transform transform, Scope scope)
        {
            transform.position = scope.position;
            transform.rotation = scope.rotation;
            transform.localScale = scope.scale;
        }
    }
}