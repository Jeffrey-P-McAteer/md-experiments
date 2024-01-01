using System;

using Microsoft.CodeAnalysis.CSharp.Scripting;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ConsoleApplication {
    public class Program {
        public static void Main() {


          var t0_data = Data.all();
          var tn_data = Data.all();

          var deltas = Delta.all();
          var conditions = Condition.all();

          var t0 = DateTime.Now.AddDays(-10);
          var tf = DateTime.Now;



          var result = CSharpScript.EvaluateAsync("1 + 3").Result;
          Console.WriteLine("Hello World! result = "+result);

        }
    }

    // Passed to each update_code as the variable `f`
    public class SimStepData {
      public List<Data> tn_data;
      public int current_data_i; // index into tn_data[i];

    }

    public class Data {
      public int oid;

      public double x;
      public double y;

      public string name;
      public string color;
      public string status;

      public static List<Data> all() {
        return new List<Data>(){
          new Data(){oid=1, x=0.0, y=0.0, name="a", color="red", status=""},
          new Data(){oid=1, x=1.0, y=0.0, name="b", color="green", status=""},
          new Data(){oid=1, x=1.5, y=0.5, name="b", color="red", status=""},
        };
      }
    }

    public class Delta {

      public string name;
      public string description;
      public string update_code;


      public static List<Delta> all() {
        return new List<Delta>(){
          new Delta(){
            name="",
            description="",

          }

        };
      }
    }
    public class Condition {
      public static List<Condition> all() {
        return new List<Condition>(){


        };
      }
    }
}

