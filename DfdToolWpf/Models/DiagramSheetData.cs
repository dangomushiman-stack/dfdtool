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
    public class DiagramSheetData
        {
            public string Name { get; set; } = "Sheet1";
            public List<NodeData> Nodes { get; set; } = new List<NodeData>();
            public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();
            public List<BranchPointData> BranchPoints { get; set; } = new List<BranchPointData>();
        }
    
}
