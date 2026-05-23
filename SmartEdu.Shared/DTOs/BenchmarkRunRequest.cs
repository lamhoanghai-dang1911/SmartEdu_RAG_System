using SmartEdu.Shared.Enums;

namespace SmartEdu.Shared.DTOs
{
    public class BenchmarkRunRequest
    {
        public string DatasetId { get; set; }
        public IEnumerable<ChunkStrategy> ChunkStrategies { get; set; }
        public IEnumerable<EmbeddingModel> EmbeddingModels { get; set; }
        public int NumQueries { get; set; } = 100;
    }
}
