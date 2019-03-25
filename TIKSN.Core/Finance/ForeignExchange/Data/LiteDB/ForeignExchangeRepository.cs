﻿using TIKSN.Data.LiteDB;

namespace TIKSN.Finance.ForeignExchange.Data.LiteDB
{
    public class ForeignExchangeRepository : LiteDbRepository<ForeignExchangeEntity, int>, IForeignExchangeRepository
    {
        public ForeignExchangeRepository(ILiteDbDatabaseProvider databaseProvider) : base(databaseProvider, "ForeignExchanges")
        {
        }
    }
}