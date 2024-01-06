using System;
using System.IO;
using System.Text.Json;

using Microsoft.CodeAnalysis.CSharp.Scripting;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;

namespace ConsoleApplication {
    public class Program {
        public static Font font;
        public static void Main() {
          var collection = new FontCollection();
          //var family = collection.Add("Arial.ttf");
          var family = collection.Add("/j/proj/md-experiments/Arial.ttf");
          font = family.CreateFont(12, FontStyle.Regular);

          var t0_data = Data.all();
          var tn_data = Data.all();

          var deltas = Delta.all();
          var conditions = Condition.all();

          int num_hours = 10;
          var t0 = DateTime.Now.AddHours(-1 * num_hours);
          var tf = DateTime.Now;

          var sim = new SimStepData(){
            tn_data = tn_data,
            current_data_i = 0,
            deltas = deltas,
            conditions = conditions,
          };

          string render_dir = "bin/renders";
          if (!Directory.Exists(render_dir)) {
            Directory.CreateDirectory(render_dir);
          }

          for (int hour_num = 0; hour_num < num_hours; hour_num += 1) {
            sim.move_forward(new TimeSpan(0, 1, 0, 0)); // 0 day, 1 hour, 0 m, 0 s
            sim.save_img($"{render_dir}/{hour_num}.png");
          }

        }
    }

    // Passed to each update_code as the variable `f`
    public class SimStepData {
      public List<Data> tn_data;
      public List<Delta> deltas;
      public List<Condition> conditions;
      public int current_data_i; // index into tn_data[i];

      public void move_forward(TimeSpan duration_to_move) {
        var prev_sim_flat_data = new List<Data>();
        foreach (var d in tn_data) {
          prev_sim_flat_data.Add(d.Clone());
        }
        foreach (var delta in deltas) {
          for (int i=0; i<tn_data.Count; i+=1) {
            delta.MaybeApply(tn_data[i], prev_sim_flat_data, duration_to_move);
          }
        }
        foreach (var condition in conditions) {
          for (int i=0; i<tn_data.Count; i+=1) {
            if (condition.Exists(tn_data[i])) {
              Console.WriteLine("condition "+condition+" has occured for "+tn_data[i].oid+"!");
            }
          }
        }
      }

      public void save_img(string output_path) {
        int px_w = 1024;
        int px_h = 1024;
        double unit_w = 5.0;
        double unit_h = 5.0;
        using(Image<Rgba32> image = new(px_w, px_h)) {
          // Paint dot + text on all items using x,y, and name attributes.
          foreach (var d in tn_data) {
            double x = d.G("x", 0.0);
            double y = d.G("y", 0.0);
            string name = d.G("name", "oid="+d.oid);

            int x_px = (int) (((x / unit_w) * px_w) + (px_w/2.0));
            int y_px = px_h - ((int) (((y / unit_h) * px_h) + (px_h/2.0)));

            var color = Color.Black;
            var d_color = d.G("color", "black");
            if (d_color.Equals("black")) {
              color = Color.Black;
            }
            else if (d_color.Equals("grey")) {
              color = Color.Gray;
            }
            else if (d_color.Equals("red")) {
              color = Color.Red;
            }
            else if (d_color.Equals("blue")) {
              color = Color.Blue;
            }

            var circle = new EllipsePolygon((float) x_px, (float) y_px, (float) 4.0);
            image.Mutate(x=> {
              x.Fill(color, circle);
              x.DrawText(name, Program.font, Color.Black, new PointF(x_px+4, y_px+2));
            });

          }
          image.Save(output_path);
        }
      }
    }

    public class Data {
      public int oid;

      public Dictionary<string, object> attributes;

      //// Data[i] has functions suitable for use in Delta.update_code equations to make complicated behaviors simple to represent.

      //r.G("food", 0) returns "food"'s int value or 0.
      public T G<T>(string name, T def=default(T)) {
        if (attributes.ContainsKey(name)) {
          return (T) attributes[name];
        }
        return def;
      }

      // Sets name to value
      public void S(string name, object val) {
        attributes[name] = val;
      }

      public void DeltaMove(double x_delta, double y_delta) {
        this.S("x", this.G("x", 0.0) + x_delta);
        this.S("y", this.G("y", 0.0) + y_delta);
      }

