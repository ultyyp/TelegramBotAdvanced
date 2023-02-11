using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using static System.Net.WebRequestMethods;

namespace ProglibIO
{
    public class Vacancy
    {
        public string VacancyName { get; set; }
        public string VacancyURL { get; set; }
        public string PublishingDate { get; set; }

        public override string ToString()
        {
            PublishingDate = PublishingDate.Replace("\n", "");
            PublishingDate = PublishingDate.Replace("\t", "");
            PublishingDate = PublishingDate.Substring(16);
            return $"Vacancy Name: {VacancyName}\n" +
                $"Publishing Date: {PublishingDate}\n" +
                $"Link: {VacancyURL}";
        }

    }

    public class ProglibIOClient
    {
        public static Vacancy NewVacancy()
        {
            return new Vacancy();
        }

        public static async Task<List<string>> GetVacancyNamesAsync(string url)
        {
            List<string> finalResult = new List<string>();
            var task = Task.Run(async () => {

                IConfiguration config = Configuration.Default.WithDefaultLoader();
                IBrowsingContext context = BrowsingContext.New(config);
                AngleSharp.Dom.IDocument document = await context.OpenAsync(url);


                string selector = "article > div > div > a > div.flex.align-between > h2";

                AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> elements =
                document.QuerySelectorAll(selector);

                IEnumerable<string> results = elements.Select(it => it.TextContent);
                finalResult = results.ToList();

            });

            await task;
            return finalResult;
        }

        public static async Task<List<string>> GetVacancyURLSAsync(string url)
        {
            List<string> finalResult = new List<string>();
            var task = Task.Run(async () => {

                IConfiguration config = Configuration.Default.WithDefaultLoader();
                IBrowsingContext context = BrowsingContext.New(config);
                AngleSharp.Dom.IDocument document = await context.OpenAsync(url);


                string selector = "article > div > div > a";

                AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> elements =
                document.QuerySelectorAll(selector);

                IEnumerable<string> results = elements.Select(it => "https://proglib.io" + it.GetAttribute("href"));
                finalResult = results.ToList();

            });

            await task;
            return finalResult;
        }

        public static async Task<List<string>> GetPublishingDatesAsync(string url)
        {
            List<string> finalResult = new List<string>();
            var task = Task.Run(async () => {

                IConfiguration config = Configuration.Default.WithDefaultLoader();
                IBrowsingContext context = BrowsingContext.New(config);
                AngleSharp.Dom.IDocument document = await context.OpenAsync(url);


                string selector = "article > header > div > div.preview-card__publish > div.publish-info";

                AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> elements =
                document.QuerySelectorAll(selector);

                IEnumerable<string> results = elements.Select(it => it.TextContent);
                finalResult = results.ToList();

            });

            await task;
            return finalResult;
        }

        public async static Task<int> GetTotalPagesAsync()
        {
            int totalPages = 0;
            string url = "https://proglib.io/vacancies/all?workType=all&workPlace=all&experience=&salaryFrom=&page=1";

            IConfiguration config = Configuration.Default.WithDefaultLoader();
            IBrowsingContext context = BrowsingContext.New(config);
            AngleSharp.Dom.IDocument document = await context.OpenAsync(url);

            string selector = "body > div.basis.sheet > div.basis__h-wrapper.sheet__center > div.basis__h-content > div > div > main > div.feed-pagination.flex.align-center";
            var element = document.QuerySelector(selector);
            totalPages = int.Parse(element.GetAttribute("data-total"));

            return totalPages;

        }

        public static async Task<ConcurrentBag<Vacancy>> GetVacanciesByURLAsync(string url)
        {
            ConcurrentBag<Vacancy> vacancies = new ConcurrentBag<Vacancy>();

            var names = await ProglibIOClient.GetVacancyNamesAsync(url);
            var urls = await ProglibIOClient.GetVacancyURLSAsync(url);
            var dates = await ProglibIOClient.GetPublishingDatesAsync(url);

            var task = Task.Run(() => {
                for (int i = 0; i < names.Count; i++)
                {
                    var vacancy = ProglibIOClient.NewVacancy();
                    vacancy.VacancyName = names[i];
                    vacancy.VacancyURL = urls[i];
                    vacancy.PublishingDate = dates[i];

                    vacancies.Add(vacancy);
                };

            });

            await task;
            return vacancies;
        }

        public static async Task<ConcurrentBag<Vacancy>> GetVacanciesFromAllPagesAsync()
        {
            int totalPages = await ProglibIOClient.GetTotalPagesAsync();
            List<string> urls = new List<string>();
            ConcurrentBag<Vacancy> finalBag = new ConcurrentBag<Vacancy>();

            await Task.Run(() => {
                for (int i = 1; i <= totalPages; i++)
                {
                    urls.Add("https://proglib.io/vacancies/all?workType=all&workPlace=all&experience=&salaryFrom=&page=" + i.ToString());
                }
            });

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = 50;
            var token = options.CancellationToken;

            await Parallel.ForEachAsync(urls, options, async (currentUrl, token) =>
            {
                ConcurrentBag<Vacancy> vacancies = await ProglibIOClient.GetVacanciesByURLAsync(currentUrl);
                Parallel.ForEach(vacancies, vac => {
                    finalBag.Add(vac);
                });
            });

            return finalBag;
        }




    }
}
