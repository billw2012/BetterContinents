using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace BetterContinents
{
    public static class BlendOperations
    {
        // https://photoshoptrainingchannel.com/blending-modes-explained/
        public enum BlendModeType
        {
            Normal, // Overwrite (still respecting mask)
                    
            // Darken modes
            Darken, // Min
            Multiply,
            ColorBurn,
            LinearBurn,
                    
            // Lighten modes
            Lighten, // Max
            Screen,
            ColorDodge,
            LinearDodge, // Add
                    
            // Contrasting modes
            Overlay,
            HardLight,
            SoftLight,
            VividLight,
            LinearLight,
            PinLight,
            HardMix,
                    
            // Inversion modes
            Difference,
            Exclusion,
            Subtract,
            Divide,
        }

        // http://www.simplefilter.de/en/basics/mixmods.html -- this has a and b the wrong way around
        // http://www.deepskycolors.com/archive/2010/04/21/formulas-for-Photoshop-blending-modes.html
        // https://en.wikipedia.org/wiki/Blend_modes
        // https://photoblogstop.com/photoshop/photoshop-blend-modes-explained
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="a">Bottom layer</param>
        /// <param name="b">Top layer</param>
        /// <param name="mode"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static float Blend(float a, float b, BlendModeType mode)
        {
            switch (mode)
            {
                case BlendModeType.Normal: return b;
                case BlendModeType.Darken: return Mathf.Min(a, b);
                case BlendModeType.Multiply: return a * b;
                case BlendModeType.ColorBurn: return 1f - (1f - a) / b;
                case BlendModeType.LinearBurn: return a + b - 1f;
                case BlendModeType.Lighten: return Mathf.Max(a, b);
                case BlendModeType.Screen: return 1f - (1f - a) * (1f - b);
                case BlendModeType.ColorDodge: return a / (1f - b);
                case BlendModeType.LinearDodge: return a + b;
                case BlendModeType.Overlay: return a > 0.5f ? 1f - 2f * (1f - a) * (1f - b) : 2f * a * b;
                case BlendModeType.HardLight: return b > 0.5f ? 1f - 2f * (1f - a) * (1f - b) : 2f * a * b;
                case BlendModeType.SoftLight: return (1f - 2f * b) * a * a + 2f * b * a; // Pegtop's formula
                case BlendModeType.VividLight: return b > 0.5f ? a / (2f * (1f - b)) : 1f - (1f - a) / (2f * b); 
                case BlendModeType.LinearLight: return a + 2f * b - 1f;
                case BlendModeType.PinLight: return a < 2f * b - 1f ? 2f * b - 1f : a > 2f * b ? 2f * b : a;  
                case BlendModeType.HardMix: return b < 1f - a ? 0f : 1f;
                case BlendModeType.Difference: return Mathf.Abs(a - b);
                case BlendModeType.Exclusion: return a + b - 2f * a * b;
                case BlendModeType.Subtract: return a - b;
                case BlendModeType.Divide: return a / b;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}