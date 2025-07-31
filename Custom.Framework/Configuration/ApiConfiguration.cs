using Custom.Framework.Contracts;
using Custom.Framework.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;
namespace Custom.Framework.Configuration
{
    public class ApiConfiguration : IConfigurationManager, IConfigurationRoot, IDisposable
    {
        // Concurrently modifying config sources or properties is not thread-safe. However, it is thread-safe to read config while modifying sources or properties.
        private readonly IList<IConfigurationSource> _sources;
        private readonly ReferenceCountedProviderManager _providerManager = new();
        private readonly List<IDisposable> _changeTokenRegistrations = new();
        private ConfigurationReloadToken _changeToken = new();
        readonly IConfiguration _configuration;

        public ApiConfiguration(IConfiguration innerConfiguration)
        {
            _configuration = innerConfiguration;
        }

        string? IConfiguration.this[string key]
        {
            get => _configuration[key];
            set => _configuration[key] = value;
        }

        public IConfigurationManager ConfigurationManager => (IConfigurationManager)_configuration;
        public IConfigurationBuilder ConfigurationBuilder => (IConfigurationBuilder)_configuration;
        public IConfigurationSection GetSection(string key) => _configuration.GetSection(key);
        public IEnumerable<IConfigurationSection> GetChildren() => _configuration.GetChildren(); //this.GetChildrenImplementation(null);
        public IList<IConfigurationSource> Sources => ConfigurationManager.Sources;

        IDictionary<string, object> IConfigurationBuilder.Properties => ConfigurationBuilder.Properties;
        IEnumerable<IConfigurationProvider> IConfigurationRoot.Providers => _providerManager.NonReferenceCountedProviders;
        IConfigurationBuilder IConfigurationBuilder.Add(IConfigurationSource source)
        {
            ApiThrowHelper.ThrowIfNull(source);
            _sources.Add(source);
            return this;
        }
        IConfigurationRoot IConfigurationBuilder.Build() => (IConfigurationRoot)ConfigurationManager;

        IChangeToken IConfiguration.GetReloadToken() => _changeToken;

        void IConfigurationRoot.Reload()
        {
            using (var reference = _providerManager.GetReference())
            {
                foreach (IConfigurationProvider provider in reference.Providers)
                {
                    provider.Load();
                }
            }
            RaiseChanged();
        }

        #region private methods

        private ReferenceCountedProviders GetProvidersReference() => _providerManager.GetReference();

        private void RaiseChanged()
        {
            var previousToken = Interlocked.Exchange(ref _changeToken, new ConfigurationReloadToken());
            previousToken.OnReload();
        }

        // Don't rebuild and reload all providers in the common case when a source is simply added to the IList.
        private void AddSource(IConfigurationSource source)
        {
            IConfigurationProvider provider = source.Build(this);

            provider.Load();
            _changeTokenRegistrations.Add(ChangeToken.OnChange(provider.GetReloadToken, RaiseChanged));

            _providerManager.AddProvider(provider);
            RaiseChanged();
        }

        // Something other than Add was called on IConfigurationBuilder.Sources or IConfigurationBuilder.Properties has changed.
        private void ReloadSources()
        {
            DisposeRegistrations();

            _changeTokenRegistrations.Clear();

            var newProvidersList = new List<IConfigurationProvider>();

            foreach (IConfigurationSource source in _sources)
            {
                newProvidersList.Add(source.Build(this));
            }

            foreach (IConfigurationProvider p in newProvidersList)
            {
                p.Load();
                _changeTokenRegistrations.Add(ChangeToken.OnChange(p.GetReloadToken, RaiseChanged));
            }

            //_providerManager.ReplaceProviders(newProvidersList);
            RaiseChanged();
        }

        public void Dispose()
        {
            DisposeRegistrations();
            _providerManager.Dispose();
        }

        private void DisposeRegistrations()
        {
            // dispose change token registrations
            foreach (IDisposable registration in _changeTokenRegistrations)
            {
                registration.Dispose();
            }
        }

        #endregion private methods

        internal abstract class ReferenceCountedProviders : IDisposable
        {
            public static ReferenceCountedProviders Create(List<IConfigurationProvider> providers) => new ActiveReferenceCountedProviders(providers);
            public static ReferenceCountedProviders CreateDisposed(List<IConfigurationProvider> providers) => new DisposedReferenceCountedProviders(providers);
            public abstract List<IConfigurationProvider> Providers { get; set; }
            public abstract List<IConfigurationProvider> NonReferenceCountedProviders { get; }

