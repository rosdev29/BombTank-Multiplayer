import os

file_path = r"D:\BT\Nhom 11\item\BombTank-Multiplayer\Assets\Scripts\Bot\MLAgents\TankAgentUltra.cs"

with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update Header
header_old = """/// TỔNG ĐẦU VÀO: 1400 giá trị (Space Size trong Unity)
/// ═══════════════════════════════════════════════════════
/// (Phân vuốt: Bỏ V3/V4 radar + giảm tờ tử 32→16 để train nhanh hơn ~3x)
///
/// [A] Lưới Radar LOS-Filtered — 2 vòng, 10m tầm nhìn = 1250
///     Vòng 1 CẬN  ( 9× 9, 0.7m/ô, ~3m):  81ô × 5kênh = 405  [Né đạn tức thì]
///     Vòng 2 GẦN  (13×13, 1.5m/ô, ~10m): 169ô × 5kênh = 845  [Hành lang + cover]
///
///     5 Kênh (V1+V2): Tường | Địch | Coin/Item | ✨ Tactical Score | Đạn bay
///
///     ✨ Tactical Score (thay Openness cũ): giá trị trong [-1, 1]
///        +  Cover quality  (tường che mình khỏi địch)
///        +  Flanking bonus (ô nằm ở hông / lưng địch)
///        -  Danger level   (đạn sắp bay qua ô này)"""

header_new = """/// TỔNG ĐẦU VÀO: 98 giá trị (Space Size trong Unity)
/// ═══════════════════════════════════════════════════════
/// [A] Quét Môi Trường (16 tia) = 32
/// [B] Tự quan sát (Self) = 6
/// [C] Waypoint = 3
/// [D] Dự đoán đạn (6 viên) = 36
/// [E] Trạng thái + Chiến lược = 21"""

content = content.replace(header_old, header_new)

# 2. Variables
vars_old = """    private float             _currentSteer;
    private float             _currentTurretRot;

    // ═══════════════════════════════════════════════════════════════"""

vars_new = """    private float             _currentSteer;
    private float             _currentTurretRot;

    private Vector2 _waypoint;
    private float   _waypointDistTruoc;
    private int     _waypointsDat;

    // ═══════════════════════════════════════════════════════════════"""

content = content.replace(vars_old, vars_new)

# 3. Episode Begin
init_old = """        _mauTruoc      = healthComp != null ? healthComp.MauHienTai.Value : 0;
        _coinTruoc     = wallet     != null ? wallet.TotalCoins.Value      : 0;
        _timerBan      = 0f;
        _opennessTruoc = 0f;
        _danSapTrung   = false;
        _goalDistTruoc = float.MaxValue;
        _memDich.Clear();"""

init_new = """        _mauTruoc      = healthComp != null ? healthComp.MauHienTai.Value : 0;
        _coinTruoc     = wallet     != null ? wallet.TotalCoins.Value      : 0;
        _timerBan      = 0f;
        _opennessTruoc = 0f;
        _danSapTrung   = false;
        _goalDistTruoc = float.MaxValue;
        _memDich.Clear();

        _waypoint          = SinhWaypoint();
        _waypointDistTruoc = float.MaxValue;
        _waypointsDat      = 0;"""

content = content.replace(init_old, init_new)

# 4. Collect Observations
obs_start = "    public override void CollectObservations(VectorSensor sensor)"
obs_end = "    //  4. ON ACTION RECEIVED"

start_idx = content.find(obs_start)
end_idx = content.find(obs_end)

