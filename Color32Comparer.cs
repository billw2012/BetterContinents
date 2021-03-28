using System.Collections.Generic;
using UnityEngine;

namespace BetterContinents
{
    // Ignores alpha entirely. Color32 doesn't have its own comparer.
    public class Color32Comparer : IEqualityComparer<Color32>
    {
        public bool Equals(Color32 x, Color32 y)
        {
            return x.r == y.r && x.g == y.g && x.b == y.b;
        }

        public int GetHashCode(Color32 obj)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + obj.r.GetHashCode();
                hash = hash * 31 + obj.g.GetHashCode();
                hash = hash * 31 + obj.b.GetHashCode();
                return hash;
            }
        }
    }
}