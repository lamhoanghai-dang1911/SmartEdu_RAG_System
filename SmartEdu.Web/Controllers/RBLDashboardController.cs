using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEdu.Business.Interfaces;
using SmartEdu.Data; // Đảm bảo đã using đúng namespace chứa AppDbContext
using SmartEdu.Shared.DTOs;
using SmartEdu.Shared.Entities;
using SmartEdu.Shared.Enums;
using SmartEdu.Shared.Models; // Đảm bảo đã using đúng namespace chứa BenchmarkResult

[ApiController]
[Route("api/rbl/dashboard")]
public class RBLDashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBenchmarkService _benchmarkService;

    // Sửa lại constructor để inject AppDbContext thay vì RblDbContext
    public RBLDashboardController(AppDbContext db, IBenchmarkService benchmarkService)
    {
        _db = db;
        _benchmarkService = benchmarkService;
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults([FromQuery] int limit = 100)
    {
        var data = await _db.BenchmarkResults
            .OrderByDescending(r => r.Timestamp)
            .Take(limit)
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("results/filter")]
    public async Task<IActionResult> GetFiltered([FromQuery] string modelName = null, [FromQuery] string chunkStrategy = null)
    {
        var q = _db.BenchmarkResults.AsQueryable();

        if (!string.IsNullOrEmpty(modelName))
            q = q.Where(x => x.ModelName == modelName);

        if (!string.IsNullOrEmpty(chunkStrategy))
        {
            // Thử chuyển đổi string sang Enum
            if (Enum.TryParse<ChunkStrategy>(chunkStrategy, true, out var strategy))
            {
                q = q.Where(x => x.ChunkStrategy == strategy);
            }
            else
            {
                return BadRequest("Invalid ChunkStrategy value.");
            }
        }

        var list = await q.OrderByDescending(x => x.Timestamp).ToListAsync();
        return Ok(list);
    }

    [HttpPost("run-test-data")]
    public async Task<IActionResult> RunTestData()
    {
        var fakeResult = new BenchmarkResult
        {
            ModelName = "RAG-Test-Model",
            ChunkStrategy = ChunkStrategy.FixedSize, // Đảm bảo khớp với Enum của bạn
            EmbeddingModel = EmbeddingModel.MultilingualE5Base,
            Precision = 0.85,
            Recall = 0.90,
            LatencyMs = 150,
            Timestamp = DateTime.UtcNow
        };

        _db.BenchmarkResults.Add(fakeResult);
        await _db.SaveChangesAsync();

        return Ok("Đã thêm dữ liệu test vào bảng!");
    }

    [HttpPost("run-benchmark")]
    public async Task<IActionResult> RunBenchmark([FromBody] BenchmarkRunRequest request)
    {
        // Gọi service chạy benchmark (đã đăng ký trong Program.cs)
        var results = await _benchmarkService.RunBenchmarkAsync(request);
        return Ok(results);
    }
}