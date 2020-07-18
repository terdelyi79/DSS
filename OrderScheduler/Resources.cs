using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderScheduler
{
    public static class Resources
    {
        public static int[] AvailableResources { get; } = new int[] { 6, 2, 3, 1, 4, 3 };

        public static Dictionary<Product, int[]> NeededResources { get; } = new Dictionary<Product, int[]> {
                    { Product.GYB, new int[] { 5, 10, 8, 5, 12, 10 } },
                    { Product.FB, new int[] { 8, 16, 12, 5, 20, 15 } },
                    { Product.SB, new int[] { 6, 15, 10, 5, 15, 12 } }
                };

        public static string[] Names { get; } = new string[] { "Vágó", "Hajlító", "Hegesztő", "Tesztelő", "Festő", "Csomagoló" };
    }
}