using System;
using System.Threading;
using System.Threading.Tasks;

namespace TIKSN.Data
{
    public abstract class UnitOfWorkBase : IUnitOfWork
    {
        public abstract Task CompleteAsync(CancellationToken cancellationToken);
        public abstract Task DiscardAsync(CancellationToken cancellationToken);

        public virtual void Dispose()
        {
            if (this.IsDirty())
            {
                throw new InvalidOperationException("Unit of work disposed without completion.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (this.IsDirty())
            {
                await this.DiscardAsync(default);
            }
        }

        protected abstract bool IsDirty();
    }
}
