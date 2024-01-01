using System;
 using Microsoft.CodeAnalysis.CSharp.Scripting;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main()
        {
            var result = CSharpScript.EvaluateAsync("1 + 3").Result;
            Console.WriteLine("Hello World! result = "+result);
        }
    }
}

