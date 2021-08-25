﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Radium.RayTracing {
    public class Face {
        public Face(UInt32 v1, UInt32 vn1, UInt32 vt1, UInt32 v2, UInt32 vn2, UInt32 vt2, UInt32 v3, UInt32 vn3, UInt32 vt3, bool useMat, UInt32 matIndex) {
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;            
            this.vn1 = vn1;
            this.vn2 = vn2;
            this.vn3 = vn3;            
            this.vt1 = vt1;
            this.vt2 = vt2;
            this.vt3 = vt3;

            this.useMaterial = useMat;
            this.matIndex = matIndex;
        }

        public UInt32 v1, v2, v3, vn1, vn2, vn3, vt1, vt2, vt3, matIndex;
        public bool useMaterial;
        public Point3D p1, p2, p3;
        public Vector3D nml1, nml2, nml3;
        public Point2D uv1, uv2, uv3;
        public Material material;

        // assitant variable for intersection calc
        // used in weight
        double weight_denominator;
        // used in intersection
        Vector3D e1, e2;

        public void FillData(MeshObject obj, Scene scene) {
            p1 = obj.vecList[v1];
            p2 = obj.vecList[v2];
            p3 = obj.vecList[v3];

            nml1 = obj.normalList[vn1];
            nml2 = obj.normalList[vn2];
            nml3 = obj.normalList[vn3];

            uv1 = obj.uvList[vt1];
            uv2 = obj.uvList[vt2];
            uv3 = obj.uvList[vt3];

            if (useMaterial) material = scene.materialList[matIndex];
            else material = UtilFunc.DEFAULT_MATERIAL;

            // construct some internal variables
            var mat = new Matrix3x3(new Vector3D(p1), new Vector3D(p2), new Vector3D(p3), true);
            this.weight_denominator = mat.Det();
            if (this.weight_denominator == 0) throw new Exception("GetWeight() zero denominator");

            this.e1 = p1 - p2;
            this.e2 = p1 - p3;
        }

        public Vector3D GetInternalPointNormal(Point3D p, double w1, double w2, double w3) {
            var intersected_point = nml1 * w1 + nml2 * w2 + nml3 * w3;
            intersected_point.SetUnit();
            return intersected_point;
        }

        public Color GetDiffuse(Point3D p, double w1, double w2, double w3) {
            if (material.base_color_texture == null) return material.diffuse;

            // intersect and convert uv to xy system
            var intersected_uv = uv1 * w1 + uv2 * w2 + uv3 * w3;
            return material.base_color_texture.GetPixel(intersected_uv.x, intersected_uv.y);
        }

#if DEBUG
        public Color GetLocalColor(Beam ray, Point3D p, Scene scene, Radium.Utils.TracingDebug debug, bool need_draw) {
#else
        public Color GetLocalColor(Beam ray, Point3D p, Scene scene) {
#endif
            // ambient
            var result = material.ambient * UtilFunc.DEFAULT_AMBIENT;

            // calc normal
            GetWeight(p, out double w1, out double w2, out double w3);
            var normal = GetInternalPointNormal(p, w1, w2, w3);
            var diffuse_color = GetDiffuse(p, w1, w2, w3);

#if DEBUG
            //if (need_draw)
            //    debug.NewVector(p, normal);
#endif

            // for each light, calc diffuse and specular
            foreach (var light in scene.lightList) {
                var L = light.GetDirectionFromPointToSource(p);
#if DEBUG
                //if (need_draw && light is SunLight)
                //    debug.NewVector(p, L);
#endif
                // shadow confirm
                var newray = new Beam(L, p);
                var in_shadow = false;
                foreach(var obj in scene.meshObjectList) {
                    if (obj.HaveIntersection(newray, light.GetDistance(p))) {
                        in_shadow = true;
                        break;
                    }
                }
                if (in_shadow) continue;    // if in shadow, skip this light

                // calc diffuse
                var V =-ray.direction;
                var LN = L * normal;
                if (LN < 0) continue;
                result = result + (light.GetColor(p) * diffuse_color * LN);

                V.SetUnit();
                var H = L + V;
                H.SetUnit();

                // calc specular
                var HN = H * normal;
                if (HN < 0) continue;
                result = result + (light.GetColor(p) * material.specular *
                    Math.Pow(HN, material.specularN));
            }

            return result;
        }

        public void GetWeight(Point3D p, out double w1, out double w2, out double w3) {
            w1 = w2 = w3 = 0;

            // have calculated
            //var mat = new Matrix3x3(new Vector3D(p1), new Vector3D(p2), new Vector3D(p3), true);
            //var denominator = mat.Det();
            //if (denominator == 0) throw new Exception("GetWeight() zero denominator");

            var mat = new Matrix3x3(new Vector3D(p), new Vector3D(p2), new Vector3D(p3), true);
            w1 = mat.Det() / weight_denominator;
            mat = new Matrix3x3(new Vector3D(p1), new Vector3D(p), new Vector3D(p3), true);
            w2 = mat.Det() / weight_denominator;
            mat = new Matrix3x3(new Vector3D(p1), new Vector3D(p2), new Vector3D(p), true);
            w3 = mat.Det() / weight_denominator;

            if (w1 < 0 || w1 > 1 || w2 < 0 || w2 > 1 || w3 < 0 || w3 > 1) {
                var cache = new Vector3D(w1, w2, w3);
                cache.SetUnit();
                w1 = cache.x;
                w2 = cache.y;
                w3 = cache.z;
            }
            //if (!UtilFunc.CloseBy(w1 + w2 + w3, 1)) throw new Exception("GetWeight() irrlegal w1 w2 w3");
        }

        public bool GetIntersection(Beam ray, out double t, out Point3D intersection) {
            /*
            // t = -( D + n * R0) / (n * Rd)
            var cache = faceN * ray.direction;
            if (cache < UtilFunc.TOLERANCE) {
                // 平行，没有交点
                t = 0;
                intersection = null;
                return false;
            }
            t = -(faceD + faceN * new Vector3D(ray.source)) / cache;
            if (t <= 0) {
                intersection = null;
                return false;
            }

            intersection = ray.source + ray.direction * t;

            // we get the 

            return true;
            */

            t = 0;
            intersection = null;

            // have calculated
            //var e1 = p1 - p2;
            //var e2 = p1 - p3;
            var s = p1 - ray.source;

            var mat = new Matrix3x3(ray.direction, e1, e2, true);
            var denominator = mat.Det();
            if (denominator == 0) return false;

            mat = new Matrix3x3(s, e1, e2, true);
            t = mat.Det() / denominator;
            mat = new Matrix3x3(ray.direction, s, e2, true);
            var beta = mat.Det() / denominator;
            mat = new Matrix3x3(ray.direction, e1, s, true);
            var gamma = mat.Det() / denominator;

            if (t <= UtilFunc.TOLERANCE) return false;
            if (beta < 0 || beta > 1 || gamma < 0 || gamma > 1 || gamma + beta > 1) return false;

            intersection = ray.source + (ray.direction * t);
            return true;
        }
    }
}
