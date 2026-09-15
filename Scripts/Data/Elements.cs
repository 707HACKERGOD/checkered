using Godot;
using System.Collections.Generic;
using System.Globalization;

public struct ElementInfo
{
    public string Symbol, Name;
    public int Z;
    public float Mass;         // g/mol
    public float Radius;       // Å — tuned so nearest-neighbour bonds render correctly for the included prototypes
    public Color Color;        // CPK-style
    public bool IsMetal;
    public float Chi;          // Pauling electronegativity
    public float IonicRadius;  // Å, 0 = use Radius
}

public static class Elements
{
    public static readonly Dictionary<string, ElementInfo> BySymbol = new();
    public static readonly List<ElementInfo> Palette = new();

    // Symbol|Name|Z|Mass|Radius|Color|Metal|Chi|IonicRadius
    const string Table = @"
H|Hydrogen|1|1.008|0.37|#FFFFFF|0|2.20|0
Li|Lithium|3|6.94|1.50|#CC80FF|1|0.98|0
Be|Beryllium|4|9.012|0.96|#C2FF00|1|1.57|0
B|Boron|5|10.81|0.84|#FFB5B5|0|2.04|0
C|Carbon|6|12.011|0.77|#909090|0|2.55|0
N|Nitrogen|7|14.007|0.70|#3050F8|0|3.04|0
O|Oxygen|8|15.999|0.66|#FF0D0D|0|3.44|1.40
F|Fluorine|9|18.998|0.57|#90E050|0|3.98|1.33
Na|Sodium|11|22.990|1.66|#AB5CF2|1|0.93|1.02
Mg|Magnesium|12|24.305|1.60|#8AFF00|1|1.31|0.72
Al|Aluminum|13|26.982|1.43|#BFA6A6|1|1.61|0.54
Si|Silicon|14|28.085|1.11|#F0C8A0|0|1.90|0
P|Phosphorus|15|30.974|1.07|#FF8000|0|2.19|0
S|Sulfur|16|32.06|1.04|#FFFF30|0|2.58|1.84
Cl|Chlorine|17|35.45|1.02|#1FF01F|0|3.16|1.81
K|Potassium|19|39.098|2.27|#8F40D4|1|0.82|1.38
Ca|Calcium|20|40.078|1.71|#3DFF00|1|1.00|1.00
Ti|Titanium|22|47.867|1.47|#BFC2C7|1|1.54|0.61
Cr|Chromium|24|51.996|1.22|#8A99C7|1|1.66|0
Mn|Manganese|25|54.938|1.35|#9C7AC7|1|1.55|0
Fe|Iron|26|55.845|1.24|#E06633|1|1.83|0
Co|Cobalt|27|58.933|1.25|#F090A0|1|1.88|0
Ni|Nickel|28|58.693|1.25|#50D050|1|1.91|0
Cu|Copper|29|63.546|1.28|#C88033|1|1.90|0
Zn|Zinc|30|65.38|1.35|#7D80B0|1|1.65|0.74
Ga|Gallium|31|69.723|1.22|#C28F8F|1|1.81|0
Ge|Germanium|32|72.630|1.20|#668F8F|1|2.01|0
As|Arsenic|33|74.922|1.19|#BD80E3|0|2.18|0
Se|Selenium|34|78.971|1.20|#FFA100|0|2.55|0
Br|Bromine|35|79.904|1.20|#A62929|0|2.96|1.96
Sr|Strontium|38|87.62|1.75|#00FF00|1|0.95|1.18
Zr|Zirconium|40|91.224|1.60|#94E0E0|1|1.33|0
Mo|Molybdenum|42|95.95|1.39|#54B5B5|1|2.16|0
Ag|Silver|47|107.87|1.44|#D9D9D9|1|1.93|0
Sn|Tin|50|118.71|1.39|#E8E8E8|1|1.96|0
Sb|Antimony|51|121.76|1.39|#9E63B5|1|2.05|0
I|Iodine|53|126.90|1.39|#940094|0|2.66|2.20
Cs|Cesium|55|132.91|1.67|#57178F|1|0.79|1.74
Ba|Barium|56|137.33|2.10|#00C900|1|0.89|1.35
W|Tungsten|74|183.84|1.39|#B01919|1|2.36|0
Pt|Platinum|78|195.08|1.36|#D0D0E0|1|2.28|0
Au|Gold|79|196.97|1.44|#FFD123|1|2.54|0
Hg|Mercury|80|200.59|1.32|#B8B8D0|1|2.00|0
Pb|Lead|82|207.2|1.75|#575961|1|2.33|0
Bi|Bismuth|83|208.98|1.55|#9E63B5|1|2.02|0";

    static Elements()
    {
        foreach (var line in Table.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0) continue;
            var p = t.Split('|');
            var e = new ElementInfo
            {
                Symbol = p[0], Name = p[1], Z = int.Parse(p[2]),
                Mass = F(p[3]), Radius = F(p[4]),
                Color = Color.FromHtml(p[5]),
                IsMetal = p[6] == "1",
                Chi = F(p[7]), IonicRadius = F(p[8]),
            };
            BySymbol[e.Symbol] = e;
            Palette.Add(e);
        }
        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }

    public static ElementInfo Get(string s) => BySymbol.TryGetValue(s, out var e) ? e : BySymbol["C"];
}