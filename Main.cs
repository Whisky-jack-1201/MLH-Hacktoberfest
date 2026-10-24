using System.Collections.Generic;
using System;
using Hacktoberfest;

public class Program
{
	public static void Main()
	{
		Console.Write("Enter your name: ");
		string name = Console.ReadLine() ?? string.Empty;

		player newPlayer = new(name);
		Console.WriteLine($"Welcome, {newPlayer.Name}!");
	}
}
