﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace HeadhunterClientAPI
{
    public class HeadhunterClient
    {
        public partial class ResponseEmployers
        {
            [JsonPropertyName("items")]
            public Item[] Items { get; set; }

            [JsonPropertyName("found")]
            public long Found { get; set; }

            [JsonPropertyName("pages")]
            public long Pages { get; set; }

            [JsonPropertyName("per_page")]
            public long PerPage { get; set; }

            [JsonPropertyName("page")]
            public long Page { get; set; }

            public override string ToString()
            {
                return string.Join("\n\n", Items.Select(x => x.ToString()));
            }
        }

        public partial class Item
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("url")]
            public Uri Url { get; set; }

            [JsonPropertyName("alternate_url")]
            public Uri AlternateUrl { get; set; }

            [JsonPropertyName("logo_urls")]
            public LogoUrls LogoUrls { get; set; }

            [JsonPropertyName("vacancies_url")]
            public Uri VacanciesUrl { get; set; }

            [JsonPropertyName("open_vacancies")]
            public long OpenVacancies { get; set; }

            public override string ToString()
            {
                if (LogoUrls == null)
                {
                    return $"Id: {Id}\nName: {Name}\nVacancies: {OpenVacancies}\nURL: {AlternateUrl}\nJson URL: {Url.ToString()}\nVacancies URL: {VacanciesUrl}";
                }
                else
                {
                    return $"Id: {Id}\nName: {Name}\nVacancies: {OpenVacancies}\nURL: {AlternateUrl}\nJson URL: {Url.ToString()}\nVacancies URL: {VacanciesUrl}\nLogo URLS:[\n{LogoUrls.ToString()}\n]";
                }


            }
        }

        public partial class LogoUrls
        {
            [JsonPropertyName("90")]
            public Uri The90 { get; set; }
            [JsonPropertyName("240")]
            public Uri The240 { get; set; }
            [JsonPropertyName("original")]
            public Uri Original { get; set; }

            public override string ToString()
            {
                return ("90: " + The90.ToString() + "\n240: " + The240.ToString() + "\nOriginal: " + Original.ToString());
            }
        }

        public partial class BadRequest
        {
            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("errors")]
            public Error[] Errors { get; set; }

            [JsonPropertyName("request_id")]
            public string RequestId { get; set; }

            public override string ToString()
            {
                return string.Join(", ", Errors.Select(error => error.ToString()));
            }
        }

        public partial class Error
        {
            [JsonPropertyName("value")]
            public string Value { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }

            public override string ToString()
            {
                return $"Error {Value} Type: {Type}";
            }
        }


        public async static Task<ResponseEmployers> SearchEmployers(string str)
        {
            if (str == null) throw new ArgumentNullException("str is null!");
            if (str.Length == 0) throw new ArgumentException("Empty Argument!");

            ResponseEmployers re = new ResponseEmployers();

            var url = "https://api.hh.ru/employers?text=" + str;
            using var httpsClient = new HttpClient()
            {
                DefaultRequestHeaders = {
                    {
                        "User-Agent", "HeadhunterClientAPI"
                    }
                }
            };
            using var response = await httpsClient.GetAsync(url).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                ResponseEmployers employeeResponse = await response.Content.ReadFromJsonAsync<ResponseEmployers>();
                re.Items = employeeResponse.Items;

            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                BadRequest BadRequest = await response.Content.ReadFromJsonAsync<BadRequest>();
                throw new WebException(BadRequest.ToString());
            }
            else
            {
                response.EnsureSuccessStatusCode();
            }

            return re;


        }


    }
}
