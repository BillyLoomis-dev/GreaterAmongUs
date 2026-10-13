using BetterAmongUs.Attributes;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class HelpCommand : BaseCommand
{
    internal override string Name => "help";
    internal override string Description => "Get help with commands";
    internal override void Run()
    {
        CommandResultText("Welcome to <color=#0dff00>♻BetterAmongUs♻</color> This mod enhances your gameplay experience with a variety of exciting features.\n" +
                    "Explore the pause menu to access more options and better settings tailored to your needs.\n" +
                    "For a full list of available commands, use the `/commands` command.\n\n" +
                    "Our features include: \n" +
                    "- Built-in Client-Sided Anti-Cheat: Enjoy a fair game with our anti-cheat system that detects and prevents unauthorized actions.\n" +
                    "- Host Enhancements: Gain additional control as a host with improved options and settings.\n" +
                    "- Better Options: Customize your game with a range of new and improved settings.\n" +
                    "- Commands: Utilize a variety of commands to manage and enhance your gameplay.\n" +
                    "- Client Improvements: Experience smoother and more efficient gameplay with our client-side enhancements.\n" +
                    "Stay tuned for more exciting features and improvements coming your way!");
    }
}
