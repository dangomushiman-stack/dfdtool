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
    public class WaypointData
        {
            public double X { get; set; }
            public double Y { get; set; }
            public bool IsJump { get; set; }
        }
    
}
