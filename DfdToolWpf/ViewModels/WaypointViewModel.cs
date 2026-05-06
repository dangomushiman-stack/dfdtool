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
    public class WaypointViewModel : ViewModelBase
        {
            private double _x, _y;
            private bool _isJump;
            public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
            public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
            public bool IsJump { get => _isJump; set { _isJump = value; OnPropertyChanged(); } }
        }
    
}
