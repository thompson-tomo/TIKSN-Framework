﻿using System.Collections.Generic;
using TIKSN.Data;

namespace TIKSN.Finance.ForeignExchange.Data
{
    public class ForeignExchangeEntity : IEntity<int>
    {
        public ForeignExchangeEntity()
        {
            ExchangeRates = new HashSet<ExchangeRateEntity>();
        }

        public int ID { get; set; }
        public int LongNameKey { get; set; }
        public int ShortNameKey { get; set; }
        public string CountryCode { get; set; }

        public virtual ICollection<ExchangeRateEntity> ExchangeRates { get; set; }
    }
}
