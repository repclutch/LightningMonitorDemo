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

namespace ShopifyMonitor
{
    class ifStockExists
    {
        public static async Task<bool> StockExists(string url, GlossaryOfProducts secondParse, int i)
        {
            var discordClient = new DiscordWebhookClient(479893514260578305, "GGoikx-zcZpvkX0VsrmogElUfJsxqgbhvkhr9Xc5yVjksZeFghsKIu0OdAteQdVK_S3j");
         
            var builder = new Discord.EmbedBuilder()
                                .WithTitle(secondParse.products[i].title)
                                .WithUrl(url + "/products/" + secondParse.products[i].handle)
                                .WithColor(new Color(0xffffff))
                                .WithFooter(footer =>
                                {
                                    footer
                                        .WithText("Easy Restock " + DateTime.Now);
                                })
                                .WithThumbnailUrl(secondParse.products[i].images[0].src)
                                .WithAuthor(author =>
                                {
                                    author
                                        .WithName("Shopify Monitor");
                                })
                            .AddField("Product Updated", "(Stock potentially coming soon!)");
            var embed = new[] { builder.Build() };
            bool posted = false;
            while (posted == false)
            {
                try
                {
                    await discordClient.SendMessageAsync("", false, embed).ConfigureAwait(false);
                    posted = true;
                }
                catch (Discord.Net.RateLimitedException)
                {
                    Console.WriteLine("Too many restocks Happenning");
                    System.Threading.Thread.Sleep(5000);
                    posted = false;
                    continue;
                }
            }
            return true;
        }

        public static async Task<bool> StockNotExists(string url, GlossaryOfProducts secondParse, int i, string stringOfSizes)
        {
            var discordClient = new DiscordWebhookClient(479893514260578305, "GGoikx-zcZpvkX0VsrmogElUfJsxqgbhvkhr9Xc5yVjksZeFghsKIu0OdAteQdVK_S3j");
            var builder = new Discord.EmbedBuilder()
                                .WithDescription(url + "/products/" + secondParse.products[i].handle)
                                .WithUrl(url + "/products/" + secondParse.products[i].handle)
                                .WithColor(new Color(0x50C878))
                                .WithFooter(footer =>
                                {
                                    footer
                                        .WithText("Easy Restock " + DateTime.Now);
                                })
                                .WithThumbnailUrl(secondParse.products[i].images[0].src)
                                .WithAuthor(author =>
                                {
                                    author
                                        .WithName("Shopify Monitor");
                                })
                            .AddField("Size[Stock]::Checkout Link::StockX Profit(Lvl 2)", stringOfSizes);
            var embed = new[] { builder.Build() };
            bool posted = false;
            while (posted == false)
            {
                try
                {
                    await discordClient.SendMessageAsync("```" + secondParse.products[i].title + "```", false, embed).ConfigureAwait(false);
                    posted = true;
                }
                catch (Discord.Net.RateLimitedException)
                {
                    Console.WriteLine("Too many restocks Happenning");
                    System.Threading.Thread.Sleep(5000);
                    posted = false;
                    continue;
                }
            }
            return true;
        }
    }

    class GenericMonitorPost
    {
        public static async Task<bool> noStockToDiscord(string productUrl, string imageUrl, string title, string shoePrice)
        {
            var discordClient = new DiscordWebhookClient(479893514260578305, "GGoikx-zcZpvkX0VsrmogElUfJsxqgbhvkhr9Xc5yVjksZeFghsKIu0OdAteQdVK_S3j");

            var builder = new Discord.EmbedBuilder()
                                .WithDescription(productUrl)
                                .WithUrl(productUrl)
                                .AddField("Retail Price", shoePrice)
                                .WithColor(new Color(0xffffff))
                                .WithFooter(footer =>
                                {
                                    footer
                                        .WithText("Easy Restock " + DateTime.Now);
                                })
                                .WithThumbnailUrl(imageUrl)
                                .WithAuthor(author =>
                                {
                                    author
                                        .WithName("Shopify Monitor");
                                })
                            .AddField("New product loaded", "(Stock potentially coming soon!)");
            var embed = new[] { builder.Build() };
            bool posted = false;
            while (posted == false)
            {
                try
                {
                    await discordClient.SendMessageAsync("```" + title + "```", false, embed).ConfigureAwait(false);
                    posted = true;
                }
                catch (Discord.Net.RateLimitedException)
                {
                    Console.WriteLine("Too many restocks Happenning");
                    System.Threading.Thread.Sleep(5000);
                    posted = false;
                    continue;
                }
            }
            return true;
        }
        public static async Task<bool> postToDiscord(string productUrl, string imageUrl, string title, string stringOfSizes, string shoePrice)
        {
            var discordClient = new DiscordWebhookClient(479893514260578305, "GGoikx-zcZpvkX0VsrmogElUfJsxqgbhvkhr9Xc5yVjksZeFghsKIu0OdAteQdVK_S3j");
            var builder = new Discord.EmbedBuilder()
                                .WithDescription(productUrl)
                                .WithUrl(productUrl)
                                .WithColor(new Color(0x50C878))
                                .WithFooter(footer =>
                                {
                                    footer
                                        .WithText("Easy Restock " + DateTime.Now);
                                })
                                .WithThumbnailUrl(imageUrl)
                                .WithAuthor(author =>
                                {
                                    author
                                        .WithName("Shopify Monitor");
                                })
                            .AddField("Retail Price", shoePrice);
            var embed = new[] { builder.Build() };
            bool posted = false;
            while (posted == false)
            {
                try
                {
                    await discordClient.SendMessageAsync("```" + title + "```\n" + "**Size[Stock]::Checkout Link::ESTIMATED StockX Profit(Lvl 2)**\n" + stringOfSizes, false, embed).ConfigureAwait(false);
                    posted = true;
                }
                catch (Discord.Net.RateLimitedException)
                {
                    Console.WriteLine("Too many restocks Happenning");
                    System.Threading.Thread.Sleep(5000);
                    posted = false;
                    continue;
                }
            }
            return true;
        }
    }
}
