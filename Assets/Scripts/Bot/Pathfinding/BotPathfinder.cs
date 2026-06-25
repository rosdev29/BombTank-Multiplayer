using System.Collections.Generic;
using UnityEngine;

public class BotPathfinder : MonoBehaviour
{
    public float WaypointTolerance = 0.05f; // Giam xuong 0.05f de bot phai di den vao dung tam cua o truoc khi quay dau, chong ket goc
    public float PathRefreshInterval = 0.5f;

    private List<Vector2> _currentPath;
    private int _currentWaypointIndex;
    private float _timeSinceLastPathRequest;
    private Vector2 _lastTargetPos;

    private BotContext _ctx;

    public void Init(BotContext ctx)
    {
        _ctx = ctx;
    }

    public BotCommand GetMoveCommandToTarget(Vector2 targetPos, float throttle = 1f)
    {
        if (PathfindingGrid.Instance == null)
            return BotSteering.MoveTowards(_ctx, targetPos, throttle);

        _timeSinceLastPathRequest += Time.deltaTime;

        // Tinh toan lai duong di neu target doi vi tri qua nhieu, hoac sau 1 khoang thoi gian
        if (_currentPath == null || _timeSinceLastPathRequest > PathRefreshInterval || Vector2.Distance(targetPos, _lastTargetPos) > 2f)
        {
            _currentPath = AStar.FindPath(_ctx.BotPosition, targetPos);
            _currentWaypointIndex = 0;
            _timeSinceLastPathRequest = 0f;
            _lastTargetPos = targetPos;
        }

        if (_currentPath != null && _currentWaypointIndex < _currentPath.Count)
        {
            Vector2 currentWaypoint = _currentPath[_currentWaypointIndex];
            
            // Neu da den gan waypoint, chuyen sang waypoint tiep theo
            if (Vector2.Distance(_ctx.BotPosition, currentWaypoint) <= WaypointTolerance)
            {
                _currentWaypointIndex++;
            }

            // Path Smoothing (String Pulling): Bo qua cac waypoint trung gian neu bot co the nhin thang thay waypoint tiep theo
            while (_currentWaypointIndex + 1 < _currentPath.Count)
            {
                // Ban kinh phai lon hon rat nhieu (0.9f) so voi ban kinh xe (0.45f) 
                // de tranh chieu dai cua xe hinh chu nhat bi quet vao goc tuong khi xoay cheo
                if (BotSteering.CoDuongThong(_ctx.BotPosition, _currentPath[_currentWaypointIndex + 1], 0.9f))
                {
                    _currentWaypointIndex++;
                }
                else
                {
                    break; // Khong the nhin thay waypoint tiep theo, dung viec bo qua
                }
            }

            if (_currentWaypointIndex < _currentPath.Count)
            {
                BotCommand cmd = BotSteering.MoveTowards(_ctx, _currentPath[_currentWaypointIndex], throttle);
                cmd.IsPathfinding = true; // Danh dau day la lenh di chuyen theo A* de BotMover khong duoi co
                return cmd;
            }
        }

        // Khong tim duoc duong A*, hoac da den waypoint cuoi cung nhung chua cham vao muc tieu (Coin)
        // Chi fallback MoveTowards neu muc tieu o gan, neu muc tieu o xa thi dung lai tranh huc tuong
        if (Vector2.Distance(_ctx.BotPosition, targetPos) <= 2.5f)
        {
            // QUAN TRONG: Phai kiem tra xem co tuong chan giua khong. Neu co tuong (vi du 2 xe cach nhau 1 buc tuong)
            // ma dung MoveTowards thi se bi loi huc tuong!
            if (BotSteering.CoDuongThong(_ctx.BotPosition, targetPos, 0.45f))
            {
                BotCommand fallbackCmd = BotSteering.MoveTowards(_ctx, targetPos, throttle);
                fallbackCmd.IsPathfinding = true; 
                return fallbackCmd;
            }
        }
        
        return new BotCommand() { MoveInput = Vector2.zero, IsPathfinding = true };
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
            {
                Gizmos.DrawSphere(_currentPath[_currentPath.Count - 1], 0.2f);
            }

            if (_currentWaypointIndex < _currentPath.Count && _ctx != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_ctx.BotPosition, _currentPath[_currentWaypointIndex]);
            }
        }
    }
}
