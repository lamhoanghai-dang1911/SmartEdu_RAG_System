using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartEdu.Business.Interfaces
{
    public interface IBenchmarkService
    {
        // Thực thi benchmark theo yêu cầu, trả về danh sách kết quả (mỗi cấu hình một BenchmarkResult)
        Task<IEnumerable<BenchmarkResult>> RunBenchmarkAsync(BenchmarkRunRequest request, CancellationToken cancellationToken = default);
    }

}
