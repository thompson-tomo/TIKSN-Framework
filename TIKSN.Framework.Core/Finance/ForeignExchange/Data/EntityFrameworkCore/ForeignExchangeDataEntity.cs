using TIKSN.Data;

namespace TIKSN.Finance.ForeignExchange.Data.EntityFrameworkCore;

public class ForeignExchangeDataEntity : IEntity<Guid>
{
    public string? CountryCode { get; set; }

    public virtual ICollection<ExchangeRateDataEntity> ExchangeRates { get; } = [];

    public Guid ID { get; set; }
}
