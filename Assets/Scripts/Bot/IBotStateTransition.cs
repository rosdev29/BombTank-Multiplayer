/// <summary>
/// Đại diện cho một điều kiện chuyển state trong Priority-based FSM.
/// BotBrain duyệt danh sách transition theo Priority giảm dần,
/// chọn transition đầu tiên mà CanEnter() trả về true.
/// </summary>
public interface IBotStateTransition
{
    /// <summary>Ưu tiên: số càng cao → càng được kiểm tra trước.</summary>
    int Priority { get; }

    /// <summary>State sẽ được kích hoạt nếu transition này thắng.</summary>
    IBotState State { get; }

    /// <summary>Điều kiện để chuyển vào state này.</summary>
    bool CanEnter(BotContext ctx);
}
