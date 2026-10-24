using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hacktoberfest;

public class GameArea(string Name) 
{
    public string Name { get; set; } = name;
}

public class Dungeon(string master_descriptor) 
{
    public string master_descriptor { get; set; } = master_descriptor;
    public List<DungeonRoom> rooms { get; } = new();

    public void ClearRooms()
    {
        // TODO: Add code to switch between the dungeon and shop.
        rooms.Clear();
    }
}

public class DungeonRoom(int prompt) 
{
    private static readonly HttpClient HttpClient = new();

    public int prompt { get; set; } = prompt;
    private string prompt_text_1 = "A treasure room containing one chest";
    private string prompt_text_2 = "A dark corridor with flickering torches";
    private string prompt_text_3 = "A library filled with ancient tomes";
    private string prompt_text_4 = "A dark room containing a growling monster";
    private string prompt_text_5 = "An empty room";
    public string description = string.Empty;

    public async Task<string> GenerateDescriptionAsync(
        string roomType,
        string roomFeature,
        CancellationToken cancellationToken = default)
    {
        string promptText =
            $"Describe a dungeon room. The dungeon should be described according to {roomType} and the room should be described according to {roomFeature}. " +
            "Return only the room description.";

        description = await SendPromptAsync(promptText, cancellationToken);
        return description;
    }

    private static async Task<string> SendPromptAsync(
        string promptText,
        CancellationToken cancellationToken)
    {
        string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("Set the GEMINI_API_KEY environment variable.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent");

        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = promptText } } }
                }
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini API returned {(int)response.StatusCode}: {responseBody}",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(responseBody);
        var output = new StringBuilder();

        if (document.RootElement.TryGetProperty("candidates", out var candidates))
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts))
                    continue;

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text))
                        output.Append(text.GetString());
                }
            }
        }

        if (output.Length == 0)
            throw new InvalidOperationException("Gemini returned no text.");

        return output.ToString().Trim();
    }
}

public class Shop
{
    public List<ShopItem> items { get; } = new();

    public Shop()
    {
        PopulateItems();
    }

    private void PopulateItems()
    {
        items.Add(new ShopItem("Health Potion", "Restores health.", 15m));
        items.Add(new ShopItem("Iron Sword", "A sturdy weapon for close combat.", 50m));
        items.Add(new ShopItem("Leather Armor", "Light armor for basic protection.", 40m));
    }

    public bool BuyProduct(string productName, player buyer)
    {
        int productIndex = items.FindIndex(item =>
            string.Equals(item.Name, productName, StringComparison.OrdinalIgnoreCase));

        if (productIndex < 0)
            return false;

        ApplyStatIncrease(buyer, items[productIndex]);
        items.RemoveAt(productIndex);
        return true;
    }

    private void ApplyStatIncrease(player buyer, ShopItem product)
    {
        switch (product.Name)
        {
            case "Health Potion":
                buyer.Health += 25;
                break;
            case "Iron Sword":
                buyer.attack += 5;
                break;
            case "Leather Armor":
                buyer.defense += 5;
                break;
        }
    }
}

public class ShopItem
{
    public string Name { get; }
    public string Description { get; }
    public decimal Price { get; }

    public ShopItem(string name, string description, decimal price)
    {
        Name = name;
        Description = description;
        Price = price;
    }
}

class player
{
    public string Name { get; set; }
    public int Health { get; set; }
    public int Gold { get; set; }
    public int attack { get; set; }
    public int defense { get; set; }

    public player(string name)
    {
        Name = name;
        Health = 50;
        Gold = 0;
        attack = 10;
        defense = 5;
    }
}