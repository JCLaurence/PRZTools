using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PRZC = NCC.PRZTools.PRZConstants;

namespace NCC.PRZTools
{
    public class YamlLegend
    {
        public YamlLegend()
        {
        }

        // Legend Type ('categorical', 'continuous', or 'manual')
        public string type = WTWLegendType.continuous.ToString();

        // Legend Class color array (hex format)
        public string[] colors = new string[]
        {
            "#ffffff",
            "#00ff00"
        };

        // Legend Class Labels (only for manual legends)
        public string[] labels;

        // Legend Methods
        public void SetCategoricalColors(List<Color> named_colors)
        {
            // Ensure parameter is not null
            if (named_colors == null)
            {
                throw new Exception("colors list is null.");
            }

            // Ensure parameter has at least 1 value
            if (named_colors.Count == 0)
            {
                throw new Exception("colors list has no members.");
            }

            List<string> color_hexes = new List<string>();

            // Add the provided colors
            for (int i = 0; i < named_colors.Count; i++)
            {
                Color color = named_colors[i];

                if (color == Color.Transparent)
                {
                    color_hexes.Add("#00000000");
                }
                else
                {
                    color_hexes.Add($"#{color.R:X2}{color.G:X2}{color.B:X2}");
                }
            }

            // update fields
            colors = color_hexes.ToArray();
            type = WTWLegendType.categorical.ToString();
        }

        public void SetManualColors(List<(Color color, string label)> color_label_pairs)
        {
            // Ensure parameter is not null
            if (color_label_pairs == null)
            {
                throw new Exception("colors list is null.");
            }

            // Ensure at least one entry
            if (color_label_pairs.Count == 0)
            {
                throw new Exception("colors list has no members.");
            }

            List<string> color_hexes = new List<string>();
            List<string> color_labels = new List<string>();

            for (int i = 0; i < color_label_pairs.Count; i++)
            {
                Color color = color_label_pairs[i].color;

                // Add the color
                if (color == Color.Transparent)
                {
                    color_hexes.Add("#00000000");
                }
                else
                {
                    color_hexes.Add($"#{color.R:X2}{color.G:X2}{color.B:X2}");
                }

                // Add the label
                color_labels.Add(color_label_pairs[i].label);
            }

            // update fields
            colors = color_hexes.ToArray();
            labels = color_labels.ToArray();
            type = WTWLegendType.manual.ToString();
        }

        public void SetContinuousColors(List<Color> named_colors)
        {
            // Ensure parameter is not null
            if (named_colors == null)
            {
                throw new Exception("colors list is null.");
            }

            // Ensure parameter has at least 1 value
            if (named_colors.Count == 0)
            {
                throw new Exception("colors list has no members.");
            }

            List<string> color_hexes = new List<string>();

            // Add the provided colors
            for (int i = 0; i < named_colors.Count; i++)
            {
                Color color = named_colors[i];

                if (color == Color.Transparent)
                {
                    color_hexes.Add("#00000000");
                }
                else
                {
                    color_hexes.Add($"#{color.R:X2}{color.G:X2}{color.B:X2}");
                }

                //color_hexes.Add($"#{color.R:X2}{color.G:X2}{color.B:X2}");
            }

            // update fields
            colors = color_hexes.ToArray();
            type = WTWLegendType.continuous.ToString();
        }

    }
}
