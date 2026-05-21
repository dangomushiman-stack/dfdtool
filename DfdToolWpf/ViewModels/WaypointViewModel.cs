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
            private bool _isSelected;

            public double X { get => _x; set { _x = value; OnPropertyChanged(); } }
            public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }
            public bool IsJump { get => _isJump; set { _isJump = value; OnPropertyChanged(); } }

            /// <summary>
            /// 範囲選択などで線分の折り曲げ点自体が選択されているか。
            /// 保存対象ではなく、現在の編集状態だけを表す。
            /// </summary>
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }
    
}
