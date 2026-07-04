using UnityEngine;

/// <summary>
/// Chịu trách nhiệm Act: nhận BotCommand từ BotBrain và thực thi
/// vật lý (di chuyển, xoay), wall avoidance (raycast né tường),
/// anti-stuck (phát hiện và thoát kẹt).
/// BotBrain chỉ Think — BotMover chỉ Act.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BotMover : MonoBehaviour
{
    [Header("Né tường")]
    [SerializeField] private LayerMask layerMaskTuong;
    [SerializeField] private float khoangNeTuong  = 4f;

    [Header("Anti-Stuck")]
    [SerializeField] private float thoiGianPhatHienKet   = 0.6f;
    [SerializeField] private float nguongDisplacementKet  = 0.15f;

    private Rigidbody2D _rb;

    // --- Anti-stuck state ---
    private float   _stuckTimer      = 0f;
    private float   _checkStuckTimer = 0f;
    private Vector2 _viTriKiemTraCu  = Vector2.zero;
    private bool    _dangThoatKet    = false;
    private float   _thoatKetTimer   = 0f;
    private float   _steerThoatKet   = 1f;

    // --- Wall avoidance state ---
    private float _steerNeTuongTruoc = 0f;

    // --- Chống dao động tiến-lùi khi tường phía trước ---
    private float _reverseThrottleCooldown = 0f;  // Khi > 0: đang giữ throttle đảo chiều
    private float _reversedThrottle        = 0f;  // Giá trị throttle đảo chiều đang giữ
    private const float REVERSE_HOLD_TIME  = 0.25f;

    // --- Corner escape state ---
    private bool    _dangThoatGoc    = false;
    private float   _thoatGocTimer   = 0f;
    private float   _steerThoatGoc   = 1f;
    private const float THOI_GIAN_THOAT_GOC_LUI  = 0.5f;
    private const float THOI_GIAN_THOAT_GOC_XOAY = 0.4f;

    private BotContext _ctx;
    private BotPathfinder _pathfinder;

    private const float STUCK_CHECK_INTERVAL = 0.3f;
    private const float THOI_GIAN_LUI_THOAT  = 0.4f;
    private const float THOI_GIAN_XOAY_THOAT = 0.4f;
    private const float TOC_DO                = 5f;
    private const float TOC_DO_XOY            = 120f;
    private const float TOC_DO_XOY_GOC        = 200f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>BotBrain gọi sau OnNetworkSpawn để truyền Context và LayerMask.</summary>
    public void KhoiTao(BotContext ctx, LayerMask maskTuong)
    {
        _ctx             = ctx;
        layerMaskTuong   = maskTuong;
        _viTriKiemTraCu  = ctx.BotPosition;
        _pathfinder      = GetComponent<BotPathfinder>();
    }

    /// <summary>Cho phép BotSpawner set LayerMask từ bên ngoài.</summary>
    public void SetLayerMaskTuong(LayerMask mask)
    {
        layerMaskTuong = mask;
        if (_ctx != null) { _ctx.LayerMaskTuong = mask; }
    }

    /// <summary>
    /// Cập nhật anti-stuck mỗi frame. Gọi bởi BotBrain.Update() với dt = Time.deltaTime.
    /// </summary>
    public void CapNhatAntiStuck(float dt, BotCommand currentCommand)
    {
        if (_dangThoatKet || _dangThoatGoc) { return; }

        _checkStuckTimer += dt;
        if (_checkStuckTimer < STUCK_CHECK_INTERVAL) { return; }

        _checkStuckTimer = 0f;

        Vector2 viTriHienTai = _ctx != null ? _ctx.BotPosition : (Vector2)transform.position;
        float   displacement  = Vector2.Distance(viTriHienTai, _viTriKiemTraCu);
        _viTriKiemTraCu = viTriHienTai;

        bool dangDiChuyen = Mathf.Abs(currentCommand.MoveInput.y) > 0.05f;
        if (!dangDiChuyen)
        {
            _stuckTimer = 0f;
            return;
        }

        if (displacement < nguongDisplacementKet)
            _stuckTimer += STUCK_CHECK_INTERVAL;
        else
            _stuckTimer = Mathf.Max(0f, _stuckTimer - STUCK_CHECK_INTERVAL * 0.5f);

        if (_stuckTimer >= thoiGianPhatHienKet)
            KichHoatThoatKet();
    }

    private void KichHoatThoatKet()
    {
        _stuckTimer    = 0f;
        _dangThoatKet  = true;
        _thoatKetTimer = THOI_GIAN_LUI_THOAT + THOI_GIAN_XOAY_THOAT;
        _pathfinder?.InvalidatePath();

        _steerThoatKet = TinhHuongThoatTuong();
    }

    /// <summary>
    /// Tính hướng xoay để thoát góc: quét 8 hướng, chọn hướng thoáng nhất.
    /// </summary>
    private float TinhHuongThoatTuong()
    {
        if (_ctx == null) return Random.value > 0.5f ? 1f : -1f;

        Vector2 escape = BotSteering.TimHuongMo(_ctx.BotPosition, 6f);
        if (escape != _ctx.BotPosition)
        {
            Vector2 dir = (escape - _ctx.BotPosition).normalized;
            float angle = Vector2.SignedAngle((Vector2)_ctx.BodyTransform.up, dir);
            float steer = Mathf.Clamp(-angle / 45f, -1f, 1f);
            if (Mathf.Abs(steer) < 0.2f)
                steer = angle > 0f ? 1f : -1f;
            return steer;
        }

        // Fallback: thử kiểm tra bên trái/phải
        Vector2 viTri = _ctx.BotPosition;
        Vector2 phai  = (Vector2)(Quaternion.Euler(0f, 0f, -90f) * _ctx.BodyTransform.up);
        Vector2 trai  = -phai;

        bool thongPhai = BotSteering.CoDuongThong(viTri, viTri + phai * 3f);
        bool thongTrai = BotSteering.CoDuongThong(viTri, viTri + trai * 3f);

        if (thongPhai && !thongTrai) return -1f;
        if (thongTrai && !thongPhai) return  1f;
        return Random.value > 0.5f ? 1f : -1f;
    }

    private BotCommand LayLenhThoatKet(float dt)
    {
        if (!_dangThoatKet) { return null; }

        _thoatKetTimer -= dt;

        if (_thoatKetTimer <= 0f)
        {
            _dangThoatKet = false;
            _stuckTimer   = 0f;
            return null;
        }

        var cmd = new BotCommand();
        // Phase 1: lùi + xoay | Phase 2: tiến + xoay
        bool luiPhase = _thoatKetTimer > THOI_GIAN_XOAY_THOAT;
        cmd.MoveInput = luiPhase
            ? new Vector2(_steerThoatKet, -1f)
            : new Vector2(_steerThoatKet,  1f);

        return cmd;
    }

    // ── Corner escape (kẹt góc nghiêm trọng) ─────────────────────────────────

    /// <summary>
    /// Kiểm tra xem xe có đang bị kẹt góc tường không:
    /// tường gần phía trước VÀ cả ít nhất 1 bên hông cũng bị chặn.
    /// </summary>
    private bool KiemTraGocKet(Vector2 huongTien)
    {
        Vector2 viTri    = _ctx.BotPosition;
        float   khoangGan = khoangNeTuong * 0.5f;

        bool tuongTruoc = BotSteering.RaycastTuong(viTri, huongTien, khoangGan, out RaycastHit2D hitF)
                          && hitF.distance < khoangGan * 0.7f;

        if (!tuongTruoc) return false;

        Vector2 phai = (Vector2)(Quaternion.Euler(0f, 0f, -70f) * huongTien);
        Vector2 trai = (Vector2)(Quaternion.Euler(0f, 0f,  70f) * huongTien);

        bool tuongPhai = BotSteering.RaycastTuong(viTri, phai, khoangGan * 1.2f, out _);
        bool tuongTrai = BotSteering.RaycastTuong(viTri, trai, khoangGan * 1.2f, out _);

        return tuongPhai || tuongTrai;
    }

    private void KichHoatThoatGoc()
    {
        _dangThoatGoc  = true;
        _thoatGocTimer = THOI_GIAN_THOAT_GOC_LUI + THOI_GIAN_THOAT_GOC_XOAY;
        _pathfinder?.InvalidatePath();
        _steerThoatGoc = TinhHuongThoatTuong();
    }

    private BotCommand LayLenhThoatGoc(float dt)
    {
        if (!_dangThoatGoc) { return null; }

        _thoatGocTimer -= dt;
        if (_thoatGocTimer <= 0f)
        {
            _dangThoatGoc = false;
            return null;
        }

        var cmd = new BotCommand();
        bool luiPhase = _thoatGocTimer > THOI_GIAN_THOAT_GOC_XOAY;
        // Khi thoát góc: lùi mạnh + xoay về phía thoáng
        cmd.MoveInput = luiPhase
            ? new Vector2(_steerThoatGoc, -1f)   // lùi + xoay
            : new Vector2(_steerThoatGoc,  1f);  // tiến + xoay ra khỏi góc
        return cmd;
    }

    /// <summary>
    /// Thực thi BotCommand: xoay thân xe + đặt velocity, có tính wall avoidance.
    /// Gọi mỗi frame bởi BotBrain.
    /// </summary>
    public void ThucThiLenh(BotCommand cmd)
    {
        if (_rb == null || _ctx == null) { return; }

        float dt = Time.deltaTime;

        // Ưu tiên: corner escape > anti-stuck > lệnh bình thường
        BotCommand thoatGocCmd = LayLenhThoatGoc(dt);
        if (thoatGocCmd != null)
        {
            ThucHanhDiChuyen(thoatGocCmd.MoveInput.x, thoatGocCmd.MoveInput.y, dt, useWallAvoid: false);
            return;
        }

        BotCommand thoatKetCmd = LayLenhThoatKet(dt);
        if (thoatKetCmd != null)
        {
            ThucHanhDiChuyen(thoatKetCmd.MoveInput.x, thoatKetCmd.MoveInput.y, dt, useWallAvoid: false);
            return;
        }

        float steer    = cmd.MoveInput.x;
        float throttle = cmd.MoveInput.y;

        // Phát hiện góc kẹt trực tiếp (không cần đợi anti-stuck)
        if (Mathf.Abs(throttle) > 0.05f)
        {
            Vector2 huongTien = (Vector2)_ctx.BodyTransform.up;
            if (throttle < 0f) huongTien = -huongTien;
            if (!_dangThoatGoc && KiemTraGocKet(huongTien))
            {
                KichHoatThoatGoc();
                return;
            }
        }

        float steerNe = TinhSteerNeTuong(throttle);

        if (Mathf.Abs(steerNe) > 0.05f)
            _steerNeTuongTruoc = steerNe;
        else
            _steerNeTuongTruoc = Mathf.MoveTowards(_steerNeTuongTruoc, 0f, dt * 3f);

        float urgency = Mathf.Clamp01(Mathf.Abs(_steerNeTuongTruoc));

        steer = Mathf.Lerp(steer, Mathf.Sign(_steerNeTuongTruoc + 0.001f) * urgency, urgency);
        float throttleNe = throttle * (1f - urgency * 0.6f);
        throttle = Mathf.Lerp(throttle, throttleNe, urgency);

        // Nếu tường gần phía trước + urgency cao → chuyển sang lùi thay vì đứng yên
        throttle = XuLyThrottleKhiTuong(throttle, urgency);

        ThucHanhDiChuyen(steer, throttle, dt, useWallAvoid: false);
    }

    /// <summary>
    /// Thay thế ChanThrottleKhiTuongPhiaTruoc — thay vì chặn về 0, chuyển sang lùi khi bị kẹt.
    /// Có cooldown để không dao động tiến-lùi mỗi frame.
    /// </summary>
    private float XuLyThrottleKhiTuong(float throttle, float urgency)
    {
        float dt = Time.deltaTime;

        // Đang trong giai đoạn giữ throttle đảo chiều → tiếp tục giữ
        if (_reverseThrottleCooldown > 0f)
        {
            _reverseThrottleCooldown -= dt;
            return _reversedThrottle;
        }

        if (Mathf.Abs(throttle) < 0.05f) { return throttle; }

        Vector2 viTri     = _ctx.BotPosition;
        Vector2 huongTien = (Vector2)_ctx.BodyTransform.up * Mathf.Sign(throttle);

        // Tường rất gần phía trước → đảo chiều và giữ REVERSE_HOLD_TIME giây
        if (!BotSteering.CoDuongThong(viTri, viTri + huongTien * khoangNeTuong * 0.4f))
        {
            float reversed = throttle > 0f ? -0.6f : 0.5f;
            _reversedThrottle        = reversed;
            _reverseThrottleCooldown = REVERSE_HOLD_TIME;
            return reversed;
        }

        // Tường hơi xa → giảm tốc
        if (!BotSteering.CoDuongThong(viTri, viTri + huongTien * khoangNeTuong))
            return throttle * Mathf.Lerp(0.4f, 0.2f, urgency);

        return throttle;
    }

    private void ThucHanhDiChuyen(float steer, float throttle, float dt, bool useWallAvoid)
    {
        _ctx.BodyTransform.Rotate(0f, 0f, steer * -TOC_DO_XOY * dt);
        _rb.velocity = (Vector2)_ctx.BodyTransform.up * throttle * TOC_DO;
    }

    /// <summary>
    /// Tính hướng né tường bằng raycast quạt phía trước xe.
    /// Trả về giá trị steer [-1, 1].
    /// </summary>
    private float TinhSteerNeTuong(float throttle)
    {
        if (Mathf.Abs(throttle) < 0.05f) { return 0f; }

        Vector2 viTri     = _ctx.BotPosition;
        Vector2 huongTien = (Vector2)_ctx.BodyTransform.up;
        if (throttle < 0f) { huongTien = -huongTien; }

        float[] cacGoc     = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 90f, -90f };
        Vector2 lucDayTong = Vector2.zero;
        bool    coTuong    = false;

        foreach (float g in cacGoc)
        {
            Vector2 huong = Quaternion.Euler(0f, 0f, g) * huongTien;
            if (!BotSteering.RaycastTuong(viTri, huong, khoangNeTuong, out RaycastHit2D hit)) { continue; }

            coTuong = true;
            float t    = 1f - (hit.distance / khoangNeTuong);
            float manh = t * t * t; // Tăng trưởng theo hàm mũ 3 để đẩy cực mạnh khi quá gần
            lucDayTong += hit.normal * manh;
        }

        if (!coTuong || lucDayTong.sqrMagnitude < 0.0001f) { return 0f; }

        float goc     = Vector2.SignedAngle(huongTien, lucDayTong.normalized);
        float huongNe = goc > 0f ? 1f : -1f;
        float cuongDo = Mathf.Clamp01(lucDayTong.magnitude * 2f); // Nhân 2 để phản ứng nhạy hơn

        return huongNe * cuongDo;
    }
}
