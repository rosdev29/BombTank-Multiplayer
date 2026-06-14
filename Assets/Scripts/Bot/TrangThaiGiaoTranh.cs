using UnityEngine;

public class TrangThaiGiaoTranh : IBotState
{
    private const float KHOANG_CACH_LY_TUONG        = 6f;
    private const float KHOANG_CACH_DUNG_LAI        = 1.0f;
    private const float KHOANG_CACH_QUA_GAN         = 3.5f;
    private const float NGUONG_GOC_DE_BAN           = 10f;
    private const float HE_SO_LEAD                  = 0.15f;
    private const int   CHI_PHI_BAN                 = 1;
    private const float TOC_DO_TIEN_LUI             = 1f;
    private const float THOI_GIAN_DOI_CHIEU         = 1.5f;
    private const float KHOANG_KIEM_TRA_TUONG_VONG  = 3f;

    private enum KieuDiChuyen { VongTrai, VongPhai, TienGan, LuiXa, DungBan }
    private KieuDiChuyen  _kieuHienTai;
    private KieuDiChuyen? _kieuVuaLam;
    private float          _timerDoiKieu;
    private float          _gocVong;
    private Vector2        _viTriDichCu;

    public void OnEnter(BotContext ctx)
    {
        _viTriDichCu = ctx.EnemyPosition;
        _kieuVuaLam  = null;
        ChonKieuMoi(null);
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
        float   khoangLech    = khoangCach - KHOANG_CACH_LY_TUONG;

        float throttle = 0f;
        float steer    = 0f;

        if (khoangCach < KHOANG_CACH_QUA_GAN)
        {
            throttle = -TOC_DO_TIEN_LUI;
            steer    = gocLechThanXe > 0f ? -1f : 1f;
            if (_kieuHienTai == KieuDiChuyen.TienGan)
            {
                _timerDoiKieu = 0f;
                _kieuVuaLam   = KieuDiChuyen.TienGan;
            }
        }
        else if (khoangLech > KHOANG_CACH_DUNG_LAI * 3f)
        {
            throttle = TOC_DO_TIEN_LUI;
            steer    = gocLechThanXe > 0f ? -1f : 1f;
            if (_kieuHienTai == KieuDiChuyen.LuiXa)
            {
                _timerDoiKieu = 0f;
                _kieuVuaLam   = KieuDiChuyen.LuiXa;
            }
        }
        else
        {
            _timerDoiKieu -= ctx.DeltaTime;
            if (_timerDoiKieu <= 0f)
            {
                bool overrideSet = _kieuVuaLam == KieuDiChuyen.TienGan
                                || _kieuVuaLam == KieuDiChuyen.LuiXa;
                if (!overrideSet)
                    _kieuVuaLam = (_kieuHienTai == KieuDiChuyen.VongTrai || _kieuHienTai == KieuDiChuyen.VongPhai)
                        ? _kieuHienTai
                        : (KieuDiChuyen?)null;

                ChonKieuMoi(_kieuVuaLam);
            }

            if ((_kieuHienTai == KieuDiChuyen.VongTrai || _kieuHienTai == KieuDiChuyen.VongPhai)
                && CoTuongTheoHuongVong(ctx))
            {
                ChonKieuAnToan();
            }

            switch (_kieuHienTai)
            {
                case KieuDiChuyen.VongTrai:
                    steer    = gocLechThanXe > -_gocVong ? -1f : 1f;
                    throttle = TOC_DO_TIEN_LUI;
                    break;

                case KieuDiChuyen.VongPhai:
                    steer    = gocLechThanXe > _gocVong ? -1f : 1f;
                    throttle = TOC_DO_TIEN_LUI;
                    break;

                case KieuDiChuyen.TienGan:
                    steer    = gocLechThanXe > 0f ? -1f : 1f;
                    throttle = TOC_DO_TIEN_LUI;
                    break;

                case KieuDiChuyen.LuiXa:
                    steer    = gocLechThanXe > 0f ? -1f : 1f;
                    throttle = -TOC_DO_TIEN_LUI * 0.7f;
                    break;

                case KieuDiChuyen.DungBan:
                    steer    = 0f;
                    throttle = 0f;
                    break;
            }
        }

        cmd.MoveInput = new Vector2(steer, throttle);

        Vector2   huongNgam   = (diemNgam - ctx.BotPosition).normalized;
        Transform nongNgam    = ctx.TurretTransform != null ? ctx.TurretTransform : ctx.BodyTransform;
        float     gocLechNgam = Vector2.Angle((Vector2)nongNgam.up, huongNgam);

        if (gocLechNgam < NGUONG_GOC_DE_BAN && ctx.DuCoinDeBan(CHI_PHI_BAN))
        {
            Vector2      huongToiDichLOS = ctx.EnemyPosition - ctx.BotPosition;
            RaycastHit2D losHit          = Physics2D.Raycast(
                ctx.BotPosition, huongToiDichLOS.normalized,
                huongToiDichLOS.magnitude, ctx.LayerMaskTuong);

            if (losHit.collider == null)
                cmd.Fire = true;
        }

        return cmd;
    }

