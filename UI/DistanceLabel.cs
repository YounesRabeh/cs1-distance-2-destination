using System;
using System.Globalization;
using ColossalFramework.UI;
using UnityEngine;

namespace RouteDistance.UI
{
    public sealed class DistanceLabel
    {
        private const string RowName = "RouteDistanceRow";
        private const string ComponentName = "RouteDistanceLabel";
        private const string Prefix = "Distance to destination: ";
        private const float VerticalSpacing = 2f;

        private UILabel label;
        private UIPanel row;
        private UIPanel container;
        private UIComponent statusRow;
        private float originalContainerHeight;

        public bool Attach(UILabel styleSource)
        {
            if (styleSource == null || styleSource.parent == null ||
                styleSource.parent.parent == null)
            {
                return false;
            }

            UIComponent requestedStatusRow = styleSource.parent;
            UIPanel requestedContainer = requestedStatusRow.parent as UIPanel;
            if (requestedContainer == null)
            {
                return false;
            }

            if (label != null && row != null &&
                statusRow == requestedStatusRow && container == requestedContainer)
            {
                row.isVisible = true;
                return true;
            }

            Remove();

            statusRow = requestedStatusRow;
            container = requestedContainer;
            originalContainerHeight = container.height;

            UIPanel existing = container.Find<UIPanel>(RowName);
            if (existing != null && existing.parent == container)
            {
                container.RemoveUIComponent(existing);
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            row = container.AddUIComponent<UIPanel>();
            row.name = RowName;
            row.autoLayout = false;
            row.width = statusRow.width;
            row.height = Math.Max(styleSource.height, statusRow.height);
            row.relativePosition = new Vector3(
                statusRow.relativePosition.x,
                statusRow.relativePosition.y + statusRow.height + VerticalSpacing,
                statusRow.relativePosition.z);
            row.zOrder = statusRow.zOrder + 1;

            label = row.AddUIComponent<UILabel>();
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
                row.width - styleSource.relativePosition.x - 8f);
            label.relativePosition = new Vector3(
                styleSource.relativePosition.x,
                styleSource.relativePosition.y,
                styleSource.relativePosition.z);

            float requiredHeight = row.relativePosition.y + row.height + VerticalSpacing;
            if (container.height < requiredHeight)
            {
                container.height = requiredHeight;
            }

            row.isVisible = true;
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
            if (row != null)
            {
                row.isVisible = true;
            }
        }

        public void SetUnavailable()
        {
            if (label != null)
            {
                label.text = Prefix + "—";
                label.isVisible = true;
                if (row != null)
                {
                    row.isVisible = true;
                }
            }
        }

        public void Remove()
        {
            if (row != null)
            {
                UIComponent parent = row.parent;
                if (parent != null)
                {
                    parent.RemoveUIComponent(row);
                }

                UnityEngine.Object.Destroy(row.gameObject);
            }

            if (container != null)
            {
                container.height = originalContainerHeight;
            }

            label = null;
            row = null;
            container = null;
            statusRow = null;
            originalContainerHeight = 0f;
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
