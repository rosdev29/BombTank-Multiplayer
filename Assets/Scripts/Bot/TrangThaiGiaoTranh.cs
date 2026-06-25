using UnityEngine;

public class TrangThaiGiaoTranh : IBotState
{
    private const float KHOANG_CACH_LY_TUONG = 10f; // Khoảng cách giữ so với địch
    private const float KHOANG_CACH_AN_TOAN = 6f;  // Nếu địch quá gần thì phải lùi
    private const float NGUONG_GOC_DE_BAN = 10f;
    private const float HE_SO_LEAD = 0.15f;
    private const int CHI_PHI_BAN = 1;

    private const float THOI_GIAN_DOI_HUONG_STRAFE = 2.5f;

    private Vector2 _viTriDichCu;
    private float _strafeDirection = 1f; // 1 = vòng phải, -1 = vòng trái
    private float _strafeTimer;
    private float _flipCooldown;
    private bool _wasLOS;
    private float _losHysteresisTimer;

    public void OnEnter(BotContext ctx)
    {
        _wasLOS = true;
        _losHysteresisTimer = 0f;
        _viTriDichCu = ctx.EnemyPosition;
        _strafeDirection = Random.value > 0.5f ? 1f : -1f;
        _strafeTimer = Random.Range(1f, THOI_GIAN_DOI_HUONG_STRAFE);
    }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();
        if (ctx.NearestEnemy == null) { return cmd; }

        // --- 1. NGẮM BẮN & TÍNH TOÁN ĐƯỜNG ĐẠN ---
        const float TOC_DO_DAN = 10f;
        Vector2 vanTocDich = (ctx.EnemyPosition - _viTriDichCu) / Mathf.Max(ctx.DeltaTime, 0.001f);
        float khoangCachToiDich = Vector2.Distance(ctx.BotPosition, ctx.EnemyPosition);
        float thoiGianBay = khoangCachToiDich / TOC_DO_DAN;

        Vector2 diemNgam = ctx.EnemyPosition + vanTocDich * thoiGianBay * HE_SO_LEAD;
        cmd.AimTarget = diemNgam;
        _viTriDichCu = ctx.EnemyPosition;

        // --- 2. KIỂM TRA LINE OF SIGHT (LOS) ---
        // Tang ban kinh quet len 0.85f de chac chan khong bi vuong da/tuong o mep
        // Neu bi vuong, bot se chuyen sang dung A* de di vong qua thay vi co tinh dam thang vao
        bool losThong = BotSteering.CoDuongThong(ctx.BotPosition, ctx.EnemyPosition, 0.85f);

        // Hysteresis cho LOS de chong flicking (bot xoay lien tuc)
        // Yeu cau them dieu kien: Phai du gan (cach <= 14f) moi bat dau the hien trang thai giao tranh
        bool duGan = khoangCachToiDich <= KHOANG_CACH_LY_TUONG + 4f;

        if (losThong && duGan)
        {
            _losHysteresisTimer += ctx.DeltaTime;
            if (_losHysteresisTimer > 0.3f)
            {
                _wasLOS = true;
            }
        }
        else
        {
            _losHysteresisTimer = 0f;
            _wasLOS = false;
        }

        if (!_wasLOS)
        {
            // --- PHA TRUY ĐUỔI (MẤT LOS) ---
            // Dùng A* để tìm đường đến chỗ địch trong mê cung
            if (ctx.Pathfinder != null)
            {
                BotCommand moveCmd = ctx.Pathfinder.GetMoveCommandToTarget(ctx.EnemyPosition);
                cmd.MoveInput = moveCmd.MoveInput;
            }
            else
            {
                // Fallback nếu không có A* (ít xảy ra)
                BotCommand moveCmd = BotSteering.MoveTowards(ctx, ctx.EnemyPosition);
                cmd.MoveInput = moveCmd.MoveInput;
            }

            // Ngừng bắn khi đang đuổi trong mê cung
            cmd.Fire = false;
        }
        else
        {
            // --- PHA ĐỌ SÚNG CHIẾN THUẬT (CÓ LOS) ---
            // Thực hiện Strafe (đi vòng) và giữ khoảng cách

            _strafeTimer -= ctx.DeltaTime;
            if (_strafeTimer <= 0f)
            {
                // Đổi chiều strafe ngẫu nhiên để né đạn
                _strafeDirection *= -1f;
                _strafeTimer = Random.Range(1f, THOI_GIAN_DOI_HUONG_STRAFE);
            }

            Vector2 huongToiDich = (ctx.EnemyPosition - ctx.BotPosition).normalized;
            float gocLechThanXe = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huongToiDich);
            float steer = 0f;
            float throttle = 0f;

            if (khoangCachToiDich > KHOANG_CACH_LY_TUONG + 2f)
            {
                // Tiến lại gần: hướng thẳng mũi xe vào địch
                steer = -Mathf.Clamp(gocLechThanXe / 30f, -1f, 1f);
                float absGoc = Mathf.Abs(gocLechThanXe);
                if (absGoc > 10f) throttle = 0f;
                else throttle = 1f;
            }
            else if (khoangCachToiDich < KHOANG_CACH_AN_TOAN)
            {
                // Lùi ra xa: Vẫn hướng mũi xe vào địch nhưng cài số lùi
                steer = -Mathf.Clamp(gocLechThanXe / 30f, -1f, 1f);
                float absGoc = Mathf.Abs(gocLechThanXe);
                if (absGoc > 10f) throttle = 0f;
                else throttle = -1f;
            }
            else
            {
                // Strafe: Xoay thân xe ngang 75 độ so với địch để chạy vòng tròn
                float gocMucTieu = 75f * _strafeDirection;
                float gocSaiLech = gocLechThanXe - gocMucTieu;

                while (gocSaiLech > 180f) gocSaiLech -= 360f;
                while (gocSaiLech < -180f) gocSaiLech += 360f;

                steer = -Mathf.Clamp(gocSaiLech / 30f, -1f, 1f);
                float absGoc = Mathf.Abs(gocSaiLech);
                if (absGoc > 10f) throttle = 0f;
                else throttle = 0.8f;
            }

            // Né tường khi đang strafe
            Vector2 huongDiChuyenThucTe = throttle >= 0 ? (Vector2)ctx.BodyTransform.up : -(Vector2)ctx.BodyTransform.up;
            _flipCooldown -= ctx.DeltaTime;
            if (_flipCooldown <= 0f && BotSteering.RaycastTuong(ctx.BotPosition, huongDiChuyenThucTe, 2.5f, out _))
            {
                _strafeDirection *= -1f; // Đảo chiều
                _strafeTimer = THOI_GIAN_DOI_HUONG_STRAFE; // Reset timer
                _flipCooldown = 0.5f; // Khong cho phep dao chieu lien tuc trong 0.5s de chong xoay xe
            }

            cmd.MoveInput = new Vector2(steer, throttle);

            // --- 3. QUYẾT ĐỊNH BẮN ---
            Vector2 huongNgam = (diemNgam - ctx.BotPosition).normalized;
            Transform nongNgam = ctx.TurretTransform != null ? ctx.TurretTransform : ctx.BodyTransform;
            float gocLechNgam = Vector2.Angle((Vector2)nongNgam.up, huongNgam);

            if (gocLechNgam < NGUONG_GOC_DE_BAN && ctx.DuCoinDeBan(CHI_PHI_BAN))
            {
                cmd.Fire = true;
            }
            else
            {
                cmd.Fire = false;
            }
        }

        return cmd;
    }

    public void OnExit(BotContext ctx)
    {
    }
}