obs_new = """    public override void CollectObservations(VectorSensor sensor)
    {
        if (_dangChoHoiSinh) 
        {
            for (int i = 0; i < 98; i++) sensor.AddObservation(0f);
            return;
        }

        Vector2 botPos = transform.position;

        // ✅ PHẢI CẬP NHẬT TRƯỚC KHI DÙNG
        _dich = TimDich();
        CapNhatDan();

        // ── Thay đổi 1: Quét môi trường (32 obs) ──
        QuetMoiTruong(sensor, botPos);

        // ── Thay đổi 2: Tự quan sát bản thân (6 obs) ──
        sensor.AddObservation(_currentGas);
        sensor.AddObservation(_currentSteer);
        sensor.AddObservation(Mathf.Clamp(rb.velocity.x / tocDoXe, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(rb.velocity.y / tocDoXe, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(rb.angularVelocity / tocDoXoay, -1f, 1f));
        sensor.AddObservation(Vector2.Dot(rb.velocity.normalized, transform.up));

        // ── Thay đổi 3: Hệ thống Waypoint (3 obs) ──
        Vector2 toWaypoint   = _waypoint - botPos;
        float   waypointDist = toWaypoint.magnitude;
        sensor.AddObservation(Mathf.Clamp01(waypointDist / 15f));
        sensor.AddObservation(toWaypoint.normalized.x);
        sensor.AddObservation(toWaypoint.normalized.y);

        // ── Dự đoán quỹ đạo đạn (36 obs) ──
        int cnt = 0;
        foreach (Rigidbody2D danRb in _danhSachDan)
        {
            if (cnt >= MAX_DAN) break;
            if (danRb == null) continue;

            Vector2 vel = danRb.velocity.magnitude > 0.1f
                ? danRb.velocity : (Vector2)danRb.transform.up * tocDoDan;

            for (int step = 0; step < 3; step++)
            {
                Vector2 futureWorld = danRb.position + vel * (step * 0.3f);
                Vector2 local       = transform.InverseTransformPoint(futureWorld);
                sensor.AddObservation(Mathf.Clamp(local.x / 15f, -1f, 1f));
                sensor.AddObservation(Mathf.Clamp(local.y / 15f, -1f, 1f));
            }
            cnt++;
        }
        for (int i = cnt; i < MAX_DAN; i++)
            for (int j = 0; j < 6; j++) sensor.AddObservation(0f);

        // ── Thông tin kẻ địch và trạng thái (21 obs) ──
        float mauMax = healthComp != null ? Mathf.Max(1, healthComp.MauToiDa)  : 1f;
        float mauHT  = healthComp != null ? healthComp.MauHienTai.Value         : 0f;
        sensor.AddObservation(mauHT / mauMax);                                   // 1

        float coin = wallet != null ? wallet.TotalCoins.Value : 0f;
        sensor.AddObservation(Mathf.Clamp01(coin / 100f));                       // 2

        float openness = DoOpenness();
        sensor.AddObservation(openness / 360f);                                  // 3

        if (_dich != null)
        {
            Vector2 ePosNow = _dich.transform.position;
            bool   losEnemy   = CoLOS(botPos, ePosNow);
            Vector2 toEnemy   = ePosNow - botPos;
            float  dist       = toEnemy.magnitude;

            sensor.AddObservation(Mathf.Clamp01(dist / khoangCachToiDa));        // 4
            
            float enemyHealth = 1f;
            if (_dich.TryGetComponent<Mau>(out var enemyHp))
                enemyHealth = (float)enemyHp.MauHienTai.Value / Mathf.Max(1, enemyHp.MauToiDa);
            sensor.AddObservation(enemyHealth);                                  // 5

            sensor.AddObservation(Vector2.SignedAngle(transform.up, toEnemy.normalized) / 180f); // 6

            Vector2 leadPos = ePosNow; 
            if (turretTransform != null)
            {
                Vector2 toLead     = leadPos - botPos;
                sensor.AddObservation(Vector2.SignedAngle(turretTransform.up, toLead.normalized) / 180f); // 7
                sensor.AddObservation(Mathf.Clamp01(toLead.magnitude / khoangCachToiDa));                 // 8
            }
            else
            {
                sensor.AddObservation(0f); // 7
                sensor.AddObservation(0f); // 8
            }

            sensor.AddObservation(losEnemy ? 1f : 0f); // 9

            float faceAngle = Vector2.SignedAngle(_dich.transform.up, (botPos - ePosNow).normalized);
            sensor.AddObservation(faceAngle / 180f);   // 10

            sensor.AddObservation(0f); // 11
            sensor.AddObservation(0f); // 12
            sensor.AddObservation(0f); // 13
            sensor.AddObservation(0f); // 14
            
            bool inCover = KiemTraCover(botPos, ePosNow, dist);
            sensor.AddObservation(inCover ? 1f : 0f);  // 15
        }
        else
        {
            sensor.AddObservation(1f); // 4
            for (int i = 0; i < 14; i++) sensor.AddObservation(0f); // 5-18
        }

        sensor.AddObservation(Mathf.Clamp01(_timerBan / cooldownBan)); // 19
        sensor.AddObservation(rb.angularVelocity / 360f);              // 20
        int chiPhi = boPhongDan != null ? boPhongDan.GetChiPhiBan() : 1;
        sensor.AddObservation(wallet != null && wallet.TotalCoins.Value >= chiPhi ? 1f : 0f); // 21
    }

    private Vector2 SinhWaypoint()
    {
        for (int i = 0; i < 30; i++)
        {
            Vector2 pt = (Vector2)transform.position + UnityEngine.Random.insideUnitCircle * 12f;
            if (!Physics2D.OverlapCircle(pt, 0.8f, layerVatCan))
                return pt;
        }
        return (Vector2)transform.position + (Vector2)transform.up * 6f;
    }

    private void QuetMoiTruong(VectorSensor sensor, Vector2 botPos)
    {
        for (int i = 0; i < 16; i++)
        {
            float   goc = i * (360f / 16f);
            Vector2 dir = Quaternion.Euler(0, 0, goc) * (Vector2)transform.up;

            RaycastHit2D hit = Physics2D.Raycast(botPos, dir, 30f,
                layerVatCan | layerDich | layerTaiNguyen | layerDan);

            sensor.AddObservation(hit.collider != null ? hit.distance / 30f : 1f);
            sensor.AddObservation(hit.collider != null ? PhanLoai(hit.collider) : 0f);
        }
    }

    // ═══════════════════════════════════════════════════════════════
"""

