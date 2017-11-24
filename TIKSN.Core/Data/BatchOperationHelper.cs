﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TIKSN.Data
{
    public static class BatchOperationHelper
    {
        public static Task BatchOperationAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken, Func<T, CancellationToken, Task> singleOperation)
        {
            if (entities == null)
                throw new ArgumentNullException(nameof(entities));

            if (singleOperation == null)
                throw new ArgumentNullException(nameof(singleOperation));

            var tasks = entities.Select(entity => singleOperation?.Invoke(entity, cancellationToken));

            return Task.WhenAll(tasks);
        }
    }
}