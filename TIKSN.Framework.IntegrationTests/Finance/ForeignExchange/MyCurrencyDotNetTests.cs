using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TIKSN.Finance.ForeignExchange.Cumulative;
using TIKSN.Framework.IntegrationTests;
using TIKSN.Globalization;
using TIKSN.Time;
using Xunit;

namespace TIKSN.Finance.ForeignExchange.Tests
{
    [Collection("ServiceProviderCollection")]
    public class MyCurrencyDotNetTests
    {
        private readonly ITimeProvider timeProvider;
        private readonly ServiceProviderFixture serviceProviderFixture;

        public MyCurrencyDotNetTests(ServiceProviderFixture serviceProviderFixture)
        {
            this.timeProvider = serviceProviderFixture.Services.GetRequiredService<ITimeProvider>();
            this.serviceProviderFixture = serviceProviderFixture ?? throw new ArgumentNullException(nameof(serviceProviderFixture));
        }

        [Fact]
        public async Task GetCurrencyPairsAsync()
        {
            var currencyFactory = this.serviceProviderFixture.Services.GetRequiredService<ICurrencyFactory>();

            var myCurrencyDotNet = new MyCurrencyDotNet(currencyFactory, this.timeProvider);

            var pairs = await myCurrencyDotNet.GetCurrencyPairsAsync(DateTimeOffset.Now, default);

            pairs.Count().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetExchangeRateAsync001()
        {
            var currencyFactory = this.serviceProviderFixture.Services.GetRequiredService<ICurrencyFactory>();

            var myCurrencyDotNet = new MyCurrencyDotNet(currencyFactory, this.timeProvider);

            var amd = currencyFactory.Create("AMD");
            var usd = currencyFactory.Create("USD");
            var pair = new CurrencyPair(usd, amd);

            var rate = await myCurrencyDotNet.GetExchangeRateAsync(pair, DateTimeOffset.Now, default);

            rate.Should().BeGreaterThan(decimal.One);
        }

        [Fact]
        public async Task GetExchangeRateAsync002()
        {
            var currencyFactory = this.serviceProviderFixture.Services.GetRequiredService<ICurrencyFactory>();

            var myCurrencyDotNet = new MyCurrencyDotNet(currencyFactory, this.timeProvider);

            var amd = currencyFactory.Create("AMD");
            var usd = currencyFactory.Create("USD");
            var pair = new CurrencyPair(amd, usd);

            var rate = await myCurrencyDotNet.GetExchangeRateAsync(pair, DateTimeOffset.Now, default);

            rate.Should().BeLessThan(decimal.One);
        }
    }
}