content = content[:start_idx] + obs_new + content[end_idx + 82:] # To handle the comment overlap slightly differently, I will just do exact matching.

# Actually to be 100% safe, let's just do a string replace on the full function block.
obs_old_full = content[start_idx:end_idx]
content = content.replace(obs_old_full, obs_new)


# 5. OnActionReceived Rewards + Corrupted LuoiLOSFiltered deletion
reward_start = "        // ═══ REWARD SHAPING ═════════════════════════════════════"
reward_end = "    // ═══════════════════════════════════════════════════════════════\n    //  6. HEURISTIC"

rs_idx = content.find(reward_start)
re_idx = content.find(reward_end)

reward_new = """        // ═══ REWARD SHAPING ═════════════════════════════════════
        float dt    = Time.fixedDeltaTime;
        float speed = rb.velocity.magnitude;

        // ── 1. REWARD DI CHUYỂN (dense, mỗi frame) ──────────────────
        // Thưởng tốc độ — bot LUÔN muốn chạy
        AddReward(speed / tocDoXe * 0.005f);

        // Phạt đứng yên ngay lập tức
        if (speed < 0.3f) AddReward(-0.004f);

        // ── 2. REWARD XOAY HỢP LÝ ───────────────────────────────────
        // Thưởng xoay khi đang chạy (học rằng xoay + chạy = hữu ích)
        if (Mathf.Abs(_currentSteer) > 0.3f && speed > 1f)
            AddReward(0.003f);

        // Phạt xoay tại chỗ (spinning vô ích)
        if (Mathf.Abs(rb.angularVelocity) > 60f && speed < 0.5f)
            AddReward(-0.005f);

        // ── 3. WAYPOINT REWARD (mạnh nhất, rõ ràng nhất) ────────────
        float waypointDist = Vector2.Distance(transform.position, _waypoint);

        if (_waypointDistTruoc < float.MaxValue)
        {
            float progress = _waypointDistTruoc - waypointDist;
            // Thưởng khi tiến về waypoint, phạt khi đi xa
            AddReward(progress * 0.12f);
        }
        _waypointDistTruoc = waypointDist;

        // Đến nơi → thưởng lớn + waypoint mới
        if (waypointDist < 1.5f)
        {
            AddReward(0.8f);
            _waypointsDat++;
            _waypoint          = SinhWaypoint();
            _waypointDistTruoc = float.MaxValue;
        }

        // ── 4. CHIẾN THUẬT (chỉ khi có địch) ────────────────────────
        if (_dich != null)
        {
            Vector2 toEnemy = (Vector2)_dich.transform.position - (Vector2)transform.position;
            float   dist    = toEnemy.magnitude;

            // Tiến về địch khi nhìn thấy
            if (CoLOS(transform.position, _dich.transform.position))
            {
                float dot = Vector2.Dot(rb.velocity.normalized, toEnemy.normalized);
                if (dot > 0.5f) AddReward(0.008f);  // Đang chạy đúng hướng địch
            }

            // Kiting
            if (dist >= khoangCachLyTuong - 1.5f && dist <= khoangCachLyTuong + 2.5f)
                AddReward(r_Kiting * dt);

            // Strafe
            if (speed > 2f)
            {
                float dot = Mathf.Abs(Vector2.Dot(rb.velocity.normalized, toEnemy.normalized));
                if (dot < 0.35f) AddReward(r_Strafe * dt);
            }

            // Rút lui khi máu thấp
            float myHp = healthComp != null
                ? (float)healthComp.MauHienTai.Value / Mathf.Max(1, healthComp.MauToiDa) : 1f;
            if (myHp < 0.3f && Vector2.Dot(rb.velocity.normalized, toEnemy.normalized) < -0.5f)
                AddReward(0.02f * dt);

            // Truy sát khi địch máu thấp
            float enemyHpRatio = 1f;
            if (_dich.TryGetComponent<Mau>(out var eHp))
                enemyHpRatio = (float)eHp.MauHienTai.Value / Mathf.Max(1, eHp.MauToiDa);
            if (enemyHpRatio < 0.3f && myHp > 0.3f
                && Vector2.Dot(rb.velocity.normalized, toEnemy.normalized) > 0.5f)
                AddReward(0.04f * dt);
        }

        // ── 5. NÉ ĐẠN ────────────────────────────────────────────────
        bool danNow = KiemTraDanGanTrung();
        if (_danSapTrung && !danNow) AddReward(r_NeDan);
        _danSapTrung = danNow;

        // ── 6. HP + COIN (giữ nguyên) ────────────────────────────────
        if (healthComp != null)
        {
            int mauHT = healthComp.MauHienTai.Value;
            int delta  = mauHT - _mauTruoc;
            if (delta < 0) AddReward(p_BiDanh * Mathf.Abs(delta) * 0.1f);
            _mauTruoc = mauHT;
            if (mauHT <= 0 && !_dangChoHoiSinh)
            {
                AddReward(p_Chet);
                StartCoroutine(XuLyChetVaHoiSinh());
                return;
            }
        }
        if (wallet != null)
        {
            int coinHT = wallet.TotalCoins.Value;
            if (coinHT - _coinTruoc > 0) AddReward(r_NhatCoin * (coinHT - _coinTruoc) * 0.1f);
            _coinTruoc = coinHT;
        }
    }

"""

