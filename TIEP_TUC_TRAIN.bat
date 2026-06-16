@echo off
echo ========================================================
echo BOMB TANK - TIEP TUC TRAIN (RESUME)
echo ========================================================
echo.
echo ⚠️  Luu y: Phai nhap DUNG ten thu muc ban da train truoc do!
echo.

cd /d "d:\BT\Nhom 11\item\BombTank-Multiplayer\MLAgents_Train"
set /p run_id="Nhap ten cua lan Train can Resume (vd: Train_01): "

mlagents-learn BotTankUltra_config.yaml --run-id=%run_id% --time-scale 5 --resume

pause
