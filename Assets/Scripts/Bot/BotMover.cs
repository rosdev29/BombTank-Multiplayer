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
    [SerializeField] private float khoangNeTuong  = 2.5f;
    [SerializeField] private float khoangKhanCap  = 0.6f;

    [Header("Anti-Stuck")]
    [SerializeField] private float thoiGianPhatHienKet   = 1.0f;
    [SerializeField] private float nguongDisplacementKet  = 0.3f;

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

    private BotContext _ctx;

    private const float STUCK_CHECK_INTERVAL = 0.5f;
    private const float THOI_GIAN_LUI_THOAT  = 0.6f;
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
        if (_dangThoatKet) { return; }

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
        _steerThoatKet = Random.value > 0.5f ? 1f : -1f;
        Debug.Log($"[BotMover] {(_ctx?.Player != null ? _ctx.Player.PlayerName.Value.ToString() : gameObject.name)} → Kich hoat thoat ket!");
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
        cmd.MoveInput = _thoatKetTimer > THOI_GIAN_XOAY_THOAT
            ? new Vector2(_steerThoatKet, -1f)
            : new Vector2(_steerThoatKet,  0f);

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

        BotCommand thoatKetCmd = LayLenhThoatKet(dt);
        if (thoatKetCmd != null)
            cmd = thoatKetCmd;

        float steer    = cmd.MoveInput.x;
        float throttle = cmd.MoveInput.y;

        float steerNe = TinhSteerNeTuong(throttle, out bool khanCap);

        _steerNeTuongTruoc = Mathf.Lerp(_steerNeTuongTruoc, steerNe, 8f * dt);

        float urgency    = khanCap ? 1f : Mathf.Abs(_steerNeTuongTruoc);
        float throttleNe = throttle * (1f - urgency * 0.75f);

        float tocDoXoayThucTe = khanCap ? TOC_DO_XOY_GOC : TOC_DO_XOY;

        if (khanCap)
        {
            float huongNe = Mathf.Abs(steerNe) > 0.001f ? Mathf.Sign(steerNe) : 1f;
            steer    = huongNe;
            throttle = throttleNe;
        }
        else
        {
            steer    = Mathf.Lerp(steer, Mathf.Sign(_steerNeTuongTruoc + 0.001f), urgency);
            throttle = throttleNe;
        }

        _ctx.BodyTransform.Rotate(0f, 0f, steer * -tocDoXoayThucTe * dt);
        _rb.velocity = (Vector2)_ctx.BodyTransform.up * throttle * TOC_DO;
    }

    /// <summary>
    /// Tính hướng né tường bằng raycast quạt phía trước xe.
    /// Trả về giá trị steer [-1, 1]. khanCap = true khi tường rất gần.
    /// </summary>
    private float TinhSteerNeTuong(float throttle, out bool khanCap)
    {
        khanCap = false;

        if (Mathf.Abs(throttle) < 0.05f) { return 0f; }
        if (layerMaskTuong.value == 0)   { return 0f; }

        Vector2 viTri     = _ctx.BotPosition;
        Vector2 huongTien = (Vector2)_ctx.BodyTransform.up;
        if (throttle < 0f) { huongTien = -huongTien; }

        // Kiểm tra khẩn cấp (tường rất gần): 0°, ±30°
        float[] gocKhanCap = { 0f, 30f, -30f };
        Vector2 lucKhanCap = Vector2.zero;
        foreach (float g in gocKhanCap)
        {
            Vector2      huong = Quaternion.Euler(0f, 0f, g) * huongTien;
            RaycastHit2D hit   = Physics2D.Raycast(viTri, huong, khoangKhanCap, layerMaskTuong);
            if (hit.collider == null) { continue; }

            khanCap    = true;
            float t    = 1f - (hit.distance / khoangKhanCap);
            lucKhanCap += hit.normal * (t * t);
        }

        if (khanCap)
        {
            if (lucKhanCap.sqrMagnitude > 0.0001f)
            {
                float gocKC = Vector2.SignedAngle(huongTien, lucKhanCap.normalized);
                return gocKC > 0f ? 1f : -1f;
            }
            return 0f; // góc tường: lực triệt tiêu, caller dùng hướng mặc định
        }

        // Né thường (tường vừa gần): ±20°, ±40°, ±60°, ±80°
        float[] cacGoc     = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f };
        Vector2 lucDayTong = Vector2.zero;
        bool    coTuong    = false;

        foreach (float g in cacGoc)
        {
            Vector2      huong = Quaternion.Euler(0f, 0f, g) * huongTien;
            RaycastHit2D hit   = Physics2D.Raycast(viTri, huong, khoangNeTuong, layerMaskTuong);
            if (hit.collider == null) { continue; }

            coTuong = true;
            float t    = 1f - (hit.distance / khoangNeTuong);
            float manh = t * t;
            lucDayTong += hit.normal * manh;
        }

        if (!coTuong || lucDayTong.sqrMagnitude < 0.0001f) { return 0f; }

        float goc     = Vector2.SignedAngle(huongTien, lucDayTong.normalized);
        float huongNe = goc > 0f ? 1f : -1f;
        float cuongDo = Mathf.Clamp01(lucDayTong.magnitude);

        return huongNe * cuongDo;
    }
}
