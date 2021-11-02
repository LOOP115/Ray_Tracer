using System;
using System.Collections.Generic;

namespace RayTracer
{
    /// <summary>
    /// Class to represent a ray traced scene, including the objects,
    /// light sources, and associated rendering logic.
    /// </summary>
    public class Scene
    {
        private SceneOptions options;
        private ISet<SceneEntity> entities;
        private ISet<PointLight> lights;

        /// <summary>
        /// Construct a new scene with provided options.
        /// </summary>
        /// <param name="options">Options data</param>
        public Scene(SceneOptions options = new SceneOptions())
        {
            this.options = options;
            this.entities = new HashSet<SceneEntity>();
            this.lights = new HashSet<PointLight>();
        }

        /// <summary>
        /// Add an entity to the scene that should be rendered.
        /// </summary>
        /// <param name="entity">Entity object</param>
        public void AddEntity(SceneEntity entity)
        {
            this.entities.Add(entity);
        }

        /// <summary>
        /// Add a point light to the scene that should be computed.
        /// </summary>
        /// <param name="light">Light structure</param>
        public void AddPointLight(PointLight light)
        {
            this.lights.Add(light);
        }

        /// <summary>
        /// Render the scene to an output image. This is where the bulk
        /// of your ray tracing logic should go... though you may wish to
        /// break it down into multiple functions as it gets more complex!
        /// </summary>
        /// <param name="outputImage">Image to store render output</param>
        public void Render(Image outputImage)
        {
            // Begin writing your code here...
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            int maxDepth = 10;

            // Anti-aliasing
            int antiAliasN = options.AAMultiplier;
            double denom = Convert.ToDouble(antiAliasN * antiAliasN);

            // Custom camera
            Vector3 oAxis = new Vector3(0, 0, 1);
            Vector3 cam_pos = options.CameraPosition;
            Vector3 cam_axis = axisProcess(options.CameraAxis);
            double cam_angle = options.CameraAngle;

            for (int y = 0; y < outputImage.Height; y++)
            {
                for (int x = 0; x < outputImage.Width; x++)
                {
                    Color pixelColor = new Color(0, 0, 0);
                    double offsetY = 0;
                    double unit = 1.0f / (1.0f + (double)antiAliasN);
                    
                    for (int ny=1; ny<=antiAliasN; ny++)
                    {
                        offsetY += unit;
                        double offsetX = 0;
                        for (int nx=1; nx<=antiAliasN; nx++)
                        {
                            offsetX += unit;
                            Vector3 dir = camRayDir(x, y, outputImage, offsetX, offsetY, cam_pos, cam_axis, cam_angle, oAxis);
                            Ray ray = new Ray(cam_pos, dir);
                            pixelColor += getPixelColor(ray, ref maxDepth);
                        }
                    }
                    outputImage.SetPixel(x, y, pixelColor/denom);
                }
            }

            watch.Stop();
            Console.WriteLine($"Render Time: {watch.ElapsedMilliseconds} ms");   
        }

        // Adjust camera rays to each pixel.
        private Vector3 camRayDir(int x, int y, Image outputImage, double offsetX, double offsetY, Vector3 pos, Vector3 axis, double fov_angle, Vector3 axisO)
        {
            
            double fov = fov_angle * (Math.PI/180);
            double aspect_ratio = outputImage.Width / outputImage.Height;

            // Normalize the pixel space.
            double pixel_x = (x - pos.X + offsetX) / outputImage.Width;
            double pixel_y = (y - pos.Y + offsetY) / outputImage.Height;
            
            // Scale to (-1, 1).
            double x_pos = (pixel_x * 2) - 1;
            double y_pos = 1 - (pixel_y * 2);
            double z_pos = 1.0f + pos.Z;

            // Adjust orientation.
            x_pos = x_pos * Math.Tan(fov/2) * aspect_ratio;
            y_pos = y_pos * Math.Tan(fov/2);

            Vector3 dir = new Vector3(x_pos, y_pos, z_pos);
            Vector3 diff = (axis - axisO);
            dir += diff;
            return dir.Normalized();
        }

