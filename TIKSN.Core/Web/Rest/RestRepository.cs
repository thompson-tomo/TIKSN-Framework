﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using TIKSN.Analytics.Telemetry;
using TIKSN.Data;
using TIKSN.Localization;

namespace TIKSN.Web.Rest
{
    public class RestRepository<TEntity, TIdentity> :
        IRestRepository<TEntity, TIdentity>, IRestBulkRepository<TEntity, TIdentity>,
        IRepository<TEntity>
        where TEntity : IEntity<TIdentity>
        where TIdentity : IEquatable<TIdentity>
    {
        private readonly IDeserializerRestFactory _deserializerRestFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<RestRepositoryOptions<TEntity>> _options;
        private readonly IRestAuthenticationTokenProvider _restAuthenticationTokenProvider;
        private readonly ISerializerRestFactory _serializerRestFactory;
        private readonly IStringLocalizer _stringLocalizer;
        private readonly ITraceTelemeter _traceTelemeter;

        public RestRepository(
            IHttpClientFactory httpClientFactory,
            ISerializerRestFactory serializerRestFactory,
            IDeserializerRestFactory deserializerRestFactory,
            IRestAuthenticationTokenProvider restAuthenticationTokenProvider,
            IOptions<RestRepositoryOptions<TEntity>> options,
            IStringLocalizer stringLocalizer,
            ITraceTelemeter traceTelemeter)
        {
            this._httpClientFactory = httpClientFactory;
            this._serializerRestFactory = serializerRestFactory;
            this._options = options;
            this._stringLocalizer = stringLocalizer;
            this._traceTelemeter = traceTelemeter;
            this._restAuthenticationTokenProvider = restAuthenticationTokenProvider;
            this._deserializerRestFactory = deserializerRestFactory;
        }

        public async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
        {
            await this._traceTelemeter.TrackTrace(
                this._stringLocalizer.GetRequiredString(LocalizationKeys.Key638306944));

            foreach (var entity in entities)
            {
                await this.RemoveAsync(entity, cancellationToken);
            }
        }

        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken) =>
            this.AddObjectAsync(entities, cancellationToken);

        public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken)
        {
            var httpClient = await this.GetHttpClientAsync();
            var uriTemplate = new UriTemplate(this._options.Value.ResourceTemplate);

            uriTemplate.Fill("ID", string.Empty);

            var requestUrl = uriTemplate.Compose();

            var response = await httpClient.PutAsync(requestUrl, this.GetContent(entities), cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken) =>
            this.AddObjectAsync(entity, cancellationToken);

        public async Task<TEntity> GetAsync(TIdentity id, CancellationToken cancellationToken)
        {
            var httpClient = await this.GetHttpClientAsync();
            var uriTemplate = new UriTemplate(this._options.Value.ResourceTemplate);

            uriTemplate.Fill("ID", id.ToString());

            var requestUrl = uriTemplate.Compose();

            var response = await httpClient.GetAsync(requestUrl, cancellationToken);

            return await this.ObjectifyResponse<TEntity>(response, true);
        }

        public async Task RemoveAsync(TEntity entity, CancellationToken cancellationToken)
        {
            var httpClient = await this.GetHttpClientAsync();
            var uriTemplate = new UriTemplate(this._options.Value.ResourceTemplate);

            uriTemplate.Fill("ID", entity.ID.ToString());

            var requestUrl = uriTemplate.Compose();

            var response = await httpClient.DeleteAsync(requestUrl, cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken)
        {
            var httpClient = await this.GetHttpClientAsync();
            var uriTemplate = new UriTemplate(this._options.Value.ResourceTemplate);

            uriTemplate.Fill("ID", entity.ID.ToString());

            var requestUrl = uriTemplate.Compose();

            var response = await httpClient.PutAsync(requestUrl, this.GetContent(entity), cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        protected async Task<IEnumerable<TEntity>> SearchAsync(IEnumerable<KeyValuePair<string, string>> parameters,
            CancellationToken cancellationToken)
        {
            var httpClient = await this.GetHttpClientAsync();
            var uriTemplate = new UriTemplate(this._options.Value.ResourceTemplate);

            foreach (var parameter in parameters)
            {
                uriTemplate.Fill(parameter.Key, parameter.Value);
            }

            var requestUrl = uriTemplate.Compose();

            var response = await httpClient.GetAsync(requestUrl, cancellationToken);

            return await this.ObjectifyResponse<IEnumerable<TEntity>>(response, false);
        }

        protected async Task<TEntity> SingleOrDefaultAsync(IEnumerable<KeyValuePair<string, string>> parameters,
            CancellationToken cancellationToken)
        {
            var httpClient = await this.GetHttpClientAsync();
            var uriTemplate = new UriTemplate(this._options.Value.ResourceTemplate);

            foreach (var parameter in parameters)
            {
                uriTemplate.Fill(parameter.Key, parameter.Value);
            }

            var requestUrl = uriTemplate.Compose();

            var response = await httpClient.GetAsync(requestUrl, cancellationToken);

            return await this.ObjectifyResponse<TEntity>(response, true);
        }

        private async Task AddObjectAsync(object requestContent, CancellationToken cancellationToken)
        {
            var httpClient = await this.GetHttpClientAsync();
            var uriTemplate = new UriTemplate(this._options.Value.ResourceTemplate);

            uriTemplate.Fill("ID", string.Empty);

            var requestUrl = uriTemplate.Compose();

            var response = await httpClient.PostAsync(requestUrl, this.GetContent(requestContent), cancellationToken);

            response.EnsureSuccessStatusCode();
        }

        private HttpContent GetContent(object requestContent) => new StringContent(
            this._serializerRestFactory.Create(this._options.Value.MediaType).Serialize(requestContent),
            this._options.Value.Encoding, this._options.Value.MediaType);

        private async Task<HttpClient> GetHttpClientAsync()
        {
            var httpClient = this._httpClientFactory.CreateClient(this._options.Value.ApiKey);

            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(this._options.Value.MediaType));

            if (this._options.Value.AcceptLanguages != null)
            {
                foreach (var acceptLanguage in this._options.Value.AcceptLanguages)
                {
                    httpClient.DefaultRequestHeaders.AcceptLanguage.Add(
                        new StringWithQualityHeaderValue(acceptLanguage.Value, acceptLanguage.Key));
                }
            }

            await this.SetAuthenticationHeader(httpClient);

            return httpClient;
        }

        private async Task<TResult> ObjectifyResponse<TResult>(HttpResponseMessage response, bool defaultIfNotFound)
        {
            if (defaultIfNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            return this._deserializerRestFactory.Create(this._options.Value.MediaType).Deserialize<TResult>(content);
        }

        private async Task SetAuthenticationHeader(HttpClient httpClient)
        {
            var authenticationSchema = string.Empty;

            switch (this._options.Value.Authentication)
            {
                case RestAuthenticationType.None:
                    return;

                case RestAuthenticationType.Basic:
                    authenticationSchema = "Basic";
                    break;

                case RestAuthenticationType.Bearer:
                    authenticationSchema = "Bearer";
                    break;

                default:
                    throw new NotSupportedException(
                        $"Authentication type '{this._options.Value.Authentication}' is not supported.");
            }

            var authenticationToken =
                await this._restAuthenticationTokenProvider.GetAuthenticationToken(this._options.Value.ApiKey);

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(authenticationSchema, authenticationToken);
        }
    }
}
