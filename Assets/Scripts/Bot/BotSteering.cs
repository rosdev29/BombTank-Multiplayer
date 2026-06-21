using UnityEngine;

// Dieu khien di chuyen bot + tim duong tranh tuong
public static class BotSteering
{
    private const float BAN_KINH_QUET_DUONG = 0.45f;  // gan bang ban kinh tank
    private const int   SO_HUONG_TIM_DUONG  = 16;     // so huong quet khi ne dich
    private const int   SO_HUONG_DEN_ZONE   = 24;     // so huong quet khi di den heal zone
    private const float GOC_QUET_ZONE       = 150f;  // do lech quanh huong zone

    // Xoay + tien ve phia target
    public static BotCommand MoveTowards(BotContext ctx, Vector2 target, float throttle = 1f)
    {
        var cmd = new BotCommand();

        Vector2 huong = target - ctx.BotPosition;
        if (huong.sqrMagnitude < 0.001f) { return cmd; }

        float gocLech = Vector2.SignedAngle((Vector2)ctx.BodyTransform.up, huong.normalized);
        float steer   = -Mathf.Clamp(gocLech / 30f, -1f, 1f); // proportional: mượt trong ±30°
        cmd.MoveInput = new Vector2(steer, throttle);

        return cmd;
    }

    // Gan target trong pham vi epsilon -> coi la da toi
    public static bool DaToiNoi(Vector2 position, Vector2 target, float epsilon = 1.5f)
    {
        return Vector2.SqrMagnitude(target - position) <= epsilon * epsilon;
    }

    // CircleCast tu A den B, false neu co tuong chan duong
    public static bool CoDuongThong(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float   dist  = delta.magnitude;
        if (dist < 0.01f) { return !CoNamTrongTuong(to); }

        RaycastHit2D[] hits = Physics2D.CircleCastAll(from, BAN_KINH_QUET_DUONG, delta / dist, dist);
        foreach (RaycastHit2D hit in hits)
        {
            if (LaTuong(hit.collider)) { return false; }
        }

        return !CoNamTrongTuong(to);
    }

    // Tim waypoint tiep theo de den heal zone (quet tuong theo huong zone)
    public static Vector2 TimDuongDenZone(Vector2 from, Vector2 zonePos, float buoc = 12f)
    {
        if (CoDuongThong(from, zonePos)) { return zonePos; }

        Vector2 totNhat     = from;
        float   ganZoneNhat = float.MaxValue;

        Vector2 huongZone = zonePos - from;
        float   gocZone   = huongZone.sqrMagnitude > 0.001f
            ? Mathf.Atan2(huongZone.y, huongZone.x)
            : 0f;

        // Quet cung quanh huong zone, chon diem gan zone nhat ma di duoc
        for (int i = 0; i < SO_HUONG_DEN_ZONE; i++)
        {
            float lechGoc = (i / (float)(SO_HUONG_DEN_ZONE - 1) - 0.5f) * 2f * GOC_QUET_ZONE * Mathf.Deg2Rad;
            float goc     = gocZone + lechGoc;
            Vector2 huong = new Vector2(Mathf.Cos(goc), Mathf.Sin(goc));

            // Thu nhieu do dai buoc (day -> ngan) de tranh bi ket
            for (float d = buoc; d >= buoc * 0.5f; d -= buoc * 0.25f)
            {
                Vector2 candidate = from + huong * d;
                if (!LaDiemDiDuoc(from, candidate)) { continue; }

                float distZone = Vector2.Distance(candidate, zonePos);
                if (distZone < ganZoneNhat)
                {
                    ganZoneNhat = distZone;
                    totNhat     = candidate;
                }
            }
        }

        if (totNhat != from) { return totNhat; }

        // Fallback: thu di doc duong thang tung doan ngan
        for (float tyLe = 0.8f; tyLe >= 0.2f; tyLe -= 0.2f)
        {
            Vector2 candidate = Vector2.Lerp(from, zonePos, tyLe);
            if (LaDiemDiDuoc(from, candidate)) { return candidate; }
        }

        return TimHuongMo(from, buoc);
    }

    // Tim diem gan target nhat ma di duoc (dung khi ne dich)
    public static Vector2 TimDiemTiepCan(Vector2 from, Vector2 target, float buoc = 12f)
    {
        if (CoDuongThong(from, target)) { return target; }

        Vector2 totNhat       = from;
        float   ganTargetNhat = float.MaxValue;

        // Thu di doc duong thang den target
        for (float tyLe = 0.25f; tyLe <= 1f; tyLe += 0.25f)
        {
            Vector2 candidate = Vector2.Lerp(from, target, tyLe);
            if (!LaDiemDiDuoc(from, candidate)) { continue; }

            float distToTarget = Vector2.Distance(candidate, target);
            if (distToTarget < ganTargetNhat)
            {
                ganTargetNhat = distToTarget;
                totNhat       = candidate;
            }
        }

        if (totNhat != from) { return totNhat; }

        // Quet 360 do, chon huong gan target nhat
        for (int i = 0; i < SO_HUONG_TIM_DUONG; i++)
        {
            float   goc       = i * (360f / SO_HUONG_TIM_DUONG) * Mathf.Deg2Rad;
            Vector2 huong     = new Vector2(Mathf.Cos(goc), Mathf.Sin(goc));
            Vector2 candidate = from + huong * buoc;
            if (!LaDiemDiDuoc(from, candidate)) { continue; }

            float distToTarget = Vector2.Distance(candidate, target);
            if (distToTarget < ganTargetNhat)
            {
                ganTargetNhat = distToTarget;
                totNhat       = candidate;
            }
        }

        return totNhat == from ? TimHuongMo(from, buoc) : totNhat;
    }

    // Diem dich khong trong tuong + co duong thong tu vi tri hien tai
    private static bool LaDiemDiDuoc(Vector2 from, Vector2 candidate)
    {
        return !CoNamTrongTuong(candidate) && CoDuongThong(from, candidate);
    }

    // OverlapCircle kiem tra diem co nam trong/sát tuong khong
    private static bool CoNamTrongTuong(Vector2 pos)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, BAN_KINH_QUET_DUONG);
        foreach (Collider2D col in hits)
        {
            if (LaTuong(col)) { return true; }
        }

        return false;
    }

    // Khi bi ket hoan toan: tim bat ky huong nao di duoc
    private static Vector2 TimHuongMo(Vector2 from, float buoc)
    {
        for (int i = 0; i < SO_HUONG_TIM_DUONG; i++)
        {
            float   goc       = i * (360f / SO_HUONG_TIM_DUONG) * Mathf.Deg2Rad;
            Vector2 candidate = from + new Vector2(Mathf.Cos(goc), Mathf.Sin(goc)) * buoc;
            if (LaDiemDiDuoc(from, candidate)) { return candidate; }
        }

        return from;
    }

    // Nhan dien tuong: ten Wall* / Boundary, bo qua player/coin/dan/zone
    private static bool LaTuong(Collider2D col)
    {
        if (col == null || col.isTrigger) { return false; }
        if (col.GetComponentInParent<TankPlayer>() != null) { return false; }
        if (col.GetComponentInParent<Coin>() != null) { return false; }
        if (col.GetComponentInParent<Projectile>() != null) { return false; }
        if (col.GetComponentInParent<HealingZone>() != null) { return false; }

        Transform t = col.transform;
        while (t != null)
        {
            string ten = t.name;
            if (ten.StartsWith("Wall") || ten.Contains("Boundary")) { return true; }
            t = t.parent;
        }

        return false;
    }
}
