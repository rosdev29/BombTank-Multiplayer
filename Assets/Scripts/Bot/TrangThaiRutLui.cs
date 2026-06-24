using UnityEngine;

// Trang thai rut lui khi mau thap: tim vung hoi mau hoac ne dich
public class TrangThaiRutLui : IBotState
{
    private Vector2 _diemMucTieu;
    private float   _timerRefresh;
    private Vector2 _viTriTruoc;
    private float   _timerBiKet;
    private Vector2 _viTriDichCu;

    private const float KHOANG_CACH_RUT_LUI       = 12f;
    private const float KHOANG_CACH_DA_TOI        = 2f;
    private const float BAN_KINH_DICH_NGUY_HIEM   = 9f;
    private const float BAN_KINH_DICH_RAT_GAN     = 5f;
    private const float BAN_KINH_NHAT_COIN        = 6f;
    private const float BAN_KINH_DICH_AN_TOAN     = 8f;
    private const float THOI_GIAN_REFRESH         = 2f;
    private const float THOI_GIAN_REFRESH_NHANH  = 0.4f;
    private const float TRONG_SO_HUONG_HOI_MAU    = 2f;
    private const float DIEM_COVER                = 15f;
    private const int   SO_HUONG_SAMPLE           = 16;
    private const float NGUONG_BI_KET             = 0.4f;
    private const float THOI_GIAN_BI_KET          = 1.2f;

    public void OnEnter(BotContext ctx)
    {
        _diemMucTieu  = ChonDiemMucTieu(ctx);
        _timerRefresh = THOI_GIAN_REFRESH;
        _viTriTruoc   = ctx.BotPosition;
        _timerBiKet   = THOI_GIAN_BI_KET;
        _viTriDichCu  = ctx.NearestEnemy != null ? ctx.EnemyPosition : ctx.BotPosition;
    }

    public BotCommand Update(BotContext ctx)
    {
        bool dichNguyHiem    = CoDichNguyHiem(ctx);
        bool coVungHoiMau    = ctx.NearestHealingZone != null;
        bool daToiVungHoiMau = coVungHoiMau
            && BotSteering.DaToiNoi(ctx.BotPosition, ctx.HealingZonePosition, KHOANG_CACH_DA_TOI);

        if (daToiVungHoiMau && !dichNguyHiem && ctx.NearestHealingZone.CoTheHoiMau)
        {
            BotCommand dungHoi = new BotCommand { Fire = false };
            ApDungBanNe(ctx, dungHoi);
            _viTriDichCu = ctx.NearestEnemy != null ? ctx.EnemyPosition : _viTriDichCu;
            return dungHoi;
        }

        _timerRefresh -= ctx.DeltaTime;

        bool canRefresh = _timerRefresh <= 0f
            || BotSteering.DaToiNoi(ctx.BotPosition, _diemMucTieu, KHOANG_CACH_DA_TOI)
            || dichNguyHiem;

        if (canRefresh)
        {
            _diemMucTieu  = ChonDiemMucTieu(ctx);
            _timerRefresh = dichNguyHiem ? THOI_GIAN_REFRESH_NHANH : THOI_GIAN_REFRESH;
        }

        _timerBiKet -= ctx.DeltaTime;
        if (Vector2.Distance(ctx.BotPosition, _viTriTruoc) < NGUONG_BI_KET)
        {
            if (_timerBiKet <= 0f)
            {
                _diemMucTieu  = ChonDiemNeDich(ctx);
                _timerRefresh = THOI_GIAN_REFRESH_NHANH;
                _timerBiKet   = THOI_GIAN_BI_KET;
            }
        }
        else
        {
            _viTriTruoc = ctx.BotPosition;
            _timerBiKet = THOI_GIAN_BI_KET;
        }

        BotCommand cmd = BotSteering.MoveTowards(ctx, _diemMucTieu);
        ApDungBanNe(ctx, cmd);

        _viTriDichCu = ctx.NearestEnemy != null ? ctx.EnemyPosition : _viTriDichCu;
        return cmd;
    }

    public void OnExit(BotContext ctx) { }

    private void ApDungBanNe(BotContext ctx, BotCommand cmd)
    {
        if (ctx.NearestEnemy == null || !ctx.DuCoinDeBan(ctx.ChiPhiBan) || !CoDeDoa(ctx))
        {
            cmd.Fire = false;
            return;
        }

        cmd.AimTarget = ctx.EnemyPosition;

        if (!BotSteering.CoDuongThong(ctx.BotPosition, ctx.EnemyPosition))
        {
            cmd.Fire = false;
            return;
        }

        Transform nongNgam    = ctx.TurretTransform != null ? ctx.TurretTransform : ctx.BodyTransform;
        Vector2   huongNgam   = (ctx.EnemyPosition - ctx.BotPosition).normalized;
        float     gocLechNgam = Vector2.Angle((Vector2)nongNgam.up, huongNgam);
        float     nguongGoc   = ctx.DistanceToEnemy < 10f ? 28f : 12f;
        cmd.Fire = gocLechNgam < nguongGoc;
    }

