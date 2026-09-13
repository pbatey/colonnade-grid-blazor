using ColonnadeGrid.Internal;

namespace ColonnadeGrid.Tests.Internal;

public class GridPagerTests
{
    [Theory]
    [InlineData(0, 1, new[] { 0 })]
    [InlineData(2, 7, new[] { 0, 1, 2, 3, 4, 5, 6 })]
    [InlineData(0, 10, new[] { 0, 1, 2, 3, 4, -1, 9 })]
    [InlineData(3, 10, new[] { 0, 1, 2, 3, 4, -1, 9 })]
    [InlineData(4, 10, new[] { 0, -1, 3, 4, 5, -1, 9 })]
    [InlineData(5, 10, new[] { 0, -1, 4, 5, 6, -1, 9 })]
    [InlineData(6, 10, new[] { 0, -1, 5, 6, 7, 8, 9 })]
    [InlineData(9, 10, new[] { 0, -1, 5, 6, 7, 8, 9 })]
    [InlineData(500, 1000, new[] { 0, -1, 499, 500, 501, -1, 999 })]
    public void GetPageItems_ShowsFirstLastAndCurrentNeighborhood(int pageIndex, int pageCount, int[] expected)
    {
        // -1 stands in for a gap (null) so the cases fit in attributes.
        var items = GridPager.GetPageItems(pageIndex, pageCount).Select(i => i ?? -1);

        Assert.Equal(expected, items);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(50)]
    public void GetPageItems_AlwaysIncludesCurrentPage_AndGapsHideAtLeastTwoPages(int pageCount)
    {
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var items = GridPager.GetPageItems(pageIndex, pageCount);

            Assert.Equal(7, items.Count);
            Assert.Contains(pageIndex, items);
            for (var i = 1; i < items.Count - 1; i++)
            {
                if (items[i] is null)
                {
                    Assert.True(items[i + 1] - items[i - 1] >= 3, $"gap at {i} for page {pageIndex}/{pageCount} hides fewer than two pages");
                }
            }
        }
    }
}
