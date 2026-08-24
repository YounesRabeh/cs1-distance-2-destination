using System;
using System.Globalization;
using ColossalFramework.UI;
using UnityEngine;

namespace RouteDistance.UI
{
    public sealed class DistanceLabel
    {
        private const string ComponentName = "RouteDistanceLabel";
        private const string Prefix = "Distance to destination: ";
        private const float VerticalSpacing = 2f;

        private UILabel label;
        private UIComponent host;
        private float originalHostHeight;

        public bool Attach(UILabel styleSource)
        {
            if (styleSource == null || styleSource.parent == null)
            {
                return false;
            }

            UIComponent requestedHost = styleSource.parent;
            if (label != null && host == requestedHost)
            {
                host.isVisible = true;
                return true;
            }

            Remove();

            host = requestedHost;
            originalHostHeight = host.height;

            UILabel existing = host.Find<UILabel>(ComponentName);
            if (existing != null)
            {
                host.RemoveUIComponent(existing);
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            label = host.AddUIComponent<UILabel>();
            label.name = ComponentName;
            label.font = styleSource.font;
            label.textScale = styleSource.textScale;
            label.textColor = styleSource.textColor;
            label.disabledTextColor = styleSource.disabledTextColor;
            label.textAlignment = styleSource.textAlignment;
            label.verticalAlignment = styleSource.verticalAlignment;
            label.autoSize = false;
            label.wordWrap = false;
            label.height = Math.Max(styleSource.height, 20f);
            label.width = Math.Max(
                styleSource.width,
                host.width - styleSource.relativePosition.x - 8f);
            label.relativePosition = new Vector3(
                styleSource.relativePosition.x,
                styleSource.relativePosition.y + styleSource.height + VerticalSpacing,
                styleSource.relativePosition.z);

            float requiredHeight = label.relativePosition.y + label.height + VerticalSpacing;
            if (host.height < requiredHeight)
            {
                host.height = requiredHeight;
            }

            host.isVisible = true;
            SetUnavailable();
            return true;
        }

        public void SetDistance(float meters)
        {
            if (label == null)
            {
                return;
            }

            string formatted = FormatDistance(meters);
            label.text = formatted == null ? Prefix + "—" : Prefix + formatted;
            label.isVisible = true;
            if (host != null)
            {
                host.isVisible = true;
            }
        }

        public void SetUnavailable()
        {
            if (label != null)
            {
                label.text = Prefix + "—";
                label.isVisible = true;
                if (host != null)
                {
                    host.isVisible = true;
                }
            }
        }

        public void Remove()
        {
            if (label != null)
            {
                UIComponent parent = label.parent;
                if (parent != null)
                {
                    parent.RemoveUIComponent(label);
                }

                UnityEngine.Object.Destroy(label.gameObject);
            }

            if (host != null && host.height > originalHostHeight)
            {
                host.height = originalHostHeight;
            }

            label = null;
            host = null;
            originalHostHeight = 0f;
        }

        public static string FormatDistance(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters) || meters < 0f)
            {
                return null;
            }

            if (meters < 1000f)
            {
                int roundedMeters = Mathf.RoundToInt(meters);
                if (roundedMeters > 999)
                {
                    roundedMeters = 999;
                }

                return roundedMeters.ToString(CultureInfo.InvariantCulture) + " m";
            }

            return (meters / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km";
        }
    }
}
