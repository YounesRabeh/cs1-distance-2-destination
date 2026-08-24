// Creates, updates, and removes the Route Distance row in vanilla information panels.
// Preserves panel layout by restoring every size changed when the row is removed.
using System;
using System.Globalization;
using ColossalFramework.UI;
using UnityEngine;

namespace RouteDistance.UI
{
    /// <summary>
    /// Owns a vanilla-styled distance row and the panel sizing required to display it.
    /// </summary>
    public sealed class DistanceLabel
    {
        private const string RowName = "RouteDistanceRow";
        private const string ComponentName = "RouteDistanceLabel";
        private const string Prefix = "Distance to destination: ";
        private const float VerticalSpacing = 2f;
        private const float InfoPanelBottomPadding = 8f;

        private UILabel label;
        private UIPanel row;
        private UIPanel container;
        private UIComponent statusRow;
        private UIComponent infoPanel;
        private float originalContainerHeight;
        private float originalInfoPanelHeight;

        /// <summary>
        /// Attaches one styled distance row below the supplied vanilla Status row.
        /// </summary>
        public bool Attach(UILabel styleSource, UIComponent requestedInfoPanel)
        {
            if (styleSource == null || styleSource.parent == null ||
                styleSource.parent.parent == null || requestedInfoPanel == null)
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
                statusRow == requestedStatusRow && container == requestedContainer &&
                infoPanel == requestedInfoPanel)
            {
                row.isVisible = true;
                return true;
            }

            Remove();

            statusRow = requestedStatusRow;
            container = requestedContainer;
            originalContainerHeight = container.height;
            infoPanel = requestedInfoPanel;
            originalInfoPanelHeight = infoPanel.height;

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

            // The new row pushes every later vanilla row down, so the window must grow
            // by the row's full height before adding the requested bottom padding.
            float requiredInfoPanelHeight =
                originalInfoPanelHeight + row.height + InfoPanelBottomPadding;
            if (infoPanel.height < requiredInfoPanelHeight)
            {
                infoPanel.height = requiredInfoPanelHeight;
            }

            row.isVisible = true;
            SetUnavailable();
            return true;
        }

        /// <summary>
        /// Displays a valid remaining distance using the mod's rounded presentation rules.
        /// </summary>
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

        /// <summary>
        /// Displays the unavailable marker for a transient or missing route.
        /// </summary>
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

        /// <summary>
        /// Removes the owned UI row and restores the original container and panel heights.
        /// </summary>
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

            if (infoPanel != null)
            {
                infoPanel.height = originalInfoPanelHeight;
            }

            label = null;
            row = null;
            container = null;
            statusRow = null;
            infoPanel = null;
            originalContainerHeight = 0f;
            originalInfoPanelHeight = 0f;
        }

        /// <summary>
        /// Formats meters as an upward-rounded metric distance suitable for the info panel.
        /// </summary>
        public static string FormatDistance(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters) || meters < 0f)
            {
                return null;
            }

            if (meters < 1000f)
            {
                int increment = meters > 100f ? 100 : meters > 50f ? 50 : 10;
                int roundedMeters = Mathf.CeilToInt(meters / increment) * increment;

                if (roundedMeters >= 1000)
                {
                    return (roundedMeters / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km";
                }

                return roundedMeters.ToString(CultureInfo.InvariantCulture) + " m";
            }

            return (meters / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " km";
        }
    }
}
