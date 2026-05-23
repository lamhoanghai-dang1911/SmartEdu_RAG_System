using SmartEdu.Business.Interfaces;
using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartEdu.Business.Services
{
    public class RBLComparisonService
    {
        private readonly IBenchmarkService _embeddingBench;
        // inject DB context or repository to store results
        public RBLComparisonService(IBenchmarkService embeddingBench /*, RblDbContext db */)
        {
            _embeddingBench = embeddingBench;
        }

        // So sánh RAG pipeline với predicted outputs from fine-tuned model (mô phỏng)
        public async Task<IEnumerable<BenchmarkResult>> CompareRagWithFineTunedAsync(BenchmarkRunRequest request, CancellationToken cancellationToken = default)
        {
            // 1) chạy benchmark cho RAG (embeddingBench hoặc chunkingBench)
            var ragResults = await _embeddingBench.RunBenchmarkAsync(request, cancellationToken);

            // 2) mô phỏng fine-tuned model evaluation (thực tế: gọi model inference, so sánh với ground-truth)
            var fineResults = new List<BenchmarkResult>();
            foreach (var r in ragResults)
            {
                // giả sử fine-tuned model có precision +/- biến đổi
                fineResults.Add(new BenchmarkResult
                {
                    ModelName = "FineTunedBaseline",
                    ChunkStrategy = r.ChunkStrategy,
                    EmbeddingModel = r.EmbeddingModel,
                    Precision = Math.Max(0, r.Precision - 0.05),
                    Recall = Math.Max(0, r.Recall - 0.06),
                    LatencyMs = r.LatencyMs / 2, // giả sử fine-tuned nhanh hơn
                    Timestamp = DateTime.UtcNow
                });
            }

            // 3) kết hợp / lưu / trả về để frontend vẽ chart so sánh
            var all = new List<BenchmarkResult>();
            all.AddRange(ragResults);
            all.AddRange(fineResults);
            return all;
        }
    }

}
