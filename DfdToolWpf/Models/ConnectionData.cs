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
            public Guid SourceId { get; set; }
            public Guid TargetId { get; set; }
            public string Text { get; set; }
            public bool IsDashed { get; set; }
            public ConnectionDashStyle? DashStyle { get; set; }
            
            public List<Point> Waypoints { get; set; } = new List<Point>(); 
            public List<WaypointData> WaypointNodes { get; set; } = new List<WaypointData>(); 
        }
    
}
