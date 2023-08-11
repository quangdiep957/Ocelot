﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Ocelot.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;

using Ocelot.Responses;

namespace Ocelot.Request.Mapper
{
    public class RequestMapper : IRequestMapper
    {
        private readonly string[] _unsupportedHeaders = { "host" };

        public async Task<Response<HttpRequestMessage>> Map(HttpRequest request, DownstreamRoute downstreamRoute)
        {
            try
            {
                var requestMessage = new HttpRequestMessage
                {
                    Content = await MapContent(request),
                    Method = MapMethod(request, downstreamRoute),
                    RequestUri = MapUri(request),
                    Version = downstreamRoute.DownstreamHttpVersion,
                };

                MapHeaders(request, requestMessage);

                return new OkResponse<HttpRequestMessage>(requestMessage);
            }
            catch (Exception ex)
            {
                return new ErrorResponse<HttpRequestMessage>(new UnmappableRequestError(ex));
            }
        }

        private static bool IsMultipartContentType(string contentType)
            => !string.IsNullOrEmpty(contentType)
                && contentType.IndexOf("multipart/form-data", StringComparison.OrdinalIgnoreCase) >= 0;

        private static async Task<HttpContent> MapContent(HttpRequest request)
        {
            if (request.Body == null || (request.Body.CanSeek && request.Body.Length <= 0))
            {
                return null;
            }

            // Never change this to StreamContent again, I forgot it doesnt work in #464.
            HttpContent content = null;

            if (this.IsMultipartContentType(request.ContentType))
            {
                content = new MultipartFormDataContent();
                if (request.Form != null && request.Form.Files != null)
                {
                    foreach (var f in request.Form.Files)
                    {
                        using (var memStream = new MemoryStream())
                        {
                            await f.CopyToAsync(memStream);
                            var fileContent = new ByteArrayContent(memStream.ToArray());
                            ((MultipartFormDataContent)content).Add(fileContent, f.Name, f.FileName);
                        }

                    }
                }
                if (request.Form != null)
                {
                    foreach (var key in request.Form.Keys)
                    {
                        var strContent = new StringContent(request.Form[key]);
                        ((MultipartFormDataContent)content).Add(strContent, key);
                    }
                }
            }
            else
            {
                content = new ByteArrayContent(await ToByteArray(request.Body));
                if (!string.IsNullOrEmpty(request.ContentType))
                {
                    content.Headers
                        .TryAddWithoutValidation("Content-Type", new[] { request.ContentType });
                }
            }

            AddHeaderIfExistsOnRequest("Content-Language", content, request);
            AddHeaderIfExistsOnRequest("Content-Location", content, request);
            AddHeaderIfExistsOnRequest("Content-Range", content, request);
            AddHeaderIfExistsOnRequest("Content-MD5", content, request);
            AddHeaderIfExistsOnRequest("Content-Disposition", content, request);
            AddHeaderIfExistsOnRequest("Content-Encoding", content, request);

            return content;
        }

        private static void AddHeaderIfExistsOnRequest(string key, HttpContent content, HttpRequest request)
        {
            if (request.Headers.ContainsKey(key))
            {
                content.Headers
                    .TryAddWithoutValidation(key, request.Headers[key].ToArray());
            }
        }

        private static HttpMethod MapMethod(HttpRequest request, DownstreamRoute downstreamRoute)
        {
            if (!string.IsNullOrEmpty(downstreamRoute?.DownstreamHttpMethod))
            {
                return new HttpMethod(downstreamRoute.DownstreamHttpMethod);
            }

            return new HttpMethod(request.Method);
        }

        private static Uri MapUri(HttpRequest request) => new(request.GetEncodedUrl());

        private void MapHeaders(HttpRequest request, HttpRequestMessage requestMessage)
        {
            foreach (var header in request.Headers)
            {
                if (IsSupportedHeader(header))
                {
                    requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        private bool IsSupportedHeader(KeyValuePair<string, StringValues> header)
        {
            return !_unsupportedHeaders.Contains(header.Key.ToLower());
        }

        private static async Task<byte[]> ToByteArray(Stream stream)
        {
            await using (stream)
            {
                using (var memStream = new MemoryStream())
                {
                    await stream.CopyToAsync(memStream);
                    return memStream.ToArray();
                }
            }
        }
    }
}
