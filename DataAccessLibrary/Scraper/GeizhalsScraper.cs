﻿using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using DataAccessLibrary.Models;
using ManfredHorst;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace DataAccessLibrary.Scraper
{
    public class GeizhalsScraper : IGeizhalsScraper
    {
        private readonly ILogger<GeizhalsScraper> logger;

        public GeizhalsScraper(ILogger<GeizhalsScraper> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Boolean> ScrapeGeizhals(Alarm alarm)
        {
            if (alarm is null)
            {
                throw new ArgumentNullException(nameof(alarm));
            }

            if (!alarm.Url.Contains("&sort=p"))
            {
                alarm.Url += "&sort=p";
            }

            IHtmlDocument document = await GetHtmlDocument(alarm.Url);

            alarm = GetProduct(document, alarm);

            this.logger.LogInformation("Checking item: {Alias}\nAlarm   Price: {Price}€\nCurrent Price: {ProductPrice}€\n", alarm.Alias, alarm.Price, alarm.ProductPrice);
            if (alarm.ProductPrice > 0 && alarm.ProductPrice <= alarm.Price)
            {
                return true;
            }

            return false;
        }

        private static async Task<IHtmlDocument> GetHtmlDocument(String url)
        {
            CancellationTokenSource cancellationToken = new();
            HttpResponseMessage request = await new HttpClient().GetAsync(url);
            cancellationToken.Token.ThrowIfCancellationRequested();

            IHtmlDocument document = new HtmlParser().ParseDocument(await request.Content.ReadAsStreamAsync());
            cancellationToken.Token.ThrowIfCancellationRequested();

            return document;
        }

        private Alarm GetProduct(IHtmlDocument document, Alarm alarm)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (document.StatusCode == System.Net.HttpStatusCode.OK)
            {
                if (GetProductMethod(document).Equals("a"))
                {
                    alarm.ProductName = GetProductName(document, "h1.variant__header__headline");
                    alarm.ProductPrice = GetProductPrice(document, "#offer__price-0 .gh_price");
                }
                else if (GetProductMethod(document).Equals("cat"))
                {
                    alarm.ProductName = GetProductName(document, "#product0 div.productlist__item .notrans");
                    alarm.ProductPrice = GetProductPrice(document, "#product0 div.productlist__price .gh_price");
                }
            }
            else
            {
                this.logger.LogError("Error: {StatusCode}", document.StatusCode);
            }
            return alarm;
        }

        private static String GetProductMethod(IHtmlDocument document)
        {
            return document.QuerySelectorAll("body").Select(x => x.GetAttribute("data-what")).FirstOrDefault();
        }

        private static String GetProductName(IHtmlDocument document, String selector)
        {
            return document.QuerySelectorAll(selector)
                                .FirstOrDefault()
                                .TextContent
                                .Trim();
        }

        private static Double GetProductPrice(IHtmlDocument document, String selector)
        {
            Double.TryParse(document.QuerySelectorAll(selector)
                                    .FirstOrDefault().TextContent
                                    .Replace("ab ", String.Empty)
                                    .Replace("€ ", String.Empty)
                                    .Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, result: out Double value);

            return value;
        }

        private static async Task<String> GetProductUrl(IHtmlDocument document, String selector)
        {
            if (selector.Equals("#product0 div.productlist__bestpriceoffer a"))
            {
                document = await GetHtmlDocument($@"https://geizhals.de/{document.QuerySelectorAll(selector)
                                    .Select(x => x.GetAttribute("href"))
                                    .FirstOrDefault()}");

                selector = "#offer__price-0 div.offer__clickout a";
            }

            return $@"https://geizhals.de{document.QuerySelectorAll(selector)
                                    .Select(x => x.GetAttribute("href"))
                                    .FirstOrDefault()}";
        }
    }
}