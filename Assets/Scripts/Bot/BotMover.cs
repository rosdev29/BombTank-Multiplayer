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

    private BotContext _ctx;
    private BotPathfinder _pathfinder;

    private const float STUCK_CHECK_INTERVAL = 0.3f;
    private const float THOI_GIAN_LUI_THOAT  = 0.3f;
    private const float THOI_GIAN_XOAY_THOAT = 0.3f;
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

        Vector2 escape = BotSteering.TimHuongMo(_ctx.BotPosition, 6f);
        if (escape != _ctx.BotPosition)
        {
            Vector2 dir = (escape - _ctx.BotPosition).normalized;
            float angle = Vector2.SignedAngle((Vector2)_ctx.BodyTransform.up, dir);
            _steerThoatKet = Mathf.Clamp(-angle / 45f, -1f, 1f);
            if (Mathf.Abs(_steerThoatKet) < 0.2f)
                _steerThoatKet = angle > 0f ? 1f : -1f;
        }
        else
        {
            _steerThoatKet = Random.value > 0.5f ? 1f : -1f;
        }
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

        // B. Tính toán steer phụ để lách khỏi tường
        // NẾU đang đi theo đường A*, tắt hoàn toàn tính năng né tường vì A* đã tính đường an toàn!
        // Nếu bật né tường, bot sẽ đánh lái loạn xạ trong hành lang hẹp và bám tường thay vì đi thẳng
        float steerNeTuong = cmd.IsPathfinding ? 0f : TinhSteerNeTuong(throttle, false);

        // C. Làm mượt steer phụ
        if (Mathf.Abs(steerNeTuong) > 0.05f)
            _steerNeTuongTruoc = steerNeTuong; 
        else
            _steerNeTuongTruoc = Mathf.MoveTowards(_steerNeTuongTruoc, 0f, dt * 3f); 

        float urgency = Mathf.Clamp01(Mathf.Abs(_steerNeTuongTruoc));
        
        steer = Mathf.Lerp(steer, Mathf.Sign(_steerNeTuongTruoc + 0.001f) * urgency, urgency); 
        float throttleNe = throttle * (1f - urgency * 0.6f);
        throttle = Mathf.Lerp(throttle, throttleNe, urgency);
        throttle = GiamTocKhiCoTuongPhiaTruoc(throttle);

        _ctx.BodyTransform.Rotate(0f, 0f, steer * -TOC_DO_XOY * dt);
        _rb.velocity = (Vector2)_ctx.BodyTransform.up * throttle * TOC_DO;
    }

    /// <summary>
    /// Tính hướng né tường bằng raycast quạt phía trước xe.
    /// Trả về giá trị steer [-1, 1]. khanCap = true khi tường rất gần.
    /// </summary>
    private float TinhSteerNeTuong(float throttle, bool isPathfinding)
    {
        if (Mathf.Abs(throttle) < 0.05f) { return 0f; }

        Vector2 viTri     = _ctx.BotPosition;
        Vector2 huongTien = (Vector2)_ctx.BodyTransform.up;
        if (throttle < 0f) { huongTien = -huongTien; }

        float[] cacGoc     = { 0f, 15f, -15f, 30f, -30f, 45f, -45f, 60f, -60f, 90f, -90f };
        Vector2 lucDayTong = Vector2.zero;
        bool    coTuong    = false;
        
        float banKinhNe = isPathfinding ? 1.0f : khoangNeTuong;

        foreach (float g in cacGoc)
        {
            Vector2 huong = Quaternion.Euler(0f, 0f, g) * huongTien;
            if (!BotSteering.RaycastTuong(viTri, huong, banKinhNe, out RaycastHit2D hit)) { continue; }

            coTuong = true;
            float t    = 1f - (hit.distance / banKinhNe);
            float manh = t * t * t; // Tang truong theo ham mu 3 de day cuc manh khi qua gan
            lucDayTong += hit.normal * manh;
        }

        if (!coTuong || lucDayTong.sqrMagnitude < 0.0001f) { return 0f; }

        float goc     = Vector2.SignedAngle(huongTien, lucDayTong.normalized);
        float huongNe = goc > 0f ? 1f : -1f;
        float cuongDo = Mathf.Clamp01(lucDayTong.magnitude * 2f); // Nhan 2 de phan ung nhay hon

        return huongNe * cuongDo;
    }

    private float GiamTocKhiCoTuongPhiaTruoc(float throttle)
    {
        if (Mathf.Abs(throttle) < 0.05f) { return throttle; }

        Vector2 viTri     = _ctx.BotPosition;
        Vector2 huongTien = (Vector2)_ctx.BodyTransform.up * Mathf.Sign(throttle);

        // Kiem tra tuong ngay truoc mui xe (truc dien)
        if (BotSteering.RaycastTuong(viTri, huongTien, 0.8f, out _))
            return 0f;

        // Kiem tra tuong o xa hon 1 chut de giam toc
        if (BotSteering.RaycastTuong(viTri, huongTien, 1.5f, out _))
            return throttle * 0.3f;

        return throttle;
    }
}
