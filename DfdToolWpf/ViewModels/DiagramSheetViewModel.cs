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
    public class DiagramSheetViewModel : ViewModelBase
        {
            private string _name;
            private bool _isNameEditing;
            private bool _isSearchHit;
    
            public string Name
            {
                get => _name;
                set
                {
                    _name = string.IsNullOrWhiteSpace(value) ? "Sheet" : value;
                    OnPropertyChanged();
                }
            }
    
            public bool IsNameEditing
            {
                get => _isNameEditing;
                set
                {
                    if (_isNameEditing == value) return;
                    _isNameEditing = value;
                    OnPropertyChanged();
                }
            }
    
            // 検索結果として該当ノードを含む別シートをオレンジ表示するための一時状態。
            // 保存対象にはしない。
            public bool IsSearchHit
            {
                get => _isSearchHit;
                set
                {
                    if (_isSearchHit == value) return;
                    _isSearchHit = value;
                    OnPropertyChanged();
                }
            }
    
            public ObservableCollection<NodeViewModel> Nodes { get; } = new ObservableCollection<NodeViewModel>();
            public ObservableCollection<ConnectionViewModel> Connections { get; } = new ObservableCollection<ConnectionViewModel>();
    
            public DiagramSheetViewModel(string name)
            {
                _name = name;
            }
        }
    
}
