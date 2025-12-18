using BisBuddy.Util;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using System;
using System.Numerics;
using System.Text.Json.Serialization;

namespace BisBuddy.Gear
{
    public delegate void ColorChangeDelegate();

    [Serializable]
    public class HighlightColor
    {
        public Vector4 BaseColor;

        [JsonIgnore]
        public Vector3 CustomNodeColor { get; private set; }

        [JsonIgnore]
        private float dimCustomNodeAlpha;
        public float CustomNodeAlpha(bool brightHighlighting)
            => brightHighlighting ? Constants.BrightListItemAlpha : dimCustomNodeAlpha;

        [JsonIgnore]
        public Vector4 ExistingNodeColor => BaseColor;

        [JsonConstructor]
        public HighlightColor(Vector4 baseColor)
        {
            BaseColor = baseColor;
            CustomNodeColor = new(
                (baseColor.X * 2) - 1,
                (baseColor.Y * 2) - 1,
                (baseColor.Z * 2) - 1
            );
            dimCustomNodeAlpha = baseColor.W;
        }

        public HighlightColor(float x, float y, float z, float w)
        {
            BaseColor = new Vector4(x, y, z, w);
            CustomNodeColor = new(
                (x * 2) - 1,
                (y * 2) - 1,
                (z * 2) - 1
            );
            dimCustomNodeAlpha = w;
        }

        public event ColorChangeDelegate? OnColorChange;

        private void triggerColorChange() =>
            OnColorChange?.Invoke();

        public void UpdateColor(Vector4 newColor)
        {
            BaseColor = newColor;
            CustomNodeColor = new(
                (newColor.X * 2) - 1,
                (newColor.Y * 2) - 1,
                (newColor.Z * 2) - 1
            );
            dimCustomNodeAlpha = newColor.W;
            triggerColorChange();
        }

        public unsafe void ColorCustomNode(NodeBase node, bool brightHighlighting)
        {
            node.AddColor = CustomNodeColor;
            node.Alpha = CustomNodeAlpha(brightHighlighting);
        }

        public unsafe void ColorExistingNode(AtkResNode* node)
        {
            node->AddRed = (short)Math.Round(255 * BaseColor.X * BaseColor.W);
            node->AddGreen = (short)Math.Round(255 * BaseColor.Y * BaseColor.W);
            node->AddBlue = (short)Math.Round(255 * BaseColor.Z * BaseColor.W);
        }

        public override bool Equals(object? other)
        {
            if (other is not HighlightColor otherColor)
                return false;

            return BaseColor.Equals(otherColor.BaseColor);
        }

        public override int GetHashCode()
        {
            return BaseColor.GetHashCode();
        }
    }
}
