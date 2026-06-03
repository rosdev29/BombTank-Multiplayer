using UnityEngine;

public class TrangThaiGiaoTranh : IBotState
{
    private const float KHOANG_CACH_LY_TUONG = 6f;
    private const float KHOANG_CACH_DUNG_LAI = 1.0f;
    private const float KHOANG_CACH_QUA_GAN  = 3.5f;

    private const float NGUONG_GOC_DE_BAN = 10f;
    private const float HE_SO_LEAD        = 0.15f;
    private const int   CHI_PHI_BAN       = 1;

    private const float TOC_DO_TIEN_LUI  = 1f;
    private const float THOI_GIAN_STRAFE = 1.8f;
    private const float BIEN_STRAFE      = 0.6f;

    private float   _timerStrafe;
    private float   _huongStrafe;
    private Vector2 _viTriDichCu;

    public void OnEnter(BotContext ctx)
    {
        _huongStrafe = Random.value > 0.5f ? 1f : -1f;
        _timerStrafe = Random.Range(THOI_GIAN_STRAFE * 0.5f, THOI_GIAN_STRAFE);
        _viTriDichCu = ctx.EnemyPosition;
    }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();

        if (ctx.NearestEnemy == null) { return cmd; }

        const float TOC_DO_DAN = 10f;
        Vector2 vanTocDich  = (ctx.EnemyPosition - _viTriDichCu) / Mathf.Max(ctx.DeltaTime, 0.001f);
        float   khoangCach  = Vector2.Distance(ctx.BotPosition, ctx.EnemyPosition);
        float   thoiGianBay = khoangCach / TOC_DO_DAN;

        Vector2 diemNgam = ctx.EnemyPosition + vanTocDich * thoiGianBay * HE_SO_LEAD;
        cmd.AimTarget    = diemNgam;
        _viTriDichCu     = ctx.EnemyPosition;

        Vector2 huongToiDich  = (ctx.EnemyPosition - ctx.BotPosition).normalized;
        float   gocLechThanXe = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huongToiDich);
        float   steer         = gocLechThanXe > 0f ? -1f : 1f;

        float khoangLech = khoangCach - KHOANG_CACH_LY_TUONG;
        float throttle   = 0f;

        if (khoangCach < KHOANG_CACH_QUA_GAN)
            throttle = -TOC_DO_TIEN_LUI;
        else if (khoangLech > KHOANG_CACH_DUNG_LAI)
            throttle = TOC_DO_TIEN_LUI;
        else if (khoangLech < -KHOANG_CACH_DUNG_LAI)
            throttle = -TOC_DO_TIEN_LUI * 0.6f;

        _timerStrafe -= ctx.DeltaTime;
        if (_timerStrafe <= 0f)
        {
            _huongStrafe = -_huongStrafe;
            _timerStrafe = Random.Range(THOI_GIAN_STRAFE * 0.7f, THOI_GIAN_STRAFE * 1.3f);
        }

        bool  dangGiuKhoangCach = Mathf.Abs(khoangLech) <= KHOANG_CACH_DUNG_LAI * 2f;
        float steerCuoi = dangGiuKhoangCach
            ? Mathf.Lerp(steer, _huongStrafe * BIEN_STRAFE, 0.5f)
            : steer;

        cmd.MoveInput = new Vector2(steerCuoi, throttle);

        Vector2 huongNgam   = (diemNgam - ctx.BotPosition).normalized;
        Transform nioNgam   = ctx.TurretTransform != null ? ctx.TurretTransform : ctx.BodyTransform;
        float   gocLechNgam = Vector2.Angle((Vector2)nioNgam.up, huongNgam);

        if (gocLechNgam < NGUONG_GOC_DE_BAN && ctx.DuCoinDeBan(CHI_PHI_BAN))
            cmd.Fire = true;

        return cmd;
    }

    public void OnExit(BotContext ctx) { }
}
