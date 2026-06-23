using UnityEngine;

public class ExitGameController : MonoBehaviour
{
    // Kéo ExitPopupPanel ở Hierarchy vào ô này trong Inspector
    public GameObject exitPopupPanel;

    // Hàm này gọi khi bấm nút "THOÁT" ngoài màn hình chính
    public void OpenPopup()
    {
        exitPopupPanel.SetActive(true);
    }

    // Hàm này gọi khi bấm nút "Không" (Hủy thoát)
    public void ClosePopup()
    {
        exitPopupPanel.SetActive(false);
    }

    // Hàm này gọi khi bấm nút "Có" (Xác nhận thoát)
    public void ConfirmExit()
    {
        // 1. Ẩn bảng popup xác nhận đi
        exitPopupPanel.SetActive(false);

        // 2. Tự động tìm thành phần GameHUD trong trận đấu để gọi lệnh thoát phòng mạng sạch sẽ
        // Cách này giúp nút Play màu xanh vẫn sáng, game không bị tắt và có thể tạo phòng mới bình thường!
        GameHUD gameHUD = FindObjectOfType<GameHUD>();

        if (gameHUD != null)
        {
            gameHUD.LeaveGame(); // Gọi hàm xử lý thoát trận, hủy kết nối mạng gốc của game bạn
        }
        else
        {
            // Phương án dự phòng: Nếu không tìm thấy GameHUD thì vẫn cố gắng chuyển về Menu
            UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
        }
    }
}