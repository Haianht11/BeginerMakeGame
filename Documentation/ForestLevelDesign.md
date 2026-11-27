# Thiết kế màn rừng — 3 chặng

## Cơ sở thiết kế

- Gói `GandalfHardcore FREE Platformer Assets.zip` đã giải nén vào `Temp/GandalfHardcore-extracted` để đọc. Bộ này đã có bản trong `Assets/project/art/Sprites/map/Theforest/GandalfHardcore FREE Platformer Assets`, nên không chép trùng vào `Assets`.
- Đọc `READ ME.txt`: cho phép dùng và chỉnh sửa trong game thương mại/phi thương mại; không được bán lại hoặc phân phối lại bộ asset.
- Asset phù hợp: `Floor Tiles1/2` (các biến thể cỏ xanh, đất khô, tuyết), `BG Dirt1/2`, cây thông, liễu rủ, thân cây, cỏ cao, đuốc, tượng thiên thần, thuyền, nước động, lửa trại và portal. Dùng biến thể cỏ xanh làm nền rừng; giữ tượng, đuốc và bóng cây làm điểm nhấn gothic.
- Project hiện dùng camera orthographic size 5, khung 16:9 xấp xỉ **17.78 × 10 world units**. Mỗi chặng dài 3 khung camera, khoảng **53.34 units**; các tọa độ bên dưới là mốc blockout, cần căn lại theo prefab và collider khi dựng trong Unity.
- Đây là thiết kế layout, chưa tạo Unity Scene, Tilemap hay collider.

## Tuyến tổng thể

```text
[01 Rừng Rêu Tắt] ── cổng rễ cây ── [02 Đầm Nước Đen] ── miếu đổ ── [03 Miếu Cội Rễ]
      3 khung camera                    3 khung camera                  3 khung camera
```

Nhịp chơi tăng dần: học nhảy và đọc địa hình → vượt đầm với đường cao/thấp → leo lên sân boss. Mỗi chặng có một lối chính dễ đọc và một nhánh bí mật tùy chọn.

## 01 — Rừng Rêu Tắt

**Vai trò:** mở đầu, dạy nhảy qua hố và nhận biết nền nguy hiểm. Không gây áp lực bằng kẻ địch ngay từ điểm xuất phát.

| Khung camera | Blockout và nhịp chơi | Asset gợi ý |
|---|---|---|
| 0–17.78 | Spawn trên nền rộng tại x≈1.5. Đường chính chạy thấp; một hố ngắn ở khoảng x=7–9 dạy cú nhảy đầu. Cho khoảng nghỉ an toàn sau hố. | Floor Tiles1 hàng cỏ xanh, BG Dirt1, Birch1/2 |
| 17.78–35.56 | Nền chia thành hai bậc thấp quanh gốc cây. Hố thứ hai dài hơn một chút; nhánh cao bên trên dẫn tới vật phẩm hồi máu/thu thập, rồi nhập lại đường chính. | Tree1–4, Tall Grass, torch, platform cỏ |
| 35.56–53.34 | Clearing rộng, không đặt hố sát cổng. Đặt lửa trại làm checkpoint trước portal/cổng rễ sang chặng 2. Có thể đặt tượng thiên thần làm landmark, tránh che silhouette nhân vật. | Campfire sheet, Angel Statue, portal sheet |

**Điểm kiểm soát:** không đặt gai trên đường bắt buộc ở chặng đầu; nhánh bí mật phải quay lại tuyến chính mà không cần rơi chết.

## 02 — Đầm Nước Đen

**Vai trò:** giới thiệu nước/hố sâu và lựa chọn độ cao. Nền thấp tạo cảm giác hiểm, nhưng không ép người chơi chờ cơ chế chuyển động chưa có trong project.

