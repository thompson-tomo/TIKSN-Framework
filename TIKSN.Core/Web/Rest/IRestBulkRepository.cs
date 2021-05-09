﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TIKSN.Data;

namespace TIKSN.Web.Rest
{
    public interface IRestBulkRepository<TEntity, TIdentity> where TEntity : IEntity<TIdentity>
        where TIdentity : IEquatable<TIdentity>
    {
        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken);

        Task<TEntity> GetAsync(TIdentity id, CancellationToken cancellationToken);

        Task RemoveAsync(TEntity entity, CancellationToken cancellationToken);

        Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken);
    }
}
