import sys

file_path = r"D:\BT\Nhom 11\item\BombTank-Multiplayer\Assets\Scripts\Bot\MLAgents\TankAgentUltra.cs"

with open(file_path, "r", encoding="utf-8") as f:
    lines = f.readlines()

# 1. Update Header
header_start = -1
header_end = -1
for i, line in enumerate(lines):
    if "TỔNG ĐẦU VÀO:" in line:
        header_start = i
    if "✨ Tactical Score" in line and "danger level" in line.lower():
        header_end = i
        break

if header_start != -1 and header_end != -1:
    new_header = [
        "/// TỔNG ĐẦU VÀO: 98 giá trị (Space Size trong Unity)\n",
        "/// ═══════════════════════════════════════════════════════\n",
        "/// [A] Quét Môi Trường (16 tia) = 32\n",
        "/// [B] Tự quan sát (Self) = 6\n",
        "/// [C] Waypoint = 3\n",
        "/// [D] Dự đoán đạn (6 viên) = 36\n",
        "/// [E] Trạng thái + Chiến lược = 21\n"
    ]
    lines = lines[:header_start] + new_header + lines[header_end+1:]

# 2. Add Waypoint Variables
vars_idx = -1
for i, line in enumerate(lines):
    if "private float" in line and "_currentTurretRot;" in line:
        vars_idx = i
        break

if vars_idx != -1:
    lines.insert(vars_idx + 1, "    private Vector2 _waypoint;\n    private float   _waypointDistTruoc;\n    private int     _waypointsDat;\n\n")

# 3. OnEpisodeBegin
init_idx = -1
for i, line in enumerate(lines):
    if "_memDich.Clear();" in line:
        init_idx = i
        break

if init_idx != -1:
    lines.insert(init_idx + 1, "        _waypoint          = SinhWaypoint();\n        _waypointDistTruoc = float.MaxValue;\n        _waypointsDat      = 0;\n")

# 4. CollectObservations replacement
obs_start = -1
obs_end = -1
for i, line in enumerate(lines):
    if "public override void CollectObservations" in line:
        obs_start = i
    if "4. ON ACTION RECEIVED" in line:
        obs_end = i - 1
        break

if obs_start != -1 and obs_end != -1:
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
    # Replace lines
    new_obs_lines = [line + "\n" for line in obs_new.split("\n")]
    lines = lines[:obs_start] + new_obs_lines + lines[obs_end:]

# 5. OnActionReceived Rewards + Corrupted block deletion
reward_start = -1
reward_end = -1

for i, line in enumerate(lines):
    if "REWARD SHAPING" in line:
        reward_start = i
    if "6. HEURISTIC" in line:
        reward_end = i - 1
        break

if reward_start != -1 and reward_end != -1:
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

    // ═══════════════════════════════════════════════════════════════
"""
    new_reward_lines = [line + "\n" for line in reward_new.split("\n")]
    lines = lines[:reward_start] + new_reward_lines + lines[reward_end:]

# 6. Delete SECOND duplicate LuoiLOSFiltered at bottom of file
dup_los_start = -1
dup_los_end = -1
for i, line in enumerate(lines):
    if "PRIVATE — LOS-FILTERED GRID" in line:
        dup_los_start = i - 1  # include the comment line before it
    if "PRIVATE — UTILITIES" in line:
        dup_los_end = i - 1
        break

if dup_los_start != -1 and dup_los_end != -1:
    lines = lines[:dup_los_start] + lines[dup_los_end:]

with open(file_path, "w", encoding="utf-8") as f:
    f.writelines(lines)

print(f"File {file_path} updated safely.")
