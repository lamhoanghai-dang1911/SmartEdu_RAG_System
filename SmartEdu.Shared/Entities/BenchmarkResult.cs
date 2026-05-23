using SmartEdu.Shared.Enums;

namespace SmartEdu.Shared.Entities
{
    public class BenchmarkResult
    {
        public int Id { get; set; }
        public string ModelName { get; set; }           // tên model / fine-tuned / RAG
        public ChunkStrategy ChunkStrategy { get; set; }
        public EmbeddingModel? EmbeddingModel { get; set; }
        public double Precision { get; set; }           // 0..1
        public double Recall { get; set; }              // 0..1
        public double F1 => (Precision + Recall) > 0 ? 2 * Precision * Recall / (Precision + Recall) : 0;
        public long LatencyMs { get; set; }
        public DateTime Timestamp { get; set; }
    }

}
