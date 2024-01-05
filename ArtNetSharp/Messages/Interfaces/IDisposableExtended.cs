using System;

namespace ArtNetSharp.Messages.Interfaces
{
    internal interface IDisposableExtended : IDisposable
    {
        bool IsDisposing { get; }
        bool IsDisposed { get; }
    }
}
