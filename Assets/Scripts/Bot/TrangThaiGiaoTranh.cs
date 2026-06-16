using UnityEngine;

public class TrangThaiGiaoTranh : IBotState
{
    private const float KHOANG_CACH_LY_TUONG = 6f;
    private const float KHOANG_CACH_DUNG_LAI = 1.5f;
    private const float NGUONG_GOC_DE_BAN    = 15f;
    private const float TOC_DO_XE            = 1f;

    private float _timerBan;

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
        
        // Thêm sai số ngắm
        float saiSo = ctx.Config != null ? ctx.Config.SaiSoNgam : 2f;
        Vector2 diemNgamBiLech = ctx.EnemyPosition + Random.insideUnitCircle * saiSo;
        cmd.AimTarget = diemNgamBiLech;

        _timerBan -= ctx.DeltaTime;

        int chiPhi = 1;
        if (ctx.Player.TryGetComponent<BoPhongDan>(out BoPhongDan combat))
        {
            chiPhi = combat.GetChiPhiBan();
            if (combat.IsDoubleBarrelActive.Value) chiPhi *= 2;
        }

        if (Mathf.Abs(gocLech) < NGUONG_GOC_DE_BAN && ctx.DuCoinDeBan(chiPhi) && _timerBan <= 0f)
        {
            cmd.Fire = true;
            _timerBan = ctx.Config != null ? ctx.Config.ThoiGianChoBan : 1f;
        }

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}
