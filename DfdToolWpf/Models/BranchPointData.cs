using System;

namespace DfdToolWpf
{
    /// <summary>
    /// .dfdj/.json 保存用の分岐点データ。
    /// ParentConnectionId は、この分岐点が乗っている親接続線を指す。
    /// </summary>
    public class BranchPointData
    {
        public Guid Id { get; set; }
        public Guid ParentConnectionId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        // 親接続線上の相対位置。旧保存ファイルには存在しないため nullable にしている。
        public int? SegmentIndex { get; set; }
        public double? SegmentT { get; set; }
    }
}
