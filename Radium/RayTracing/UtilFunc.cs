﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Radium.RayTracing {
    public static class UtilFunc {

        public static readonly double TOLERANCE = 1e-9;

        public static readonly Material DEFAULT_MATERIAL = new Material();

        public static readonly Color DEFAULT_AMBIENT = new Color(0.4, 0.4, 0.4);

        public static readonly Color DEFAULT_ENVIRONMENT = new Color(0.2, 0.2, 0.2);

        public static bool CloseBy(double num, double expected) {
            return Math.Abs(num - expected) < TOLERANCE;
        }

        public static bool ToBoolean(this byte bt) {
            return (bt != 0);
        }
    }
}