        // Calculate pixel color based on ray tracing.
        private Color fireRay(RayHit hit, SceneEntity entity, ref int maxDepth, int depth, bool transmit) 
        {
            Color c = new Color(0, 0, 0);
            Vector3 O = new Vector3(0, 0, 0);
            Material material = entity.Material;
            Vector3 bias = hit.Normal/100000;
            
            // Max depth reached.
            if (depth == maxDepth)
            {
                return c;
            }

            // Diffuse material.
            if ( material.Type == Material.MaterialType.Diffuse )
            {  
                foreach (PointLight light in this.lights)
                {
                    Vector3 l = (light.Position - hit.Position).Normalized();
                    if ( ((Math.Acos(hit.Normal.Dot(l)) * 180/Math.PI) <= 90) && !inShadow(hit.Position + bias, light) )
                    {
                        Color cm = material.Color;
                        c +=  cm * light.Color * (hit.Normal.Dot(l));
                    }
                }
                return c;
            }

            // Reflective material.
            if ( material.Type == Material.MaterialType.Reflective )
            {
                Vector3 reflectDir = hit.Incident - 2 * hit.Incident.Dot(hit.Normal) * hit.Normal;
                Vector3 reflectOrigin = hit.Position + reflectDir.Normalized()/100000;
                Ray reflectRay = new Ray(reflectOrigin, reflectDir.Normalized());
                RayHit reflectHit = null;
                double minHit = Double.MaxValue;
                SceneEntity hitEntity = null;
                foreach (SceneEntity ent in this.entities)
                {
                    RayHit potentialHit = ent.Intersect(reflectRay);
                    if (potentialHit != null && (potentialHit.Position - reflectOrigin).LengthSq() < minHit)
                    {
                        reflectHit = potentialHit;
                        minHit = (reflectHit.Position - reflectOrigin).LengthSq();
                        hitEntity = ent;
                    } 
                }
                if (reflectHit != null) 
                {
                    c += fireRay(reflectHit, hitEntity, ref maxDepth, depth+1, transmit);
                }  
            }

            // Refractive material.
            if ( material.Type == Material.MaterialType.Refractive )
            {  
                Color refractC = new Color(0, 0, 0);
                Color reflectC = new Color(0, 0, 0);
                double k = fresnel(hit.Incident, hit.Normal, material.RefractiveIndex, transmit);

                // Caculate refraction if not total reflecttion.
                if (k < 1)
                {
                    Vector3 refractDir = getRefractDir(hit.Incident, hit.Normal, material.RefractiveIndex, transmit);
                    Vector3 refractOrigin = hit.Position + refractDir.Normalized()/100000;
                    if (!transmit && entity.GetType() == typeof(RayTracer.Sphere)) 
                    {
                        transmit = true;
                    }
                    else
                    {
                        transmit = false;
                        
                    }
                    if (refractDir.X == O.X && refractDir.Y == O.Y && refractDir.Z == O.Z)
                    {
                        return c;
                    }
                    
                    Ray refractRay = new Ray(refractOrigin, refractDir.Normalized());
                    
                    RayHit refractHit = null;
                    double minHit = Double.MaxValue;
                    SceneEntity hitEntity = null;
                    foreach (SceneEntity ent in this.entities)
                    {
                        RayHit potentialHit = ent.Intersect(refractRay);
                        if (potentialHit != null && (potentialHit.Position - refractOrigin).LengthSq() < minHit)
                        {
                            refractHit = potentialHit;
                            minHit = (refractHit.Position - refractOrigin).LengthSq();
                            hitEntity = ent;
                        } 
                    }
                    if (refractHit != null) 
                    {
                        refractC = fireRay(refractHit, hitEntity, ref maxDepth, depth+1, transmit);
                        
                        // Compute Beer's law.
                        if (material.Color.R != 0 && material.Color.G != 0 && material.Color.B != 0)
                        {
                            Color absorbance = material.Color;
                            double absorbDistance = (refractHit.Position - refractOrigin).Length();
                            double absorbR = Math.Exp(-absorbance.R * absorbDistance);
                            double absorbG = Math.Exp(-absorbance.G * absorbDistance);
                            double absorbB = Math.Exp(-absorbance.B * absorbDistance);
                            Color absorb = new Color(absorbR, absorbG, absorbB);
                            refractC *= absorb;
                        }
                    }
                }

                // Calculate reflection.
                Vector3 reflectDir = hit.Incident - 2 * hit.Incident.Dot(hit.Normal) * hit.Normal;
                // Vector3 reflectOrigin = hit.Position + reflectDir.Normalized()/1000;
                Vector3 reflectOrigin;
                if (!transmit) 
                {
                    reflectOrigin = hit.Position - bias;
                }
                else
                {
                    reflectOrigin = hit.Position + bias;
                }

                Ray reflectRay = new Ray(reflectOrigin, reflectDir.Normalized());
                RayHit reflectHit = null;
                double minHit2 = Double.MaxValue;
                SceneEntity hitEntity2 = null;
                foreach (SceneEntity ent in this.entities)
                {
                    RayHit potentialHit = ent.Intersect(reflectRay);
                    if (potentialHit != null && (potentialHit.Position - reflectOrigin).LengthSq() < minHit2)
                    {
                        reflectHit = potentialHit;
                        minHit2 = (reflectHit.Position - reflectOrigin).LengthSq();
                        hitEntity2 = ent;
                    } 
                }
                if (reflectHit != null) 
                {
                    reflectC = fireRay(reflectHit, hitEntity2, ref maxDepth, depth+1, transmit);
                }  

                c += reflectC * k + refractC * (1 - k);
            }

            // Glossy material.
            if ( material.Type == Material.MaterialType.Glossy )
            {
                Color reflectC = new Color(0, 0, 0);
                double cos = hit.Incident.Dot(hit.Normal);
                double angleR = Math.Acos(cos);

                for (int i=-2; i<3; i++)
                {
                    double angle = angleR + 3 * i * Math.PI/100;

                    Vector3 reflectDir = hit.Incident - 2 * Math.Cos(angle) * hit.Normal;
                    Vector3 reflectOrigin = hit.Position + reflectDir.Normalized()/100000;
                    Ray reflectRay = new Ray(reflectOrigin, reflectDir.Normalized());
                    RayHit reflectHit = null;
                    double minHit = Double.MaxValue;
                    SceneEntity hitEntity = null;
                    foreach (SceneEntity ent in this.entities)
                    {
                        RayHit potentialHit = ent.Intersect(reflectRay);
                        if (potentialHit != null && (potentialHit.Position - reflectOrigin).LengthSq() < minHit)
                        {
                            reflectHit = potentialHit;
                            minHit = (reflectHit.Position - reflectOrigin).LengthSq();
                            hitEntity = ent;
                        } 
                    }
                    if (reflectHit != null) 
                    {
                        reflectC += fireRay(reflectHit, hitEntity, ref maxDepth, depth+1, transmit);
                    }

                }


                Color origC = new Color(0, 0, 0);
                foreach (PointLight light in this.lights)
                {
                    Vector3 l = (light.Position - hit.Position).Normalized();
                    if ( ((Math.Acos(hit.Normal.Dot(l)) * 180/Math.PI) <= 90) && !inShadow(hit.Position + bias, light) )
                    {
                        Color cm = material.Color;
                        origC =  cm * light.Color * (hit.Normal.Dot(l));
                    }
                }

                c += reflectC*0.3/5.0 + origC*0.7;
  

            }

            return c;
        }

