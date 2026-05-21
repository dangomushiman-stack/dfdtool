using System;
using System.ComponentModel;
using System.Windows;

namespace DfdToolWpf
{
    /// <summary>
    /// 接続線上に作る分岐点を表すViewModel。
    /// 分岐点は座標(X/Y)だけでなく、親線上の「線分番号」と「線分内割合」も保持する。
    /// これにより、親線の中継点や接続ノードが動いても同じ位置関係で追従できる。
    /// </summary>
    public class BranchPointViewModel : ViewModelBase
    {
        private double _x;
        private double _y;
        private int _segmentIndex;
        private double _segmentT;
        private ConnectionViewModel? _parentConnection;
        private bool _isSelected;

        public Guid Id { get; set; } = Guid.NewGuid();


        /// <summary>
        /// 範囲選択などで分岐点自体が選択されているか。
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

                if (_parentConnection != null)
                {
                    _parentConnection.GeometryUpdated -= ParentConnection_GeometryUpdated;
                }

                _parentConnection = value;

                if (_parentConnection != null)
                {
                    _parentConnection.GeometryUpdated += ParentConnection_GeometryUpdated;
                }

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 親線のどの線分上にあるか。
        /// 例: 始点→中継点1 が 0、中継点1→中継点2 が 1。
        /// </summary>
        public int SegmentIndex
        {
            get => _segmentIndex;
            set
            {
                if (_segmentIndex == value) return;
                _segmentIndex = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// SegmentIndex の線分内における割合。
        /// 0.0 が線分始点、1.0 が線分終点。
        /// </summary>
        public double SegmentT
        {
            get => _segmentT;
            set
            {
                double clamped = Math.Max(0.0, Math.Min(1.0, value));
                if (Math.Abs(_segmentT - clamped) < 0.0001) return;
                _segmentT = clamped;
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

        /// <summary>
        /// 親線上への投影結果で、座標と相対位置を同時に更新する。
        /// </summary>
        public void ApplyProjection(PolylineProjection projection)
        {
            SegmentIndex = projection.SegmentIndex;
            SegmentT = projection.SegmentT;
            X = projection.Point.X;
            Y = projection.Point.Y;
        }

        /// <summary>
        /// 保存済みの SegmentIndex / SegmentT をもとに、現在の親線形状へ座標を追従させる。
        /// </summary>
        public void SyncToParentConnection()
        {
            if (ParentConnection == null)
            {
                return;
            }

            Point point = ParentConnection.GetPointAtSegmentPosition(SegmentIndex, SegmentT);
            X = point.X;
            Y = point.Y;
        }

        private void ParentConnection_GeometryUpdated(object? sender, EventArgs e)
        {
            SyncToParentConnection();
        }
    }
}
