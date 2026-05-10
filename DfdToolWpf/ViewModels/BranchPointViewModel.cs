using System;

namespace DfdToolWpf
{
    /// <summary>
    /// 接続線上に作る分岐点を表すViewModel。
    /// 第1段階ではモデルだけを追加し、描画・ドラッグ・分岐線作成は後続ステップで実装する。
    /// </summary>
    public class BranchPointViewModel : ViewModelBase
    {
        private double _x;
        private double _y;
        private ConnectionViewModel? _parentConnection;

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// この分岐点が乗っている親接続線。
        /// 旧ファイル読込や段階実装中は null になる可能性がある。
        /// </summary>
        public ConnectionViewModel? ParentConnection
        {
            get => _parentConnection;
            set
            {
                if (ReferenceEquals(_parentConnection, value)) return;
                _parentConnection = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// キャンバス座標での分岐点X座標。
        /// </summary>
        public double X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) < 0.0001) return;
                _x = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// キャンバス座標での分岐点Y座標。
        /// </summary>
        public double Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) < 0.0001) return;
                _y = value;
                OnPropertyChanged();
            }
        }
    }
}
