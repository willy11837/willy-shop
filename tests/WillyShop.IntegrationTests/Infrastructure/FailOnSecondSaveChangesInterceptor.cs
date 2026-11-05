using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WillyShop.IntegrationTests.Infrastructure;

internal sealed class FailOnSecondSaveChangesInterceptor : SaveChangesInterceptor
{
    private int _saveCount;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _saveCount) == 2)
            throw new InvalidOperationException("測試注入的第二次儲存失敗。");

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}