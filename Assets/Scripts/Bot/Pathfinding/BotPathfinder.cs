using System.Collections.Generic;
using UnityEngine;

public class BotPathfinder : MonoBehaviour
{
    public float WaypointTolerance = 1.0f;
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
        {
            // Fallback neu khong co grid
            return BotSteering.MoveTowards(_ctx, targetPos, throttle);
        }

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
                // Su dung ban kinh 0.8f de dam bao duong chim bay du rong cho kich co xe tang
                if (BotSteering.CoDuongThong(_ctx.BotPosition, _currentPath[_currentWaypointIndex + 1], 0.8f))
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
                currentWaypoint = _currentPath[_currentWaypointIndex];
            }

            if (_currentWaypointIndex < _currentPath.Count)
            {
                return BotSteering.MoveTowards(_ctx, currentWaypoint, throttle);
            }
        }

        // Khong tim duoc duong A*, hoac da den waypoint cuoi cung nhung chua cham vao muc tieu (Coin)
        // Thay vi dung im, ta su dung MoveTowards tiep can truc tiep de "let" vao muc tieu thuc te
        return BotSteering.MoveTowards(_ctx, targetPos, throttle);
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
