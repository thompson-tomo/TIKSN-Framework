﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using TIKSN.Data;
using TIKSN.Finance.ForeignExchange.Data;
using TIKSN.Globalization;
using TIKSN.Localization;

namespace TIKSN.Finance.ForeignExchange
{
    public abstract class ExchangeRateServiceBase : IExchangeRateService
    {
        private static int nextID;
        private static readonly SemaphoreSlim nextIdLocker = new(1, 1);
        protected readonly ICurrencyFactory _currencyFactory;
        private readonly IExchangeRateRepository _exchangeRateRepository;
        private readonly IForeignExchangeRepository _foreignExchangeRepository;
        protected readonly ILogger<ExchangeRateServiceBase> _logger;

        private readonly Dictionary<int, (IExchangeRatesProvider BatchProvider, IExchangeRateProvider IndividualProvider
            , int LongNameKey, int ShortNameKey, RegionInfo Country, TimeSpan InvalidationInterval)> _providers;

        private readonly Random _random;
        protected readonly IRegionFactory _regionFactory;
        protected readonly IStringLocalizer _stringLocalizer;
        private readonly IUnitOfWorkFactory _unitOfWorkFactory;

        protected ExchangeRateServiceBase(
            ILogger<ExchangeRateServiceBase> logger,
            IStringLocalizer stringLocalizer,
            ICurrencyFactory currencyFactory,
            IRegionFactory regionFactory,
            IExchangeRateRepository exchangeRateRepository,
            IForeignExchangeRepository foreignExchangeRepository,
            IUnitOfWorkFactory unitOfWorkFactory,
            Random random)
        {
            this._logger = logger;
            this._stringLocalizer = stringLocalizer;
            this._exchangeRateRepository = exchangeRateRepository;
            this._foreignExchangeRepository = foreignExchangeRepository;

            this._providers =
                new Dictionary<int, (IExchangeRatesProvider BatchProvider, IExchangeRateProvider IndividualProvider, int
                    LongNameKey, int ShortNameKey, RegionInfo Country, TimeSpan InvalidationInterval)>();

            this._currencyFactory = currencyFactory;
            this._regionFactory = regionFactory;
            this._random = random;
            this._unitOfWorkFactory = unitOfWorkFactory ?? throw new ArgumentNullException(nameof(unitOfWorkFactory));
        }

        public async Task<Money> ConvertCurrencyAsync(Money baseMoney, CurrencyInfo counterCurrency,
            DateTimeOffset asOn, CancellationToken cancellationToken)
        {
            var pair = new CurrencyPair(baseMoney.Currency, counterCurrency);

            var rate = await this.GetExchangeRateAsync(pair, asOn, cancellationToken);

            return new Money(counterCurrency, baseMoney.Amount * rate);
        }