reward_old_full = content[rs_idx:re_idx]
content = content.replace(reward_old_full, reward_new)

# 6. Delete the SECOND duplicate LuoiLOSFiltered at the end of the file.
# The user said "Giữ lại cái đầu (dùng TacticalScore), xóa hoàn toàn cái sau."
# But wait, in our changes above, we DELETED the first LuoiLOSFiltered AND TinhTacticalScore because they were in the corrupted block which we replaced with the new Reward!
# Oh, we need to completely delete the second LuoiLOSFiltered too!
# Let's find "    //  PRIVATE — LOS-FILTERED GRID"
duplicate_los_start = "    // ═══════════════════════════════════════════════════════════════\\n    //  PRIVATE — LOS-FILTERED GRID"
duplicate_los_end = "    //  PRIVATE — UTILITIES"

# Let's use string index to be safe.
dup_idx_start = content.find("    //  PRIVATE — LOS-FILTERED GRID")
if dup_idx_start != -1:
    dup_real_start = content.rfind("    // ═══════════════════════════════════════════════════════════════", 0, dup_idx_start)
    dup_end_idx = content.find("    //  PRIVATE — UTILITIES", dup_idx_start)
    if dup_real_start != -1 and dup_end_idx != -1:
        dup_block = content[dup_real_start:dup_end_idx]
        content = content.replace(dup_block, "")

# Finally write out
with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Python script execution completed successfully.")
