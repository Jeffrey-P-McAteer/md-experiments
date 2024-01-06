using System;
using System.IO;

using Microsoft.CodeAnalysis.CSharp.Scripting;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ConsoleApplication {
    public class Program {
        public static void Main() {


          var t0_data = Data.all();
          var tn_data = Data.all();

          var deltas = Delta.all();
          var conditions = Condition.all();

          int num_days = 10;
          var t0 = DateTime.Now.AddDays(-1 * num_days);
          var tf = DateTime.Now;

          var sim = new SimStepData(){
            tn_data = tn_data,
            current_data_i = 0,
          };

          string render_dir = "bin/renders";
          if (!Directory.Exists(render_dir)) {
            Directory.CreateDirectory(render_dir);
          }

          for (int day_num = 0; day_num < num_days; day_num += 1) {
            sim.move_forward(new TimeSpan(1, 0, 0, 0)); // 1 day, 0 h, 0 m, 0 s
            sim.save_img($"{render_dir}/{day_num}.png");
          }



          var result = CSharpScript.EvaluateAsync("1 + 3").Result;
          Console.WriteLine("Hello World! result = "+result);


        }
    }

    // Passed to each update_code as the variable `f`
    public class SimStepData {
      public List<Data> tn_data;
      public int current_data_i; // index into tn_data[i];

      public void move_forward(TimeSpan duration_to_move) {

      }

      public void save_img(string output_path) {
        int w = 1024;
        int h = 1024;
        using(Image<Rgba32> image = new(w, h)) {

          image.Save(output_path);
        }
      }
    }

    public class Data {
      public int oid;

      public Dictionary<string, object> attributes;

      public T G<T>(string name, T def=default(T)) {
        if (attributes.ContainsKey(name)) {
          return (T) attributes[name];
        }
        return def;
      }


      public static List<Data> all() {
        return new List<Data>(){
          new Data(){ oid=1, attributes=new Dictionary<string, object>(){{"x", 0.0}, {"y", 0.0}, {"name", "a"}, } },
          // new Data(){oid=1, x=1.0, y=0.0, name="b", color="green", status=""},
          // new Data(){oid=1, x=1.5, y=0.5, name="b", color="red", status=""},
        };
      }
    }

    public class Delta {

      public string name;
      public string description;

      public int t0_oid;
      public string attr_name;

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