            public abstract void AddReference();
            // This is Dispose() rather than RemoveReference() so we can conveniently release a reference at the end of a using block.
            public abstract void Dispose();

            private sealed class ActiveReferenceCountedProviders : ReferenceCountedProviders
            {
                private long _refCount = 1;
                private volatile List<IConfigurationProvider> _providers;

                public ActiveReferenceCountedProviders(List<IConfigurationProvider> providers)
                {
                    _providers = providers;
                }

                public override List<IConfigurationProvider> Providers
                {
                    get
                    {
                        Debug.Assert(_refCount > 0);
                        return _providers;
                    }
                    set
                    {
                        Debug.Assert(_refCount > 0);
                        _providers = value;
                    }
                }

                public override List<IConfigurationProvider> NonReferenceCountedProviders => _providers;

                public override void AddReference()
                {
                    // AddReference() is always called with a lock to ensure _refCount hasn't already decremented to zero.
                    Debug.Assert(_refCount > 0);
                    Interlocked.Increment(ref _refCount);
                }

                public override void Dispose()
                {
                    if (Interlocked.Decrement(ref _refCount) == 0)
                    {
                        foreach (IConfigurationProvider provider in _providers)
                        {
                            (provider as IDisposable)?.Dispose();
                        }
                    }
                }
            }

            private sealed class DisposedReferenceCountedProviders : ReferenceCountedProviders
            {
                public DisposedReferenceCountedProviders(List<IConfigurationProvider> providers)
                {
                    Providers = providers;
                }

                public override List<IConfigurationProvider> Providers { get; set; }
                public override List<IConfigurationProvider> NonReferenceCountedProviders => Providers;

                public override void AddReference() { }
                public override void Dispose() { }
            }
        }

        internal sealed class ReferenceCountedProviderManager : IDisposable
        {
            private readonly object _replaceProvidersLock = new object();
            private ReferenceCountedProviders _refCountedProviders = ReferenceCountedProviders.Create(new List<IConfigurationProvider>());
            private bool _disposed;

            // This is only used to support IConfigurationRoot.Providers because we cannot track the lifetime of that reference.
            public IEnumerable<IConfigurationProvider> NonReferenceCountedProviders => _refCountedProviders.NonReferenceCountedProviders;

            public ReferenceCountedProviders GetReference() // ReferenceCountedProviders
            {
                // Lock to ensure oldRefCountedProviders.Dispose() in ReplaceProviders() or Dispose() doesn't decrement ref count to zero
                // before calling _refCountedProviders.AddReference().
                lock (_replaceProvidersLock)
                {
                    if (_disposed)
                    {
                        // Return a non-reference-counting ReferenceCountedProviders instance now that the ConfigurationManager is disposed.
                        // We could preemptively throw an ODE instead, but this might break existing apps that were previously able to
                        // continue to read configuration after disposing an ConfigurationManager.
                        return ReferenceCountedProviders.CreateDisposed(_refCountedProviders.NonReferenceCountedProviders);
                    }

                    _refCountedProviders.AddReference();
                    return _refCountedProviders;
                }
            }

            // Providers should never be concurrently modified. Reading during modification is allowed.
            public void ReplaceProviders(List<IConfigurationProvider> providers)
            {
                object oldRefCountedProviders = _refCountedProviders;

                lock (_replaceProvidersLock)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(nameof(ConfigurationManager));
                    }

                    //_refCountedProviders = ReferenceCountedProviders.Create(providers);
                }

                // Decrement the reference count to the old providers. If they are being concurrently read from
                // the actual disposal of the old providers will be delayed until the final reference is released.
                // Never dispose ReferenceCountedProviders with a lock because this may call into user code.
                //oldRefCountedProviders.Dispose();
            }

            public void AddProvider(IConfigurationProvider provider)
            {
                lock (_replaceProvidersLock)
                {
                    if (_disposed)
                    {
                        throw new ObjectDisposedException(nameof(ConfigurationManager));
                    }

                    // Maintain existing references, but replace list with copy containing new item.
                    _refCountedProviders.Providers = new List<IConfigurationProvider>(_refCountedProviders.Providers)
                {
                    provider
                };
                }
            }

            public void Dispose()
            {
                var oldRefCountedProviders = _refCountedProviders;

                // This lock ensures that we cannot reduce the ref count to zero before GetReference() calls AddReference().
                // Once _disposed is set, GetReference() stops reference counting.
                lock (_replaceProvidersLock)
                {
                    _disposed = true;
                }

                // Never dispose ReferenceCountedProviders with a lock because this may call into user code.
                oldRefCountedProviders.Dispose();
            }
        }
    }
}