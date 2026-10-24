using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hacktoberfest;

public class Program
{
	private static readonly HttpClient HttpClient = new();

	public static async Task Main()
	{
		Console.Write("Enter your name: ");
		string name = Console.ReadLine() ?? string.Empty;

		while (true)
		{
			await PlayGameAsync(name);
			ConsoleSections.WriteSeparator();
			Console.Write("Would you like to play again? (y/n): ");
			string? replayChoice = Console.ReadLine()?.Trim();
			if (!string.Equals(replayChoice, "y", StringComparison.OrdinalIgnoreCase))
				return;
		}
	}

	private static async Task PlayGameAsync(string name)
	{
		player newPlayer = new(name);
		ConsoleSections.WriteSeparator();
		Console.WriteLine($"Welcome, {newPlayer.Name}!");
		Console.WriteLine("You approach a dungeon, with a small shop nearby stocked with supplies for the journey.");

		Shop shop = new();
		Dungeon? dungeon = null;

		while (true)
		{
			ConsoleSections.WriteSeparator();
			Console.WriteLine("1. Enter the shop");
			Console.WriteLine("2. Enter the dungeon");
			Console.WriteLine("3. View your stats");
			Console.WriteLine("0. Leave the area");
			Console.Write("Choose an option: ");

			string? choice = Console.ReadLine()?.Trim();
			switch (choice)
			{
				case "1":
					shop.ShopLoop(newPlayer);
					break;
				case "2":
					if (dungeon is null)
					{
						string masterDescriptor = await GenerateMasterDescriptorAsync();
						dungeon = new Dungeon(masterDescriptor);
					}

					bool playerDied = await dungeon.DungeonLoopAsync(newPlayer);
					if (playerDied)
					{
						Console.WriteLine("Game over.");
						return;
					}
					break;
				case "3":
					ConsoleSections.WriteSeparator();
					Console.WriteLine($"Name: {newPlayer.Name}");
					Console.WriteLine($"Health: {newPlayer.Health}");
					Console.WriteLine($"Defense: {newPlayer.defense}");
					Console.WriteLine($"Attack: {newPlayer.attack}");
					Console.WriteLine($"Gold: {newPlayer.Gold}");
					Console.WriteLine("Press any key to return to the menu.");
					Console.ReadKey(intercept: true);
					break;
				case "0":
				case null:
					ConsoleSections.WriteSeparator();
					Console.WriteLine("Thanks for playing!");
					return;
				default:
					Console.WriteLine("Please enter 1 for the shop, 2 for the dungeon, 3 for stats, or 0 to leave.");
					break;
			}
		}
	}

	private static async Task<string> GenerateMasterDescriptorAsync()
	{
		string apiKey = "<API Key Here>"
			?? throw new InvalidOperationException("Set the GEMINI_API_KEY environment variable.");
		const string prompt =
			"Create a concise, evocative short description for a fantasy dungeon. " +
			"Describe its setting, atmosphere, and overarching theme, without describing a specific room. " +
			"Return only the descriptor.";

		using var request = new HttpRequestMessage(
			HttpMethod.Post,
			"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent");
		request.Headers.Add("x-goog-api-key", apiKey);
		request.Content = new StringContent(
			JsonSerializer.Serialize(new
			{
				contents = new[]
				{
					new { parts = new[] { new { text = prompt } } }
				}
			}),
			Encoding.UTF8,
			"application/json");

		using var response = await HttpClient.SendAsync(request);
		string responseBody = await response.Content.ReadAsStringAsync();
		if (!response.IsSuccessStatusCode)
		{
			throw new HttpRequestException(
				$"Gemini API returned {(int)response.StatusCode}: {responseBody}",
				null,
				response.StatusCode);
		}

		using var document = JsonDocument.Parse(responseBody);
		if (document.RootElement.TryGetProperty("candidates", out var candidates))
		{
			foreach (var candidate in candidates.EnumerateArray())
			{
				if (!candidate.TryGetProperty("content", out var content) ||
					!content.TryGetProperty("parts", out var parts))
					continue;

				var descriptor = new StringBuilder();
				foreach (var part in parts.EnumerateArray())
				{
					if (part.TryGetProperty("text", out var text))
						descriptor.Append(text.GetString());
				}

				if (descriptor.Length > 0)
					return descriptor.ToString().Trim();
			}
		}

		throw new InvalidOperationException("Gemini returned no dungeon descriptor.");
	}
}