    public void OnExit(BotContext ctx) { }

    private bool CoTuongTheoHuongVong(BotContext ctx)
    {
        Vector2 huongToiDich = (ctx.EnemyPosition - ctx.BotPosition).normalized;
        Vector2 huongVong    = _kieuHienTai == KieuDiChuyen.VongTrai
            ? new Vector2(-huongToiDich.y,  huongToiDich.x)
            : new Vector2( huongToiDich.y, -huongToiDich.x);

        RaycastHit2D hit = Physics2D.Raycast(
            ctx.BotPosition, huongVong, KHOANG_KIEM_TRA_TUONG_VONG, ctx.LayerMaskTuong);

        if (hit.collider == null) { return false; }
        return hit.collider.GetComponent<TankPlayer>() == null;
    }

    private void ChonKieuAnToan()
    {
        _kieuHienTai  = Random.value > 0.5f ? KieuDiChuyen.TienGan : KieuDiChuyen.LuiXa;
        _timerDoiKieu = Random.Range(THOI_GIAN_DOI_CHIEU * 0.5f, THOI_GIAN_DOI_CHIEU * 1.0f);
    }

    private void ChonKieuMoi(KieuDiChuyen? loaiTru)
    {
        float r = Random.value;

        KieuDiChuyen chon;
        if      (r < 0.10f) chon = KieuDiChuyen.VongTrai;
        else if (r < 0.20f) chon = KieuDiChuyen.VongPhai;
        else if (r < 0.45f) chon = KieuDiChuyen.TienGan;
        else if (r < 0.70f) chon = KieuDiChuyen.LuiXa;
        else                chon = KieuDiChuyen.DungBan;

        if (loaiTru.HasValue && chon == loaiTru.Value)
        {
            float r2 = Random.value;
            if (loaiTru.Value == KieuDiChuyen.TienGan)
                chon = r2 < 0.15f ? KieuDiChuyen.VongTrai
                     : r2 < 0.30f ? KieuDiChuyen.VongPhai
                     : r2 < 0.65f ? KieuDiChuyen.LuiXa
                     :               KieuDiChuyen.DungBan;
            else if (loaiTru.Value == KieuDiChuyen.LuiXa)
                chon = r2 < 0.15f ? KieuDiChuyen.VongTrai
                     : r2 < 0.30f ? KieuDiChuyen.VongPhai
                     : r2 < 0.65f ? KieuDiChuyen.TienGan
                     :               KieuDiChuyen.DungBan;
            else
                chon = r2 < 0.35f ? KieuDiChuyen.TienGan
                     : r2 < 0.70f ? KieuDiChuyen.LuiXa
                     :               KieuDiChuyen.DungBan;
        }

        _kieuHienTai  = chon;
        _gocVong      = Random.Range(45f, 90f);
        _timerDoiKieu = Random.Range(THOI_GIAN_DOI_CHIEU * 0.6f, THOI_GIAN_DOI_CHIEU * 1.4f);
    }
}
