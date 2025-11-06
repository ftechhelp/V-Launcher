using CommunityToolkit.Mvvm.ComponentModel;

namespace V_Launcher.ViewModels;

/// <summary>
/// Base class for all ViewModels providing common functionality
/// </summary>
public abstract class ViewModelBase : ObservableObject, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets whether the ViewModel has been disposed
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                OnDisposing();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Called when the ViewModel is being disposed. Override to clean up resources.
    /// </summary>
    protected virtual void OnDisposing()
    {
        // Override in derived classes to clean up resources
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the ViewModel has been disposed
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }
}