      public void MoveTowards(List<Data> prev_sim_data, string target_name, TimeSpan duration_to_move, double velocity_in_units_per_hour){
        int oid_target = -1;
        foreach (var d in prev_sim_data) {
          if (d.G("name", "").Equals(target_name)) {
            oid_target = d.oid;
          }
        }
        if (oid_target >= 0) {
          this.MoveTowards(prev_sim_data, oid_target, duration_to_move, velocity_in_units_per_hour);
        }
      }

      public void MoveTowards(List<Data> prev_sim_data, int oid_target, TimeSpan duration_to_move, double velocity_in_units_per_hour){
        Data? maybe_target = null;
        foreach (var d in prev_sim_data) {
          if (d.oid == oid_target) {
            maybe_target = d;
            break;
          }
        }
        if (maybe_target is Data target) {
          double target_x = target.G("x", 0.0);
          double target_y = target.G("y", 0.0);

          double our_x = this.G("x", 0.0);
          double our_y = this.G("y", 0.0);

          double units_moved = duration_to_move.TotalHours * velocity_in_units_per_hour;

          double radians_angle_to_target = Math.Atan2(
            target_x - our_x,
            target_y - our_y
          );

          this.S("x", our_x + (units_moved*Math.Sin(radians_angle_to_target)) );
          this.S("y", our_y + (units_moved*Math.Cos(radians_angle_to_target)) );

        }
      }

      public Data Clone() {
        return new Data() {
          oid = this.oid,
          attributes = new Dictionary<string, object>(this.attributes),
        };
      }

      public static List<Data> all() {
        return new List<Data>(){
          new Data(){ oid=1, attributes=new Dictionary<string, object>(){
            {"x", 0.0}, {"y", 0.0}, {"name", "Bird A"}, {"status", "hungry"}, {"food", 5}, {"color", "blue"},
          } },
          new Data(){ oid=2, attributes=new Dictionary<string, object>(){
            {"x", 0.0}, {"y", 1.0}, {"name", "Bird B"}, {"status", "hungry"}, {"food", 15}, {"color", "red"},
          } },
          new Data(){ oid=3, attributes=new Dictionary<string, object>(){
            {"x", 1.2}, {"y", 1.2}, {"name", "Bird Feeder"}, {"status", "na"}, {"food", 500}, {"color", "gray"},
          } },
        };
      }
    }

    public class DeltaGlobals {
      public Data row;
      public List<Data> neighbors;
      public TimeSpan duration_to_move;

      public DeltaGlobals(Data row, List<Data> neighbors, TimeSpan duration_to_move) {
        this.row = row;
        this.neighbors = neighbors;
        this.duration_to_move = duration_to_move;
      }

      // Single-letter Shortcuts
      public Data r { get { return this.row; } }
      public List<Data> n { get { return this.neighbors; } }
      public TimeSpan t { get { return this.duration_to_move; } }

    }

    public class Delta {

      public string name;
      public string description;

      public int t0_oid;

      public string update_code;

      public void MaybeApply(Data r, List<Data> neighbors, TimeSpan duration_to_move) {
        if (t0_oid >= 0) {
          if (r.oid == t0_oid) {
            this.Apply(r, neighbors, duration_to_move);
          }
        }
        else { // t0_oid is -1, indicating it's applicable to all rows
          this.Apply(r, neighbors, duration_to_move);
        }
      }

      public void Apply(Data r, List<Data> neighbors, TimeSpan duration_to_move) {
        var globals = new DeltaGlobals(r, neighbors, duration_to_move);

        /* // For debugging
        Console.WriteLine("DELTA> "+this.update_code);
        Console.WriteLine("     > r.attributes = "+JsonSerializer.Serialize(r.attributes, new JsonSerializerOptions { IncludeFields = true }));
        Console.WriteLine("     > neighbors = "+JsonSerializer.Serialize(neighbors, new JsonSerializerOptions { IncludeFields = true }));
        /* */

        var result = CSharpScript.EvaluateAsync(this.update_code, globals: globals).Result;

      }

      public static List<Delta> all() {
        return new List<Delta>(){
          new Delta(){
            name="Bird A moves toward food!",
            description="Bird A moves towards food at a speed of 0.2 units/hour.",
            t0_oid=1,
            update_code="r.MoveTowards(neighbors, \"Bird Feeder\", t, 0.2)",
          }

        };
      }
    }
    public class Condition {

      public bool Exists(Data r) {
        return false; // TODO test if this condition applies to r and if it does is it occuring?
      }

      public static List<Condition> all() {
        return new List<Condition>(){


        };
      }
    }
}

