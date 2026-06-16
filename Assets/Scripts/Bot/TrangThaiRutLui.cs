using UnityEngine;

public class TrangThaiRutLui : IBotState
{
    private Vector2 _diemRutLui;

    public void OnEnter(BotContext ctx)
    {
        if (ctx.NearestEnemy != null)
        {
            Vector2 huongKhoiDich = (ctx.BotPosition - ctx.EnemyPosition).normalized;
            _diemRutLui = ctx.BotPosition + huongKhoiDich * 12f;
        }
        else
        {
            _diemRutLui = ctx.BotPosition + Random.insideUnitCircle.normalized * 10f;
        }
    }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();

        cmd.PathDestination = _diemRutLui;
        cmd.Fire      = false;

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}