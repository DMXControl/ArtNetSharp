using ArtNetSharp;
using NUnit.Framework.Internal;

namespace ArtNetTests
{
    [Order(int.MaxValue)]
    public class Dispose
    {
        [Test]
        public async Task DoDispose()
        {
            var artnet = new ArtNet();
            Assert.Multiple(() =>
            {
                Assert.That(artnet.IsDisposing, Is.False);
                Assert.That(artnet.IsDisposed, Is.False);
            });
            await Task.Delay(8000);
            ((IDisposable)artnet).Dispose();
            await Task.Delay(4000);

            Assert.That(artnet.IsDisposed, Is.True);
        }
    }
}