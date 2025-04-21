using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Gear
{
    internal class HighlightColor
    {
        private Vector4 baseColor;

        public HighlightColor(Vector4 color)
        {
            baseColor = color;
        }

        public HighlightColor(float x, float y, float z, float w)
        {
            baseColor = new Vector4(x, y, z, w);
        }

        public Vector3 CustomNodeColor => baseColor.AsVector3();
        public float CustomNodeAlpha => 1.0f;
        public Vector4 ExistingNodeColor => baseColor;
    }
}
