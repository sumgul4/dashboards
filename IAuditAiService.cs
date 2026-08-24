using TeftisAsistani.Models;

namespace TeftisAsistani.Services;

public interface IAuditAiService
{
    Task<PromptResult> RunPromptAsync(PromptRequest request, CancellationToken ct = default);

    Task<FindingTable> BuildFindingTableAsync(FindingRequest request, CancellationToken ct = default);
}