        public async Task<decimal> GetExchangeRateAsync(CurrencyPair pair, DateTimeOffset asOn,
            CancellationToken cancellationToken)
        {
            var combinedRates = new List<ExchangeRateEntity>();

            using (var uow = this._unitOfWorkFactory.Create())
            {
                foreach (var provider in this._providers)
                {
                    var ticksToIntervalRatio = asOn.Ticks / provider.Value.InvalidationInterval.Ticks;
                    var dateFrom = new DateTimeOffset(ticksToIntervalRatio * provider.Value.InvalidationInterval.Ticks,
                        asOn.Offset);
                    var dateTo =
                        new DateTimeOffset((ticksToIntervalRatio + 1) * provider.Value.InvalidationInterval.Ticks,
                            asOn.Offset);

                    var rates = await this._exchangeRateRepository.SearchAsync(provider.Key,
                        pair.BaseCurrency.ISOCurrencySymbol, pair.CounterCurrency.ISOCurrencySymbol, dateFrom, dateTo,
                        cancellationToken);

                    if (rates.Count == 0)
                    {
                        if (provider.Value.BatchProvider != null)
                        {
                            await this.FetchExchangeRatesAsync(provider.Key, provider.Value.BatchProvider, asOn,
                                cancellationToken);
                        }
                        else if (provider.Value.IndividualProvider != null)
                        {
                            await this.FetchExchangeRatesAsync(provider.Key, provider.Value.IndividualProvider, pair,
                                asOn, cancellationToken);
                        }
                        else
                        {
                            throw new Exception(
                                $"{nameof(provider.Value.BatchProvider)} and {nameof(provider.Value.IndividualProvider)} are both null, one of them should be null and other should not.");
                        }

                        var rate = await this._exchangeRateRepository.GetOrDefaultAsync(provider.Key,
                            pair.BaseCurrency.ISOCurrencySymbol, pair.CounterCurrency.ISOCurrencySymbol, asOn,
                            cancellationToken);

                        if (rate != null)
                        {
                            combinedRates.Add(rate);
                        }
                    }
                    else
                    {
                        combinedRates.AddRange(rates);
                    }
                }

                await uow.CompleteAsync(cancellationToken);
            }

            var exchangeRateEntity = combinedRates
                .MinBy(item => Math.Abs((item.AsOn - asOn).Ticks))
                .First();

            this._logger.LogInformation("Exchange rate provided by {0}",
                this._stringLocalizer.GetRequiredString(exchangeRateEntity.ForeignExchange.ShortNameKey));

            return exchangeRateEntity.Rate;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            using (var uow = this._unitOfWorkFactory.Create())
            {
                foreach (var provider in this._providers)
                {
                    var forex = await this._foreignExchangeRepository.GetOrDefaultAsync(provider.Key,
                        cancellationToken);

                    if (forex == null)
                    {
                        forex = new ForeignExchangeEntity
                        {
                            ID = provider.Key,
                            LongNameKey = provider.Value.LongNameKey,
                            ShortNameKey = provider.Value.ShortNameKey,
                            CountryCode = provider.Value.Country.Name
                        };

                        await this._foreignExchangeRepository.AddAsync(forex, cancellationToken);
                    }
                }

                await uow.CompleteAsync(cancellationToken);
            }

            await nextIdLocker.WaitAsync();
            try
            {
                nextID = await this._exchangeRateRepository.GetMaximalIdAsync(cancellationToken);
            }
            finally
            {
                nextIdLocker.Release();
            }
        }

        protected void AddBatchProvider(int providerID, IExchangeRatesProvider provider, int longNameKey,
            int shortNameKey, string country, TimeSpan invalidationInterval) => this._providers.Add(providerID,
            (provider, null, longNameKey, shortNameKey, this._regionFactory.Create(country), invalidationInterval));

        protected void AddIndividualProvider(int providerID, IExchangeRateProvider provider, int longNameKey,
            int shortNameKey, string country, TimeSpan invalidationInterval) => this._providers.Add(providerID,
            (null, provider, longNameKey, shortNameKey, this._regionFactory.Create(country), invalidationInterval));

        private async Task FetchExchangeRatesAsync(int foreignExchangeID, IExchangeRatesProvider batchProvider,
            DateTimeOffset asOn, CancellationToken cancellationToken)
        {
            try
            {
                var exchangeRates = await batchProvider.GetExchangeRatesAsync(asOn, cancellationToken);

                await this.SaveExchangeRatesAsync(foreignExchangeID, exchangeRates, cancellationToken);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, ex.Message);
            }
        }

        private async Task FetchExchangeRatesAsync(int foreignExchangeID, IExchangeRateProvider individualProvider,
            CurrencyPair pair, DateTimeOffset asOn, CancellationToken cancellationToken)
        {
            try
            {
                var exchangeRate = await individualProvider.GetExchangeRateAsync(pair.BaseCurrency,
                    pair.CounterCurrency, asOn, cancellationToken);

                await this.SaveExchangeRatesAsync(foreignExchangeID, new[] {exchangeRate}, cancellationToken);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, ex.Message);
            }
        }

        private async Task SaveExchangeRatesAsync(int foreignExchangeID, IEnumerable<ExchangeRate> exchangeRates,
            CancellationToken cancellationToken)
        {
            var entities = new List<ExchangeRateEntity>();

            foreach (var exchangeRate in exchangeRates)
            {
                var id = Interlocked.Increment(ref nextID);

                entities.Add(new ExchangeRateEntity
                {
                    ID = id,
                    AsOn = exchangeRate.AsOn,
                    BaseCurrencyCode = exchangeRate.Pair.BaseCurrency.ISOCurrencySymbol,
                    CounterCurrencyCode = exchangeRate.Pair.CounterCurrency.ISOCurrencySymbol,
                    ForeignExchangeID = foreignExchangeID,
                    Rate = exchangeRate.Rate
                });
            }

            await this._exchangeRateRepository.AddRangeAsync(entities, cancellationToken);
        }
    }
}
