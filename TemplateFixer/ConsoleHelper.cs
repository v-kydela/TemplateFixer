using System;

namespace TemplateFixer
{
    internal class ConsoleHelper
    {
        internal static void WriteLine(string value)
        {
            Console.ResetColor();
            Console.WriteLine(value);
        }
    }
}
