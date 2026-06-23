using UnityEngine;

// Trang thai rut lui khi mau thap: tim vung hoi mau hoac ne dich
public class TrangThaiRutLui : IBotState
{
    private Vector2 _diemMucTieu;   // diem bot dang huong toi
    private float   _timerRefresh;  // dem thoi gian chon lai diem moi
    private Vector2 _viTriTruoc;    // vi tri lan truoc de phat hien bi ket tuong
    private float   _timerBiKet;    // dem thoi gian dung yen

    private const float KHOANG_CACH_RUT_LUI       = 12f;  // buoc di moi lan chon huong
    private const float KHOANG_CACH_DA_TOI        = 2f;   // gan bao nhieu thi coi la toi noi
    private const float BAN_KINH_DICH_NGUY_HIEM   = 9f;   // dich trong vong nay -> phai ne
    private const float BAN_KINH_DICH_RAT_GAN     = 5f;   // dich qua gan -> chay thang nguoc lai
    private const float THOI_GIAN_REFRESH         = 2f;   // chon diem moi moi 2 giay
    private const float THOI_GIAN_REFRESH_NHANH  = 0.4f; // ne dich: cap nhat huong nhanh hon
    private const float TRONG_SO_HUONG_HOI_MAU    = 2f;   // uu tien huong ve vung hoi mau
    private const int   SO_HUONG_SAMPLE           = 16;   // so huong quet khi tim diem ne
    private const float NGUONG_BI_KET             = 0.4f; // di chuyen it hon nay = bi ket
    private const float THOI_GIAN_BI_KET          = 1.2f; // giay truoc khi chon huong khac

    public void OnEnter(BotContext ctx)
    {
        _diemMucTieu  = ChonDiemMucTieu(ctx);
        _timerRefresh = THOI_GIAN_REFRESH;
        _viTriTruoc   = ctx.BotPosition;
        _timerBiKet   = THOI_GIAN_BI_KET;
    }

    public BotCommand Update(BotContext ctx)
    {
        bool dichNguyHiem    = CoDichNguyHiem(ctx);
        bool coVungHoiMau    = ctx.NearestHealingZone != null;
        bool daToiVungHoiMau = coVungHoiMau
            && BotSteering.DaToiNoi(ctx.BotPosition, ctx.HealingZonePosition, KHOANG_CACH_DA_TOI);

        // Toi vung hoi mau + khong co dich + vung con nang luong -> dung lai hoi mau
        if (daToiVungHoiMau && !dichNguyHiem && ctx.NearestHealingZone.CoTheHoiMau)
        {
            return new BotCommand { Fire = false };
        }

        _timerRefresh -= ctx.DeltaTime;

        // Het gio / toi diem cu / dich duoi gan -> chon diem moi
        bool canRefresh = _timerRefresh <= 0f
            || BotSteering.DaToiNoi(ctx.BotPosition, _diemMucTieu, KHOANG_CACH_DA_TOI)
            || dichNguyHiem;

        if (canRefresh)
        {
            _diemMucTieu  = ChonDiemMucTieu(ctx);
            _timerRefresh = dichNguyHiem ? THOI_GIAN_REFRESH_NHANH : THOI_GIAN_REFRESH;
        }

        // Bi ket tuong -> chon huong khac ngay
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

        BotCommand cmd;
        if (ctx.Pathfinder != null)
        {
            cmd = ctx.Pathfinder.GetMoveCommandToTarget(_diemMucTieu);
        }
        else
        {
            cmd = BotSteering.MoveTowards(ctx, _diemMucTieu);
        }
        cmd.Fire = false;
        return cmd;
    }

    public void OnExit(BotContext ctx) { }

    // Dich gan hon nguong -> khong duoc dung yen hoi mau
    private static bool CoDichNguyHiem(BotContext ctx)
    {
        return ctx.NearestEnemy != null && ctx.DistanceToEnemy < BAN_KINH_DICH_NGUY_HIEM;
    }

    // Uu tien: thoat gap -> ve vung hoi mau -> ne dich tim diem an toan
    private static Vector2 ChonDiemMucTieu(BotContext ctx)
    {
        if (ctx.NearestEnemy != null && ctx.DistanceToEnemy < BAN_KINH_DICH_RAT_GAN)
        {
            return ChonDiemThoatGan(ctx);
        }

        // An toan -> tim duong den heal zone (co quet tuong)
        if (ctx.NearestHealingZone != null && !CoDichNguyHiem(ctx))
        {
            return BotSteering.TimDuongDenZone(ctx.BotPosition, ctx.HealingZonePosition, KHOANG_CACH_RUT_LUI);
        }

        return ChonDiemNeDich(ctx);
    }

    // Chay thang nguoc huong dich khi bi ap sat
    private static Vector2 ChonDiemThoatGan(BotContext ctx)
    {
        Vector2 huongThoat = (ctx.BotPosition - ctx.EnemyPosition).normalized;
        if (huongThoat.sqrMagnitude < 0.001f)
        {
            huongThoat = Random.insideUnitCircle.normalized;
        }

        Vector2 diem = ctx.BotPosition + huongThoat * KHOANG_CACH_RUT_LUI;
        return BotSteering.TimDiemTiepCan(ctx.BotPosition, diem, KHOANG_CACH_RUT_LUI);
    }

    // Quet nhieu huong, chon diem xa dich nhat (co the huong ve vung hoi mau)
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

    // Diem cang xa dich cang tot; cong them diem neu gan vung hoi mau hon
    private static float TinhDiemRutLui(Vector2 candidate, BotContext ctx)
    {
        // Bo qua huong bi tuong chan
        if (!BotSteering.CoDuongThong(ctx.BotPosition, candidate)) { return float.MinValue; }

        float tongDiem = TinhKhoangCachAnToan(candidate, ctx);

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

    // Khoang cach toi dich gan nhat tai diem candidate
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
