using ColonnadeGrid.Internal;

namespace ColonnadeGrid.Tests.Internal;

public class IdentityRowKeysTests
{
    [Fact]
    public void SameObject_GetsTheSameKey()
    {
        var item = new object();

        Assert.Equal(IdentityRowKeys.For(item), IdentityRowKeys.For(item));
    }

    [Fact]
    public void DistinctObjects_NeverShareAKey()
    {
        // Object hash codes are only about 26 bits, so at this count they'd
        // collide dozens of times; keys must not.
        var items = Enumerable.Range(0, 100_000).Select(_ => new object()).ToList();

        var keys = items.Select(IdentityRowKeys.For).ToHashSet();

        Assert.Equal(items.Count, keys.Count);
    }
}
