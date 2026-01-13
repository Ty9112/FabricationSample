using System;
using System.Collections.Generic;
using Autodesk.Fabrication.Geometry;

namespace FabricationSample.Models
{
    /// <summary>
    /// Captures the position and orientation data of a fabrication item.
    /// Used to retain XYZ coordinates when swapping items.
    /// </summary>
    public class ItemPositionData
    {
        /// <summary>
        /// Whether position data was successfully captured.
        /// </summary>
        public bool HasValidPosition { get; set; }

        /// <summary>
        /// The primary connector endpoint (connector 0) - used as the reference position.
        /// </summary>
        public Point3D PrimaryEndpoint { get; set; }

        /// <summary>
        /// Direction vector of the primary connector.
        /// </summary>
        public Point3D DirectionVector { get; set; }

        /// <summary>
        /// Width vector of the primary connector.
        /// </summary>
        public Point3D WidthVector { get; set; }

        /// <summary>
        /// Depth vector of the primary connector.
        /// </summary>
        public Point3D DepthVector { get; set; }

        /// <summary>
        /// All connector endpoints for the item.
        /// </summary>
        public List<ConnectorPositionData> ConnectorPositions { get; set; }

        /// <summary>
        /// The AutoCAD handle for the item (used for AutoCAD API operations).
        /// </summary>
        public string AcadHandle { get; set; }

        /// <summary>
        /// Creates a new instance of ItemPositionData.
        /// </summary>
        public ItemPositionData()
        {
            ConnectorPositions = new List<ConnectorPositionData>();
        }

        /// <summary>
        /// Captures position data from a fabrication item.
        /// </summary>
        /// <param name="item">The item to capture position from.</param>
        /// <returns>ItemPositionData containing the item's position information.</returns>
        public static ItemPositionData CaptureFromItem(Autodesk.Fabrication.Item item)
        {
            if (item == null)
                return null;

            var positionData = new ItemPositionData();

            try
            {
                // Capture primary connector (index 0) position and vectors
                if (item.Connectors.Count > 0)
                {
                    positionData.PrimaryEndpoint = item.GetConnectorEndPoint(0);
                    positionData.DirectionVector = item.GetConnectorDirectionVector(0);
                    positionData.WidthVector = item.GetConnectorWidthVector(0);
                    positionData.DepthVector = item.GetConnectorDepthVector(0);
                    positionData.HasValidPosition = true;
                }

                // Capture all connector positions
                for (int i = 0; i < item.Connectors.Count; i++)
                {
                    positionData.ConnectorPositions.Add(new ConnectorPositionData
                    {
                        Index = i,
                        Endpoint = item.GetConnectorEndPoint(i),
                        DirectionVector = item.GetConnectorDirectionVector(i),
                        ConnectionType = item.GetConnectorConnectionType(i)
                    });
                }

                // Get AutoCAD handle
                positionData.AcadHandle = Autodesk.Fabrication.Job.GetACADHandleFromItem(item);
            }
            catch (Exception)
            {
                // If we can't capture position, return what we have
            }

            return positionData;
        }
    }

    /// <summary>
    /// Position data for a single connector.
    /// </summary>
    public class ConnectorPositionData
    {
        /// <summary>
        /// The connector index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The endpoint position of the connector.
        /// </summary>
        public Point3D Endpoint { get; set; }

        /// <summary>
        /// The direction vector of the connector.
        /// </summary>
        public Point3D DirectionVector { get; set; }

        /// <summary>
        /// The connection type of the connector.
        /// </summary>
        public Autodesk.Fabrication.ConnectionType ConnectionType { get; set; }
    }
}
