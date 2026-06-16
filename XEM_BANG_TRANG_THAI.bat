@echo off
echo ========================================================
echo BOMB TANK - BANG TRANG THAI MODEL (TENSORBOARD)
echo ========================================================
echo.
echo Dang mo bang trang thai...
echo Vui long truy cap vao duong link: http://localhost:6006 tren trinh duyet web cua ban.
echo (Ban co the de cua so nay chay ngam cung voi file Train)
echo.

cd /d "d:\BT\Nhom 11\item\BombTank-Multiplayer\MLAgents_Train"

:: Kiem tra xem da co thu muc results chua
if not exist "results" (
    echo [CANH BAO] Chua thay thu muc "results". Ban can chay Train it nhat 1 lan truoc nhe!
    pause
    exit
)

tensorboard --logdir results

pause
