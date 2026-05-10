using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DfdToolWpf.Services
{
    /// <summary>
    /// キャンバス上の範囲選択に関する状態管理と矩形計算を担当するクラス。
    /// WPF の Rectangle 表示や MouseCapture は MainWindow 側に残し、
    /// ここでは開始点・選択矩形・範囲内ノードの判定だけを扱う。
    /// </summary>
    public sealed class RangeSelectionController
    {
        private const double TinySelectionThreshold = 4.0;

        private Point _startPoint;

        public bool IsSelecting { get; private set; }

        public Point StartPoint => _startPoint;

        public void Begin(Point startPoint)
        {
            _startPoint = startPoint;
            IsSelecting = true;
        }

        public Rect Update(Point currentPoint)
        {
            return CreateNormalizedRect(_startPoint, currentPoint);
        }

        public IReadOnlyList<NodeViewModel> Complete(Point endPoint, IEnumerable<NodeViewModel>? nodes)
        {
            var selectionRect = CreateNormalizedRect(_startPoint, endPoint);
            IsSelecting = false;

            if (IsTinySelection(selectionRect))
            {
                return Array.Empty<NodeViewModel>();
            }

            return GetNodesInRange(selectionRect, nodes).ToList();
        }

        public void Cancel()
        {
            IsSelecting = false;
        }

        public bool IsTinySelection(Rect selectionRect)
        {
            return selectionRect.Width < TinySelectionThreshold &&
                   selectionRect.Height < TinySelectionThreshold;
        }

        public IEnumerable<NodeViewModel> GetNodesInRange(Rect selectionRect, IEnumerable<NodeViewModel>? nodes)
        {
            if (nodes == null)
            {
                yield break;
            }

            foreach (var node in nodes)
            {
                var nodeRect = new Rect(node.X, node.Y, node.Width, node.Height);
                if (selectionRect.IntersectsWith(nodeRect))
                {
                    yield return node;
                }
            }
        }

        private static Rect CreateNormalizedRect(Point p1, Point p2)
        {
            return new Rect(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p2.X - p1.X),
                Math.Abs(p2.Y - p1.Y));
        }
    }
}
