using System;
using Mono.Options;

namespace DevOps.Util.DotNet
{
    // TODO: a bit weird here to be using Console.WriteLine now that this gets used in
    // a lot of different places. May need to move this around. 
    public static class OptionSetUtil
    {
        public static void OptionFailure(string message, OptionSet optionSet)
        {
            Console.WriteLine(message);
            optionSet.WriteOptionDescriptions(Console.Out);
        }

        public static Exception OptionFailureWithException(string message, OptionSet optionSet)
        {
            OptionFailure(message, optionSet);
            return CreateBadOptionException();
        }

        public static void OptionFailureDefinition(string definition, OptionSet optionSet)
        {
            Console.WriteLine($"{definition} is not a valid definition name or id");
            Console.WriteLine("Supported definition names");
            foreach (var (name, _, id) in DotNetUtil.BuildDefinitions)
            {
                Console.WriteLine($"{id}\t{name}");
            }

            optionSet.WriteOptionDescriptions(Console.Out);
        }


        public static Exception CreateBadOptionException() => new Exception("Bad option");
    }
}