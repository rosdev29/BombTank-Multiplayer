@echo off
echo ========================================================
echo BOMB TANK - ML-AGENTS TRAINING (ULTRA VERSION)
echo ========================================================
echo.
echo Train 1 Game
echo.

cd /d "d:\BT\Nhom 11\item\BombTank-Multiplayer\MLAgents_Train"
set /p run_id="Nhap ten cho lan Train nay (vd: Train_01): "

mlagents-learn BotTankUltra_config.yaml --run-id=%run_id% --time-scale 5 --force

pause
