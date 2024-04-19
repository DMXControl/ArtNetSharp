using System;

namespace ArtNetSharp.Messages.Interfaces
{
    public interface IDisposableExtended : IDisposable
    {
        bool IsDisposing { get; }
        bool IsDisposed { get; }
    }
}
