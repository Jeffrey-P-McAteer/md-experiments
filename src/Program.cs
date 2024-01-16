using System;
using System.IO;
using System.Text;
using System.Text.Json;

using Microsoft.CodeAnalysis.CSharp.Scripting;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.Fonts;

namespace ConsoleApplication {
    public class Program {
        public static Font font;
        public static void Main(string[] args) {
          var collection = new FontCollection();
          //var family = collection.Add("Arial.ttf");
          var family = collection.Add("/j/proj/md-experiments/Arial.ttf");
          font = family.CreateFont(12, FontStyle.Regular);

          var tn_data = args.Length > 1? Data.read_from(args[1]) : Data.all();

          var deltas = args.Length > 2? Delta.read_from(args[2]) : Delta.all();

          var conditions = Condition.all();


          // TimeSpan sim_increment_amount = new TimeSpan(0, 1, 0, 0); // Simulate 1 hour at a time
          TimeSpan sim_increment_amount = new TimeSpan(0, 0, 5, 0); // Simulate 15min at a time
          //int num_hours = 10;
          //var t0 = DateTime.Now.AddHours(-1 * num_hours);
          //var tf = DateTime.Now;
          int num_hours = 24;
          int num_sim_steps = 0;
          while ((sim_increment_amount * num_sim_steps).TotalHours < num_hours) {
            num_sim_steps += 1;
          }
          Console.WriteLine($"Running {num_sim_steps} sim steps at {sim_increment_amount} each to sim {num_hours} hours total.");

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

          List<Image> all_imgs = new List<Image>();
          for (int step_num = 0; step_num < num_sim_steps; step_num += 1) {
            sim.move_forward(sim_increment_amount);
            all_imgs.Add(
              sim.save_img($"{render_dir}/{step_num}.png")
            );
          }

          // Also render a .gif w/ frames
          int frameDelay = 10;
          int px_w = 1024;
          int px_h = 1024;
          Image<Rgba32> gif = new(px_w, px_h, Color.White);

          // Set animation loop repeat count to 5.
          var gifMetaData = gif.Metadata.GetGifMetadata();
          gifMetaData.RepeatCount = 5;

          // Set the delay until the next image is displayed.
          GifFrameMetadata metadata = gif.Frames.RootFrame.Metadata.GetGifMetadata();
          metadata.FrameDelay = frameDelay;
          foreach (var image in all_imgs) {

              // Set the delay until the next image is displayed.
              metadata = image.Frames.RootFrame.Metadata.GetGifMetadata();
              metadata.FrameDelay = frameDelay;

              // Add the color image to the gif.
              gif.Frames.AddFrame(image.Frames.RootFrame);
          }

          // Save the final result.
          gif.SaveAsGif($"{render_dir}/render.gif");


        }



        public static object ParseToSimplest(string val) {
          if (double.TryParse(val.Trim(), out double parsed_d)) {
            return parsed_d;
          }
          if (int.TryParse(val.Trim(), out int parsed_i)) {
            return parsed_i;
          }
          if (long.TryParse(val.Trim(), out long parsed_l)) {
            return parsed_l;
          }
          return val;
        }
    }


    public class SimStepData {
      public List<Data> tn_data;
      public List<Delta> deltas;
      public List<Condition> conditions;
      public int current_data_i; // index into tn_data[i];

      public TimeSpan total_time_simulated = new TimeSpan(0, 0, 0, 0); // 0 days, 0h, 0m ,0s

      // Designed to be used in delta equations!
      public List<Data> prev_sim_flat_data = new List<Data>();

      // Holds items where indexes == OIDs; a non-existing element will have a dummy Data item w/ oid == -1 inserted and no attributes.
      public List<Data> f = new List<Data>();
      // Holds the _current_step_ time delta
      public TimeSpan t = new TimeSpan(0, 0, 0, 0);

      public void move_forward(TimeSpan duration_to_move) {
        // Setup sim frame data
        this.prev_sim_flat_data = new List<Data>();
        this.f = new List<Data>();
        foreach (var d in tn_data) {
          var d_c = d.Clone();
          prev_sim_flat_data.Add(d_c);
          while (!(f.Count > d_c.oid)) {
            f.Add(new Data(){oid=-1, attributes=new Dictionary<string, object>()});
          }
          if (f.Count > d_c.oid) {
            f[d_c.oid] = d_c;
          }
        }
        this.t = duration_to_move;

        // Run all deltas
        foreach (var delta in deltas) {
          for (int i=0; i<tn_data.Count; i+=1) {
            delta.Apply(this, duration_to_move);
          }
        }

        // Check conditions
        foreach (var condition in conditions) {
          if (condition.Exists(this)) {
            Console.WriteLine("condition "+condition+" has occured!");
          }
        }

        total_time_simulated += duration_to_move;
      }

