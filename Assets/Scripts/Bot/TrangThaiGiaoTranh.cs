using UnityEngine;

public class TrangThaiGiaoTranh : IBotState
{
    private const float KHOANG_CACH_LY_TUONG = 10f; // Khoảng cách giữ so với địch
    private const float KHOANG_CACH_AN_TOAN = 6f;  // Nếu địch quá gần thì phải lùi
    
    private const float THOI_GIAN_DOI_HUONG_STRAFE = 2.5f;
    private const float TOC_DO_DAN = 10f; // Vận tốc đạn để tính lead shot

    private Vector2 _viTriDichCu;
    private Vector2 _vanTocDichSmooth;
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
        _vanTocDichSmooth = Vector2.zero;
        
        ChonHuongStrafeThoang(ctx);
        _strafeTimer = Random.Range(1f, THOI_GIAN_DOI_HUONG_STRAFE);
    }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();
        if (ctx.NearestEnemy == null)
        {
            // Di chuyển về vị trí địch cuối biết — không đứng im
            if (ctx.Pathfinder != null)
            {
                BotCommand moveCmd = ctx.Pathfinder.GetMoveCommandToTarget(_viTriDichCu);
                cmd.MoveInput = moveCmd.MoveInput;
            }
            else
            {
                BotCommand moveCmd = BotSteering.MoveTowards(ctx, _viTriDichCu);
                cmd.MoveInput = moveCmd.MoveInput;
            }
            return cmd;
        }

        float dt = Time.deltaTime; // Dùng real-time thay vì chu kỳ cập nhật cho các timer mượt

        // --- 1. NGẮM BẮN & TÍNH TOÁN ĐƯỜNG ĐẠN (LEAD SHOT) ---
        Vector2 vanTocDichHienTai = (ctx.EnemyPosition - _viTriDichCu) / Mathf.Max(ctx.DeltaTime, 0.001f);
        // Làm mượt vận tốc để tránh điểm ngắm giật cục
        _vanTocDichSmooth = Vector2.Lerp(_vanTocDichSmooth, vanTocDichHienTai, 0.4f);
        
        float khoangCachToiDich = Vector2.Distance(ctx.BotPosition, ctx.EnemyPosition);
        float thoiGianBay = khoangCachToiDich / TOC_DO_DAN;

        // Tính lead shot chuẩn: Vị trí hiện tại + (Vận tốc * Thời gian đạn bay)
        Vector2 diemNgam = ctx.EnemyPosition + _vanTocDichSmooth * thoiGianBay;
        cmd.AimTarget = diemNgam;
        _viTriDichCu = ctx.EnemyPosition;

        // --- 2. KIỂM TRA LINE OF SIGHT (LOS) ---
        bool losThong = BotSteering.CoDuongThong(ctx.BotPosition, ctx.EnemyPosition);

        // Hysteresis cho LOS để chống flicking (bot xoay liên tục)
        if (losThong)
        {
            _wasLOS = true;
            _losHysteresisTimer = 0.5f; // Delay trước khi tin rằng đã mất LOS hoàn toàn (dùng 0.5s real-time)
        }
        else
        {
            _losHysteresisTimer -= dt;
            if (_losHysteresisTimer <= 0f)
            {
                _wasLOS = false;
            }
        }

        if (!_wasLOS)
        {
            // --- PHA TRUY ĐUỔI (MẤT LOS) ---
            float throttle = 1f;
            // Giảm tốc khi đã đến gần nơi nghi ngờ có địch
            if (khoangCachToiDich < KHOANG_CACH_LY_TUONG)
            {
                throttle = Mathf.Lerp(0.4f, 1f, khoangCachToiDich / KHOANG_CACH_LY_TUONG);
            }

            if (ctx.Pathfinder != null)
            {
                BotCommand moveCmd = ctx.Pathfinder.GetMoveCommandToTarget(ctx.EnemyPosition, throttle);
                cmd.MoveInput = moveCmd.MoveInput;
            }
            else
            {
                BotCommand moveCmd = BotSteering.MoveTowards(ctx, ctx.EnemyPosition, throttle);
                cmd.MoveInput = moveCmd.MoveInput;
            }

            // Ngừng bắn khi đang đuổi trong mê cung
            cmd.Fire = false;
        }
        else
        {
            // --- PHA ĐỌ SÚNG CHIẾN THUẬT (CÓ LOS) ---
            _strafeTimer -= dt;
            if (_strafeTimer <= 0f)
            {
                ChonHuongStrafeThoang(ctx);
                _strafeTimer = Random.Range(1.5f, THOI_GIAN_DOI_HUONG_STRAFE);
            }

            Vector2 huongToiDich = (ctx.EnemyPosition - ctx.BotPosition).normalized;
            float gocLechThanXe = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huongToiDich);
            float steer = 0f;
            float throttle = 0f;

            if (khoangCachToiDich > KHOANG_CACH_LY_TUONG + 2f)
            {
                // Tiến lại gần
                steer = -Mathf.Clamp(gocLechThanXe / 30f, -1f, 1f);
                throttle = Mathf.Abs(gocLechThanXe) > 45f ? 0.3f : 1f;
            }
            else if (khoangCachToiDich < KHOANG_CACH_AN_TOAN)
            {
                // Lùi ra xa
                steer = -Mathf.Clamp(gocLechThanXe / 30f, -1f, 1f);
                throttle = Mathf.Abs(gocLechThanXe) > 45f ? -0.3f : -1f;
            }
            else
            {
                // Strafe
                float gocMucTieu = 75f * _strafeDirection; 
                float gocSaiLech = gocLechThanXe - gocMucTieu;
                
                while (gocSaiLech > 180f) gocSaiLech -= 360f;
                while (gocSaiLech < -180f) gocSaiLech += 360f;

                steer = -Mathf.Clamp(gocSaiLech / 30f, -1f, 1f);
                throttle = 0.8f;
            }

            // Né tường khi đang strafe / tiến-lùi (Chỉ dùng CoDuongThong cho nhẹ và bao quát)
            Vector2 huongDiChuyenThucTe = throttle >= 0 ? (Vector2)ctx.BodyTransform.up : -(Vector2)ctx.BodyTransform.up;
            _flipCooldown -= dt;
            
            if (Mathf.Abs(throttle) > 0.05f && !BotSteering.CoDuongThong(ctx.BotPosition, ctx.BotPosition + huongDiChuyenThucTe * 3.5f))
            {
                if (_flipCooldown <= 0f && khoangCachToiDich >= KHOANG_CACH_AN_TOAN && khoangCachToiDich <= KHOANG_CACH_LY_TUONG + 2f)
                {
                    // Đảo strafe nếu sắp đâm tường
                    _strafeDirection *= -1f;
                    _strafeTimer = THOI_GIAN_DOI_HUONG_STRAFE;
                    _flipCooldown = 1f;
                }
                
                // Hãm phanh khi tường quá gần
                if (!BotSteering.CoDuongThong(ctx.BotPosition, ctx.BotPosition + huongDiChuyenThucTe * 1.5f))
                {
                    throttle = 0f;
                }
                else
                {
                    throttle *= 0.3f;
                }
            }

            cmd.MoveInput = new Vector2(steer, throttle);

            // --- 3. QUYẾT ĐỊNH BẮN ---
            Vector2 huongNgam = (diemNgam - ctx.BotPosition).normalized;
            Transform nongNgam = ctx.TurretTransform != null ? ctx.TurretTransform : ctx.BodyTransform;
            float gocLechNgam = Vector2.Angle((Vector2)nongNgam.up, huongNgam);

            // Ngưỡng góc bắn linh hoạt: địch càng gần thì nới lỏng góc bắn
            float nguongGocDeBan = khoangCachToiDich < 8f ? 20f : 10f;

            if (gocLechNgam < nguongGocDeBan && ctx.DuCoinDeBan(ctx.ChiPhiBan))
            {
                // Check Line of Fire trước khi bóp cò
                if (BotSteering.CoDuongThong(ctx.BotPosition, diemNgam))
                {
                    cmd.Fire = true;
                }
            }
        }

        return cmd;
    }

    private void ChonHuongStrafeThoang(BotContext ctx)
    {
        Vector2 huongToiDich = (ctx.EnemyPosition - ctx.BotPosition).normalized;
        Vector2 huongPhai = Quaternion.Euler(0, 0, -75f) * huongToiDich;
        Vector2 huongTrai = Quaternion.Euler(0, 0, 75f) * huongToiDich;

        bool thongPhai = BotSteering.CoDuongThong(ctx.BotPosition, ctx.BotPosition + huongPhai * 5f);
        bool thongTrai = BotSteering.CoDuongThong(ctx.BotPosition, ctx.BotPosition + huongTrai * 5f);

        if (thongPhai && !thongTrai)
        {
            _strafeDirection = 1f; // Phải thoáng hơn
        }
        else if (thongTrai && !thongPhai)
        {
            _strafeDirection = -1f; // Trái thoáng hơn
        }
        else
        {
            _strafeDirection = Random.value > 0.5f ? 1f : -1f; // Đều thoáng hoặc đều tắc
        }
    }

    public void OnExit(BotContext ctx)
    {
    }
}