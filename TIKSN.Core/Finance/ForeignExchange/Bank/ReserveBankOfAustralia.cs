﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TIKSN.Globalization;
using TIKSN.Time;

namespace TIKSN.Finance.ForeignExchange.Bank
{
    public class ReserveBankOfAustralia : ICurrencyConverter, IExchangeRatesProvider
    {
        private const string RSS = "http://www.rba.gov.au/rss/rss-cb-exchange-rates.xml";

        private static CurrencyInfo AustralianDollar;

        private readonly ICurrencyFactory _currencyFactory;
        private DateTimeOffset lastFetchDate;
        private DateTimeOffset publishedDate;
        private Dictionary<CurrencyInfo, decimal> rates;
        private readonly ITimeProvider _timeProvider;

        static ReserveBankOfAustralia()
        {
            var Australia = new System.Globalization.RegionInfo("en-AU");
            AustralianDollar = new CurrencyInfo(Australia);
        }

        public ReserveBankOfAustralia(ICurrencyFactory currencyFactory, ITimeProvider timeProvider)
        {
            this.publishedDate = DateTimeOffset.MinValue;

            this.rates = new Dictionary<CurrencyInfo, decimal>();

            this.lastFetchDate = DateTimeOffset.MinValue;

            _currencyFactory = currencyFactory;
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<Money> ConvertCurrencyAsync(Money baseMoney, CurrencyInfo counterCurrency, DateTimeOffset asOn, CancellationToken cancellationToken)
        {
            var pair = new CurrencyPair(baseMoney.Currency, counterCurrency);

            decimal rate = await this.GetExchangeRateAsync(pair, asOn, cancellationToken);

            return new Money(counterCurrency, rate * baseMoney.Amount);
        }

        public async Task<IEnumerable<CurrencyPair>> GetCurrencyPairsAsync(DateTimeOffset asOn, CancellationToken cancellationToken)
        {
            await this.FetchOnDemandAsync(cancellationToken);

            this.VerifyDate(asOn);

            var pairs = new List<CurrencyPair>();

            pairs.AddRange(this.rates.Keys.Select(R => new CurrencyPair(AustralianDollar, R)));
            pairs.AddRange(this.rates.Keys.Select(R => new CurrencyPair(R, AustralianDollar)));

            return pairs;
        }

        public async Task<decimal> GetExchangeRateAsync(CurrencyPair pair, DateTimeOffset asOn, CancellationToken cancellationToken)
        {
            await this.FetchOnDemandAsync(cancellationToken);

            this.VerifyDate(asOn);

            if (pair.BaseCurrency == AustralianDollar)
            {
                if (this.rates.ContainsKey(pair.CounterCurrency))
                    return this.rates[pair.CounterCurrency];
            }
            else if (pair.CounterCurrency == AustralianDollar)
            {
                if (this.rates.ContainsKey(pair.BaseCurrency))
                    return decimal.One / this.rates[pair.BaseCurrency];
            }

            throw new ArgumentException("Currency pair not supported.");
        }

        public async Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync(DateTimeOffset asOn, CancellationToken cancellationToken)
        {
            var result = new List<ExchangeRate>();

            using (var httpClient = new HttpClient())
            {
                var responseStream = await httpClient.GetStreamAsync(RSS);

                var xdoc = XDocument.Load(responseStream);

                lock (this.rates)
                {
                    foreach (var item in xdoc.Element("{http://www.w3.org/1999/02/22-rdf-syntax-ns#}RDF").Elements("{http://purl.org/rss/1.0/}item"))
                    {
                        var exchangeRateElement = item.Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}statistics").Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}exchangeRate");
                        var baseCurrencyElement = exchangeRateElement.Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}baseCurrency");
                        var targetCurrencyElement = exchangeRateElement.Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}targetCurrency");
                        var observationValueElement = exchangeRateElement.Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}observation").Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}value");
                        var periodElement = exchangeRateElement.Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}observationPeriod").Element("{http://www.cbwiki.net/wiki/index.php/Specification_1.2/}period");

                        Debug.Assert(baseCurrencyElement.Value == "AUD");

                        string counterCurrencyCode = targetCurrencyElement.Value;

                        decimal exchangeRate = decimal.Parse(observationValueElement.Value);
                        var period = DateTimeOffset.Parse(periodElement.Value);

                        CurrencyInfo foreignCurrency = _currencyFactory.Create(counterCurrencyCode);

                        this.rates[foreignCurrency] = exchangeRate;

                        this.publishedDate = period;

                        result.Add(new ExchangeRate(new CurrencyPair(AustralianDollar, foreignCurrency), period, exchangeRate));
                    }

                    this.lastFetchDate = _timeProvider.GetCurrentTime();
                }
            }

            return result;
        }

        private async Task FetchOnDemandAsync(CancellationToken cancellationToken)
        {
            if (_timeProvider.GetCurrentTime() - lastFetchDate > TimeSpan.FromDays(1d))
            {
                await this.GetExchangeRatesAsync(_timeProvider.GetCurrentTime(), cancellationToken);
            }
        }

        private void VerifyDate(DateTimeOffset asOn)
        {
            if (asOn > _timeProvider.GetCurrentTime())
                throw new ArgumentException("Exchange rate forecasting are not supported.");

            if (asOn < this.publishedDate)
                throw new ArgumentException("Exchange rate history not supported.");
        }
    }
}