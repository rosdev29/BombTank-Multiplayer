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

        Vector2 huong = _diemRutLui - ctx.BotPosition;
        float gocLech = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huong.normalized);

        cmd.MoveInput = new Vector2(gocLech > 0 ? -1f : 1f, 1f);
        cmd.Fire      = false;

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}