using Coworking.Application.Common.Pagination;

namespace Coworking.UnitTests.Common;

/// <summary>TotalPages rounds up and is 0 for an empty result.</summary>
public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(41, 20, 3)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expectedPages)
    {
        var page = new PagedResult<int>([], 1, pageSize, totalCount);

        Assert.Equal(expectedPages, page.TotalPages);
    }
}
