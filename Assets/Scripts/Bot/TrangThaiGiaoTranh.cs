using UnityEngine;

public class TrangThaiGiaoTranh : IBotState
{
    private const float KHOANG_CACH_LY_TUONG = 6f;
    private const float KHOANG_CACH_DUNG_LAI = 1.5f;
    private const float NGUONG_GOC_DE_BAN    = 15f;
    private const float TOC_DO_XE            = 1f;
    private const int   CHI_PHI_BAN          = 1;

    public void OnEnter(BotContext ctx) { }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();

        if (ctx.NearestEnemy == null) { return cmd; }

        Vector2 toEnemy    = ctx.EnemyPosition - ctx.BotPosition;
        float khoangCach   = toEnemy.magnitude;
        float gocLech      = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, toEnemy.normalized);
        float huongLai     = gocLech > 0 ? -1f : 1f;

        float throttle   = 0f;
        float khoangLech = khoangCach - KHOANG_CACH_LY_TUONG;

        if (khoangLech > KHOANG_CACH_DUNG_LAI)
            throttle = TOC_DO_XE;
        else if (khoangLech < -KHOANG_CACH_DUNG_LAI)
            throttle = -TOC_DO_XE;

        cmd.MoveInput = new Vector2(huongLai, throttle);
        cmd.AimTarget = ctx.EnemyPosition;

        if (Mathf.Abs(gocLech) < NGUONG_GOC_DE_BAN && ctx.DuCoinDeBan(CHI_PHI_BAN))
            cmd.Fire = true;

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}
