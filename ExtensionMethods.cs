using System.Text;
using UnityEngine;

namespace BetterContinents
{
    public static class ExtensionMethods
    {
        public static string AddSpacesToWords(this string text, bool preserveAcronyms = true)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            var newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]))
                    if ((text[i - 1] != ' ' && !char.IsUpper(text[i - 1])) ||
                        (preserveAcronyms && char.IsUpper(text[i - 1]) && 
                         i < text.Length - 1 && !char.IsUpper(text[i + 1])))
                        newText.Append(' ');
                newText.Append(text[i]);
            }
            return newText.ToString();
        }
        
        public static string UpTo(this string s, string stopper) => s.IndexOf(stopper) == -1? s : s.Substring(0, s.IndexOf(stopper));
    }
}