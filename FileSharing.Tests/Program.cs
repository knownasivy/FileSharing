using FileSharing.Api.Shared;

namespace FileSharing.Tests;

public class Program
{
    [Test]
    public async Task Test1()
    {
        const int sum = 1024 * 1024 * 1024;
        var expected = BytesSize.FromGb(1);
        await Assert.That(expected).IsEqualTo(sum);
    }
}