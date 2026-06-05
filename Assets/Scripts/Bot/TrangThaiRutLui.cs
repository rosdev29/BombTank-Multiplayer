using UnityEngine;

public class TrangThaiRutLui : IBotState
{
    private Vector2 _diemRutLui;
    private float   _timerRefresh;

    private const float KHOANG_CACH_RUT_LUI = 12f;
    private const float KHOANG_CACH_DA_TOI    = 1.5f;
    private const float THOI_GIAN_REFRESH     = 2f;
    private const int   SO_HUONG_SAMPLE       = 12;

    public void OnEnter(BotContext ctx)
    {
        _diemRutLui   = ChonDiemAnToan(ctx);
        _timerRefresh = THOI_GIAN_REFRESH;
    }

    public BotCommand Update(BotContext ctx)
    {
        _timerRefresh -= ctx.DeltaTime;

        if (BotSteering.DaToiNoi(ctx.BotPosition, _diemRutLui, KHOANG_CACH_DA_TOI) || _timerRefresh <= 0f)
        {
            _diemRutLui   = ChonDiemAnToan(ctx);
            _timerRefresh = THOI_GIAN_REFRESH;
        }

        BotCommand cmd = BotSteering.MoveTowards(ctx, _diemRutLui);
        cmd.Fire = false;
        return cmd;
    }

    public void OnExit(BotContext ctx) { }

    private static Vector2 ChonDiemAnToan(BotContext ctx)
    {
        Vector2 viTri     = ctx.BotPosition;
        Vector2 totNhat   = viTri;
        float   diemTotNhat = -1f;

        for (int i = 0; i < SO_HUONG_SAMPLE; i++)
        {
            float goc = i * (360f / SO_HUONG_SAMPLE) * Mathf.Deg2Rad;
            Vector2 huong     = new Vector2(Mathf.Cos(goc), Mathf.Sin(goc));
            Vector2 candidate = viTri + huong * KHOANG_CACH_RUT_LUI;
            float   diem      = TinhDiemAnToan(candidate, ctx);

            if (diem > diemTotNhat)
            {
                diemTotNhat = diem;
                totNhat     = candidate;
            }
        }

        return totNhat;
    }

    private static float TinhDiemAnToan(Vector2 diem, BotContext ctx)
    {
        if (ctx.DanhSachDichGan.Count == 0)
        {
            return float.MaxValue;
        }

        float minDist = float.MaxValue;
        foreach (TankPlayer dich in ctx.DanhSachDichGan)
        {
            if (dich == null) { continue; }

            float dist = Vector2.Distance(diem, (Vector2)dich.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
            }
        }

        return minDist;
    }
}
