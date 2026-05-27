using UnityEngine;

public class TrangThaiTuanTra : IBotState
{
    private Vector2     _diemMucTieu;
    private float       _timerDoiDiem;
    private const float THOI_GIAN_DOI_DIEM  = 3f;
    private const float BAN_KINH_NGAU_NHIEN = 10f;

    public void OnEnter(BotContext ctx)
    {
        _diemMucTieu  = ChonDiemNgauNhien(ctx.BotPosition);
        _timerDoiDiem = THOI_GIAN_DOI_DIEM;
    }

    public BotCommand Update(BotContext ctx)
    {
        var cmd = new BotCommand();

        _timerDoiDiem -= ctx.DeltaTime;
        if (_timerDoiDiem <= 0f)
        {
            _diemMucTieu  = ChonDiemNgauNhien(ctx.BotPosition);
            _timerDoiDiem = THOI_GIAN_DOI_DIEM;
        }

        Vector2 huong = _diemMucTieu - ctx.BotPosition;
        float gocLech = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huong.normalized);

        cmd.MoveInput = new Vector2(gocLech > 0 ? -1f : 1f, 1f);

        return cmd;
    }

    public void OnExit(BotContext ctx) { }

    private static Vector2 ChonDiemNgauNhien(Vector2 center)
    {
        float goc = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float r   = Random.Range(3f, BAN_KINH_NGAU_NHIEN);
        return center + new Vector2(Mathf.Cos(goc), Mathf.Sin(goc)) * r;
    }
}