using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DfdToolWpf.Services
{
    /// <summary>
    /// 枠ノードのクリック位置判定と、重なった枠の優先対象解決を担当するサービス。
    /// MainWindow 側はイベント制御だけを担当し、枠線・タイトル・ボディの判定はここへ委譲する。
    /// </summary>
    public class FrameHitTestResolver
    {
        private const double BorderHitWidth = 8.0;
        private const double TitleHitHeight = 28.0;
        private const double ContainmentTolerance = 0.5;

        public bool IsFrame(NodeViewModel node)
        {
            return node.Type == EditorMode.CategoryFrame
                || node.Type == EditorMode.ConnectableFrame;
        }

        public FrameHitArea GetFrameHitArea(NodeViewModel frame, Point canvasPoint)
        {
            if (!IsFrame(frame))
            {
                return FrameHitArea.None;
            }

            double x = canvasPoint.X - frame.X;
            double y = canvasPoint.Y - frame.Y;

            if (x < 0 || y < 0 || x > frame.Width || y > frame.Height)
            {
                return FrameHitArea.None;
            }

            bool onBorder =
                x <= BorderHitWidth ||
                y <= BorderHitWidth ||
                x >= frame.Width - BorderHitWidth ||
                y >= frame.Height - BorderHitWidth;

            bool onTitle = y <= TitleHitHeight;

            if (onBorder)
            {
                return FrameHitArea.Border;
            }

            if (onTitle)
            {
                return FrameHitArea.Title;
            }

            return FrameHitArea.Body;
        }

        public List<FrameHitResult> GetFramesAt(IEnumerable<NodeViewModel> nodes, Point canvasPoint)
        {
            return nodes
                .Select((node, index) => new FrameHitResult(node, GetFrameHitArea(node, canvasPoint), index))
                .Where(x => x.Area != FrameHitArea.None)
                .ToList();
        }

        public NodeViewModel? ResolveFrameForLeftClick(IEnumerable<NodeViewModel> nodes, Point canvasPoint)
        {
            var frames = GetFramesAt(nodes, canvasPoint)
                .Where(x => x.Area == FrameHitArea.Border || x.Area == FrameHitArea.Title)
                .ToList();

            return ResolveFrameByContainmentThenFront(frames);
        }

        public NodeViewModel? ResolveFrameForRightClick(IEnumerable<NodeViewModel> nodes, Point canvasPoint)
        {
            return ResolveFrameByContainmentThenFront(GetFramesAt(nodes, canvasPoint));
        }

        public bool IsFrameBodyOnlyLeftClick(IEnumerable<NodeViewModel> nodes, Point canvasPoint)
        {
            var frames = GetFramesAt(nodes, canvasPoint);

            return frames.Any()
                && frames.All(x => x.Area == FrameHitArea.Body);
        }

        private NodeViewModel? ResolveFrameByContainmentThenFront(List<FrameHitResult> frames)
        {
            if (frames.Count == 0)
            {
                return null;
            }

            // クリック位置にある枠の中に「完全な入れ子関係」がある場合は、
            // 作成順やZ順ではなく、より内側の枠を優先する。
            // 一部だけ重なっている場合は Depth が全て 0 になるため、
            // Layer → Nodes内の後ろ順で前面の枠を選ぶ。
            var rankedFrames = frames
                .Select(frame =>
                {
                    int containmentDepth = frames.Count(other =>
                        !ReferenceEquals(other.Node, frame.Node) &&
                        ContainsFrame(other.Node, frame.Node));

                    return new
                    {
                        frame.Node,
                        frame.Area,
                        frame.Index,
                        ContainmentDepth = containmentDepth
                    };
                })
                .ToList();

            int maxDepth = rankedFrames.Max(x => x.ContainmentDepth);

            var candidates = maxDepth > 0
                ? rankedFrames.Where(x => x.ContainmentDepth == maxDepth)
                : rankedFrames;

            return candidates
                .OrderByDescending(x => x.Node.Layer)
                .ThenByDescending(x => x.Index)
                .Select(x => x.Node)
                .FirstOrDefault();
        }

        private bool ContainsFrame(NodeViewModel outer, NodeViewModel inner)
        {
            if (!IsFrame(outer) || !IsFrame(inner) || ReferenceEquals(outer, inner))
            {
                return false;
            }

            return inner.X >= outer.X - ContainmentTolerance
                && inner.Y >= outer.Y - ContainmentTolerance
                && inner.X + inner.Width <= outer.X + outer.Width + ContainmentTolerance
                && inner.Y + inner.Height <= outer.Y + outer.Height + ContainmentTolerance;
        }
    }

    public enum FrameHitArea
    {
        None,
        Border,
        Title,
        Body
    }

    public sealed record FrameHitResult(NodeViewModel Node, FrameHitArea Area, int Index);
}
