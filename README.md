# BeginerMakeGame

anh chào các em

Màn chơi thử 2D. Nhảy qua các bục từ trái sang phải và chạm cờ vàng để về đích. Cờ chuyển sang xanh đến khi chạy lại màn. Nếu rơi khỏi đường đi, nhân vật tự hồi sinh tại bục xuất phát.

## Chạy trong Unity

1. Mở thư mục dự án bằng Unity `6000.3.10f1`.
2. Mở `Assets/Scenes/SampleScene.unity`, bấm **Play** và chọn cửa sổ **Game**.

## Điều khiển

| Phím | Hành động |
| --- | --- |
| `A` / `D` hoặc `←` / `→` | Di chuyển |
| `Space`, `W` hoặc `↑` | Nhảy. Bấm lần nữa trên không để nhảy đôi. |
| `Z` | Đánh. Bấm tiếp trong lúc đang đánh để nối tối đa ba đòn. |

Lượt nhảy trên không được nạp lại khi đáp lên mặt bục. Nếu bước khỏi mép bục, bạn còn một lượt nhảy trên không. Dùng nhảy đôi để lên bục cắm cờ cao nhất.

## Thử nhanh

- Bấm `Z` một lần để xem đòn đầu. Sau đó bấm `Z` trong từng đòn để chạy đủ combo ba đòn.
- Nhảy hai lần, thử bấm lần ba trước khi đáp, rồi nhảy lại sau khi chạm bục.
- Rơi xuống khe để kiểm tra hồi sinh. Sau đó đi hết đường và kiểm tra cờ đổi sang màu xanh khi chạm vào.
