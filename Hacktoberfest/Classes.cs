using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hacktoberfest;

internal static class ConsoleSections
{
    public static void WriteSeparator()
    {
        Console.WriteLine(new string('-', 40));
    }
}

public class GameArea(string name) 
{
    public string Name { get; set; } = name;
}

public class Dungeon(string master_descriptor) : GameArea("Dungeon")
{
    public string master_descriptor { get; set; } = master_descriptor;
    public List<DungeonRoom> rooms { get; } = new();

    public void ClearRooms()
    {
        // TODO: Add code to switch between the dungeon and shop.
        rooms.Clear();
    }

    public async Task<bool> DungeonLoopAsync(player adventurer)
    {
        ConsoleSections.WriteSeparator();
        Console.WriteLine(master_descriptor);

        while (true)
        {
            ConsoleSections.WriteSeparator();
            Console.WriteLine("1. Venture forward into a new room");
            Console.WriteLine("0. Exit the dungeon");
            Console.Write("Choose an option: ");

            string? choice = Console.ReadLine();
            if (choice is null || choice.Trim() == "0")
            {
                Console.WriteLine("You leave the dungeon.");
                return false;
            }

            if (choice.Trim() != "1")
            {
                Console.WriteLine("Please enter 1 to continue or 0 to exit.");
                continue;
            }

            var room = new DungeonRoom(Random.Shared.Next(1, 6));
            await room.GenerateDescriptionAsync(
                master_descriptor,
                $"room {room.prompt}, newly discovered deeper within the dungeon");
            rooms.Add(room);
            ConsoleSections.WriteSeparator();
            Console.WriteLine($"Room {room.prompt}: {room.description}");

            if (room.prompt == 1)
            {
                int treasure = Random.Shared.Next(5, 21);
                adventurer.Gold += treasure;
                Console.WriteLine($"You find {treasure} gold. You now have {adventurer.Gold} gold.");
            }
            else if (room.prompt == 5)
            {
                DungeonRoom.FightOutcome outcome = await room.StartFightAsync(adventurer);
                if (outcome == DungeonRoom.FightOutcome.Defeat)
                    return true;

                if (outcome == DungeonRoom.FightOutcome.Fled)
                    return false;
            }
        }
    }
}

public class DungeonRoom(int prompt) : GameArea("Dungeon Room")
{
    private static readonly HttpClient HttpClient = new();

    public enum FightOutcome
    {
        Victory,
        Defeat,
        Fled
    }

    public int prompt { get; set; } = prompt;
    private string prompt_text_1 = "A treasure room containing one chest";
    private string prompt_text_2 = "A dark corridor with flickering torches";
    private string prompt_text_3 = "A library filled with ancient tomes";
    private string prompt_text_4 = "A dark room containing an unseen growling monster";
    private string prompt_text_5 = "An empty room";
    public string description = string.Empty;

    public async Task<string> GenerateDescriptionAsync(
        string roomType,
        string roomFeature,
        CancellationToken cancellationToken = default)
    {
        string promptText =
            $"Describe a dungeon room. The dungeon should be described according to {roomType} and the room should be described according to {roomFeature}. The description should be short but impactful." +
            "Return only the room description.";

        description = await SendPromptAsync(promptText, cancellationToken);
        return description;
    }

    public async Task<FightOutcome> StartFightAsync(
        player fighter,
        CancellationToken cancellationToken = default)
    {
        string monsterDescription = await SendPromptAsync(
            $"Describe one monster the player encounters in this dungeon room: {description}. " +
            "Write a short, vivid description and do not include combat statistics.",
            cancellationToken);

        int monsterHealth = Random.Shared.Next(10, 46);
        int monsterAttack = Random.Shared.Next(5, 21);

        ConsoleSections.WriteSeparator();
        Console.WriteLine($"A monster appears: {monsterDescription}");
        Console.WriteLine($"Monster health: {monsterHealth}");

        while (fighter.Health > 0 && monsterHealth > 0)
        {
            ConsoleSections.WriteSeparator();
            Console.WriteLine($"Your health: {fighter.Health} | Monster health: {monsterHealth}");
            Console.WriteLine("1. Attack");
            Console.WriteLine("2. Flee and leave the dungeon");
            Console.Write("Choose an action: ");

            string? choice = Console.ReadLine()?.Trim();
            if (choice == "2" || choice is null)
            {
                Console.WriteLine("You flee and leave the dungeon.");
                return FightOutcome.Fled;
            }

            if (choice != "1")
            {
                Console.WriteLine("Please enter 1 to attack or 2 to flee.");
                continue;
            }

            monsterHealth = Math.Max(0, monsterHealth - fighter.attack);
            Console.WriteLine($"You deal {fighter.attack} damage.");
            if (monsterHealth == 0)
            {
                Console.WriteLine("You defeated the monster.");
                return FightOutcome.Victory;
            }

            int damageTaken = Math.Max(0, monsterAttack - fighter.defense);
            fighter.Health = Math.Max(0, fighter.Health - damageTaken);
            Console.WriteLine($"The monster attacks for {monsterAttack} damage. " +
                $"Your armor reduces it to {damageTaken} damage.");
        }

        Console.WriteLine("You were defeated by the monster.");
        return FightOutcome.Defeat;
    }

    private static async Task<string> SendPromptAsync(
        string promptText,
        CancellationToken cancellationToken)
    {
        string apiKey = "<API Key Here>
            ?? throw new InvalidOperationException("Set the GEMINI_API_KEY environment variable.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent");

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

public class Shop : GameArea
{
    public List<ShopItem> items { get; } = new();

    public Shop() : base("Shop")
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

    public void ShopLoop(player buyer)
    {
        while (true)
        {
            ConsoleSections.WriteSeparator();
            Console.WriteLine("Current shop stock:");
            for (int i = 0; i < items.Count; i++)
            {
                ShopItem item = items[i];
                Console.WriteLine($"{i + 1}. {item.Name} - {item.Description} ({item.Price} gold)");
            }

            Console.WriteLine("0. Leave shop");
            Console.Write("Would you like to buy anything? Enter an item number or 0 to leave: ");

            string? choice = Console.ReadLine();
            if (choice is null || choice.Trim() == "0")
                return;

            if (!int.TryParse(choice, out int itemNumber) ||
                itemNumber < 1 ||
                itemNumber > items.Count)
            {
                Console.WriteLine("That isn't a valid option. Please choose an item number or 0 to leave.");
                continue;
            }

            ShopItem selectedItem = items[itemNumber - 1];
            if (BuyProduct(selectedItem.Name, buyer))
                Console.WriteLine($"You bought {selectedItem.Name}.");
        }
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

public class player
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
