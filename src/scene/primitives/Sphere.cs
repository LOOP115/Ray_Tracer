using System;

namespace RayTracer
{
    /// <summary>
    /// Class to represent an (infinite) plane in a scene.
    /// </summary>
    public class Sphere : SceneEntity
    {
        private Vector3 center;
        private double radius;
        private Material material;

        /// <summary>
        /// Construct a sphere given its center point and a radius.
        /// </summary>
        /// <param name="center">Center of the sphere</param>
        /// <param name="radius">Radius of the spher</param>
        /// <param name="material">Material assigned to the sphere</param>
        public Sphere(Vector3 center, double radius, Material material)
        {
            this.center = center;
            this.radius = radius;
            this.material = material;
        }

        /// <summary>
        /// Determine if a ray intersects with the sphere, and if so, return hit data.
        /// </summary>
        /// <param name="ray">Ray to check</param>
        /// <returns>Hit data (or null if no intersection)</returns>
        public RayHit Intersect(Ray ray)
        {
            // Write your code here...
            Vector3 p = ray.Origin;
            Vector3 u = ray.Direction;
            Vector3 v = p - this.center;
            double b = 2 * (v.Dot(u));
            double c = v.Dot(v) - radius * radius;
		    double disc = b*b - 4*c;

            if(disc < 0) return null;

		    double tMinus = (-b - Math.Sqrt(disc)) / 2;
		    double tPlus = (-b + Math.Sqrt(disc)) / 2;

		    if(tMinus < 0 && tPlus < 0) {
			    // Sphere is behind the camera.
			    return null;
		    }

            double t;
            Vector3 position;
            Vector3 normal;
            if(tMinus < 0 && tPlus > 0) {
                // Camera lies inside the sphere.
                t = tPlus;
                position = p + t * u;
                normal = this.center - position;
            } else {
                // Camera is in front of the sphere.
                t = tMinus;
                position = p + t * u;
                normal = position - this.center;
            }
            
            return new RayHit(position, normal.Normalized(), ray.Direction, material);
        }

        /// <summary>
        /// The material of the sphere.
        /// </summary>
        public Material Material { get { return this.material; } }
    }

}
