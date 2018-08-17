using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Net.Http;
using Newtonsoft.Json;
using System;
using System.Net;

namespace ShopifyMonitor
{
    class StockX
    {
        public static async Task<StockXClass> ShopifyStockX(string titleOfRestock)
        {

            var httpClient = new HttpClient();
            try
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.186 Safari/537.36");
                var html = await httpClient.GetStringAsync("https://www.google.com/search?q=Supreme stockx " + titleOfRestock);
                var htmlDocument = new HtmlDocument();
                var getProductDoc = new HtmlDocument();
                htmlDocument.LoadHtml(html);


                var ListOfColorsHtml = htmlDocument.DocumentNode.Descendants("a")
                    .Where(node => node.GetAttributeValue("href", "")
                    .Contains("stockx.com/")).ToList();
                var ColorsSoldOut = ListOfColorsHtml[0].GetAttributeValue("href", "");
                string stockXUrl = ColorsSoldOut.Replace("/url?q=", string.Empty);
                stockXUrl = stockXUrl.Substring(0, stockXUrl.IndexOf("&") + 1);
                stockXUrl = stockXUrl.Replace("&", "");

                html = await httpClient.GetStringAsync(ColorsSoldOut);
                htmlDocument.LoadHtml(html);
                var json = htmlDocument.DocumentNode.Descendants("script")
                    .Where(node => node.GetAttributeValue("type", "")
                    .Contains("application/ld+json")).ToList();
                var jsonCleaned = json[4].InnerHtml;

                var rootObject = JsonConvert.DeserializeObject<StockXClass>(jsonCleaned);
                return rootObject;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }

    public class StockXClass
    {
        public string context { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public string image { get; set; }
        public string releaseDate { get; set; }
        public string brand { get; set; }
        public string model { get; set; }
        public string sku { get; set; }
        public string color { get; set; }
        public string itemCondition { get; set; }
        public string description { get; set; }
        public Offers offers { get; set; }
    }

    public class Offers
    {
        public string type { get; set; }
        public string lowPrice { get; set; }
        public string highPrice { get; set; }
        public string priceCurrency { get; set; }
        public string url { get; set; }
        public Offer[] offers { get; set; }
    }

    public class Offer
    {
        public string type { get; set; }
        public string availability { get; set; }
        public string sku { get; set; }
        public string description { get; set; }
        public string price { get; set; }
        public string priceCurrency { get; set; }
    }
}