    private bool CoDeDoa(BotContext ctx)
    {
        if (CoDichNguyHiem(ctx)) { return true; }

        if (ctx.NearestEnemy == null) { return false; }

        Vector2 vanToc = (ctx.EnemyPosition - _viTriDichCu) / Mathf.Max(ctx.DeltaTime, 0.001f);
        if (vanToc.sqrMagnitude < 0.25f) { return false; }

        Vector2 huongToiBot = (ctx.BotPosition - ctx.EnemyPosition).normalized;
        return Vector2.Dot(vanToc.normalized, huongToiBot) > 0.3f;
    }

    private static bool CoDichNguyHiem(BotContext ctx)
    {
        return ctx.NearestEnemy != null && ctx.DistanceToEnemy < BAN_KINH_DICH_NGUY_HIEM;
    }

    private static Vector2 ChonDiemMucTieu(BotContext ctx)
    {
        if (ctx.NearestCoin != null
            && ctx.DistanceToCoin < BAN_KINH_NHAT_COIN
            && (ctx.NearestEnemy == null || ctx.DistanceToEnemy > BAN_KINH_DICH_AN_TOAN))
        {
            return ctx.CoinPosition;
        }

        if (ctx.NearestEnemy != null && ctx.DistanceToEnemy < BAN_KINH_DICH_RAT_GAN)
        {
            return ChonDiemThoatGan(ctx);
        }

        if (ctx.NearestHealingZone != null && !CoDichNguyHiem(ctx))
        {
            return BotSteering.TimDuongDenZone(ctx.BotPosition, ctx.HealingZonePosition, KHOANG_CACH_RUT_LUI);
        }

        return ChonDiemNeDich(ctx);
    }

    private static Vector2 ChonDiemThoatGan(BotContext ctx)
    {
        Vector2 huongThoat = (ctx.BotPosition - ctx.EnemyPosition).normalized;
        if (huongThoat.sqrMagnitude < 0.001f)
        {
            huongThoat = Random.insideUnitCircle.normalized;
        }

        float   gocLech   = Random.Range(45f, 90f) * (Random.value > 0.5f ? 1f : -1f);
        float   rad       = gocLech * Mathf.Deg2Rad;
        float   cos       = Mathf.Cos(rad);
        float   sin       = Mathf.Sin(rad);
        Vector2 huongNe   = new Vector2(
            huongThoat.x * cos - huongThoat.y * sin,
            huongThoat.x * sin + huongThoat.y * cos
        );

        Vector2 diem = ctx.BotPosition + huongNe * KHOANG_CACH_RUT_LUI;
        return BotSteering.TimDiemTiepCan(ctx.BotPosition, diem, KHOANG_CACH_RUT_LUI);
    }

    private static Vector2 ChonDiemNeDich(BotContext ctx)
    {
        Vector2 viTri       = ctx.BotPosition;
        Vector2 totNhat     = viTri;
        float   diemTotNhat = float.MinValue;

        for (int i = 0; i < SO_HUONG_SAMPLE; i++)
        {
            float goc = i * (360f / SO_HUONG_SAMPLE) * Mathf.Deg2Rad;
            Vector2 huong     = new Vector2(Mathf.Cos(goc), Mathf.Sin(goc));
            Vector2 candidate = viTri + huong * KHOANG_CACH_RUT_LUI;
            float   diem      = TinhDiemRutLui(candidate, ctx);

            if (diem > diemTotNhat)
            {
                diemTotNhat = diem;
                totNhat     = candidate;
            }
        }

        return totNhat;
    }

    private static float TinhDiemRutLui(Vector2 candidate, BotContext ctx)
    {
        if (!BotSteering.CoDuongThong(ctx.BotPosition, candidate)) { return float.MinValue; }

        float tongDiem = TinhKhoangCachAnToan(candidate, ctx);

        if (ctx.NearestEnemy != null && !BotSteering.CoDuongThong(candidate, ctx.EnemyPosition))
        {
            tongDiem += DIEM_COVER;
        }

        if (ctx.NearestHealingZone != null)
        {
            float distHienTai = Vector2.Distance(ctx.BotPosition, ctx.HealingZonePosition);
            float distMoi     = Vector2.Distance(candidate, ctx.HealingZonePosition);
            if (distMoi < distHienTai)
            {
                tongDiem += TRONG_SO_HUONG_HOI_MAU * (distHienTai - distMoi);
            }
        }

        return tongDiem;
    }

    private static float TinhKhoangCachAnToan(Vector2 candidate, BotContext ctx)
    {
        if (ctx.DanhSachDichGan.Count == 0)
        {
            return float.MaxValue;
        }

        float minDist = float.MaxValue;
        foreach (TankPlayer dich in ctx.DanhSachDichGan)
        {
            if (dich == null) { continue; }

            float dist = Vector2.Distance(candidate, (Vector2)dich.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
            }
        }

        return minDist;
    }
}
