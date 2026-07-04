using System.Collections.Generic;
using UnityEngine;

public class BotPathfinder : MonoBehaviour
{
    public float WaypointTolerance    = 1.5f;  // Tăng từ 1.0 → 1.5 để tránh vòng lặp waypoint hẹp
    public float PathRefreshInterval  = 0.5f;

    private List<Vector2> _currentPath;
    private int           _currentWaypointIndex;
    private float         _timeSinceLastPathRequest;
    private Vector2       _lastTargetPos;

    // Fallback khi A* không tìm được đường: thử lại sau khoảng này
    private float _fallbackRetryTimer = 0f;
    private const float FALLBACK_RETRY_INTERVAL = 0.8f;

    private BotContext _ctx;

    public void Init(BotContext ctx)
    {
        _ctx = ctx;
    }

    public void InvalidatePath()
    {
        _currentPath = null;
        _timeSinceLastPathRequest = PathRefreshInterval; // Kích hoạt tìm đường lại ngay
    }

    public BotCommand GetMoveCommandToTarget(Vector2 targetPos, float throttle = 1f)
    {
        if (PathfindingGrid.Instance == null)
            return FallbackKhiKhongCoGrid(targetPos, throttle);

        _timeSinceLastPathRequest += Time.deltaTime;

        // Kiểm tra waypoint hiện tại còn thấy được không
        if (_currentPath != null && _currentWaypointIndex < _currentPath.Count)
        {
            Vector2 wp = _currentPath[_currentWaypointIndex];
            if (!BotSteering.CoDuongThong(_ctx.BotPosition, wp))
                InvalidatePath();
        }

        // Tính toán lại đường đi nếu target đổi vị trí quá nhiều hoặc đã hết thời gian
        bool canRefresh = _currentPath == null
            || _timeSinceLastPathRequest > PathRefreshInterval
            || Vector2.Distance(targetPos, _lastTargetPos) > 2f;

        if (canRefresh)
        {
            List<Vector2> newPath = AStar.FindPath(_ctx.BotPosition, targetPos);
            if (newPath != null && newPath.Count > 0)
            {
                _currentPath              = newPath;
                _currentWaypointIndex     = 0;
                _fallbackRetryTimer       = 0f;
            }
            else
            {
                // A* thất bại — giữ path cũ nếu còn dùng được, nếu không dùng fallback
                _currentPath = null;
            }
            _timeSinceLastPathRequest = 0f;
            _lastTargetPos            = targetPos;
        }

        // Thực thi path nếu có
        if (_currentPath != null && _currentWaypointIndex < _currentPath.Count)
        {
            // Advance waypoint nếu đã đến gần
            if (Vector2.Distance(_ctx.BotPosition, _currentPath[_currentWaypointIndex]) <= WaypointTolerance)
                _currentWaypointIndex++;

            // Path Smoothing (String Pulling): bỏ qua waypoint trung gian nếu nhìn thẳng thấy waypoint xa hơn
            while (_currentWaypointIndex + 1 < _currentPath.Count)
            {
                if (BotSteering.CoDuongThong(_ctx.BotPosition, _currentPath[_currentWaypointIndex + 1], BotSteering.BanKinhQuetDuong))
                    _currentWaypointIndex++;
                else
                    break;
            }

            if (_currentWaypointIndex < _currentPath.Count)
                return BotSteering.MoveTowards(_ctx, _currentPath[_currentWaypointIndex], throttle);
        }

        // --- Fallback khi không có path ---
        return FallbackKhiKhongCoPath(targetPos, throttle);
    }

    /// <summary>
    /// Fallback khi A* trả về null hoặc path rỗng.
    /// Dùng TimHuongMo() thay vì TimDiemTiepCan() — nhanh hơn và đảm bảo luôn có hướng.
    /// </summary>
    private BotCommand FallbackKhiKhongCoPath(Vector2 targetPos, float throttle)
    {
        _fallbackRetryTimer -= Time.deltaTime;

        // Thử đi thẳng đến target nếu không có tường chặn
        if (BotSteering.CoDuongThong(_ctx.BotPosition, targetPos))
            return BotSteering.MoveTowards(_ctx, targetPos, throttle * 0.8f);

        // Tìm hướng thoáng gần nhất thay vì tính tiếp cận phức tạp
        Vector2 huongMo = BotSteering.TimHuongMo(_ctx.BotPosition, 6f);
        if (huongMo != _ctx.BotPosition)
            return BotSteering.MoveTowards(_ctx, huongMo, throttle * 0.7f);

        // Tình huống cuối: xoay tại chỗ để thoát
        var cmd = new BotCommand();
        cmd.MoveInput = new Vector2(1f, 0.3f); // Xoay + tiến chậm
        return cmd;
    }

    /// <summary>Fallback khi không có PathfindingGrid trong scene.</summary>
    private BotCommand FallbackKhiKhongCoGrid(Vector2 targetPos, float throttle)
    {
        if (BotSteering.CoDuongThong(_ctx.BotPosition, targetPos))
            return BotSteering.MoveTowards(_ctx, targetPos, throttle);

        Vector2 huongMo = BotSteering.TimHuongMo(_ctx.BotPosition, 6f);
        return BotSteering.MoveTowards(_ctx, huongMo != _ctx.BotPosition ? huongMo : targetPos, throttle * 0.7f);
    }

    private void OnDrawGizmos()
    {
        if (_currentPath != null && _currentPath.Count > 0)
        {
            Gizmos.color = Color.green;
            for (int i = _currentWaypointIndex; i < _currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(_currentPath[i], _currentPath[i + 1]);
                Gizmos.DrawSphere(_currentPath[i], 0.2f);
            }
            if (_currentPath.Count > 0)
                Gizmos.DrawSphere(_currentPath[_currentPath.Count - 1], 0.2f);

            if (_currentWaypointIndex < _currentPath.Count && _ctx != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_ctx.BotPosition, _currentPath[_currentWaypointIndex]);
            }
        }
    }
}
