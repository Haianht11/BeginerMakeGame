# Mẫu UI Dark Fantasy theo phong cách Metroidvania

Mở `Assets/Scenes/DarkFantasyUIPreview.unity` và bấm **Play** để xem HUD trên nền Boss Arena. Bấm **Inventory** để mở túi đồ rồi chọn một trong sáu vật phẩm. Bấm **Pause** để xem menu tạm dừng, bảng điều khiển và nút **Resume**.

| Prefab trong `Assets/project/UI/DarkFantasy/` | Thành phần |
| --- | --- |
| `CombatHUD.prefab` | Chân dung Swordsman, thanh máu, thể lực, thanh máu Doom Knight và ô dùng nhanh |
| `InventoryScreen.prefab` | Lưới 4 × 4, viền ô đang chọn và bảng chi tiết với sáu vật phẩm mẫu |
| `PauseMenu.prefab` | Nút tiếp tục, nút mở túi đồ và bảng phím Move, Jump, Attack |

## Đưa vào scene game

1. Kéo prefab vào dưới `Canvas` của scene. Scene xem thử dùng `Screen Space - Overlay`, bật `Pixel Perfect`, và đặt `Canvas Scaler` ở chế độ `Scale With Screen Size` với độ phân giải tham chiếu 1920 × 1080, `Match` bằng 0.5.
2. Dùng `Graphic Raycaster` trên Canvas và `InputSystemUIInputModule` trên `EventSystem` để nhận thao tác với nút. Hai prefab menu thường được đặt ở trạng thái ẩn khi bắt đầu màn chơi.
3. Trong Inspector, gán trường `Health Bar` của `Player` và `DoomKnightBoss` tới component `HealthBar` trên `PlayerHealth` và `BossHealth` tương ứng trong `CombatHUD`. Hai component này đã có sẵn tham chiếu tới ảnh Fill. Dùng `PlayerStamina/Fill.fillAmount` với giá trị 0–1 khi hệ thống thể lực có dữ liệu.
4. Gán các sự kiện `Button.onClick` mở và đóng màn hình vào logic điều hướng của game. Scene demo sử dụng các sự kiện `GameObject.SetActive`. Khi tích hợp, thêm hành vi tạm dừng gameplay vào nút Pause và thay dữ liệu mẫu bằng dữ liệu kho đồ thực tế.

Các khung PNG trong `Art` có lề 9-slice để kéo giãn mà giữ góc. Sprite UI dùng bộ lọc `Point` để giữ cạnh pixel. Chỉnh chữ trực tiếp trong `Text` của prefab. Bộ giao diện dùng Silkscreen cho tiêu đề và Pixelify Sans cho nội dung. Giấy phép font nằm trong thư mục `Fonts`. [Nguồn Silkscreen](https://github.com/google/fonts/silkscreen) · [Nguồn Pixelify Sans](https://github.com/eifetx/Pixelify-Sans)

[HUD chiến đấu](CombatHUD.jpg) · [Túi đồ](Inventory.jpg) · [Menu tạm dừng](PauseMenu.jpg)
