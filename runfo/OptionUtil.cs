using System;
using Mono.Options;

// TODO: a bit weird here to be using Console.WriteLine now that this gets used in
// a lot of different places. May need to move this around. 
internal static class OptionUtil
{
    internal static void OptionFailure(string message, OptionSet optionSet)
    {
        Console.WriteLine(message);
        optionSet.WriteOptionDescriptions(Console.Out);
    }

    internal static Exception OptionFailureWithException(string message, OptionSet optionSet)
    {
        OptionFailure(message, optionSet);
        return CreateBadOptionException();
    }

    internal static void OptionFailureDefinition(string definition, OptionSet optionSet)
    {
        Console.WriteLine($"{definition} is not a valid definition name or id");
        Console.WriteLine("Supported definition names");
        foreach (var (name, _, id) in RuntimeInfo.BuildDefinitions)
        {
            Console.WriteLine($"{id}\t{name}");
        }

        optionSet.WriteOptionDescriptions(Console.Out);
    }


    internal static Exception CreateBadOptionException() => new Exception("Bad option");
}