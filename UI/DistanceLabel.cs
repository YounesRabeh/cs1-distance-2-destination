// Creates, updates, and removes the Distance 2 Destination row in vanilla information panels.
// Preserves panel layout by restoring every size changed when the row is removed.
using System;
using System.Globalization;
using ColossalFramework.UI;
using UnityEngine;

namespace DistanceToDestination.UI
{
    /// <summary>
    /// Owns a vanilla-styled distance row and the panel sizing required to display it.
    /// </summary>
    public sealed class DistanceLabel
    {
        private const string RowName = "DistanceToDestinationRow";
        private const string ComponentName = "DistanceToDestinationLabel";
        private const string Prefix = "Distance to destination: ";
        private const float VerticalSpacing = 2f;
        private const float InfoPanelBottomPadding = 8f;
        private const float FeetPerMeter = 3.28084f;
        private const float FeetPerMile = 5280f;

        private UILabel label;
        private UIPanel row;
        private UIPanel container;
        private UIComponent statusRow;
        private UIComponent infoPanel;
        private float originalContainerHeight;
        private float originalInfoPanelHeight;
        private float appliedContainerHeight;
        private float appliedInfoPanelHeight;
        private bool layoutApplied;

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

            bool sameAttachment = label != null && row != null &&
                                  statusRow == requestedStatusRow &&
                                  container == requestedContainer &&
                                  infoPanel == requestedInfoPanel;
            if (!sameAttachment)
            {
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

                label = row.AddUIComponent<UILabel>();
                label.name = ComponentName;
                label.autoSize = false;
                label.wordWrap = false;
                SetUnavailable();
            }
            else if (!layoutApplied)
            {
                // The prefix restored the previous baseline before vanilla refreshed it.
                originalContainerHeight = container.height;
                originalInfoPanelHeight = infoPanel.height;
            }

            ApplyLayout(styleSource);
            return true;
        }

        /// <summary>
        /// Restores and hides an attached row before vanilla recalculates the panel layout.
        /// </summary>
        public void PrepareForVanillaRefresh(UIComponent requestedInfoPanel)
        {
            if (row == null || infoPanel != requestedInfoPanel)
            {
                return;
            }

            RestoreLayout();
            row.isVisible = false;
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
            RestoreLayout();

            if (row != null)
            {
                UIComponent parent = row.parent;
                if (parent != null)
                {
                    parent.RemoveUIComponent(row);
                }

                UnityEngine.Object.Destroy(row.gameObject);
            }

            label = null;
            row = null;
            container = null;
            statusRow = null;
            infoPanel = null;
            originalContainerHeight = 0f;
            originalInfoPanelHeight = 0f;
            appliedContainerHeight = 0f;
            appliedInfoPanelHeight = 0f;
            layoutApplied = false;
        }

        /// <summary>
        /// Reapplies the row style, geometry, panel growth, and bottom padding.
        /// </summary>
        private void ApplyLayout(UILabel styleSource)
        {
            row.width = statusRow.width;
            row.height = Math.Max(styleSource.height, statusRow.height);
            row.relativePosition = new Vector3(
                statusRow.relativePosition.x,
                statusRow.relativePosition.y + statusRow.height + VerticalSpacing,
                statusRow.relativePosition.z);
            row.zOrder = statusRow.zOrder + 1;

            label.font = styleSource.font;
            label.textScale = styleSource.textScale;
            label.textColor = styleSource.textColor;
            label.disabledTextColor = styleSource.disabledTextColor;
            label.textAlignment = styleSource.textAlignment;
            label.verticalAlignment = styleSource.verticalAlignment;
            label.height = Math.Max(styleSource.height, 20f);
            label.width = Math.Max(
                styleSource.width,
                row.width - styleSource.relativePosition.x - 8f);
            label.relativePosition = new Vector3(
                styleSource.relativePosition.x,
                styleSource.relativePosition.y,
                styleSource.relativePosition.z);

            float requiredContainerHeight =
                row.relativePosition.y + row.height + VerticalSpacing;
            container.height = Math.Max(originalContainerHeight, requiredContainerHeight);
            infoPanel.height =
                originalInfoPanelHeight + row.height + InfoPanelBottomPadding;
            appliedContainerHeight = container.height;
            appliedInfoPanelHeight = infoPanel.height;
            row.isVisible = true;
            layoutApplied = true;
        }

        /// <summary>
        /// Restores the latest vanilla dimensions when this instance owns applied growth.
        /// </summary>
        private void RestoreLayout()
        {
            if (!layoutApplied)
            {
                return;
            }

            if (container != null)
            {
                // Do not overwrite a resize performed after this label applied its layout.
                if (Math.Abs(container.height - appliedContainerHeight) < 0.01f)
                {
                    container.height = originalContainerHeight;
                }
            }

            if (infoPanel != null)
            {
                // The next vanilla refresh becomes the new baseline when sizes differ.
                if (Math.Abs(infoPanel.height - appliedInfoPanelHeight) < 0.01f)
                {
                    infoPanel.height = originalInfoPanelHeight;
                }
            }

            appliedContainerHeight = 0f;
            appliedInfoPanelHeight = 0f;
            layoutApplied = false;
        }

        /// <summary>
        /// Formats meters as an upward-rounded metric distance suitable for the info panel.
        /// </summary>
        public static string FormatDistance(float meters)
        {
            return FormatDistance(meters, ModSettings.UseImperial);
        }

        /// <summary>
        /// Formats meters using an explicit unit preference for deterministic verification.
        /// </summary>
        internal static string FormatDistance(float meters, bool useImperial)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters) || meters < 0f)
            {
                return null;
            }

            return useImperial
                ? FormatImperialDistance(meters)
                : FormatMetricDistance(meters);
        }

        /// <summary>
        /// Formats a valid distance using metres below one kilometre and kilometres above it.
        /// </summary>
        private static string FormatMetricDistance(float meters)
        {
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

            float roundedKilometers = Mathf.Ceil(meters / 100f) / 10f;
            return roundedKilometers.ToString("0.0", CultureInfo.InvariantCulture) + " km";
        }

        /// <summary>
        /// Formats a valid distance using feet below one mile and miles above it.
        /// </summary>
        private static string FormatImperialDistance(float meters)
        {
            float feet = meters * FeetPerMeter;
            if (feet < FeetPerMile)
            {
                int increment = feet > 100f ? 100 : feet > 50f ? 50 : 10;
                int roundedFeet = Mathf.CeilToInt(feet / increment) * increment;

                if (roundedFeet >= FeetPerMile)
                {
                    return (roundedFeet / FeetPerMile).ToString(
                               "0.0",
                               CultureInfo.InvariantCulture) + " mi";
                }

                return roundedFeet.ToString(CultureInfo.InvariantCulture) + " ft";
            }

            float roundedMiles = Mathf.Ceil((feet / FeetPerMile) * 10f) / 10f;
            return roundedMiles.ToString("0.0", CultureInfo.InvariantCulture) + " mi";
        }
    }
}
