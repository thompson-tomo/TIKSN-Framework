﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using TIKSN.Serialization;

namespace TIKSN.Data.Cache.Distributed
{
    public class QueryRepositoryDistributedCacheDecorator<TEntity, TIdentity>
        : RepositoryDistributedCacheDecorator<TEntity, TIdentity>, IQueryRepository<TEntity, TIdentity>
        where TEntity : IEntity<TIdentity>
        where TIdentity : IEquatable<TIdentity>
    {
        protected readonly IQueryRepository<TEntity, TIdentity> _queryRepository;

        public QueryRepositoryDistributedCacheDecorator(IQueryRepository<TEntity, TIdentity> queryRepository,
            IRepository<TEntity> repository,
            IDistributedCache distributedCache,
            ISerializer<byte[]> serializer,
            IDeserializer<byte[]> deserializer,
            IOptions<DistributedCacheDecoratorOptions> genericOptions,
            IOptions<DistributedCacheDecoratorOptions<TEntity>> specificOptions)
            : base(repository, distributedCache, serializer, deserializer, genericOptions, specificOptions) =>
            this._queryRepository = queryRepository;

        public async Task<bool> ExistsAsync(TIdentity id, CancellationToken cancellationToken)
        {
            var entity = await this.GetOrDefaultAsync(id, cancellationToken);
            return entity != null;
        }

        public Task<TEntity> GetAsync(TIdentity id, CancellationToken cancellationToken)
        {
            var cacheKey = Tuple.Create(entityType, CacheKeyKind.Entity, id).ToString();

            var result = this.GetFromDistributedCacheAsync(cacheKey, cancellationToken,
                () => this._queryRepository.GetAsync(id, cancellationToken));

            if (result == null)
            {
                throw new NullReferenceException("Result retrieved from cache or from original source is null.");
            }

            return result;
        }

        public Task<TEntity> GetOrDefaultAsync(TIdentity id, CancellationToken cancellationToken)
        {
            var cacheKey = Tuple.Create(entityType, CacheKeyKind.Entity, id).ToString();

            return this.GetFromDistributedCacheAsync(cacheKey, cancellationToken,
                () => this._queryRepository.GetAsync(id, cancellationToken));
        }

        public async Task<IEnumerable<TEntity>>
            ListAsync(IEnumerable<TIdentity> ids, CancellationToken cancellationToken) =>
            await BatchOperationHelper.BatchOperationAsync(ids, cancellationToken, (id, ct) => this.GetAsync(id, ct));

        protected Task<IEnumerable<TEntity>> QueryFromDistributedCacheAsync(
            Func<Task<IEnumerable<TEntity>>> queryFromSource, CancellationToken cancellationToken)
        {
            var cacheKey = Tuple.Create(entityType, CacheKeyKind.Query).ToString();

            return this.GetFromDistributedCacheAsync(cacheKey, cancellationToken, queryFromSource);
        }
    }
}
