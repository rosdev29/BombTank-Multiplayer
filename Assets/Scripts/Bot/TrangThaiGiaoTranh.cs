using UnityEngine;

public class TrangThaiGiaoTranh : IBotState
{
    private const float KHOANG_CACH_LY_TUONG = 6f;
    private const float KHOANG_CACH_DUNG_LAI = 1.0f;
    private const float KHOANG_CACH_QUA_GAN  = 3.5f;

    private const float NGUONG_GOC_DE_BAN = 10f;
    private const float HE_SO_LEAD        = 0.15f;
    private const int   CHI_PHI_BAN       = 1;

    private const float TOC_DO_TIEN_LUI    = 1f;
    private const float THOI_GIAN_DOI_CHIEU = 1.5f;

    // Các kiểu di chuyển ngẫu nhiên khi giữ khoảng cách lý tưởng
    private enum KieuDiChuyen { VongTrai, VongPhai, TienGan, LuiXa, DungBan }
    private KieuDiChuyen _kieuHienTai;
    private float         _timerDoiKieu;
    private Vector2       _viTriDichCu;

    public void OnEnter(BotContext ctx)
    {
        _viTriDichCu = ctx.EnemyPosition;
        ChonKieuMoi();
    }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();

        if (ctx.NearestEnemy == null) { return cmd; }

        // --- Ngắm đón đầu ---
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

        // --- Ưu tiên 1: Xử lý khoảng cách tuyệt đối ---
        if (khoangCach < KHOANG_CACH_QUA_GAN)
        {
            // Quá gần → lùi ngay, hướng mũi xe về phía địch
            throttle = -TOC_DO_TIEN_LUI;
            steer    = gocLechThanXe > 0f ? -1f : 1f;
        }
        else if (khoangLech > KHOANG_CACH_DUNG_LAI * 3f)
        {
            // Quá xa → tiến thẳng vào
            throttle = TOC_DO_TIEN_LUI;
            steer    = gocLechThanXe > 0f ? -1f : 1f;
        }
        else
        {
            // --- Ưu tiên 2: Khoảng cách lý tưởng → đổi kiểu di chuyển theo timer ---
            _timerDoiKieu -= ctx.DeltaTime;
            if (_timerDoiKieu <= 0f)
            {
                ChonKieuMoi();
            }

            switch (_kieuHienTai)
            {
                case KieuDiChuyen.VongTrai:
                    // Chạy vòng cung sang trái quanh địch
                    steer    = gocLechThanXe > -70f ? -1f : 1f;
                    throttle = TOC_DO_TIEN_LUI;
                    break;

                case KieuDiChuyen.VongPhai:
                    // Chạy vòng cung sang phải quanh địch
                    steer    = gocLechThanXe > 70f ? -1f : 1f;
                    throttle = TOC_DO_TIEN_LUI;
                    break;

                case KieuDiChuyen.TienGan:
                    // Áp sát địch một chút rồi lập tức đổi kiểu
                    steer    = gocLechThanXe > 0f ? -1f : 1f;
                    throttle = TOC_DO_TIEN_LUI;
                    break;

                case KieuDiChuyen.LuiXa:
                    // Giật lùi tạo khoảng trống
                    steer    = gocLechThanXe > 0f ? -1f : 1f;
                    throttle = -TOC_DO_TIEN_LUI * 0.7f;
                    break;

                case KieuDiChuyen.DungBan:
                    // Đứng yên tại chỗ, chỉ xoay nòng nhắm bắn
                    steer    = 0f;
                    throttle = 0f;
                    break;
            }
        }

        cmd.MoveInput = new Vector2(steer, throttle);

        // --- Kiểm tra góc bắn ---
        Vector2   huongNgam   = (diemNgam - ctx.BotPosition).normalized;
        Transform nongNgam    = ctx.TurretTransform != null ? ctx.TurretTransform : ctx.BodyTransform;
        float     gocLechNgam = Vector2.Angle((Vector2)nongNgam.up, huongNgam);

        if (gocLechNgam < NGUONG_GOC_DE_BAN && ctx.DuCoinDeBan(CHI_PHI_BAN))
            cmd.Fire = true;

        return cmd;
    }

    public void OnExit(BotContext ctx) { }

    private void ChonKieuMoi()
    {
        // Trọng số: VongTrai 25%, VongPhai 25%, TienGan 20%, LuiXa 20%, DungBan 10%
        float r = Random.value;
        if      (r < 0.25f) _kieuHienTai = KieuDiChuyen.VongTrai;
        else if (r < 0.50f) _kieuHienTai = KieuDiChuyen.VongPhai;
        else if (r < 0.70f) _kieuHienTai = KieuDiChuyen.TienGan;
        else if (r < 0.90f) _kieuHienTai = KieuDiChuyen.LuiXa;
        else                _kieuHienTai = KieuDiChuyen.DungBan;

        _timerDoiKieu = Random.Range(THOI_GIAN_DOI_CHIEU * 0.6f, THOI_GIAN_DOI_CHIEU * 1.4f);
    }
}