      public Image<Rgba32> save_img(string output_path) {
        int px_w = 1024;
        int px_h = 1024;
        double unit_w = 5.0;
        double unit_h = 5.0;
        Image<Rgba32> image = new(px_w, px_h);

        // White background for all frames
        image.Mutate(x => x.Fill(Color.White, new Rectangle(0, 0, px_w, px_h)) );

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

        // May as well stick all the rules in upper-left corner
        int delta_num = 0;
        int px_per_line = 20;
        foreach (var delta in this.deltas) {
          image.Mutate(x=> {
            x.DrawText(delta.description, Program.font, Color.Black, new PointF(40, 40 + (delta_num*px_per_line) ));
          });
          delta_num += 1;
        }


        // Finally stick the timestamp string at lower-left corner
        string ts = ""+total_time_simulated;
        image.Mutate(x=> {
          x.DrawText(ts, Program.font, Color.Black, new PointF(40, px_h-40));
        });

        image.Save(output_path);
        return image;
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

      public void MoveTowards(double target_x, double target_y, TimeSpan duration_to_move, double velocity_in_units_per_hour){

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

      public void MoveAway(List<Data> prev_sim_data, string target_name, TimeSpan duration_to_move, double velocity_in_units_per_hour) {
        this.MoveTowards(prev_sim_data, target_name, duration_to_move, -1.0 * velocity_in_units_per_hour);
      }

      public double UnitDistTo(List<Data> prev_sim_data, string target_name) {
        double dist = double.PositiveInfinity;

        double our_x = this.G("x", 0.0);
        double our_y = this.G("y", 0.0);

        Data? maybe_target = null;
        foreach (var d in prev_sim_data) {
          if (d.G("name", "").Equals(target_name)) {
            maybe_target = d;
            break;
          }
        }
        if (maybe_target is Data target) {
          double target_x = target.G("x", 0.0);
          double target_y = target.G("y", 0.0);

          dist = Math.Sqrt(
            Math.Pow(our_x - target_x, 2.0) +
            Math.Pow(our_y - target_y, 2.0)
          );

        }


        return dist;
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

      public static List<Data> read_from(string csv_file) {
        List<Data> all = new List<Data>();

        string[] column_names = new string[8];
        int oid_column = 0;

        int line_num = 0;
        using (var fileStream = File.OpenRead(csv_file)) {
          using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 4096)) {
            string line;
            while ((line = streamReader.ReadLine()) != null) {
              if (line_num == 0) {
                column_names = line.Split(',');
                for (int i=0; i<column_names.Length; i+=1) {
                  column_names[i] = column_names[i].Trim();
                  if (column_names[i].Equals("oid", StringComparison.InvariantCultureIgnoreCase)) {
                    oid_column = i;
                  }
                }
              }
              else {
                string[] str_values = line.Split(',');
                object[] parsed_vals = new object[str_values.Length];
                for (int i=0; i<parsed_vals.Length; i+=1) {
                  parsed_vals[i] = Program.ParseToSimplest(str_values[i]);
                }
                if (parsed_vals.Length < 2) {
                  continue;
                }
                int d_oid = Convert.ToInt32(parsed_vals[oid_column]);
                var d_attributes = new Dictionary<string, object>();
                for (int i=0; i<Math.Min(column_names.Length, parsed_vals.Length); i+=1) {
                  d_attributes[column_names[i]] = parsed_vals[i];
                }

                all.Add(new Data(){
                  oid=d_oid,
                  attributes=d_attributes
                });


              }
              line_num += 1;
            }
          }
        }

        return all;
      }

    }

    public class Delta {

      public string description;
      public string update_code;

      public void Apply(SimStepData sim_step, TimeSpan duration_to_move) {
        var result = CSharpScript.EvaluateAsync(this.update_code, globals: sim_step).Result;
      }

      public static List<Delta> all() {
        return new List<Delta>(){
          new Delta(){
            //name="Bird A moves toward food!",
            description="Bird A moves towards food at a speed of 0.25 units/hour.",
            //t0_oid=1,
            update_code="f[1].MoveTowards(\"Bird Feeder\", t, 0.25)",
          },
          new Delta(){
            //name="Bird B moves toward unknown location!",
            description="Bird B moves towards x=1.0,y=0.0 at a speed of 0.2 units/hour.",
            //t0_oid=2,
            update_code="f[2].MoveTowards(1.0, 0.0, t, 0.2)",
          },
          new Delta(){
            //name="Bird A moves away from bird B if within 0.4 units!",
            description="Bird A moves away from bird B if within 0.4 units at a speed of 0.45 units/hour.",
            //t0_oid=1,
            update_code="if (f[1].UnitDistTo(\"Bird B\") < 0.4) { f[1].MoveAway(\"Bird B\", t, 0.45); }",
          },

        };
      }

      public static List<Delta> read_from(string csv_file) {
        var all = new List<Delta>();
        string[] column_names = new string[8];
        int description_column = 0;
        int code_column = 0;

        int line_num = 0;
        using (var fileStream = File.OpenRead(csv_file)) {
          using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, 4096)) {
            string line;
            while ((line = streamReader.ReadLine()) != null) {
              if (line_num == 0) {
                column_names = line.Split(',');
                for (int i=0; i<column_names.Length; i+=1) {
                  column_names[i] = column_names[i].Trim();
                  if (column_names[i].Equals("description", StringComparison.InvariantCultureIgnoreCase)) {
                    description_column = i;
                  }
                  if (column_names[i].Equals("code", StringComparison.InvariantCultureIgnoreCase)) {
                    code_column = i;
                  }
                }
              }
              else {
                string[] str_values = line.Split(',');
                object[] parsed_vals = new object[str_values.Length];
                for (int i=0; i<parsed_vals.Length; i+=1) {
                  parsed_vals[i] = Program.ParseToSimplest(str_values[i]);
                }
                if (parsed_vals.Length < 2) {
                  continue;
                }

                all.Add(new Delta(){
                  description=""+parsed_vals[description_column],
                  update_code=""+parsed_vals[code_column]
                });


              }
              line_num += 1;
            }
          }
        }

        return all;
      }


    }
    public class Condition {

      public bool Exists(SimStepData sim_step) {
        return false; // TODO test if this condition applies to r and if it does is it occuring?
      }

      public static List<Condition> all() {
        return new List<Condition>(){

        };
      }

      public static List<Condition> read_from(string csv_file) {
        var all = new List<Condition>();
        return all;
      }

    }
}

