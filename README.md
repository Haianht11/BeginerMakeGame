# BeginerMakeGame

anh chào các em

## Demo hầm ngục

Mở `Assets/Scenes/DungeonDemo.unity` và bấm **Play**. Đây là một phòng thử đi ngang với tường đá xanh lạnh, xích treo, lồng sắt và bốn cụm nến PixelFX cháy lập lòe dọc lối đi. [Ảnh xem trước](Documentation/DungeonDemo/Preview.jpg)

Dùng `A` / `D` hoặc phím mũi tên để sang bên phải. Đứng gần hình nhân, quay mặt về phía nó rồi bấm `Z`. Hình nhân chớp màu, rung và phát tia lửa khi đòn đánh chạm. Bấm `Z` trong từng đòn để thử cả combo ba đòn. Dùng `Space`, `W` hoặc `↑` để nhảy và bấm lần nữa trên không để nhảy đôi.

Agent Sprite Forge và PixelFX nằm trong `.agents/skills` của project. Agent Sprite Forge cung cấp các skill cho map và sprite. Hiệu ứng `impacts/hit-spark` của PixelFX đã được xuất thành sprite sheet để Unity dùng trực tiếp. [Agent Sprite Forge](https://github.com/0x0funky/agent-sprite-forge) · [PixelFX](https://github.com/patch-gg/pixelfx)

## Màn chơi bục nhảy

Màn chơi thử 2D. Nhảy qua các bục từ trái sang phải và chạm cờ vàng để về đích. Cờ chuyển sang xanh đến khi chạy lại màn. Nếu rơi khỏi đường đi, nhân vật tự hồi sinh tại bục xuất phát.

### Chạy trong Unity

1. Mở thư mục dự án bằng Unity `6000.3.10f1`.
2. Mở `Assets/Scenes/SampleScene.unity`, bấm **Play** và chọn cửa sổ **Game**.

### Điều khiển

| Phím | Hành động |
| --- | --- |
| `A` / `D` hoặc `←` / `→` | Di chuyển |
| `Space`, `W` hoặc `↑` | Nhảy. Bấm lần nữa trên không để nhảy đôi. |
| `Z` | Đánh. Bấm tiếp trong lúc đang đánh để nối tối đa ba đòn. |

Lượt nhảy trên không được nạp lại khi đáp lên mặt bục. Nếu bước khỏi mép bục, bạn còn một lượt nhảy trên không. Dùng nhảy đôi để lên bục cắm cờ cao nhất.

### Thử nhanh

- Bấm `Z` một lần để xem đòn đầu. Sau đó bấm `Z` trong từng đòn để chạy đủ combo ba đòn.
- Nhảy hai lần, thử bấm lần ba trước khi đáp, rồi nhảy lại sau khi chạm bục.
- Rơi xuống khe để kiểm tra hồi sinh. Sau đó đi hết đường và kiểm tra cờ đổi sang màu xanh khi chạm vào.