| Khung camera | Blockout và nhịp chơi | Asset gợi ý |
|---|---|---|
| 0–17.78 | Vào đầm bằng đường đất hẹp, mặt nước ở dưới chân. Hai bục cố định qua vùng nước; bờ đối diện rộng để người chơi ổn định. | Animated Water Tiles, Boat (trang trí), BG Dirt2 |
| 17.78–35.56 | Thân cây đổ tạo cầu thấp; lối trên rễ cây là đường chính, lối trên cao có vật phẩm phụ và rủi ro rơi xuống nước. Một cụm gai treo báo trước hazard trước khi xuất hiện. | Weeping Willow, Birch logs, spike/ground pieces của Floor Tiles2 |
| 35.56–53.34 | Đường chính leo dần lên bờ đá. Đặt checkpoint cạnh đuốc/tượng trước cửa miếu. Lối tắt thấp nối ngược về điểm giữa, mở sau khi chạm công tắc hoặc nhặt chìa khóa (nếu gameplay có tương tác). | Torch, Angel Statue, Decor, portal/door |

**Điểm kiểm soát:** dùng bục tĩnh trong bản đầu để nhảy chính xác; thuyền chỉ là trang trí đến khi có quy tắc platform chuyển động.

## 03 — Miếu Cội Rễ

**Vai trò:** đoạn kết của cụm rừng, kết hợp platforming đã học rồi mở ra một đấu trường boss.

| Khung camera | Blockout và nhịp chơi | Asset gợi ý |
|---|---|---|
| 0–17.78 | Lối vào hẹp, nền liên tục trong vài đơn vị rồi chuyển thành bậc đá/rễ so le. Có một khoảng nghỉ trước đoạn leo. | Floor Tiles2 đất khô, Decor, Birch deadwood |
| 17.78–35.56 | Leo qua ba tầng bục: thấp → trung → thấp. Một nhánh trên cao giấu vật phẩm; đường chính vẫn nhìn thấy rõ cửa miếu ở phía xa. Chèn một encounter nhỏ ở nền rộng, không giữa cú nhảy. | Tree1–4, Angel Statue, Alchemy Decor (nếu hợp tông) |
| 35.56–53.34 | Cửa vào sân boss trên nền phẳng rộng khoảng 12–14 units. Khóa cổng sau khi vào; giữ hai mép sân thông thoáng cho né/đánh. Boss có thể dùng Doom Knight hiện có; lửa trại/checkpoint đặt trước cửa, không đặt trong sân. | Boss prefab hiện có, torches, campfire, statue, BG Dirt2 |

**Điểm kiểm soát:** cho người chơi thấy sân đấu trước khi bước qua cửa; không đặt platform nhỏ trong vùng boss nếu chưa thử collider và camera.

## Quy tắc blockout chung

- Dùng mặt đất liên tục để thể hiện tuyến chính; khoảng trống bên dưới luôn là hố/rủi ro, không để nền trang trí trông giống bề mặt đi được.
- Đặt cây, tán lá, tượng và cành ở layer nền/tiền cảnh theo chiều sâu; không che điểm đáp hoặc mép platform.
- Giữ khoảng trống phía trên các cú nhảy và trước checkpoint/cửa. Đặt landmark sáng (đuốc, lửa trại) tại chỗ người chơi cần định hướng.
- Chỉ dùng các hazard mà asset và gameplay hiện có hỗ trợ. Nước động là hình ảnh cho tới khi có collider hoặc luật chết/đẩy lùi được xác nhận.

## Thứ tự dựng đề xuất

1. Blockout nền, hố, bục, spawn, checkpoint và exit theo các mốc 0–53.34 units.
2. Chạy thử với prefab Player để chỉnh độ dài cú nhảy, khoảng hố và camera; các khoảng cách hiện tại là phác thảo, chưa được kiểm tra bằng physics.
3. Thêm nền parallax và cây lớn; cuối cùng thêm cỏ, đuốc, tượng, nước và hạt/hiệu ứng.
4. Nếu cần chốt một màn trước, dựng **Màn 01** trước; Màn 02 và 03 mở rộng cùng bộ tileset, không cần thêm asset pack.
