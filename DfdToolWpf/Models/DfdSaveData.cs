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
    public class DfdSaveData
        {
            // 旧バージョン互換用：旧JSONではここに直接ノード・接続が保存されている。
            // 新バージョンでは、主に Sheets を使用する。
            public List<NodeData> Nodes { get; set; } = new List<NodeData>();
            public List<ConnectionData> Connections { get; set; } = new List<ConnectionData>();
    
            // 新機能：Excelのように1ファイル内に複数シートを保存する。
            public List<DiagramSheetData> Sheets { get; set; } = new List<DiagramSheetData>();
            public int ActiveSheetIndex { get; set; } = 0;
        }
    
}
