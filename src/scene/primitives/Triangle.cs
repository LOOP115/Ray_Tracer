using System;

namespace RayTracer
{
    /// <summary>
    /// Class to represent a triangle in a scene represented by three vertices.
    /// </summary>
    public class Triangle : SceneEntity
    {
        private Vector3 v0, v1, v2;
        private Material material;

        /// <summary>
        /// Construct a triangle object given three vertices.
        /// </summary>
        /// <param name="v0">First vertex position</param>
        /// <param name="v1">Second vertex position</param>
        /// <param name="v2">Third vertex position</param>
        /// <param name="material">Material assigned to the triangle</param>
        public Triangle(Vector3 v0, Vector3 v1, Vector3 v2, Material material)
        {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.material = material;
        }

        /// <summary>
        /// Determine if a ray intersects with the triangle, and if so, return hit data.
        /// </summary>
        /// <param name="ray">Ray to check</param>
        /// <returns>Hit data (or null if no intersection)</returns>
        public RayHit Intersect(Ray ray)
        {
            // Write your code here...
            Vector3 u = this.v1 - this.v0;
            Vector3 v = this.v2 - this.v0;
            Vector3 normal = u.Cross(v);
            if (Math.Abs(ray.Direction.Dot(normal)) > 0.0001) {

                double d = -this.v0.Dot(normal);
                Vector3 origin = ray.Origin;
                double t = - (normal.Dot(ray.Origin) + d) / ray.Direction.Dot(normal);
                if (t < 0) return null;
                Vector3 position = origin + t * ray.Direction;

                double uu, uv, vv, wu, wv, D;
                uu = u.Dot(u);
                uv = u.Dot(v);
                vv = v.Dot(v);
                Vector3 w = position - v0;

                wu = w.Dot(u);
		        wv = w.Dot(v);
		        D = uv * uv  - uu * vv;

                double s = (uv * wv - vv * wu) / D;
                double k = (uv * wu - uu * wv) / D;

                // Intersection not in triangle.
                if (s < 0 || s > 1) return null; 
                if (k < 0 || (s + k) > 1) return null;

                return new RayHit(position, normal.Normalized(), ray.Direction, material);
            }
            return null;
        }

        /// <summary>
        /// The material of the triangle.
        /// </summary>
        public Material Material { get { return this.material; } }
    }

}
