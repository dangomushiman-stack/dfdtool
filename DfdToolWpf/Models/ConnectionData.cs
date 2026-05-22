using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace DfdToolWpf
{
    public class ConnectionData
        {
            public Guid Id { get; set; }
            public Guid SourceId { get; set; }
            public Guid FromBranchPointId { get; set; }
            public Guid TargetId { get; set; }
            public Guid ToBranchPointId { get; set; }
            public string Text { get; set; }
            public bool? IsTextVisible { get; set; }
            public string StrokeColor { get; set; } = "Black";
            public bool IsDashed { get; set; }
            public ConnectionDashStyle? DashStyle { get; set; }
            
            public List<Point> Waypoints { get; set; } = new List<Point>(); 
            public List<WaypointData> WaypointNodes { get; set; } = new List<WaypointData>(); 
        }
    
}
