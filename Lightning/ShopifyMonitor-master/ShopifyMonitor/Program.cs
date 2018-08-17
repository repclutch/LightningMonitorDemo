using HtmlAgilityPack;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord.Webhook;
using Discord;
using System.IO;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Xml.Linq;
using static Kith;
using static GenericShopifySites;

namespace ShopifyMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] keyWords = File.ReadAllLines(@"C:\Users\Rohan\Desktop\shopifyGenericSites.txt");

            foreach(string line in keyWords)
            {
                SiteMapMonitor(line);
                System.Threading.Thread.Sleep(50000);
            }

            Console.ReadKey();
        }

        private static async void ProductsJsonExists(string url)
        {
            Console.WriteLine(url + " is now being monitored");
            string[] usingProxies = File.ReadAllLines(@"C:\Users\Rohan\Desktop\proxies.txt");
            string[] keyWords = File.ReadAllLines(@"C:\Users\Rohan\Desktop\shopifyKeywords.txt");
            Dictionary<string, string> productsMatchKeywords = new Dictionary<string, string>();
            Dictionary<string, string> newProductsMatchKeywords = new Dictionary<string, string>();

            string line;
            var listOfProxies = new List<Proxy>();
            System.IO.StreamReader file = new System.IO.StreamReader(@"C:\Users\Rohan\Desktop\proxies.txt");
            while ((line = file.ReadLine()) != null)
            {
                int i = 0;
                string[] words = line.Split(':');
                var ip = words[0];
                var port = words[1];
                listOfProxies.Add(new Proxy(ref ip, ref port));
                if (listOfProxies[i].working == false)
                { listOfProxies.RemoveAt(i); }
                else
                    ++i;
            }
            file.Close();
            var stringOfSizes = ""; //String which gets udpated with stockX info
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Chrome/41.0.2227.0");
            var html = await httpClient.GetStringAsync(url + "/products.json");
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            GlossaryOfProducts firstParse = new GlossaryOfProducts();
            Newtonsoft.Json.JsonConvert.PopulateObject(html, firstParse);

            for (int i = 0; i < firstParse.products.Count(); ++i)
            {
                foreach (string lineK in keyWords)
                {
                    string[] wordsSplit = lineK.Split(' ');
                    if ((firstParse.products[i].handle.Contains(wordsSplit[0])) && (firstParse.products[i].handle.Contains(wordsSplit[1])))
                    {
                        productsMatchKeywords.Add(firstParse.products[i].handle, firstParse.products[i].updated_at.ToString());
                        break;
                    }
                }
            }

            while (true)
            {
                string UpdatedOn;
                newProductsMatchKeywords.Clear();
                html = await httpClient.GetStringAsync(url + "/products.json");
                htmlDocument.LoadHtml(html);
                GlossaryOfProducts secondParse = new GlossaryOfProducts();
                Newtonsoft.Json.JsonConvert.PopulateObject(html, secondParse);

                for (int i = 0; i < secondParse.products.Count(); ++i)
                {
                    foreach (string lineK in keyWords)
                    {
                        string[] wordsSplit = lineK.Split(' ');

                        if ((secondParse.products[i].handle.Contains(wordsSplit[0])) && (secondParse.products[i].handle.Contains(wordsSplit[1])))
                        {
                            if (productsMatchKeywords.ContainsKey(secondParse.products[i].handle))
                            {
                                productsMatchKeywords.TryGetValue(secondParse.products[i].handle, out UpdatedOn);
                                if (secondParse.products[i].updated_at.ToString() != UpdatedOn)
                                {
                                    if (url.Contains("undefeated"))
                                    {
                                        bool pagePosted = false;
                                        html = await httpClient.GetStringAsync(url + "/products/" + secondParse.products[i].handle);
                                        htmlDocument.LoadHtml(html);

                                        var AllScriptOfInventory = htmlDocument.DocumentNode.Descendants("script")
                                                            .Where(node => node.GetAttributeValue("type", "")
                                                            .Contains("text/javascript")).ToList();
                                        var FinalScript = AllScriptOfInventory[0];

                                        var myString = FinalScript.InnerHtml.ToString();

                                        string result = myString;
                                        int indexOfFirstPhrase = myString.IndexOf("{ product: ");
                                        if (indexOfFirstPhrase >= 0)
                                        {
                                            indexOfFirstPhrase += "{ product: {\"id\"".Length;
                                            int indexOfSecondPhrase = myString.IndexOf(", onVariantSelected:", indexOfFirstPhrase);
                                            if (indexOfSecondPhrase >= 0)
                                                result = myString.Substring(indexOfFirstPhrase, indexOfSecondPhrase - indexOfFirstPhrase);
                                            else
                                                result = myString.Substring(indexOfFirstPhrase);
                                        }
                                        var finalJson = "{\"id\"" + result;

                                        if (!finalJson.Contains("jQuery"))
                                        {
                                            GlossaryOfProductPage productPage = new GlossaryOfProductPage();
                                            Newtonsoft.Json.JsonConvert.PopulateObject(finalJson, productPage);
                                            try
                                            {
                                                var stockXPrices = await StockX.ShopifyStockX(secondParse.products[i].title);
                                                for (int n = 0; n < productPage.variants.Count(); ++n)
                                                {
                                                    var profitForSize = "";

                                                    if (productPage.variants[n].available)
                                                    {
                                                        var priceOfSize = productPage.variants[n].price.ToString();
                                                        priceOfSize = priceOfSize.Remove(priceOfSize.Length - 2);

                                                        foreach (var offer in stockXPrices.offers.offers)
                                                        {
                                                            if (offer.description == productPage.variants[n].option2)
                                                            {
                                                                profitForSize = ("**$" + Math.Round(((Convert.ToDouble(offer.price) - Convert.ToDouble(priceOfSize)) - Convert.ToDouble(offer.price) * 0.12)) + "**");
                                                                break;
                                                            }
                                                            profitForSize = "N/A";
                                                        }
                                                        stringOfSizes += ("**" + productPage.variants[n].option2 + "** [" + productPage.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productPage.variants[n].id + ":1):: " + profitForSize + "\n");
                                                    }
                                                }
                                            }
                                            catch (NullReferenceException)
                                            {
                                                for (int n = 0; n < productPage.variants.Count(); ++n)
                                                {
                                                    if (productPage.variants[n].available)
                                                        stringOfSizes += ("**" + productPage.variants[n].option2 + "** [" + productPage.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productPage.variants[n].id + ":1):: N/A\n");
                                                }
                                            }
                                            catch (ArgumentOutOfRangeException)
                                            {
                                                for (int n = 0; n < productPage.variants.Count(); ++n)
                                                {
                                                    if (productPage.variants[n].available)
                                                        stringOfSizes += ("**" + productPage.variants[n].option2 + "** [" + productPage.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productPage.variants[n].id + ":1):: N/A\n");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            pagePosted = true;
                                        }

                                        if (pagePosted == true)
                                            await ifStockExists.StockExists(url, secondParse, i);
                                        else
                                        {
                                            await ifStockExists.StockNotExists(url, secondParse, i, stringOfSizes);
                                        }
                                        stringOfSizes = "";
                                    }
                                }
                            }
                            newProductsMatchKeywords.Add(secondParse.products[i].handle, secondParse.products[i].updated_at.ToString());
                            break;
                        }
                    }
                }
                productsMatchKeywords = newProductsMatchKeywords.ToDictionary(entry => entry.Key,
                                               entry => entry.Value);
                System.Threading.Thread.Sleep(18000);
            }
        }

        private static async void SiteMapMonitor(string url)
        {
            Console.WriteLine(url + " is now being monitored");

            string[] usingProxies = File.ReadAllLines(@"C:\Users\Rohan\Desktop\proxies.txt");
            string[] keyWords = File.ReadAllLines(@"C:\Users\Rohan\Desktop\shopifyKeywords.txt");
            Dictionary<string, KeyValuePair<string, string>> productsMatchKeywords = new Dictionary<string, KeyValuePair<string, string>>();
            Dictionary<string, KeyValuePair<string, string>> newProductsMatchKeywords = new Dictionary<string, KeyValuePair<string, string>>();

            string line;
            var listOfPersons = new List<Proxy>();
            System.IO.StreamReader file = new System.IO.StreamReader(@"C:\Users\Rohan\Desktop\proxies.txt");
            while ((line = file.ReadLine()) != null)
            {
                int i = 0;
                string[] words = line.Split(':');
                var ip = words[0];
                var port = words[1];
                listOfPersons.Add(new Proxy(ref ip, ref port));
                if (listOfPersons[i].working == false)
                { listOfPersons.RemoveAt(i); }
                else
                    ++i;
            }
            file.Close();

            int max = listOfPersons.Count;
            var httpClient = new HttpClient();
            var htmlDocument = new HtmlDocument();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Chrome/41.0.2227.0");
            string html;
            bool through = false;
            while(through == false)
            {
                try
                {
                    html = await httpClient.GetStringAsync(url + "sitemap_products_1.xml");
                    htmlDocument.LoadHtml(html);
                    through = true;
                }
                catch (HttpRequestException)
                {
                    System.Threading.Thread.Sleep(18000);
                    continue;
                }
            }
            HtmlNode.ElementsFlags["loc"] = HtmlElementFlag.Closed;
            


            //First parse through the XML to add desired items to dictionary
            HtmlNodeCollection nodeList = htmlDocument.DocumentNode.SelectNodes("//url");
            foreach (HtmlNode node in nodeList)
            {
                try
                {
                    string title = node.ChildNodes["image:image"].ChildNodes["image:title"].InnerText;
                    string imageUrl = node.ChildNodes["image:image"].ChildNodes["image:loc"].InnerText;
                    string lastMod = node.ChildNodes["lastmod"].InnerText;
                    string productUrl = node.ChildNodes["loc"].InnerText;

                    foreach (string lineK in keyWords)
                    {
                        string[] wordsSplit = lineK.Split(' ');

                        if (((title.ToLower().Contains(wordsSplit[0])) && (title.ToLower().Contains(wordsSplit[1])))
                            || ((productUrl.ToLower().Contains(wordsSplit[0])) && (productUrl.ToLower().Contains(wordsSplit[1])))
                            || ((imageUrl.ToLower().Contains(wordsSplit[0])) && (imageUrl.ToLower().Contains(wordsSplit[1]))))
                        {
                            productsMatchKeywords.Add(productUrl, new KeyValuePair<string, string>(lastMod, title));
                            break;
                        }
                    }
                }
                catch (NullReferenceException)
                {
                    continue;
                }
            }

            //Infinite loop to keep checking
            while (true)
            {
                newProductsMatchKeywords.Clear();
                through = false;
                while(through == false)
                try
                {
                    html = await httpClient.GetStringAsync(url + "sitemap_products_1.xml");
                    through = true;
                    htmlDocument.LoadHtml(html);
                    nodeList = htmlDocument.DocumentNode.SelectNodes("//url");
                }
                catch (HttpRequestException)
                {
                    System.Threading.Thread.Sleep(18000);
                        continue;
                }

                //Populates the second comparative dictionary
                foreach (HtmlNode curNode in nodeList)
                {
                    bool addedToDic = false;
                    try
                    {
                        string title = curNode.ChildNodes["image:image"].ChildNodes["image:title"].InnerText;
                        string imageUrl = curNode.ChildNodes["image:image"].ChildNodes["image:loc"].InnerText;
                        string lastMod = curNode.ChildNodes["lastmod"].InnerText;
                        string productUrl = curNode.ChildNodes["loc"].InnerText;
                        string stringOfSizes = "";

                        foreach (string lineK in keyWords)
                        {
                            string[] wordsSplit = lineK.Split(' ');

                            if (((title.ToLower().Contains(wordsSplit[0])) && (title.ToLower().Contains(wordsSplit[1])))
                                || ((productUrl.ToLower().Contains(wordsSplit[0])) && (productUrl.ToLower().Contains(wordsSplit[1])))
                                || ((imageUrl.ToLower().Contains(wordsSplit[0])) && (imageUrl.ToLower().Contains(wordsSplit[1]))))
                            {
                                newProductsMatchKeywords.Add(productUrl, new KeyValuePair<string, string>(lastMod, title));
                                addedToDic = true;
                                break;
                            }
                        }

                        //Checks to see if an item was added to new dictionary and if it's contained in the second dictionary
                        if ((addedToDic == true) && (productsMatchKeywords.ContainsKey(productUrl)))
                        {
                            productsMatchKeywords.TryGetValue(productUrl, out KeyValuePair<string, string> value);
                            //Last mod dates are different, check for restock with differences to other websites
                            if (value.Key != lastMod)
                            {
                                //Kith restock
                                if (url.Contains("kith"))
                                {
                                    html = await httpClient.GetStringAsync(productUrl);
                                    var productHtml = new HtmlDocument();
                                    productHtml.LoadHtml(html);

                                    var AllScriptOfInventory = productHtml.DocumentNode.Descendants("script")
                                                            .Where(node => node.GetAttributeValue("", "")
                                                            .Contains("")).ToList();
                                    string ScriptOfInventory = "";
                                    for (int i = 0; i < AllScriptOfInventory.Count(); ++i)
                                    {
                                        if (AllScriptOfInventory[i].InnerText.Contains("inventory_quantity"))
                                            ScriptOfInventory = AllScriptOfInventory[i].InnerText.ToString();
                                    }
                                    int indexOfFirstPhrase = ScriptOfInventory.IndexOf(" var product = ");
                                    string result = "";
                                    if (indexOfFirstPhrase >= 0)
                                    {
                                        indexOfFirstPhrase += "{ product: {\"id\"".Length;
                                        int indexOfSecondPhrase = ScriptOfInventory.IndexOf("$(document).on('ready',function(){", indexOfFirstPhrase);
                                        if (indexOfSecondPhrase >= 0)
                                            result = ScriptOfInventory.Substring(indexOfFirstPhrase, indexOfSecondPhrase - indexOfFirstPhrase);
                                        else
                                            result = ScriptOfInventory.Substring(indexOfFirstPhrase);
                                    }
                                    result = result.Replace(";", "");

                                    KithProductPage kithProduct = new KithProductPage();
                                    Newtonsoft.Json.JsonConvert.PopulateObject(result, kithProduct);

                                    if (kithProduct.available)
                                    {
                                        var shoePrice = kithProduct.variants[0].price.ToString();
                                        shoePrice = shoePrice.Remove(shoePrice.Length - 2);
                                        shoePrice = shoePrice.Replace(".", "");
                                        shoePrice = ("$" + shoePrice + ".00");
                                        try
                                        {
                                            var stockXPrices = await StockX.ShopifyStockX(title);
                                            for (int n = 0; n < kithProduct.variants.Count(); ++n)
                                            {
                                                var profitForSize = "";

                                                if (kithProduct.variants[n].available)
                                                {
                                                    var priceOfSize = kithProduct.variants[n].price.ToString();
                                                    priceOfSize = priceOfSize.Remove(priceOfSize.Length - 2);

                                                    foreach (var offer in stockXPrices.offers.offers)
                                                    {
                                                        if (offer.description == kithProduct.variants[n].option1)
                                                        {
                                                            profitForSize = ("**$" + Math.Round(((Convert.ToDouble(offer.price) - Convert.ToDouble(priceOfSize)) - Convert.ToDouble(offer.price) * 0.12)) + "**");
                                                            break;
                                                        }
                                                        profitForSize = "N/A";
                                                    }
                                                    stringOfSizes += ("**" + kithProduct.variants[n].option1 + "** [" + kithProduct.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + kithProduct.variants[n].id + ":1)" + " :: " + profitForSize + "\n");
                                                }
                                            }
                                        }
                                        catch (NullReferenceException)
                                        {
                                            for (int n = 0; n < kithProduct.variants.Count(); ++n)
                                            {
                                                if (kithProduct.variants[n].available)
                                                    stringOfSizes += ("**" + kithProduct.variants[n].option1 + "** [" + kithProduct.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + kithProduct.variants[n].id + ":1) :: N/A\n");
                                            }
                                        }
                                        catch (ArgumentOutOfRangeException)
                                        {
                                            for (int n = 0; n < kithProduct.variants.Count(); ++n)
                                            {
                                                if (kithProduct.variants[n].available)
                                                    stringOfSizes += ("**" + kithProduct.variants[n].option1 + "** [" + kithProduct.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + kithProduct.variants[n].id + ":1) :: N/A\n");
                                            }
                                        }

                                        await GenericMonitorPost.postToDiscord(productUrl, imageUrl, title, stringOfSizes, shoePrice);
                                    }
                                }

                                //Restock product.json includes inventory quantity
                                else
                                {
                                    var shoePrice = "";
                                    var stockExists = false;
                                    html = await httpClient.GetStringAsync(productUrl + ".json");
                                    var productHtml = new HtmlDocument();
                                    productHtml.LoadHtml(html);
                                    GenericProductJson productJson = new GenericProductJson();
                                    Newtonsoft.Json.JsonConvert.PopulateObject(html, productJson);

                                    for (int i = 0; i < productJson.product.variants.Count(); ++i)
                                    {
                                        if (productJson.product.variants[i].inventory_quantity > 0)
                                        {
                                            shoePrice = productJson.product.variants[i].price.ToString();
                                            shoePrice = shoePrice.Remove(shoePrice.Length - 2);
                                            shoePrice = shoePrice.Replace(".", "");
                                            shoePrice = ("$" + shoePrice + ".00");
                                            stockExists = true;
                                            break;
                                        }
                                    }

                                    if (stockExists == true)
                                    {
                                        var productSize = "";                                    

                                        try
                                        {
                                            var stockXPrices = await StockX.ShopifyStockX(title);
                                            for (int n = 0; n < productJson.product.variants.Count(); ++n)
                                            {
                                                if ((productJson.product.variants[n].title.Contains(productJson.product.variants[n].option1)))
                                                    productSize = (productJson.product.variants[n].option2);
                                                else
                                                    productSize = productJson.product.variants[n].option1;

                                                var profitForSize = "";

                                                if (productJson.product.variants[n].inventory_quantity > 0)
                                                {
                                                    var priceOfSize = productJson.product.variants[n].price.ToString();
                                                    priceOfSize = priceOfSize.Remove(priceOfSize.Length - 2);
                                                    priceOfSize = priceOfSize.Replace(".", "");

                                                    foreach (var offer in stockXPrices.offers.offers)
                                                    {
                                                        if (offer.description == productSize)
                                                        {
                                                            profitForSize = ("**$" + Math.Round(((Convert.ToDouble(offer.price) - Convert.ToDouble(priceOfSize)) - Convert.ToDouble(offer.price) * 0.12) - 20) + "**");
                                                            break;
                                                        }
                                                        profitForSize = "N/A";
                                                    }
                                                    stringOfSizes += ("**" + productSize + "** [" + productJson.product.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productJson.product.variants[n].id + ":1)" + " :: " + profitForSize + "\n");
                                                }
                                            }
                                        }
                                        catch (NullReferenceException)
                                        {
                                            for (int n = 0; n < productJson.product.variants.Count(); ++n)
                                            {
                                                if ((productJson.product.variants[n].title.Contains(productJson.product.variants[n].option1)))
                                                    productSize = (productJson.product.variants[n].option2);
                                                else
                                                    productSize = productJson.product.variants[n].option1;

                                                if (productJson.product.variants[n].inventory_quantity > 0)
                                                    stringOfSizes += ("**" + productSize + "** [" + productJson.product.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productJson.product.variants[n].id + ":1) :: N/A\n");
                                            }
                                        }
                                        catch (ArgumentOutOfRangeException)
                                        {
                                            for (int n = 0; n < productJson.product.variants.Count(); ++n)
                                            {
                                                if ((productJson.product.variants[n].title.Contains(productJson.product.variants[n].option1)))
                                                    productSize = (productJson.product.variants[n].option2);
                                                else
                                                    productSize = productJson.product.variants[n].option1;

                                                if (productJson.product.variants[n].inventory_quantity > 0)
                                                    stringOfSizes += ("**" + productSize + "** [" + productJson.product.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productJson.product.variants[n].id + ":1) :: N/A\n");
                                            }
                                        }
                                        await GenericMonitorPost.postToDiscord(productUrl, imageUrl, title, stringOfSizes, shoePrice);
                                    }
                                }
                            }
                        }
                        //New product added to website
                        else if (((addedToDic == true) && (!productsMatchKeywords.ContainsKey(productUrl))))
                        {
                            //Kith new product
                            if (url.Contains("kith"))
                            {
                                html = await httpClient.GetStringAsync(productUrl);
                                var productHtml = new HtmlDocument();
                                productHtml.LoadHtml(html);

                                var AllScriptOfInventory = productHtml.DocumentNode.Descendants("script")
                                                        .Where(node => node.GetAttributeValue("", "")
                                                        .Contains("")).ToList();
                                string ScriptOfInventory = "";
                                for (int i = 0; i < AllScriptOfInventory.Count(); ++i)
                                {
                                    if (AllScriptOfInventory[i].InnerText.Contains("inventory_quantity"))
                                        ScriptOfInventory = AllScriptOfInventory[i].InnerText.ToString();
                                }
                                int indexOfFirstPhrase = ScriptOfInventory.IndexOf(" var product = ");
                                string result = "";
                                if (indexOfFirstPhrase >= 0)
                                {
                                    indexOfFirstPhrase += "{ product: {\"id\"".Length;
                                    int indexOfSecondPhrase = ScriptOfInventory.IndexOf("$(document).on('ready',function(){", indexOfFirstPhrase);
                                    if (indexOfSecondPhrase >= 0)
                                        result = ScriptOfInventory.Substring(indexOfFirstPhrase, indexOfSecondPhrase - indexOfFirstPhrase);
                                    else
                                        result = ScriptOfInventory.Substring(indexOfFirstPhrase);
                                }
                                result = result.Replace(";", "");

                                KithProductPage kithProduct = new KithProductPage();
                                Newtonsoft.Json.JsonConvert.PopulateObject(result, kithProduct);
                                var shoePrice = kithProduct.variants[0].price.ToString();
                                shoePrice = shoePrice.Remove(shoePrice.Length - 2);
                                shoePrice = shoePrice.Replace(".", "");
                                shoePrice = ("$" + shoePrice + ".00");

                                if (kithProduct.available)
                                {
                                    try
                                    {
                                        var stockXPrices = await StockX.ShopifyStockX(title);
                                        for (int n = 0; n < kithProduct.variants.Count(); ++n)
                                        {
                                            var profitForSize = "";

                                            if (kithProduct.variants[n].available)
                                            {
                                                var priceOfSize = kithProduct.variants[n].price.ToString();
                                                priceOfSize = priceOfSize.Remove(priceOfSize.Length - 2);

                                                foreach (var offer in stockXPrices.offers.offers)
                                                {
                                                    if (offer.description == kithProduct.variants[n].option1)
                                                    {
                                                        profitForSize = ("**$" + Math.Round(((Convert.ToDouble(offer.price) - Convert.ToDouble(priceOfSize)) - Convert.ToDouble(offer.price) * 0.12)) + "**");
                                                        break;
                                                    }
                                                    profitForSize = "N/A";
                                                }
                                                stringOfSizes += ("**" + kithProduct.variants[n].option1 + "** [" + kithProduct.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + kithProduct.variants[n].id + ":1)" + " :: " + profitForSize + "\n");
                                            }
                                        }
                                    }
                                    catch (NullReferenceException)
                                    {
                                        for (int n = 0; n < kithProduct.variants.Count(); ++n)
                                        {
                                            if (kithProduct.variants[n].available)
                                                stringOfSizes += ("**" + kithProduct.variants[n].option1 + "** [" + kithProduct.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + kithProduct.variants[n].id + ":1) :: N/A\n");
                                        }
                                    }
                                    catch (ArgumentOutOfRangeException)
                                    {
                                        for (int n = 0; n < kithProduct.variants.Count(); ++n)
                                        {
                                            if (kithProduct.variants[n].available)
                                                stringOfSizes += ("**" + kithProduct.variants[n].option1 + "** [" + kithProduct.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + kithProduct.variants[n].id + ":1) :: N/A\n");
                                        }
                                    }

                                    await GenericMonitorPost.postToDiscord(productUrl, imageUrl, title, stringOfSizes, shoePrice);
                                }
                                else
                                {
                                    await GenericMonitorPost.noStockToDiscord(productUrl, imageUrl, title, shoePrice);
                                }
                            }

                            //New product and product.json includes inventory quantity
                            else
                            {
                                var shoePrice = "";
                                var stockExists = false;
                                html = await httpClient.GetStringAsync(productUrl + ".json");
                                var productHtml = new HtmlDocument();
                                productHtml.LoadHtml(html);
                                GenericProductJson productJson = new GenericProductJson();
                                Newtonsoft.Json.JsonConvert.PopulateObject(html, productJson);

                                shoePrice = productJson.product.variants[0].price.ToString();
                                shoePrice = shoePrice.Remove(shoePrice.Length - 2);
                                shoePrice = shoePrice.Replace(".", "");
                                shoePrice = ("$" + shoePrice + ".00");

                                for (int i = 0; i < productJson.product.variants.Count(); ++i)
                                {
                                    if (productJson.product.variants[i].inventory_quantity > 0)
                                    {
                                        stockExists = true;
                                        break;
                                    }
                                }

                                if (stockExists == true)
                                {
                                    var productSize = "";
                                    try
                                    {
                                        var stockXPrices = await StockX.ShopifyStockX(title);
                                        for (int n = 0; n < productJson.product.variants.Count(); ++n)
                                        {
                                            if ((productJson.product.variants[n].title.Contains(productJson.product.variants[n].option1)))
                                                productSize = (productJson.product.variants[n].option2);
                                            else
                                                productSize = productJson.product.variants[n].option1;
                                            var profitForSize = "";

                                            if (productJson.product.variants[n].inventory_quantity > 0)
                                            {
                                                var priceOfSize = productJson.product.variants[n].price.ToString();
                                                priceOfSize = priceOfSize.Remove(priceOfSize.Length - 2);
                                                priceOfSize = priceOfSize.Replace(".", "");
                                                foreach (var offer in stockXPrices.offers.offers)
                                                {
                                                    if (offer.description == productSize)
                                                    {
                                                        profitForSize = ("**$" + Math.Round(((Convert.ToDouble(offer.price) - Convert.ToDouble(priceOfSize)) - Convert.ToDouble(offer.price) * 0.12) - 20) + "**");
                                                        break;
                                                    }
                                                    profitForSize = "N/A";
                                                }
                                                stringOfSizes += ("**" + productSize + "** [" + productJson.product.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productJson.product.variants[n].id + ":1)" + " :: " + profitForSize + "\n");
                                            }
                                        }
                                    }
                                    catch (NullReferenceException)
                                    {
                                        for (int n = 0; n < productJson.product.variants.Count(); ++n)
                                        {
                                            if ((productJson.product.variants[n].title.Contains(productJson.product.variants[n].option1)))
                                                productSize = (productJson.product.variants[n].option2);
                                            else
                                                productSize = productJson.product.variants[n].option1;
                                            if (productJson.product.variants[n].inventory_quantity > 0)
                                                stringOfSizes += ("**" + productSize + "** [" + productJson.product.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productJson.product.variants[n].id + ":1):: N/A\n");
                                        }
                                    }
                                    catch (ArgumentOutOfRangeException)
                                    {
                                        for (int n = 0; n < productJson.product.variants.Count(); ++n)
                                        {
                                            if ((productJson.product.variants[n].title.Contains(productJson.product.variants[n].option1)))
                                                productSize = (productJson.product.variants[n].option2);
                                            else
                                                productSize = productJson.product.variants[n].option1;
                                            if (productJson.product.variants[n].inventory_quantity > 0)
                                                stringOfSizes += ("**" + productSize + "** [" + productJson.product.variants[n].inventory_quantity + "] :: [Checkout](" + url + "/cart/" + productJson.product.variants[n].id + ":1):: N/A\n");
                                        }
                                    }

                                    await GenericMonitorPost.postToDiscord(productUrl, imageUrl, title, stringOfSizes, shoePrice);
                                }

                                else
                                    await GenericMonitorPost.noStockToDiscord(productUrl, imageUrl, title, shoePrice);
                            }
                        }
                    }
                    catch (NullReferenceException)
                    {
                        continue;
                    }
                }

                //Deletes the old one, and the new one is the old one and restarted process
                productsMatchKeywords = newProductsMatchKeywords.ToDictionary(entry => entry.Key, entry => entry.Value);
                System.Threading.Thread.Sleep(50000);
            }
        }
    }

    public class GlossaryOfProducts
    {
        public Product[] products { get; set; }
    }

    public class Product
    {
        public long id { get; set; }
        public string title { get; set; }
        public string handle { get; set; }
        public string body_html { get; set; }
        public DateTime published_at { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string vendor { get; set; }
        public string product_type { get; set; }
        public string[] tags { get; set; }
        public Variant[] variants { get; set; }
        public Image[] images { get; set; }
        public Option[] options { get; set; }
    }

    public class Variant
    {
        public long id { get; set; }
        public string title { get; set; }
        public string option1 { get; set; }
        public string option2 { get; set; }
        public object option3 { get; set; }
        public string sku { get; set; }
        public bool requires_shipping { get; set; }
        public bool taxable { get; set; }
        public object featured_image { get; set; }
        public bool available { get; set; }
        public string price { get; set; }
        public int grams { get; set; }
        public string compare_at_price { get; set; }
        public int position { get; set; }
        public long product_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
    }

    public class Image
    {
        public long id { get; set; }
        public DateTime created_at { get; set; }
        public int position { get; set; }
        public DateTime updated_at { get; set; }
        public long product_id { get; set; }
        public object[] variant_ids { get; set; }
        public string src { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class Option
    {
        public string name { get; set; }
        public int position { get; set; }
        public string[] values { get; set; }
    }
}
internal class Proxy
{
    public string ip;
    public string port;
    public bool working = true;

    public Proxy(ref string ip, ref string port)
    {
        this.ip = ip;
        this.port = port;
    }
}





public class GlossaryOfProductPage
{
    public long id { get; set; }
    public string title { get; set; }
    public string handle { get; set; }
    public string description { get; set; }
    public DateTime published_at { get; set; }
    public DateTime created_at { get; set; }
    public string vendor { get; set; }
    public string type { get; set; }
    public string[] tags { get; set; }
    public int price { get; set; }
    public int price_min { get; set; }
    public int price_max { get; set; }
    public bool available { get; set; }
    public bool price_varies { get; set; }
    public object compare_at_price { get; set; }
    public int compare_at_price_min { get; set; }
    public int compare_at_price_max { get; set; }
    public bool compare_at_price_varies { get; set; }
    public Variant[] variants { get; set; }
    public string[] images { get; set; }
    public string featured_image { get; set; }
    public string[] options { get; set; }
    public string content { get; set; }
}

public class Variant
{
    public long id { get; set; }
    public string title { get; set; }
    public string option1 { get; set; }
    public string option2 { get; set; }
    public object option3 { get; set; }
    public string sku { get; set; }
    public bool requires_shipping { get; set; }
    public bool taxable { get; set; }
    public object featured_image { get; set; }
    public bool available { get; set; }
    public string name { get; set; }
    public string public_title { get; set; }
    public string[] options { get; set; }
    public int price { get; set; }
    public int weight { get; set; }
    public object compare_at_price { get; set; }
    public int inventory_quantity { get; set; }
    public string inventory_management { get; set; }
    public string inventory_policy { get; set; }
    public string barcode { get; set; }
}









class Kith
{
    public class KithProductPage
    {
        public long id { get; set; }
        public string title { get; set; }
        public string handle { get; set; }
        public string description { get; set; }
        public DateTime published_at { get; set; }
        public DateTime created_at { get; set; }
        public string vendor { get; set; }
        public string type { get; set; }
        public string[] tags { get; set; }
        public int price { get; set; }
        public int price_min { get; set; }
        public int price_max { get; set; }
        public bool available { get; set; }
        public bool price_varies { get; set; }
        public int compare_at_price { get; set; }
        public int compare_at_price_min { get; set; }
        public int compare_at_price_max { get; set; }
        public bool compare_at_price_varies { get; set; }
        public Variant[] variants { get; set; }
        public string[] images { get; set; }
        public string featured_image { get; set; }
        public string[] options { get; set; }
        public string content { get; set; }
        public Shop shop { get; set; }
    }

    public class Shop
    {
        public string meganav_image { get; set; }
        public string url { get; set; }
        public string product_title { get; set; }
        public string product_title_variant { get; set; }
    }

    public class Variant
    {
        public long id { get; set; }
        public string title { get; set; }
        public string option1 { get; set; }
        public object option2 { get; set; }
        public object option3 { get; set; }
        public string sku { get; set; }
        public bool requires_shipping { get; set; }
        public bool taxable { get; set; }
        public object featured_image { get; set; }
        public bool available { get; set; }
        public string name { get; set; }
        public string public_title { get; set; }
        public string[] options { get; set; }
        public int price { get; set; }
        public int weight { get; set; }
        public int compare_at_price { get; set; }
        public int inventory_quantity { get; set; }
        public string inventory_management { get; set; }
        public string inventory_policy { get; set; }
        public string barcode { get; set; }
    }
}



class GenericShopifySites
{

    public class GenericProductJson
    {
        public Product product { get; set; }
    }

    public class Product
    {
        public long id { get; set; }
        public string title { get; set; }
        public string body_html { get; set; }
        public string vendor { get; set; }
        public string product_type { get; set; }
        public DateTime created_at { get; set; }
        public string handle { get; set; }
        public DateTime updated_at { get; set; }
        public DateTime published_at { get; set; }
        public string template_suffix { get; set; }
        public string published_scope { get; set; }
        public string tags { get; set; }
        public Variant[] variants { get; set; }
        public Option[] options { get; set; }
        public Image1[] images { get; set; }
        public Image image { get; set; }
    }

    public class Image
    {
        public long id { get; set; }
        public long product_id { get; set; }
        public int position { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public object alt { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string src { get; set; }
        public object[] variant_ids { get; set; }
    }

    public class Variant
    {
        public long id { get; set; }
        public long product_id { get; set; }
        public string title { get; set; }
        public string price { get; set; }
        public string sku { get; set; }
        public int position { get; set; }
        public string inventory_policy { get; set; }
        public string compare_at_price { get; set; }
        public string fulfillment_service { get; set; }
        public string inventory_management { get; set; }
        public string option1 { get; set; }
        public string option2 { get; set; }
        public object option3 { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public bool taxable { get; set; }
        public string barcode { get; set; }
        public int grams { get; set; }
        public object image_id { get; set; }
        public int inventory_quantity { get; set; }
        public float weight { get; set; }
        public string weight_unit { get; set; }
        public long inventory_item_id { get; set; }
        public string tax_code { get; set; }
        public int old_inventory_quantity { get; set; }
        public bool requires_shipping { get; set; }
    }

    public class Option
    {
        public long id { get; set; }
        public long product_id { get; set; }
        public string name { get; set; }
        public int position { get; set; }
        public string[] values { get; set; }
    }

    public class Image1
    {
        public long id { get; set; }
        public long product_id { get; set; }
        public int position { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public object alt { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string src { get; set; }
        public object[] variant_ids { get; set; }
    }
}