        // Calculate Shadow rays.
        private bool inShadow(Vector3 hitPosition, PointLight light)
        {
            Vector3 shadowDir = hitPosition - light.Position;
            double st = shadowDir.LengthSq();
            Ray shadowRay = new Ray(light.Position, shadowDir.Normalized());

            foreach (SceneEntity entity in this.entities)
            {
                RayHit h = entity.Intersect(shadowRay);
                if (h != null) 
                {
                    Vector3 dir = h.Position - light.Position;
                    double ht = dir.LengthSq();

                    if (ht < st) 
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Caculate refraction ray direction.
        private Vector3 getRefractDir(Vector3 incident, Vector3 normal, double r, bool transmit)
        {
            double inR;
            double outR;
            Vector3 n = normal;
            double cosi = clamp(-1, 1, -incident.Dot(normal));
            
            if (!transmit) 
            { 
                inR = 1;
                outR = r;
            } 
            else 
            {
                inR = r;
                outR = 1;
            }

            double ratio = inR / outR;
            double k = 1 - ratio * ratio * (1 - cosi * cosi);

            if (k < 0)
            {
                return new Vector3(0, 0, 0);
            }
            return ratio * incident + (ratio * cosi - Math.Sqrt(k)) * n;
        }

        // Caculate fresnel effect.
        private double fresnel(Vector3 incident, Vector3 normal, double r, bool transmit)
        {
            double inR;
            double outR;
            Vector3 n = normal;
            
            double k;
            
            if (!transmit) 
            { 
                inR = 1;
                outR = r;
            } 
            else 
            {
                inR = r;
                outR = 1;
            }

            double cosi = clamp(-1, 1, -incident.Dot(normal));
            double sint = inR / outR * Math.Sqrt(Math.Max(0, 1 - cosi * cosi));
            // Total reflection
            if (sint >= 1)
            {
                k = 1;
            }
            else 
            {
                double cost = Math.Sqrt(Math.Max(0, 1 - sint * sint));
                cosi = Math.Abs(cosi);
                double Rs = ((outR * cosi) - (inR * cost)) / ((outR * cosi) + (inR * cost)); 
                double Rp = ((inR * cosi) - (outR * cost)) / ((inR * cosi) + (outR * cost)); 
                k = (Rs * Rs + Rp * Rp) / 2; 
            }


            // // Schlick aproximation
            // double r0 = (inR - outR) / (inR + outR);
            // r0 *= r0;
            // double cosX = clamp(-1, 1, -incident.Dot(normal));
            // if (inR > outR)
            // {
            //     double q = inR/outR;
            //     double sinT2 = q * q * (1.0 - cosX * cosX);
            //     if (sinT2 >= 1.0f)
            //     {
            //         return 1;
            //     }
            //     cosX = Math.Sqrt(1 - sinT2);
            // }
            // double x = 1 - cosX;
            // k = r0 + (1 - r0)*x*x*x*x*x;

            return k;
        }

        // Caculate color for each pixel.
        private Color getPixelColor(Ray ray, ref int maxDepth) 
        {
            Color c = new Color(0, 0, 0);
            double minDist = Double.MaxValue;
            foreach (SceneEntity entity in this.entities)
            {
                // Calculate intersection and judge entity order
                RayHit hit = entity.Intersect(ray);
                if (hit != null && hit.Position.Z > 0 && hit.Position.Z < minDist)
                {
                    minDist = hit.Position.Z;
                    // Console.WriteLine("hit");
                    c = fireRay(hit, entity, ref maxDepth, 0, false);
                }
            }
            return c;
        }

        // Bound a value.
        private double clamp(double l, double h, double n)
        {
            return Math.Max(l, Math.Min(h, n));
        }

        private Vector3 axisProcess(Vector3 axis)
        {
            if (Math.Abs(axis.Z) > 1)
            {
                double x = axis.X / Math.Abs(axis.Z);
                double y = axis.Y / Math.Abs(axis.Z);
                double z = axis.Z / Math.Abs(axis.Z);
                return new Vector3(x, y, z);
            }
            return axis;
        }

    }
}
