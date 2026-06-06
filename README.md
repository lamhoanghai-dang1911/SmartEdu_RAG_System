# SmartEdu RAG System 🎓🤖

Hệ thống hỗ trợ học tập thông minh dựa trên kiến trúc **RAG (Retrieval-Augmented Generation)**. Dự án được phát triển nhằm giúp sinh viên truy vấn kiến thức từ tài liệu môn học một cách nhanh chóng, chính xác, đồng thời cung cấp công cụ quản lý tri thức trực quan cho giảng viên.

## 🌟 Tính năng chính
- **Quản lý tài liệu:** Hỗ trợ upload file bài giảng (PDF, DOCX). Tự động trích xuất và phân nhỏ văn bản (Chunking).
- **Hỏi đáp thông minh:** Chatbot trả lời sinh viên dựa trên ngữ cảnh tài liệu thực tế của môn học, có trích dẫn nguồn gốc rõ ràng.
- **Nghiên cứu RBL (Research-Based Learning):** Module thực nghiệm so sánh chất lượng câu trả lời giữa các mô hình AI khác nhau.
- **Trải nghiệm tức thì:** Giao diện trực quan, phản hồi thời gian thực.

## 🏗 Kiến trúc & Công nghệ
- **Mô hình:** 3-Layers Architecture (Web - Business - Data).
- **Công nghệ lõi:** .NET 8.0, PostgreSQL/MySQL (Vector Storage).
- **AI & Frontend:** OpenAI API / PhoBERT, Bootstrap 5, AJAX, SignalR.

---

## Khởi tạo Cơ sở dữ liệu
Dự án cung cấp sẵn file Script SQL chứa đầy đủ cấu trúc bảng và dữ liệu mẫu (Tài khoản, phân quyền, dữ liệu tài liệu test).

1. Mở phần mềm SQL Server Management Studio (SSMS) và kết nối vào Server của bạn.
2. Tìm đến tệp tin script nằm tại đường dẫn: Database/script.sql
3. Đảm bảo đã chọn đúng môi trường, nhấp nút Execute (F5) trên thanh công cụ để khởi chạy. Quá trình này sẽ thiết lập cấu trúc bảng cùng toàn bộ dữ liệu thực nghiệm đầu vào.
4. Mở file `appsettings.json` trong dự án `SmartEdu.Web` và cấu hình lại chuỗi kết nối `DefaultConnection` sao cho khớp với thông tin SQL Server trên máy của bạn:

## Cấu hình API Key bảo mật (User Secrets)
1. Nhấp chuột phải vào project SmartEdu.Web trong cửa sổ Solution Explorer.
2. Chọn Manage User Secrets.
3. Một file secrets.json sẽ hiện ra. Bạn hãy dán đoạn mã sau vào, thay thế bằng Key của bạn và lưu lại


## 📖 Hướng dẫn sử dụng

Hệ thống được thiết kế với 3 nhóm người dùng (Role) riêng biệt, mỗi nhóm sẽ có luồng thao tác và quyền hạn khác nhau:

👑 1. Quản trị viên (Admin)
Đóng vai trò điều phối và thiết lập dữ liệu nền tảng cho hệ thống.

Quản lý tài khoản: Truy cập menu Quản lý tài khoản để tạo mới tài khoản, menu Quản lý giáo viên và Quản lý học sinh để cấp quyền tài khoản của Giảng viên và Sinh viên.

Quản lý danh mục: Truy cập 📚 Quản lý môn học để khởi tạo các môn học mới và phân công Giảng viên phụ trách.

ACCOUNT SỬ DỤNG: admin/123

👨‍🏫 2. Giảng viên (Lecturer)
Đóng vai trò xây dựng "bộ não" kiến thức cho AI thông qua việc cung cấp và kiểm duyệt tài liệu.

Đóng góp tài liệu: Truy cập menu 📄 Tài liệu, chọn môn học mình phụ trách và tải lên các file bài giảng (PDF/DOCX).

Huấn luyện AI (Embedding): - Tại bảng danh sách tài liệu, nhấn nút ⚡ (Kích hoạt). Hệ thống sẽ tự động đọc, bóc tách văn bản và lưu trữ dưới dạng Vector.

Quá trình này chạy ngầm và có cửa sổ Log hiển thị tiến độ thời gian thực.

Kiểm duyệt chất lượng: - Sau khi xử lý xong (trạng thái ✅ Sẵn sàng), nhấn nút 🧩 (Xem Chunks).

Hệ thống sẽ liệt kê chi tiết từng "mảnh ghép" nội dung đã được AI trích xuất, giúp giảng viên dễ dàng kiểm tra xem AI có hiểu đúng cấu trúc bài giảng hay không.

ACCOUNT SỬ DỤNG: lecturer/123

🎓 3. Sinh viên (Student)
Người dùng cuối tận hưởng trải nghiệm học tập và ôn thi thông minh.

Truy cập không gian học tập: Đăng nhập và truy cập vào menu 🤖 Chat RAG.

Hỏi đáp trực tiếp: - Lựa chọn môn học cần ôn tập từ danh sách.

Nhập câu hỏi vào khung chat.

Nhận kết quả chuẩn xác: - Thay vì trả lời chung chung, Chatbot sẽ tìm kiếm đúng các "mảnh ghép" tài liệu mà Giảng viên đã upload để tổng hợp câu trả lời.

Mỗi câu trả lời đều có chú thích rõ ràng: Được trích xuất từ tài liệu nào, đoạn văn bản số mấy, giúp sinh viên hoàn toàn yên tâm về độ tin cậy của thông tin.

ACCOUNT SỬ DỤNG: student/123

SƠ ĐỒ KIẾN TRÚC:

<img width="667" height="679" alt="image" src="https://github.com/user-attachments/assets/25dbecd1-bfbd-427a-a33d-50061902950e" />

